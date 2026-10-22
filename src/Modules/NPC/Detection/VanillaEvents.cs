using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    // Observe vanilla actions without replacing them. Gunplay sends corresponding events
    // at projectile creation/impact, including its immediate over-cap fallback.
    internal static class VanillaEvents
    {
        // (WW) Core.Hooks.SensorRayHitEnter has no __state: the lengthened burst sensor is kept here (prefix) and restored by the
        // finalizer of the same OnEnter (a small stack: an OnEnter inside an OnEnter stays paired).
        internal sealed class RayState { public SensorGetDetectionRayHit Action; public Micosmo.SensorToolkit.RaySensor Sensor; public float Length; }
        private static readonly List<RayState> _rays = new List<RayState>(4);
        private static readonly List<RayState> _rayPool = new List<RayState>(4);
        public static bool BeforeNpcShot(SensorGetDetectionRayHit __instance)
        {
            if (!NpcModule.Running) return true;
            try
            {
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Damage Ranged" || fsm.GameObject == null) return true;
                Brain.Shot(fsm.GameObject, fsm);        // (1.1.2) one ray = one shot of the magazine (counted whatever else is on)
                if (WeaponRanges.ProjectilesEnabled || (!Senses.On && !NpcModule.AimEnabled.Value && !NpcModule.BrainEnabled.Value)) return true;
                WeaponRanges.Kind kind;
                var owner = fsm.GameObject;
                if (!WeaponRanges.GunKindOf(owner, out kind)) return true;
                // Vanilla's gun sensor is only 80 m. Aim chooses positions from the
                // effective range; repulse at that reach so rifle/sniper NPCs can hit
                // from those positions without requiring Gunplay's projectiles.
                var refs = RefsOf(owner);
                if (NpcModule.AimEnabled.Value || NpcModule.BrainEnabled.Value)
                {
                    var sensor = refs.Sensor;
                    if (sensor != null)
                    {
                        RayState st;
                        if (_rayPool.Count > 0) { st = _rayPool[_rayPool.Count - 1]; _rayPool.RemoveAt(_rayPool.Count - 1); } else st = new RayState();
                        st.Action = __instance; st.Sensor = sensor; st.Length = sensor.Length;
                        _rays.Add(st);
                        sensor.Length = WeaponRanges.RangeOf(kind);
                        sensor.Pulse();
                    }
                }
                Vector3 origin = refs.Muzzle != null ? refs.Muzzle.position : owner.transform.position;
                if (Senses.On) Senses.Shot(owner.transform.root.gameObject, origin, kind, false);
            }
            catch (Exception e) { NpcModule.Warn("Vanilla NPC shot observation: " + e.Message); }
            return true;
        }

        // per NPC gunman: its burst sensor and the muzzle (fire_effect) of the gun in its hand. Looked up once instead of a full
        // GetComponentsInChildren + a name string per bone on every burst ray; the muzzle is looked up again when that gun is put away.
        private sealed class ShooterRefs { public GameObject Owner; public Micosmo.SensorToolkit.RaySensor Sensor; public Transform Muzzle; public float NextScan; }
        private static readonly System.Collections.Generic.Dictionary<int, ShooterRefs> _refs = new System.Collections.Generic.Dictionary<int, ShooterRefs>();
        private static ShooterRefs RefsOf(GameObject owner)
        {
            int id = owner.GetInstanceID();
            ShooterRefs r;
            if (!_refs.TryGetValue(id, out r) || r.Owner != owner)
            {
                if (_refs.Count > 512) _refs.Clear();
                var rayObject = owner.transform.Find("AttackRaycast_Ranged");
                r = new ShooterRefs { Owner = owner, Sensor = rayObject != null ? rayObject.GetComponent<Micosmo.SensorToolkit.RaySensor>() : null };
                _refs[id] = r;
            }
            if ((r.Muzzle == null || r.Muzzle.parent == null || !r.Muzzle.parent.gameObject.activeInHierarchy) && Time.time >= r.NextScan)
            {
                r.Muzzle = null; r.NextScan = Time.time + 1f;
                foreach (var t in owner.GetComponentsInChildren<Transform>(true))
                    if (t.name == "fire_effect" && t.parent != null && t.parent.name != "fire_effect" && t.parent.gameObject.activeInHierarchy)
                    { r.Muzzle = t; break; }
            }
            return r;
        }

        public static Exception AfterNpcShot(SensorGetDetectionRayHit __instance, Exception __exception)
        {
            for (int i = _rays.Count - 1; i >= 0; i--)
            {
                var st = _rays[i];
                if (!ReferenceEquals(st.Action, __instance)) continue;
                if (st.Sensor != null) st.Sensor.Length = st.Length;
                _rays.RemoveAt(i); st.Action = null; st.Sensor = null; _rayPool.Add(st);
                break;
            }
            return __exception;
        }

        // Shutdown: a sensor still lengthened (an exception between prefix and finalizer cannot leave one, but be safe) gets its length back
        internal static void Shutdown()
        {
            foreach (var st in _rays) if (st.Sensor != null) st.Sensor.Length = st.Length;
            _rays.Clear(); _refs.Clear(); _shotInfo.Clear();
        }

        internal static bool PlayerWeapon(GameObject weapon)
        {
            for (var t = weapon != null ? weapon.transform : null; t != null; t = t.parent)
                if (t.name == "PlayerCamera" || t.name == "PlayerCameraHolder") return true;
            return false;
        }

        private sealed class ShotInfo { public bool Gun; public WeaponRanges.Kind Kind; public Transform[] Muzzles; }
        private static readonly Dictionary<Fsm, ShotInfo> _shotInfo = new Dictionary<Fsm, ShotInfo>();
        private static readonly List<Transform> _muzzleBuf = new List<Transform>();
        internal static void OnSceneLoaded() { _shotInfo.Clear(); }
        private static ShotInfo MakeShotInfo(Fsm fsm)
        {
            var si = new ShotInfo { Muzzles = new Transform[0] };
            foreach (var f in fsm.GameObject.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "Reload") { si.Gun = true; break; }
            if (!si.Gun) return si;
            var kind = WeaponRanges.Classify(fsm.GameObject.name);
            bool shotgun = false;
            var hit = fsm.GetState("hit");
            if (hit != null && hit.Actions != null)
                foreach (var a in hit.Actions)
                { var ic = a as IntCompare; if (ic != null && ic.integer1 != null && ic.integer1.Name == "pellets" && ic.integer2 != null) { shotgun = true; break; } }
            if (shotgun) kind = WeaponRanges.Kind.Shotgun;
            else if (kind == WeaponRanges.Kind.Shotgun) kind = WeaponRanges.Kind.Rifle;
            si.Kind = kind;
            _muzzleBuf.Clear();
            foreach (var t in fsm.GameObject.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("muzzle_flesh_effect", StringComparison.Ordinal) || t.name.StartsWith("muzzle_flash", StringComparison.Ordinal)) _muzzleBuf.Add(t);
            si.Muzzles = _muzzleBuf.ToArray();
            return si;
        }

        public static bool BeforePlayerShot(Raycast __instance)
        {
            try
            {
                if (!Senses.On || WeaponRanges.ProjectilesEnabled) return true;
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Attack" || fsm.GameObject == null || __instance.repeatInterval == null || __instance.repeatInterval.Value != 0f) return true;
                // (perf) per Attack FSM, once: has a Reload FSM (a gun), its kind and its muzzle transforms (the NPCs' Attack FSMs end here
                // after one dictionary lookup instead of a GetComponents per ray)
                ShotInfo si;
                if (!_shotInfo.TryGetValue(fsm, out si))
                {
                    if (_shotInfo.Count > 512) _shotInfo.Clear();
                    si = MakeShotInfo(fsm);
                    _shotInfo[fsm] = si;
                }
                if (!si.Gun || !PlayerWeapon(fsm.GameObject)) return true;
                var from = fsm.GetOwnerDefaultTarget(__instance.fromGameObject);
                Vector3 position = from != null ? from.transform.position : fsm.GameObject.transform.position;
                bool found = false;
                foreach (var t in si.Muzzles)
                    if (t != null && t.gameObject.activeInHierarchy) { position = t.position; found = true; break; }
                if (!found)       // none of the known muzzles is lit: the full search as before (a flash spawned at run time), remembered for next time
                {
                    _muzzleBuf.Clear();
                    foreach (var t in fsm.GameObject.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith("muzzle_flesh_effect", StringComparison.Ordinal) || t.name.StartsWith("muzzle_flash", StringComparison.Ordinal))
                        { _muzzleBuf.Add(t); if (!found && t.gameObject.activeInHierarchy) { position = t.position; found = true; } }
                    si.Muzzles = _muzzleBuf.ToArray();
                }
                Senses.Shot(Core.GameRefs.Player, position, si.Kind, true);
            }
            catch (Exception e) { NpcModule.Warn("Vanilla player shot observation: " + e.Message); }
            return true;
        }

        public static void AfterDamage(SetFsmFloat __instance)
        {
            try
            {
                if (!Senses.On || __instance.fsmName == null || __instance.fsmName.Value != "Bodypart" || __instance.variableName == null || __instance.variableName.Value != "Damage" || __instance.setValue == null || __instance.setValue.Value >= 0f) return;
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.GameObject == null) return;
                bool player = fsm.Name == "Attack" && PlayerWeapon(fsm.GameObject);
                bool ranged = fsm.Name == "Damage Ranged";
                bool melee = fsm.Name == "Damage";
                if (!player && !ranged && !melee) return;
                // Melee stays vanilla with Gunplay installed, and still needs this observer.
                bool gun = ranged;
                if (player)
                    foreach (var f in fsm.GameObject.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "Reload") { gun = true; break; }
                if (gun && WeaponRanges.ProjectilesEnabled) return;
                var target = fsm.GetOwnerDefaultTarget(__instance.gameObject);
                if (target == null) return;
                if(melee&&!player){Core.Services.RaiseNpcHurt(target,fsm.GameObject);return;}
                Senses.Hurt(target, player ? Core.GameRefs.Player : fsm.GameObject);
            }
            catch (Exception e) { NpcModule.Warn("Vanilla hurt observation: " + e.Message); }
        }
    }
}
