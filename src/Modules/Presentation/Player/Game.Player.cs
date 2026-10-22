using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Game part (split from PlayerBody/Game.cs in Wave A): what the player's hands and weapon are doing, read from the game's FSMs and
    /// first-person arms (drawn weapon, reload, kick, grenade, attack, ADS, binoculars, tool in hand). Target: Presentation player input
    /// adapter (B05). The player-state part (Ready, Dead, InCar, Velocity ...) stays in Core/Game/_fromPlayerBody/Game.cs for B01 (PlayerState).
    /// </summary>
    internal static partial class Game
    {
        public static bool ToolInHand { get { return ToolActive(_toolArm) || ToolActive(_repairArm) || ToolActive(_adjustArm); } }
        private static bool ToolActive(Transform arm) { return arm != null && _handUse != null && arm.parent == _handUse && arm.gameObject.activeSelf; }
        // the drawn first-person weapon (name of the active child of WeaponsArm/Parent), "" when none
        // (WW perf) asked 8-10 times a frame: the answer is kept for the frame (re-read when WeaponsParent changes) and each child's name is read
        // once (Transform.name made a new string per call)
        private static int _drawnFrame = -1; private static string _drawn = ""; private static Transform _drawnParent, _drawnChild;
        private static readonly Dictionary<int, string> _childNames = new Dictionary<int, string>();
        public static string DrawnWeapon
        {
            get
            {
                var wp = WeaponsParent;
                // a hit is re-validated (the weapon FSMs may switch it later in the same frame): the drawn child still active, or with none a fresh scan
                if (_drawnFrame == Time.frameCount && ReferenceEquals(_drawnParent, wp) && !ReferenceEquals(_drawnChild, null) && _drawnChild != null && _drawnChild.gameObject.activeInHierarchy) return _drawn;
                _drawnFrame = Time.frameCount; _drawnParent = wp; _drawn = ""; _drawnChild = null;
                if (wp == null || !wp.gameObject.activeInHierarchy) return _drawn;
                for (int i = 0; i < wp.childCount; i++)
                {
                    var c = wp.GetChild(i);
                    if (!c.gameObject.activeSelf) continue;
                    string n; int id = c.GetInstanceID();
                    if (!_childNames.TryGetValue(id, out n)) { if (_childNames.Count > 256) _childNames.Clear(); _childNames[id] = n = c.name; }
                    if (n != "kick") { _drawn = n; _drawnChild = c; break; }
                }
                return _drawn;
            }
        }
        // WeaponsArm/Parent/kick [Animation]: off -> (Kick button) on for 0.55 s -> off.
        // Attack's fire state can end immediately on a hit; Animation stays on for the whole kick.
        public static string KickState
        {
            get
            {
                try
                {
                    if (_kickFsm == null && WeaponsParent != null)
                    {
                        var k = WeaponsParent.Find("kick");
                        if (k != null) foreach (var f in k.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Animation") _kickFsm = f;
                    }
                    return _kickFsm != null && _kickFsm.gameObject.activeInHierarchy ? _kickFsm.ActiveStateName : "";
                }
                catch (Exception) { return ""; }
            }
        }

        // the drawn gun really reloads: its [ReloadAnimation] FSM is "on" (akms etc.), or its [Reload] FSM is past idle/checkAmmo
        public static bool IsReloading(string weapon)
        {
            if (string.IsNullOrEmpty(weapon) || WeaponsParent == null) return false;
            try
            {
                PlayMakerFSM[] f;
                if (!_reloadFsms.TryGetValue(weapon, out f) || f[0] == null && f[1] == null)
                {
                    f = new PlayMakerFSM[2];
                    var w = WeaponsParent.Find(weapon);
                    if (w != null) foreach (var x in w.GetComponents<PlayMakerFSM>()) { if (x.FsmName == "ReloadAnimation") f[0] = x; else if (x.FsmName == "Reload") f[1] = x; }
                    _reloadFsms[weapon] = f;
                }
                if (f[0] != null) { string s = f[0].ActiveStateName; if (s != "off" && !string.IsNullOrEmpty(s)) return true; }
                if (f[1] != null) { string s = f[1].ActiveStateName; return !string.IsNullOrEmpty(s) && s != "idle" && s != "checkAmmo" && s != f[1].Fsm.StartState; }
            }
            catch (Exception) { }
            return false;
        }

        // the drawn weapon's first-person arms play their reload: Animator (WeaponsArm/Parent/<weapon>/...) in a state named "reload" (the
        // ReloadAnimation FSM's AnimatorPlay) or playing a clip whose name contains "reload". known = there is an active Animator to ask.
        private static readonly Dictionary<string, Animator> _arms = new Dictionary<string, Animator>();

        // any of the game's first-person arms (material Player2*, under the camera holder; not the kick leg) is drawn this frame.
        // The renderer list is refreshed every second (weapons/items create theirs when first drawn).
        private static readonly List<Renderer> _fpArmRenderers = new List<Renderer>();
        private static float _fpArmsScan;
        private static readonly List<SkinnedMeshRenderer> _fpScan = new List<SkinnedMeshRenderer>();
        private static readonly Dictionary<long, bool> _fpVerdict = new Dictionary<long, bool>();
        private static Transform _fpArmsHolder;
        private static int _fpScanRevision, _fpArmsRevision = -1;

        /// <summary>(B05 perf, README 8 #10) the skinned renderers under the camera holder, rescanned at most once a second (or when the
        /// holder changes) and shared by <see cref="GameArmsShown"/> and the arm skin (Arms.Tick).</summary>
        internal static List<SkinnedMeshRenderer> HolderSkins
        {
            get
            {
                var holder = PlayerState.CameraHolder;
                if (holder == null) { _fpScan.Clear(); return _fpScan; }
                if (Time.unscaledTime >= _fpArmsScan || _fpArmsHolder != holder)
                {
                    _fpArmsScan = Time.unscaledTime + 1f;
                    _fpArmsHolder = holder;
                    holder.GetComponentsInChildren<SkinnedMeshRenderer>(true, _fpScan);   // (WW perf) List overload
                    _fpScanRevision++;
                }
                return _fpScan;
            }
        }

        public static bool GameArmsShown
        {
            get
            {
                if (PlayerState.CameraHolder == null) return false;
                var skins = HolderSkins;
                if (_fpArmsRevision != _fpScanRevision)
                {
                    _fpArmsRevision = _fpScanRevision;
                    _fpArmRenderers.Clear();
                    for (int k = 0; k < skins.Count; k++)   // verdict memo per renderer+material
                    {
                        var r = skins[k];
                        if (r == null) continue;
                        var m = r.sharedMaterial;
                        if (m == null) continue;
                        int mid = m.GetInstanceID(); long key = ((long)r.GetInstanceID() << 32) ^ (uint)mid;
                        bool arm;
                        if (!_fpVerdict.TryGetValue(key, out arm))
                        {
                            arm = m.name.StartsWith("Player2") && r.name.IndexOf("leg", StringComparison.OrdinalIgnoreCase) < 0;
                            if (_fpVerdict.Count > 2048) _fpVerdict.Clear();
                            _fpVerdict[key] = arm;
                        }
                        if (arm) _fpArmRenderers.Add(r);
                    }
                }
                for (int i = 0; i < _fpArmRenderers.Count; i++)
                {
                    var r = _fpArmRenderers[i];
                    if (r != null && r.enabled && r.gameObject.activeInHierarchy) return true;
                }
                return false;
            }
        }
        public static bool ArmsReloading(string weapon, out bool known)
        {
            known = false;
            if (string.IsNullOrEmpty(weapon) || WeaponsParent == null) return false;
            try
            {
                Animator an;
                if (!_arms.TryGetValue(weapon, out an) || an == null)
                {
                    var w = WeaponsParent.Find(weapon);
                    an = w != null ? w.GetComponentInChildren<Animator>(true) : null;
                    _arms[weapon] = an;
                }
                if (an == null || !an.isActiveAndEnabled || an.runtimeAnimatorController == null) return false;
                known = true;
                if (IsReload(an.GetCurrentAnimatorStateInfo(0))) return true;
                if (an.IsInTransition(0) && IsReload(an.GetNextAnimatorStateInfo(0))) return true;
                an.GetCurrentAnimatorClipInfo(0, _clipInfo);   // (WW perf) List overload: no array per frame
                foreach (var ci in _clipInfo)
                    if (ci.clip != null && ci.weight > 0.5f && ci.clip.name.IndexOf("reload", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            catch (Exception) { }
            return false;
        }
        private static readonly List<AnimatorClipInfo> _clipInfo = new List<AnimatorClipInfo>();
        private static bool IsReload(AnimatorStateInfo s) { return ReloadPhaseOf(s) != 0; }
        // (2.1.8) the arms' reload states: "reload" (one magazine / one round), and for the break-action / bolt guns (rochester_m24, redmark_m11)
        // "reload 1" (open), "reload 2" (one round, again per round), "reload 3" / "reload 4" (close). 1 = opening, 2 = loading, 3 = closing
        private static int ReloadPhaseOf(AnimatorStateInfo s)
        {
            if (s.IsName("reload") || s.IsName("Reload") || s.IsName("Base Layer.reload") || s.IsName("Base Layer.Reload") || s.IsName("reload 2") || s.IsName("Base Layer.reload 2")) return 2;
            if (s.IsName("reload 1") || s.IsName("Base Layer.reload 1")) return 1;
            if (s.IsName("reload 3") || s.IsName("Base Layer.reload 3") || s.IsName("reload 4") || s.IsName("Base Layer.reload 4")) return 3;
            return 0;
        }
        // the drawn weapon's arms in a reload state: which phase, and how far through it (0..1)
        public static bool ArmsReloadPhase(string weapon, out int phase, out float norm)
        {
            phase = 0; norm = 0f;
            bool known;
            ArmsReloading(weapon, out known);
            Animator an;
            if (!known || !_arms.TryGetValue(weapon, out an) || an == null) return false;
            try
            {
                var s = an.GetCurrentAnimatorStateInfo(0);
                phase = ReloadPhaseOf(s);
                if (phase == 0 && an.IsInTransition(0)) { s = an.GetNextAnimatorStateInfo(0); phase = ReloadPhaseOf(s); }
                norm = Mathf.Clamp01(s.normalizedTime);
                return phase != 0;
            }
            catch (Exception) { return false; }
        }
        // the gun reloads one round at a time: its [Reload] FSM loops reload -> checkAmmoInStore -> the arms' round animation (revolvers,
        // shotguns, the bolt rifle, the double barrel); magazines don't have that loop
        private static readonly Dictionary<string, bool> _perRound = new Dictionary<string, bool>();
        public static bool ReloadsPerRound(string weapon)
        {
            if (string.IsNullOrEmpty(weapon)) return false;
            bool r;
            if (_perRound.TryGetValue(weapon, out r)) return r;
            IsReloading(weapon);
            PlayMakerFSM[] f;
            r = false;
            try
            {
                if (_reloadFsms.TryGetValue(weapon, out f) && f[1] != null)
                    foreach (var st in f[1].FsmStates) if (st.Name == "checkAmmoInStore") { r = true; break; }
            }
            catch (Exception) { }
            if (WeaponsParent != null && WeaponsParent.Find(weapon) != null) _perRound[weapon] = r;
            return r;
        }
        // right mouse button (the game's "Aim Down Sights"): PlayerCamera [AimDownSIghts_Hold] sits in "ads" while it is held (it sends
        // AimDownSights_ON to the drawn weapon: the first-person gun slides to the sights and MouseCrosshair is switched off; scoped
        // weapons also set the camera FOV to 25 and show their scope overlay)
        private static PlayMakerFSM _adsFsm;
        public static bool AimDownSights
        {
            get
            {
                try
                {
                    if (_adsFsm == null && PlayerCamera != null)
                        foreach (var f in PlayerCamera.GetComponents<PlayMakerFSM>()) if (f.FsmName == "AimDownSIghts_Hold" || f.FsmName == "AimDownSights_Hold") { _adsFsm = f; break; }
                    return _adsFsm != null && _adsFsm.enabled && _adsFsm.ActiveStateName == "ads";
                }
                catch (Exception) { return false; }
            }
        }

        // binoculars raised: PlayerCamera/ItemAnim/Binocular Anim [Animation] off -(Activate)-> animOn (raise, 0.4 s) -> on (arms off,
        // BinocularUI + Binocular_Effect on, SetCameraFOV 20 every frame) -(Deactivate)-> animOff -> off
        private static PlayMakerFSM _binoFsm;
        public static bool BinocularInUse
        {
            get
            {
                bool raised = Binoculars; // also resolves the animation FSM
                string state = _binoFsm != null && _binoFsm.enabled ? _binoFsm.ActiveStateName : "";
                return raised || state == "animOn" || state == "animOff";
            }
        }
        public static bool Binoculars
        {
            get
            {
                try
                {
                    if (_binoFsm == null && PlayerCamera != null)
                    {
                        var t = PlayerCamera.Find("ItemAnim/Binocular Anim");
                        if (t != null) foreach (var f in t.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Animation") { _binoFsm = f; break; }
                    }
                    return _binoFsm != null && _binoFsm.enabled && _binoFsm.ActiveStateName == "on";
                }
                catch (Exception) { return false; }
            }
        }

        public static string GrenadeState
        {
            get
            {
                try { return GrenadeFsm != null && GrenadeFsm.gameObject.activeInHierarchy ? GrenadeFsm.ActiveStateName : ""; }
                catch (Exception) { return ""; }
            }
        }
        // melee / bare hands: the drawn weapon's [Attack] FSM (on -> fire: SphereCast = the hit, at once; then its float "Time" (0.35 hands,
        // 0.4 machete ...) counts down to the next swing; a held Fire button goes straight on to the next one)
        private static readonly Dictionary<string, PlayMakerFSM> _attackFsms = new Dictionary<string, PlayMakerFSM>();
        public static bool Attack(string weapon, out string state, out float time)
        {
            state = ""; time = 0f;
            if (string.IsNullOrEmpty(weapon) || WeaponsParent == null) return false;
            try
            {
                PlayMakerFSM f;
                if (!_attackFsms.TryGetValue(weapon, out f) || f == null)
                {
                    f = null;
                    var w = WeaponsParent.Find(weapon);
                    if (w != null) foreach (var c in w.GetComponents<PlayMakerFSM>()) if (c.FsmName == "Attack") { f = c; break; }
                    _attackFsms[weapon] = f;
                }
                if (f == null || !f.gameObject.activeInHierarchy) return false;
                state = f.ActiveStateName ?? "";
                var v = f.FsmVariables.GetFsmFloat("Time");
                time = v != null ? v.Value : 0f;
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
