using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// <summary>NPC shots (Wave A split of Tracers.cs, B09): the SensorGetDetectionRayHit prefix that fires a projectile, shooter info, muzzle, target velocity for the lead and the NPC weapon kind.</summary>
    internal static partial class Tracers
    {
        // ---------- the hook ----------
        // Harmony prefix on Micosmo.SensorToolkit.PlayMaker.SensorGetDetectionRayHit.OnEnter. false = vanilla skipped.
        public static bool BeforeRayHit(SensorGetDetectionRayHit __instance)
        {
            try
            {
                if (!GunplaySettings.TracersEnabled.Value) return true;
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Damage Ranged") return true;
                var owner = fsm.GameObject;
                if (owner == null) return true;
                var target = __instance.targetObject != null ? __instance.targetObject.Value : null;
                if (target == null) return true;
                var info = Info(fsm, owner);
                if (!info.IsGun) return true;

                // The vanilla target is whatever collider-object the sensor found nearest - for the player that is the "head" child
                // (its own Bodypart FSM), 3 cm nearer to an NPC's eye-level sensor than the body pivot. The bullet needs the whole
                // body: resolve to the root that carries the capsule, remember the head for head hits, and aim at the body centre
                // ([Tracers] NpcAimAtBody) instead of the head so the vanilla jitter lands on the body, not above it.
                GameObject head = null;
                var root = BodyRootOf(target);
                if (root != null && root != target) { head = target; target = root; }
                else if (root != null) head = HeadOf(root);
                var offsetVar = fsm.Variables.GetFsmVector3("offset");
                Vector3 jitter = offsetVar != null ? offsetVar.Value : Vector3.zero;
                // Optional NPCAI spread calculation; standalone uses the preserved default.
                    jitter *= Bridge.SpreadFactor(Vector3.Distance(owner.transform.position, target.transform.position));
                Vector3 aim;
                var cap0 = root != null ? PlayerCapsule(root) : null;
                if (cap0 != null && GunplaySettings.AimAtBody) aim = cap0.transform.TransformPoint(cap0.center);
                else aim = (head != null ? head : target).transform.position;
                Transform muzzle = MuzzleOf(info, owner);
                Vector3 from = muzzle != null ? muzzle.TransformPoint(info.MuzzleLocal) : fsm.GetOwnerDefaultTarget(__instance.gameObject).transform.position;
                float speed0 = info.Kind == Kind.Crossbow ? GunplaySettings.BoltSpeed.Value : GunplaySettings.BulletSpeed.Value;
                if (WeaponStats.Instance.HasModifiers) speed0 = WeaponStats.Instance.Modify(info.Handle, null, WeaponStat.ProjectileSpeed, speed0);
                // lead a moving target ([NpcAim] LeadTargets): where it will be when the bullet arrives, as well as this NPC can guess
                if (GunplaySettings.Lead)
                {
                    Vector3 v = VelocityOf(target, root);
                    v.y *= 0.5f;                                   // a jump is not a direction
                    if (v.sqrMagnitude > 0.25f && speed0 > 1f)
                    {
                        float t = Vector3.Distance(from, aim) / speed0;
                        t = Vector3.Distance(from, aim + v * t) / speed0;   // one refinement: the far point takes longer
                        t = Mathf.Min(t, Mathf.Max(0f, GunplaySettings.MaxLeadTime.Value));
                        float skill = Mathf.Clamp01(GunplaySettings.LeadAccuracy.Value) * info.LeadSkill;
                        float err = 1f + UnityEngine.Random.Range(-1f, 1f) * Mathf.Clamp(GunplaySettings.LeadError.Value, 0f, 100f) / 100f;
                        Vector3 lead = v * (t * skill * err);
                        aim += lead;
                        if (Core.Log.Verbose) Core.Log.Info("[Guns] Lead: " + owner.name + " leads " + lead.magnitude.ToString("0.00") + " m (target " + v.magnitude.ToString("0.0") + " m/s, flight " + t.ToString("0.00") + " s, skill " + skill.ToString("0.00") + ")");
                    }
                }
                aim += jitter;
                Vector3 dir = aim - from;
                if (dir.sqrMagnitude < 1e-4f) dir = owner.transform.forward;
                dir.Normalize();

                float damage = info.DamageVar != null ? info.DamageVar.Value : info.DamageLiteral;
                float range = RangeOf(info.Kind);
                var stats = WeaponStats.Instance;
                if (stats.HasModifiers)
                {
                    range = stats.Modify(info.Handle, null, WeaponStat.Range, range);
                    damage *= stats.Modify(info.Handle, null, WeaponStat.DamageMultiplier, 1f);
                }
                int pellets = info.Kind == Kind.Shotgun ? Mathf.Clamp(GunplaySettings.ShotgunPellets.Value, 1, 8) : 1;
                float spread = info.Kind == Kind.Shotgun ? GunplaySettings.ShotgunPelletSpread.Value : 0f;
                for (int i = 0; i < pellets; i++)
                {
                    Vector3 d = dir;
                    if (spread > 0f && pellets > 1)
                    {
                        Vector2 offset = SpreadOffset(GunplaySettings.Accuracy(false)) * Mathf.Tan(spread * Mathf.Deg2Rad);
                        d = Quaternion.FromToRotation(Vector3.forward, dir) * new Vector3(offset.x, offset.y, 1f).normalized;
                    }
                    var s = new Shot
                    {
                        Pos = from, Start = from, Dir = d,
                        Speed = speed0,
                        Range = range,
                        Damage = damage / pellets * (info.Kind == Kind.Shotgun ? Mathf.Max(0f, GunplaySettings.NpcShotgunDamage.Value) : 1f),
                        Target = target,
                        Head = head,
                        Cap = cap0,
                        ShooterRoot = owner.transform.root.gameObject,
                        Impact = info.Impact,
                        EventName = info.EventName, EventFsm = info.EventFsm,
                        Bolt = info.Kind == Kind.Crossbow,
                        Shotgun = info.Kind == Kind.Shotgun,
                        Kind = info.Kind, DmgMult = 1f,
                        Alive = true,
                        Layers = info.ObstructLayers | info.DetectLayers,
                        DetectLayers = info.DetectLayers,
                    };
                    LaunchBolt(ref s);
                    if (_shots.Count >= GunplaySettings.MaxTracers.Value) { Snapshot(); s.Speed = s.Range * 2f; Step(ref s, 1f); if (s.Visual != null) UnityEngine.Object.Destroy(s.Visual.gameObject); }   // over the cap: hitscan
                    else _shots.Add(s);
                }

                Bridge.Shot(owner.transform.root.gameObject, from, (int)info.Kind, false);
                fsm.Event(__instance.isNotIntersectedEvent);    // vanilla goes down its miss branch: Wait rpm, next shot
                return false;
            }
            catch (Exception e)
            {
                Core.Log.Error("[Guns] Tracers", e);
                return true;
            }
        }


        // ---------- target velocity (for the lead) ----------
        // The player: sampled every frame by Tick and smoothed over ~0.1 s (works however the game moves them, on foot or in a car).
        // Other targets (NPC vs NPC): their Rigidbody, which the Movement FSM drives with SetVelocity.
        private static Vector3 _playerVel, _playerLast;
        private static bool _playerTracked;
        private static float _nextFind;

        private static void TrackPlayer(float dt)
        {
            if (_player == null)
            {
                if (Time.unscaledTime < _nextFind) return;         // no player (menu): look once a second, not every frame
                _nextFind = Time.unscaledTime + 1f;
                _player = GameRefs.Player;
                if (_player == null) { _playerTracked = false; return; }
            }
            Vector3 p = _player.transform.position;
            if (!_playerTracked || dt <= 0f) { _playerLast = p; _playerVel = Vector3.zero; _playerTracked = true; return; }
            Vector3 v = (p - _playerLast) / dt;
            _playerLast = p;
            if (v.sqrMagnitude > 50f * 50f) v = Vector3.zero;              // a teleport / load, not movement
            float k = Mathf.Clamp01(dt / 0.1f);
            _playerVel = Vector3.Lerp(_playerVel, v, k);
        }

        private static Vector3 VelocityOf(GameObject target, GameObject root)
        {
            if (_player != null && (target == _player || (root != null && root == _player))) return _playerTracked ? _playerVel : Vector3.zero;
            var rb = target.GetComponentInParent<Rigidbody>();
            return rb != null ? rb.velocity : Vector3.zero;
        }


        // ---------- shooter info (once per FSM owner) ----------
        private static ShooterInfo Info(Fsm fsm, GameObject owner)
        {
            ShooterInfo info;
            int id = owner.GetInstanceID();
            if (_shooters.TryGetValue(id, out info) && (info.Weapon != null ? info.Weapon.gameObject.activeInHierarchy : Time.time < info.Retry)) return info;
            int tries = info != null && info.Weapon == null ? info.Tries + 1 : 0;
            info = new ShooterInfo { Owner = owner, LeadSkill = UnityEngine.Random.Range(0.5f, 1f), Tries = tries, Retry = tries < 10 ? Time.time + 1f : float.MaxValue };
            _shooters[id] = info;

            // the weapon in the hand: a model with a fire_effect child (guns) or an active "crossbow"
            foreach (var t in owner.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "fire_effect" && t.parent != null && t.parent.name != "fire_effect" && t.parent.gameObject.activeInHierarchy)
                { info.Weapon = t.parent; info.Muzzle = t; break; }
            }
            if (info.Weapon == null)
                foreach (var t in owner.GetComponentsInChildren<Transform>(false))
                    if (t.name.StartsWith("crossbow", StringComparison.OrdinalIgnoreCase) && t.GetComponent<Renderer>() != null)
                    { info.Weapon = t; break; }
            if (info.Weapon == null) return info;    // a monster: vanilla
            info.IsGun = true;
            info.Kind = Classify(info.Weapon.name);
            info.Handle = WeaponCatalog.Instance.Handle(info.Weapon.name);
            if (info.Kind == Kind.Crossbow)
            {
                CacheBoltModel(info.Weapon);
                foreach (var mf in info.Weapon.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.name != "arrow" || mf.sharedMesh == null) continue;
                    var b = mf.sharedMesh.bounds; info.Muzzle = mf.transform; info.MuzzleLocal = new Vector3(b.center.x, b.center.y, b.max.z);
                    if (Core.Log.Verbose) GunplayLog.Main("NPC crossbow shot origin: native arrow tip on " + owner.name);
                    break;
                }
            }

            // the vanilla attack state: impact prefab, damage, event
            var st = fsm.GetState("attack");
            if (st != null && st.Actions != null)
                foreach (var a in st.Actions)
                {
                    var co = a as CreateObject;
                    if (co != null && co.gameObject != null && co.gameObject.Value != null) info.Impact = co.gameObject.Value;
                    var sf = a as SetFsmFloat;
                    if (sf != null && sf.setValue != null)
                    {
                        info.DamageVar = sf.setValue;
                        if (sf.fsmName != null && !string.IsNullOrEmpty(sf.fsmName.Value)) info.BodypartFsm = sf.fsmName.Value;
                        if (sf.variableName != null && !string.IsNullOrEmpty(sf.variableName.Value)) info.BodypartVar = sf.variableName.Value;
                    }
                    var se = a as SendEvent;
                    if (se != null && se.sendEvent != null && !string.IsNullOrEmpty(se.sendEvent.Name))
                    {
                        info.EventName = se.sendEvent.Name;
                        var et = se.eventTarget;
                        info.EventFsm = et != null && et.target == FsmEventTarget.EventTarget.GameObjectFSM && et.fsmName != null && !string.IsNullOrEmpty(et.fsmName.Value) ? et.fsmName.Value : null;
                    }
                }
            if (info.DamageVar == null) info.DamageLiteral = -7f;

            // the vanilla ray's layers
            var ray = owner.transform.Find("AttackRaycast_Ranged");
            if (ray != null)
            {
                var rs = ray.GetComponent<Micosmo.SensorToolkit.RaySensor>();
                if (rs != null) { info.ObstructLayers = rs.ObstructedByLayers.value; info.DetectLayers = rs.DetectsOnLayers.value; }
            }
            if (Core.Log.Verbose) GunplayLog.Main("Tracers: " + owner.name + " fires " + info.Kind + " (" + info.Weapon.name + "), damage " +
                           (info.DamageVar != null ? info.DamageVar.Value : info.DamageLiteral) + ", impact " + (info.Impact != null ? info.Impact.name : "none"));
            return info;
        }

        private static Transform MuzzleOf(ShooterInfo info, GameObject owner)
        {
            if (info.Muzzle != null) return info.Muzzle;
            return info.Weapon;
        }

        /// <summary>Gun kind of a weapon name (weapon catalog rule; the first six WeaponKind values are the Tracers kinds).</summary>
        internal static Kind Classify(string weapon) { return (Kind)(int)WeaponCatalog.GunKindOfName(weapon); }

        /// <summary>The gun an NPC/turret holds: its kind and model name (shooter cache; false for gunless shooters / non-shooters).</summary>
        internal static bool HeldWeaponName(GameObject owner, out Kind kind, out string name)
        {
            name = null;
            if (!GunKindOf(owner, out kind)) return false;
            ShooterInfo info;
            if (!_shooters.TryGetValue(owner.GetInstanceID(), out info) || info.Weapon == null) return false;
            name = info.Weapon.name;
            return true;
        }

        // The gun an NPC holds, for the Aim pacing (cached like Info; finds the Damage Ranged FSM itself).
        internal static bool GunKindOf(GameObject owner, out Kind kind)
        {
            kind = Kind.Rifle;
            ShooterInfo info;
            if (!_shooters.TryGetValue(owner.GetInstanceID(), out info) || (info.Weapon != null ? !info.Weapon.gameObject.activeInHierarchy : Time.time >= info.Retry))
            {
                Fsm dr = null;
                foreach (var f in owner.GetComponents<PlayMakerFSM>())
                    if (f != null && f.FsmName == "Damage Ranged") { dr = f.Fsm; break; }
                if (dr == null) return false;
                info = Info(dr, owner);
            }
            if (!info.IsGun) return false;
            kind = info.Kind;
            return true;
        }
    }
}
