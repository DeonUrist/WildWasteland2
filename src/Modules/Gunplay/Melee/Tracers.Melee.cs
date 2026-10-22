using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>Melee (Wave A split of Tracers.cs, B09): blood on corpses, blade stab sound, melee damage on wheels and fitted wheels.</summary>
    internal static partial class Tracers
    {
        // ---------- melee on corpses (1.6.1) ----------
        // The player's melee weapons (hands, kick, old_knife, shiv, machete, pipe_wrench: an Attack FSM without Reload under WeaponsArm) pick
        // the hit effect in their own "getLayer" state: GetLayer(hitObj) == 10 (Actor) -> "actorHitSound" (knife_hit + BloodHit_Effect),
        // anything else -> Crash-02 + MeleeHit_Effect (brown dust). A corpse's limbs are on Default, so a corpse got dust like the guns did
        // before 1.5.2. Postfix on GetLayer.OnEnter (DoGetLayer could be inlined): in that state, on a corpse, the stored layer becomes 10 -
        // the FSM's own blood path runs. A blade (name in hidden [Tracers] StabWeapons) stabs living actors and corpses with Sounds/knifestab.wav
        // instead of knife_hit: postfix on SetAudioClip.OnEnter in "actorHitSound" of the player's melee FSM.
        private static AudioClip _stab; private static bool _stabTried;

        /// <summary>The weapon is one of the player's melee weapons (under WeaponsArm, no Reload FSM). Cached.</summary>
        internal static bool IsPlayerMelee(GameObject weapon)
        {
            if (weapon == null) return false;
            bool melee;
            if (_isMelee.TryGetValue(weapon.GetInstanceID(), out melee)) return melee;
            // the player's weapons live under PlayerCameraHolder/PlayerCamera/WeaponsArm (Camera.main may be another camera)
            melee = false;
            for (var t = weapon.transform.parent; t != null; t = t.parent) if (t.name == "WeaponsArm") { melee = true; break; }
            if (melee) foreach (var f in weapon.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "Reload") { melee = false; break; }
            _isMelee[weapon.GetInstanceID()] = melee;
            return melee;
        }

        /// <summary>Harmony postfix on GetLayer.OnEnter: in the player's melee "getLayer" state a corpse counts as an actor, so it bleeds.</summary>
        public static void AfterGetLayer(GetLayer __instance)
        {
            try
            {
                if (!GunplaySettings.TracersEnabled.Value) return;
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Attack" || __instance.State == null || __instance.State.Name != "getLayer") return;
                if (__instance.storeResult == null || __instance.storeResult.IsNone || __instance.storeResult.Value == 10) return;
                if (!IsPlayerMelee(fsm.GameObject)) return;
                var hit = __instance.gameObject != null ? __instance.gameObject.Value : null;
                if (hit == null || !IsCorpse(hit)) return;
                __instance.storeResult.Value = 10;                       // the FSM goes on to actorHitSound: knife_hit + BloodHit_Effect
                if (Log.L2) GunplayLog.Deep("Hit: " + fsm.GameObject.name + " (melee) -> corpse " + hit.transform.root.name + "/" + hit.name + ": blood" + (IsBlade(fsm.GameObject.name) ? ", stab sound" : ""));
            }
            catch (Exception e) { GunplayLog.Error("Tracers melee corpse", e); }
        }

        /// <summary>Harmony postfix on SetAudioClip.OnEnter: blades play the stab sound in "actorHitSound". First filter: FSM name length.</summary>
        public static void AfterSetAudioClip(SetAudioClip __instance)
        {
            if (!GunplaySettings.TracersEnabled.Value) return;
            try
            {
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name.Length != 6 || fsm.Name != "Attack" || __instance.State == null || __instance.State.Name != "actorHitSound" || fsm.GameObject == null) return;
                if (!IsPlayerMelee(fsm.GameObject) || !IsBlade(fsm.GameObject.name)) return;
                var clip = StabClip();
                if (clip == null) return;
                var go = fsm.GetOwnerDefaultTarget(__instance.gameObject);
                var src = go != null ? go.GetComponent<AudioSource>() : null;
                if (src != null)
                {
                    src.clip = clip;                                    // the hit state's AudioPlay plays it (pitch 0.5-1.5, volume 0.5 as vanilla)
                    if (Log.L2) GunplayLog.Deep("Hit: " + fsm.GameObject.name + " (melee): stab sound");
                }
            }
            catch (Exception e) { GunplayLog.Error("Tracers stab sound", e); }
        }

        /// <summary>Core.Hooks.CreateObjectEnter prefix: the player's melee hit on car metal shows metal sparks instead of dust; false = replaced.</summary>
        public static bool BeforeMeleeEffect(CreateObject __instance)
        {
            if (!GunplaySettings.TracersEnabled.Value || !GunplaySettings.MetalSparks.Value) return true;
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Name.Length != 9 || fsm.Name != "HitEffect" || !IsPlayerMelee(fsm.GameObject)) return true;
            var attack = FsmUtil.Find(fsm.GameObject, "Attack");
            var target = attack != null ? attack.FsmVariables.GetFsmGameObject("hitObj") : null;
            var hit = target != null ? target.Value : null;
            if (hit == null || !IsCarMetal(hit.transform)) return true;
            var col = hit.GetComponent<Collider>();
            if (col == null) return true;
            FindEffects();
            if (_metalImpact == null) return true;
            MetalSparks(col, __instance.position.Value, __instance.rotation.Value);
            if (Log.L2) GunplayLog.Deep("Hit: " + fsm.GameObject.name + " (melee) -> " + hit.name + ": metal sparks");
            __instance.Finish();
            return false;
        }

        // (WW perf) [Gunplay / Melee] StabWeapons parsed once per setting text instead of a string.Split per blade hit
        private static string _bladeSource;
        private static string[] _blades = new string[0];

        /// <summary>The weapon name contains one of the StabWeapons entries (case-insensitive).</summary>
        private static bool IsBlade(string weapon)
        {
            string list = GunplaySettings.StabWeapons.Value ?? "";
            if (!ReferenceEquals(list, _bladeSource))
            {
                var parts = new List<string>();
                foreach (var part in list.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)) { string k = part.Trim(); if (k.Length > 0) parts.Add(k); }
                _blades = parts.ToArray(); _bladeSource = list;
            }
            for (int i = 0; i < _blades.Length; i++) if (weapon.IndexOf(_blades[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // Sounds/knifestab.wav next to the DLL (hidden [Tracers] StabSound), loaded once
        /// <summary>Sounds/knifestab.wav from the Gunplay asset folder, amplified for AudioPlay's 0.5 volume; loaded once.</summary>
        internal static AudioClip StabClip()
        {
            if (_stab != null || _stabTried) return _stab;
            _stabTried = true;
            string rel = GunplaySettings.StabSound.Value ?? "";
            if (rel.Length == 0) return null;
            string path = System.IO.Path.IsPathRooted(rel) ? rel : System.IO.Path.Combine(GunplayModule.AssetDir ?? "", rel);
            try
            {
                if (!System.IO.File.Exists(path)) { GunplayLog.Warn("Tracers: no stab sound at " + path + " - blades get the game's knife sound"); return null; }
                int ch, rate;
                float[] smp = Core.Audio.DecodeWav(System.IO.File.ReadAllBytes(path), out ch, out rate);
                // AudioPlay sets source volume to 0.5; amplify the clip for an effective 1.5 (source volume clamps at 1).
                for (int i = 0; i < smp.Length; i++) smp[i] *= 1.5f / 0.5f;
                _stab = AudioClip.Create("knifestab", Math.Max(1, smp.Length / ch), ch, rate, false);
                _stab.SetData(smp, 0);
                _stab.hideFlags = HideFlags.DontUnloadUnusedAsset;
                if (Log.L1) GunplayLog.Main("Tracers: stab sound loaded (" + System.IO.Path.GetFileName(path) + ", " + (smp.Length / ch / (float)rate).ToString("0.00") + " s)");
            }
            catch (Exception e) { GunplayLog.Warn("Tracers: stab sound " + path + " not loaded: " + e.Message); _stab = null; }
            return _stab;
        }

        // ---------- melee on wheels ----------
        // The player's melee weapons (an "Attack" FSM without a Reload FSM, under the camera) hit with SetFsmFloat(hitObj / Bodypart.Damage) +
        // SendEvent Damage; a vehicle part's Bodypart FSM takes that straight off its Condition. Postfix on SetFsmFloat.OnEnter (DoSetFsmFloat could be inlined): when the
        // target is a wheel, its Bodypart.Damage is multiplied by [Tracers] WheelDamageMultiplier (and shown in blue); a wheel brought to 0
        // jumps off next frame (WheelPopOff).
        /// <summary>A wheel hit by melee, checked for pop-off the next frame.</summary>
        private struct MeleeWheel { public Transform Part; public float Before; public Vector3 Dir; public int Frame; }
        private static readonly List<MeleeWheel> _meleeWheels = new List<MeleeWheel>();
        private static readonly Dictionary<int, bool> _isMelee = new Dictionary<int, bool>();
        /// <summary>Core.Hooks.SetFsmFloatEnter postfix: the player's melee damage on a wheel x WheelDamageMultiplier, part hit feedback and pop-off check. First filter: FSM name length.</summary>
        public static void AfterSetFsmFloat(SetFsmFloat __instance)
        {
            if (!GunplaySettings.MeleeOnWheels.Value) return;                 // (WW) [Guns] MeleeOnWheels
            try
            {
                var fsm = __instance.Fsm;
                if (fsm == null) return;
                string fn = fsm.Name;                                     // (WW) owner name first: one length compare rejects most FSMs
                if (fn == null || fn.Length != 6 || fn != "Attack" || fsm.GameObject == null) return;
                if (__instance.fsmName == null || __instance.fsmName.Value != "Bodypart" || __instance.variableName == null || __instance.variableName.Value != "Damage") return;
                var weapon = fsm.GameObject;
                if (!IsPlayerMelee(weapon)) return;
                var target = fsm.GetOwnerDefaultTarget(__instance.gameObject);
                if (target == null) return;
                var part = OwningPart(target.transform);
                if (part == null || !IsWheel(target.transform, part)) return;
                float mult = Mathf.Max(0f, GunplaySettings.WheelDamageMultiplier.Value);
                float before = PartCondition(part);
                var tf = FsmBuffers.Rent(); target.GetComponents(tf);
                foreach (var f in tf)
                {
                    if (f == null || f.FsmName != "Bodypart") continue;
                    var v = f.FsmVariables.FindFsmFloat("Damage");
                    if (v == null) continue;
                    v.Value *= mult;
                    Bridge.PartHit(part.gameObject, target.transform.position, Mathf.Abs(v.Value));
                    if (Log.L2) GunplayLog.Deep("Hit: " + weapon.name + " (melee) -> " + part.name + ", condition " + before.ToString("0.0") + " " + v.Value.ToString("0.0") + " (x" + mult.ToString("0.#") + ")");
                    break;
                }
                FsmBuffers.Return(tf);
                var mainCam = GameRefs.MainCamera;
                Vector3 dir = mainCam != null ? mainCam.transform.forward : Vector3.forward;
                CombatEvents.PartHit(part, GameRefs.Player, true, before, -1f, true);
                if (GunplaySettings.WheelPopOff.Value && before > 0f) _meleeWheels.Add(new MeleeWheel { Part = part, Before = before, Dir = dir, Frame = Time.frameCount });
            }
            catch (Exception e) { GunplayLog.Error("Tracers melee", e); }
        }

        // ---------- melee on FITTED wheels (1.5.1) ----------
        // A fitted wheel's collider is a trigger (its CheckTag FSM, state vehPart: ColliderSetIsTrigger true) and the melee weapons' own
        // SphereCast2 (PlayerCamera, r 0.02, 1.8 m) ignores triggers: knives cut loose wheels but passed through fitted ones, so the hook
        // above never ran for them. While a melee Attack FSM is in "fire" (the swing) the cast is repeated with triggers; a fitted wheel
        // met before anything solid gets the weapon's hit value x WheelDamageMultiplier on its Bodypart (+ the Damage event), the blue hit
        // number, and the WheelPopOff check - once per swing. (Apocapatrol has a plain version that steps aside when it sees this type.)
        private static PlayMakerFSM _wSwing;
        // (WW) weapon child instance id -> its Attack FSMs (GetComponents per child per frame allocated an array every frame)
        private static readonly Dictionary<int, PlayMakerFSM[]> _wAttack = new Dictionary<int, PlayMakerFSM[]>();
        private static readonly List<PlayMakerFSM> _wBuf = new List<PlayMakerFSM>(4);
        private static readonly PlayMakerFSM[] NoFsm = new PlayMakerFSM[0];
        /// <summary>Attack FSMs of a weapon child (cached per child).</summary>
        private static PlayMakerFSM[] AttackFsms(Transform w)
        {
            PlayMakerFSM[] a;
            int id = w.GetInstanceID();
            if (_wAttack.TryGetValue(id, out a)) return a;
            w.GetComponents(_wBuf);
            int n = 0;
            for (int i = 0; i < _wBuf.Count; i++) if (_wBuf[i] != null && _wBuf[i].FsmName == "Attack") n++;
            a = n == 0 ? NoFsm : new PlayMakerFSM[n];
            n = 0;
            for (int i = 0; i < _wBuf.Count; i++) if (_wBuf[i] != null && _wBuf[i].FsmName == "Attack") a[n++] = _wBuf[i];
            _wBuf.Clear();
            _wAttack[id] = a;
            return a;
        }
        private static bool _wDone;
        private static readonly Dictionary<int, string> _wPrev = new Dictionary<int, string>();   // Attack FSM id -> last seen state
        private static readonly Dictionary<int, float> _wDamage = new Dictionary<int, float>();   // Attack FSM id -> hit value; NaN = not melee
        private static readonly RaycastHit[] _wHits = new RaycastHit[24];
        private const int WheelSwingMask = (1 << 0) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 13) | (1 << 14) | (1 << 16);

        /// <summary>Per frame: a melee swing repeats the game's cast with triggers, so fitted wheels take the hit once per swing.</summary>
        private static void SwingAtFittedWheels()
        {
            // (WW) the rig comes from Core PlayerState (retried by Core while missing)
            var _wParent = PlayerState.WeaponsParent;
            if (_wParent == null) return;
            var _wCam = PlayerState.PlayerCamera;
            // a swing: the Attack FSM in "fire", or (1.5.4) one that just left "on" for "hit" / "wait" - when the game's own cast meets
            // something at once (the hub around a fitted tyre), on -> fire -> hit -> wait runs within ONE frame and "fire" is never seen here
            PlayMakerFSM active = null;
            bool fresh = false;
            for (int i = 0; i < _wParent.childCount; i++)
            {
                var w = _wParent.GetChild(i);
                if (!w.gameObject.activeInHierarchy) continue;
                foreach (var f in AttackFsms(w))
                {
                    if (f == null || f.Fsm == null || !f.Fsm.Initialized) continue;
                    int fid = f.GetInstanceID();
                    float known;
                    if (_wDamage.TryGetValue(fid, out known) && float.IsNaN(known)) break;   // (WW perf) a gun's Attack FSM (known not melee): nothing to track
                    string cur = f.ActiveStateName, prev;
                    _wPrev.TryGetValue(fid, out prev);
                    _wPrev[fid] = cur;
                    if (active != null) break;
                    if (cur == "fire") active = f;
                    else if (prev == "on" && (cur == "hit" || cur == "wait")) { active = f; fresh = true; }
                    break;
                }
            }
            if (active == null) { _wSwing = null; _wDone = false; return; }
            if (active != _wSwing || fresh) { _wSwing = active; _wDone = false; }
            if (_wDone) return;
            float dmg = SwingDamage(active);
            if (float.IsNaN(dmg)) return;

            int n = Physics.SphereCastNonAlloc(_wCam.position, 0.02f, _wCam.forward, _wHits, 1.8f, WheelSwingMask, QueryTriggerInteraction.Collide);
            if (n <= 0) return;
            Array.Sort(_wHits, 0, n, HitDistance.Instance);                // (WW) was a new Comparer + lambda per swing
            Transform wheel = null; Vector3 at = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var col = _wHits[i].collider;
                if (col == null) continue;
                var root = col.transform.root;
                if (root == _wCam.root || root.name == "Player") continue;   // the player's own body / arms
                var w = FittedWheelAt(col.transform);
                if (w != null) { wheel = w; at = _wHits[i].point; break; }
                if (!col.isTrigger) return;          // something solid first (a body panel, a door, the ground): the game's own cast handles that
            }
            if (wheel == null) return;
            _wDone = true;
            { var observed=new Core.Events.PartAttackObserved { Part=Core.Entities.Of(wheel.gameObject),Attacker=Core.Entities.Of(Core.GameRefs.Player) };Core.ModEvents.Publish(ref observed); }
            float mult = Mathf.Max(0f, GunplaySettings.WheelDamageMultiplier.Value);
            float before = PartCondition(wheel);
            foreach (var f in wheel.GetComponents<PlayMakerFSM>())
            {
                if (f == null || f.FsmName != "Bodypart") continue;
                var v = f.FsmVariables.FindFsmFloat("Damage");
                if (v == null) break;
                v.Value = dmg * mult;
                f.SendEvent("Damage");
                Bridge.PartHit(wheel.gameObject, at == Vector3.zero ? wheel.position : at, Mathf.Abs(v.Value));
                if (Log.L2) GunplayLog.Deep("Hit: " + active.gameObject.name + " (melee, fitted wheel) -> " + wheel.name + ", condition " + before.ToString("0.0") + " " + v.Value.ToString("0.0") + " (x" + mult.ToString("0.#") + ")");
                break;
            }
            CombatEvents.PartHit(wheel, GameRefs.Player, true, before, -1f, true);
            if (GunplaySettings.WheelPopOff.Value && before > 0f) _meleeWheels.Add(new MeleeWheel { Part = wheel, Before = before, Dir = _wCam.forward, Frame = Time.frameCount });
        }

        // (1.5.3) The wheel a collider belongs to: the wheel item itself (tag vehPart on a hinge_wheel*), or anything else under a wheel
        // hinge - the solid sphere collider of hinge_wheel*/wheel_hub (the game's AddSphereCollider, about the tyre's size, part of the
        // FRAME) wraps the fitted wheel, so a swing meets it before the tyre's own trigger and 1.5.1 stopped there as "solid first".
        // That hub now counts as the wheel on its hinge. Another part on the way up (a fender, a plate) = not a wheel.
        /// <summary>The fitted wheel a collider belongs to (wheel item or anything under its hinge), else null.</summary>
        private static Transform FittedWheelAt(Transform t)
        {
            for (var a = t; a != null; a = a.parent)
            {
                if (a.CompareTag("vehPart"))
                    return a.parent != null && a.parent.name.StartsWith("hinge_wheel", StringComparison.Ordinal) ? a : null;
                if (a.name.StartsWith("hinge_wheel", StringComparison.Ordinal))
                {
                    for (int i = 0; i < a.childCount; i++) if (a.GetChild(i).CompareTag("vehPart")) return a.GetChild(i);
                    return null;                     // a bare hub: no wheel fitted
                }
            }
            return null;
        }

        // the weapon's "hit" state value (SetFsmFloat Bodypart.Damage, old_knife -12); a weapon with a Reload FSM is a gun
        /// <summary>The weapon's "hit" state damage value (NaN = a gun). Cached.</summary>
        private static float SwingDamage(PlayMakerFSM attack)
        {
            float d;
            if (_wDamage.TryGetValue(attack.GetInstanceID(), out d)) return d;
            d = float.NaN;
            bool gun = false;
            foreach (var x in attack.GetComponents<PlayMakerFSM>()) if (x != null && x.FsmName == "Reload") { gun = true; break; }
            if (!gun)
                foreach (var st in attack.Fsm.States)
                {
                    if (st.Name != "hit") continue;
                    var acts = st.Actions;
                    if (acts == null || acts.Length == 0) { st.LoadActions(); acts = st.Actions; }
                    if (acts != null)
                        foreach (var a in acts)
                        {
                            var sf = a as SetFsmFloat;
                            if (sf != null && sf.fsmName != null && sf.fsmName.Value == "Bodypart" && sf.variableName != null && sf.variableName.Value == "Damage" && sf.setValue != null) { d = sf.setValue.Value; break; }
                        }
                    break;
                }
            _wDamage[attack.GetInstanceID()] = d;
            return d;
        }

        /// <summary>Condition of a vehicle part (its own Condition/Repair FSM), -1 when none.</summary>
        private static float PartCondition(Transform part)
        {
            var fsms = FsmBuffers.Rent();
            part.GetComponentsInChildren(true, fsms);
            try
            {
                foreach (var f in fsms)
                {
                    if (f == null || (f.FsmName != "Condition" && f.FsmName != "Repair") || f.Fsm == null || !f.Fsm.Initialized) continue;
                    if (OwningPart(f.transform) != part) continue;
                    var v = f.FsmVariables.FindFsmFloat("Condition");
                    if (v != null) return v.Value;
                }
                return -1f;
            }
            finally { FsmBuffers.Return(fsms); }
        }

        /// <summary>The frame after a melee wheel hit: a wheel at 0 pops off.</summary>
        private static void CheckMeleeWheels()
        {
            for (int i = _meleeWheels.Count - 1; i >= 0; i--)
            {
                var m = _meleeWheels[i];
                if (Time.frameCount <= m.Frame) continue;       // the Damage event lands the same frame; look the frame after
                _meleeWheels.RemoveAt(i);
                if (m.Part == null || !m.Part.CompareTag("vehPart")) continue;
                float c = PartCondition(m.Part);
                if (c >= 0f && c <= 0f) PopOff(m.Part, m.Dir);
            }
        }
    }
}
