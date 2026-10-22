using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    /// <summary>Player shots (Wave A split of Tracers.cs, B09): the Raycast prefix of first-person guns, the per-gun stats (MakeGun), pellet spread and the camera-ray first hit.</summary>
    internal static partial class Tracers
    {
        // ---------- the player's guns ----------
        private static readonly Dictionary<Fsm, PlayerGun> _guns = new Dictionary<Fsm, PlayerGun>();
        private static readonly Dictionary<Fsm, bool> _notGun = new Dictionary<Fsm, bool>();
        private static GameObject _player;

        // Unit disk: 50% is uniform by area; 0% weights the rim, 100% weights the centre.
        internal static Vector2 SpreadOffset(float accuracy)
        {
            float exponent = 0.5f * Mathf.Pow(2f, (Mathf.Clamp(accuracy, 0f, 100f) - 50f) / 50f);
            float radius = Mathf.Pow(UnityEngine.Random.value, exponent);
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }


        // Harmony prefix on HutongGames.PlayMaker.Actions.Raycast.OnEnter. Only the "fire" Raycast of a first-person gun's Attack FSM
        // (an FSM named Attack whose GameObject also has a Reload FSM; melee weapons have none). false = vanilla skipped.
        public static bool BeforeRaycast(Raycast __instance)
        {
            try
            {
                if (!GunsModule.TracersEnabled.Value || !GunsModule.PlayerTracers.Value) return true;
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Attack" || __instance.repeatInterval == null || __instance.repeatInterval.Value != 0) return true;
                bool no;
                if (_notGun.TryGetValue(fsm, out no)) return true;
                PlayerGun gun;
                if (!_guns.TryGetValue(fsm, out gun))
                {
                    gun = MakeGun(fsm, __instance);
                    if (gun == null) { _notGun[fsm] = true; return true; }
                    _guns[fsm] = gun;
                }

                var from = fsm.GetOwnerDefaultTarget(__instance.fromGameObject);
                if (from == null) return true;
                Transform ft = from.transform;
                Vector3 origin = ft.position + (__instance.fromPosition != null && !__instance.fromPosition.IsNone ? __instance.fromPosition.Value : Vector3.zero);
                Vector3 local = __instance.direction != null && !__instance.direction.IsNone ? __instance.direction.Value : Vector3.forward;
                float range = RangeOf(gun.Kind) * gun.RangeMultiplier;
                if (_player == null) _player = GameRefs.Player;

                Vector3 muzzle = gun.Muzzle != null && gun.Muzzle.gameObject.activeInHierarchy ? gun.Muzzle.position
                               : origin + ft.forward * 0.4f - ft.up * 0.15f;
                Vector3 bodyMuzzle;
                if (Services.Animation != null && Services.Animation.TryPlayerWeaponMuzzle(out bodyMuzzle))
                {
                    muzzle = bodyMuzzle;
                    if (Core.Log.Verbose && _playerMuzzleLogs++ < 3) GunsModule.Verbose("Player shot origin: visible body muzzle " + muzzle);
                }
                for (int p = 0; p < gun.Pellets; p++)
                {
                    Vector3 l = local;
                    if (gun.Kind == Kind.Shotgun)
                    {
                        Vector2 offset = SpreadOffset(GunsModule.Accuracy(true)) * gun.PelletSpread;
                        l = new Vector3(offset.x, offset.y, 1f);
                    }
                    Vector3 dir = __instance.space == Space.Self ? ft.TransformDirection(l) : l;
                    dir.Normalize();
                    // aim where the camera ray points, fly from the muzzle
                    RaycastHit aimHit;
                    Vector3 aim = FirstHit(origin, dir, range, gun.Layers, ft.root, out aimHit) ? aimHit.point : origin + dir * range;
                    Vector3 d = aim - muzzle;
                    if (d.sqrMagnitude < 0.01f) d = dir;
                    var s = new Shot
                    {
                        Pos = muzzle, Start = muzzle, Dir = d.normalized,
                        Speed = gun.Kind == Kind.Crossbow ? GunsModule.BoltSpeed.Value : GunsModule.BulletSpeed.Value,
                        Range = range, Bolt = gun.Kind == Kind.Crossbow, Shotgun = gun.Kind == Kind.Shotgun, Alive = true, Kind = gun.Kind, DmgMult = 1f,
                        Layers = gun.Layers, Gun = gun, Player = _player,
                        ShooterRoot = ft.root.gameObject,
                    };
                    LaunchBolt(ref s);
                    if (_shots.Count >= GunsModule.MaxTracers.Value) { Snapshot(); s.Speed = s.Range * 2f; Step(ref s, 1f); if (s.Visual != null) UnityEngine.Object.Destroy(s.Visual.gameObject); }
                    else _shots.Add(s);
                }
                if (gun.Kind == Kind.Shotgun && Core.Log.Verbose)
                    GunsModule.Verbose("Shotgun spread: circular, " + gun.Pellets + " pellets, player accuracy " + GunsModule.Accuracy(true).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%");
                Bridge.Shot(_player, muzzle, (int)gun.Kind, true);
                __instance.Finish();         // vanilla: no hit -> FINISHED -> wait -> next shot, same rhythm
                return false;
            }
            catch (Exception e)
            {
                Core.Log.Error("[Guns] Tracers (player)", e);
                return true;
            }
        }

        private static PlayerGun MakeGun(Fsm fsm, Raycast ray)
        {
            var go = fsm.GameObject;
            if (go == null) return null;
            bool reload = false;
            foreach (var f in go.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Reload") { reload = true; break; }
            if (!reload) return null;
            var hit = fsm.GetState("hit");
            if (hit == null) return null;
            var gun = new PlayerGun { Fsm = fsm, Hit = hit.Actions };
            foreach (var a in gun.Hit) { var sf = a as SetFsmFloat; if (sf != null && sf.fsmName != null && sf.fsmName.Value == "Bodypart") gun.Damage = sf.setValue; }
            var gl = fsm.GetState("getLayer"); if (gl != null) gun.GetLayer = gl.Actions;
            var ah = fsm.GetState("actorHitSound"); if (ah != null) gun.ActorHit = ah.Actions;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("muzzle_flesh_effect", StringComparison.Ordinal) || t.name.StartsWith("muzzle_flash", StringComparison.Ordinal)) { gun.Muzzle = t; break; }
            // layers: the Raycast's own mask
            int mask = 0;
            if (ray.layerMask != null) foreach (var l in ray.layerMask) if (l != null) mask |= 1 << l.Value;
            if (mask == 0) mask = ~0;
            if (ray.invertMask != null && ray.invertMask.Value) mask = ~mask;
            gun.Layers = mask;
            // shotguns: a "pellets" counter; the hit state loops until pellets > N
            // (FsmVariables.GetFsmInt returns a placeholder for a missing variable, so the variable's presence proves nothing:
            // only the loop itself - IntCompare on {pellets} in the hit state - makes a shotgun. 0.4.3 and earlier fired 9 bullets per shot from every gun.)
            bool shotgun = false;
            foreach (var a in gun.Hit)
            {
                var ic = a as IntCompare;
                if (ic != null && ic.integer1 != null && ic.integer1.Name == "pellets" && ic.integer2 != null)
                { shotgun = true; gun.Pellets = Mathf.Clamp(ic.integer2.Value + 1, 1, 32); }
            }
            if (shotgun)
            {
                var fire = fsm.GetState("fire");
                gun.PelletSpread = 0.02f;
                if (fire != null) foreach (var a in fire.Actions) { var rf = a as RandomFloat; if (rf != null && rf.max != null) { gun.PelletSpread = Mathf.Abs(rf.max.Value); break; } }
                if (go.name.StartsWith("rochester_m24", StringComparison.OrdinalIgnoreCase) || go.name.StartsWith("slamberg_500", StringComparison.OrdinalIgnoreCase))
                {
                    if (go.name.IndexOf("_chopped", StringComparison.OrdinalIgnoreCase) >= 0)
                    { gun.Pellets = 9; gun.PelletSpread = 0.03f; gun.DamageMultiplier = 1.5f; }
                    else gun.RangeMultiplier = 2f;
                }
            }
            gun.Kind = shotgun ? Kind.Shotgun : Classify(go.name);
            if (gun.Kind == Kind.Crossbow) gun.DamageMultiplier = CrossbowDamageMultiplier;
            else if (go.name.StartsWith("redmark_m11", StringComparison.OrdinalIgnoreCase)) gun.DamageMultiplier = 100f / 90f;
            if (gun.Kind == Kind.Crossbow) CacheBoltModel(go.transform);
            if (gun.Kind == Kind.Shotgun && !shotgun) gun.Kind = Kind.Rifle;      // a "rochester" without pellets is not a shotgun
            if (Core.Log.Verbose) GunsModule.Verbose("Tracers: player gun " + go.name + " = " + gun.Kind + (shotgun ? ", " + gun.Pellets + " pellets" : "") + (gun.Muzzle != null ? "" : ", no muzzle point"));
            return gun;
        }

        // the nearest thing the camera ray meets, as the vanilla Raycast sees it (QueriesHitTriggers is on in this game: head
        // triggers with a Bodypart FSM count, other triggers don't), skipping the player and their car
        private static bool FirstHit(Vector3 origin, Vector3 dir, float range, int mask, Transform shooterRoot, out RaycastHit hit)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, _hits, range, mask, QueryTriggerInteraction.Collide);
            if (n >= _hits.Length) { return Physics.Raycast(origin, dir, out hit, range, mask, QueryTriggerInteraction.Ignore); }
            if (n > 1) Array.Sort(_hits, 0, n, HitDistance.Instance);
            for (int k = 0; k < n; k++)
            {
                var c = _hits[k].collider;
                if (c == null || Ignored(c.transform, shooterRoot, _player) || !Counts(c)) continue;
                hit = _hits[k];
                return true;
            }
            hit = default(RaycastHit);
            return false;
        }
    }
}
