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
    /// <summary>Ballistics (Wave A split of Tracers.cs, B09): flight steps, falloff, ranges, hit replay on targets and the world, head factor, the virtual player hitbox, car-part penetration and wheel pop-off.</summary>
    internal static partial class Tracers
    {
        // Does a collider stop a bullet? Solid colliders always; trigger colliders only when their object has a Bodypart FSM
        // (the NPC head capsules, the player's head sphere) - vanilla raycasts hit those (QueriesHitTriggers), FireDamageCollider
        // and other triggers are not bodies. Cached per collider.
        private static readonly Dictionary<int, bool> _counts = new Dictionary<int, bool>();
        internal static bool Counts(Collider c)
        {
            if (!c.isTrigger) return true;
            bool ok;
            int id = c.GetInstanceID();
            if (_counts.TryGetValue(id, out ok)) return ok;
            ok = HasBodypart(c.gameObject);
            _counts[id] = ok;
            if (ok) ReportHitbox(c);
            return ok;
        }


        // Verbose, once per creature type: where its head trigger and body capsule reach, against the visible model
        private static readonly HashSet<string> _reported = new HashSet<string>();
        private static void ReportHitbox(Collider head)
        {
            if (!Core.Log.Verbose) return;
            var root = RootOf(head.transform);
            string name = root.name; int cut = name.IndexOf('('); if (cut > 0) name = name.Substring(0, cut);
            if (!_reported.Add(name)) return;
            try
            {
                var body = PlayerCapsule(root.gameObject);
                float feet = body != null ? body.bounds.min.y : root.position.y;
                float bodyTop = body != null ? body.bounds.max.y - feet : 0f;
                float headTop = head.bounds.max.y - feet, headBottom = head.bounds.min.y - feet;
                float meshTop = 0f;
                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>()) meshTop = Mathf.Max(meshTop, smr.bounds.max.y - feet);
                Core.Log.Info("[Guns] Hitbox " + name + ": body capsule 0.00-" + bodyTop.ToString("0.00") + " m above the feet, head " + headBottom.ToString("0.00")
                    + "-" + headTop.ToString("0.00") + " (r " + head.bounds.extents.x.ToString("0.00") + "), model top " + meshTop.ToString("0.00"));
            }
            catch (Exception e) { GunplayLog.Main("Hitbox report failed: " + e.Message); }
        }

        private static bool Ignored(Transform t, Transform shooterRoot, GameObject player)
        {
            if (shooterRoot != null && t.IsChildOf(shooterRoot)) return true;
            if (player != null && t.IsChildOf(player.transform.root)) return true;     // the player and, when driving, their car
            return false;
        }

        // the gun's vanilla hit path, for the collider the bullet met: set its hitObj/hitPoint/hitNormal, then getLayer or
        // actorHitSound (which impact sound), then the hit state's CreateObject / SetFsmFloat (damage x falloff) / SendEvent actions
        private static void PlayerHit(ref Shot s, RaycastHit h, float falloff)
        {
            var gun = s.Gun;
            var fsm = gun.Fsm;
            if (fsm == null) return;
            var go = h.collider.gameObject;
            var vo = fsm.Variables.GetFsmGameObject("hitObj"); if (vo != null) vo.Value = go;
            var vp = fsm.Variables.GetFsmVector3("hitPoint"); if (vp != null) vp.Value = h.point;
            var vn = fsm.Variables.GetFsmVector3("hitNormal"); if (vn != null) vn.Value = h.normal;
            // vehicle parts carry a Bodypart FSM too (their condition): not a creature - no hurt ghost, no white/red number (the part rule shows blue)
            bool isPart = OwningPart(go.transform) != null || go.CompareTag("vehPartRemoved") || IsVehiclePart(go);   // the same test HitWorld uses (a vehPart-tagged ancestor)
            bool corpse = !isPart && IsCorpse(go);                // (1.5.2) a dead NPC's ragdoll: blood, but no number / marker / hurt
            bool creature = !isPart && !corpse && (HasBodypart(go) || HasBodypart(RootOf(go.transform).gameObject));
            if (creature) Bridge.Hurt(go, s.Player);
            bool feedback = creature;
            float before = Core.Log.Verbose || feedback ? HealthOf(go) : 0f;
            Replay(fsm, (go.layer == 10 || corpse) && gun.ActorHit != null ? gun.ActorHit : gun.GetLayer, falloff);   // a corpse bleeds like the living NPC did
            float headF = creature && col_isHead(go) && !IsPlayerObj(go) ? HeadFactor(go) : 1f;     // [Gunplay] HeadshotMultiplier instead of the head's own x2
            Replay(fsm, gun.Hit, falloff * headF, GunplaySettings.VehicleDamage.Value && isPart, (creature || corpse) && (go.layer == 10 || corpse) && gun.ActorHit != null);
            float after = Core.Log.Verbose || feedback ? HealthOf(go) : 0f;
            if (feedback)
            {
                float nominal = gun.Damage != null ? gun.Damage.Value * falloff : 0f;
                float dealt = !float.IsNaN(before) && !float.IsNaN(after) && after < before ? after - before : nominal;
                bool head = col_isHead(go);
                Bridge.PlayerHit(RootOf(go.transform).gameObject, h.point, dealt, head);
            }
            if (Core.Log.Verbose)
            {
                Core.Log.Info("[Guns] Hit: " + fsm.GameObject.name + " -> " + RootOf(go.transform).name + "/" + go.name + " at " + (s.Travelled + h.distance).ToString("0.0") + " m, damage "
                    + (gun.Damage != null ? gun.Damage.Value * falloff : 0f).ToString("0.0") + " (x" + falloff.ToString("0.00") + ")"
                    + (float.IsNaN(before) ? ", no Health FSM" : ", Health " + before.ToString("0.0") + " -> " + after.ToString("0.0")));
            }
        }

        // An NPC's head carries its own Bodypart FSM whose damage state multiplies the damage (FloatMultiply, x2 on every NPC) before taking it
        // off Health. A hit there is pre-scaled so the result is [Gunplay] HeadshotMultiplier x the damage. Cached per head object.
        private static readonly Dictionary<int, float> _headMul = new Dictionary<int, float>();
        private static float HeadFactor(GameObject head)
        {
            float m;
            int id = head.GetInstanceID();
            if (!_headMul.TryGetValue(id, out m))
            {
                m = 1f;
                foreach (var f in head.GetComponents<PlayMakerFSM>())
                {
                    if (f == null || f.FsmName != "Bodypart" || f.Fsm == null || f.Fsm.States == null) continue;
                    foreach (var st in f.Fsm.States)
                    {
                        if (st.Actions == null) continue;
                        foreach (var a in st.Actions) { var fm = a as FloatMultiply; if (fm != null && fm.multiplyBy != null && fm.multiplyBy.Value > 0f) m = fm.multiplyBy.Value; }
                    }
                }
                _headMul[id] = m;
            }
            return Mathf.Max(0f, GunplaySettings.HeadshotMultiplier.Value) / m;
        }


        private static void Replay(Fsm fsm, FsmStateAction[] actions, float falloff, bool skipBodypartDamage = false, bool skipDust = false)
        {
            if (actions == null) return;
            foreach (var a in actions)
            {
                if (skipBodypartDamage)
                {
                    var sfx = a as SetFsmFloat;
                    if (sfx != null && sfx.fsmName != null && sfx.fsmName.Value == "Bodypart") continue;
                    var sex = a as SendEvent;
                    if (sex != null && sex.sendEvent != null && sex.sendEvent.Name == "Damage") continue;
                }
                try
                {
                    var co = a as CreateObject;
                    if (co != null)
                    {
                        var prefab = co.gameObject != null ? co.gameObject.Value : null;
                        if (prefab == null) continue;
                        if (skipDust && prefab.name == "RangedHit_Effect") continue;     // (1.5.5) a creature / corpse: its blood only, no ground dust
                        var sp = co.spawnPoint != null ? co.spawnPoint.Value : null;
                        Vector3 pos; Quaternion rot;
                        bool hasPos = co.position != null && !co.position.IsNone, hasRot = co.rotation != null && !co.rotation.IsNone;
                        if (sp != null)
                        {
                            pos = sp.transform.position + (hasPos ? co.position.Value : Vector3.zero);
                            rot = hasRot ? Quaternion.Euler(co.rotation.Value) : sp.transform.rotation;
                        }
                        else
                        {
                            pos = hasPos ? co.position.Value : Vector3.zero;
                            rot = hasRot ? Quaternion.Euler(co.rotation.Value) : prefab.transform.rotation;
                        }
                        var obj = UnityEngine.Object.Instantiate(prefab, pos, rot);
                        var par = co.parent != null ? co.parent.Value : null;
                        if (par != null) obj.transform.parent = par.transform;
                        continue;
                    }
                    var sf = a as SetFsmFloat;
                    if (sf != null)
                    {
                        var tgt = fsm.GetOwnerDefaultTarget(sf.gameObject);
                        if (tgt == null || sf.setValue == null) continue;
                        string fn = sf.fsmName != null ? sf.fsmName.Value : "", vn = sf.variableName != null ? sf.variableName.Value : "";
                        foreach (var f in tgt.GetComponents<PlayMakerFSM>())
                            if (f.FsmName == fn) { var v = f.FsmVariables.GetFsmFloat(vn); if (v != null) v.Value = sf.setValue.Value * falloff; break; }
                        continue;
                    }
                    var sg = a as SetFsmGameObject;
                    if (sg != null)
                    {
                        var tgt = fsm.GetOwnerDefaultTarget(sg.gameObject);
                        if (tgt == null || sg.setValue == null) continue;
                        string fn = sg.fsmName != null ? sg.fsmName.Value : "", vn = sg.variableName != null ? sg.variableName.Value : "";
                        foreach (var f in tgt.GetComponents<PlayMakerFSM>())
                            if (f.FsmName == fn) { var v = f.FsmVariables.GetFsmGameObject(vn); if (v != null) v.Value = sg.setValue.Value; break; }
                        continue;
                    }
                    var se = a as SendEvent;
                    if (se != null && se.sendEvent != null) { fsm.Event(se.eventTarget, se.sendEvent); continue; }
                }
                catch (Exception e) { if (Core.Log.Verbose) GunplayLog.Main("Tracers: replay " + a.GetType().Name + " failed: " + e.Message); }
            }
        }


        // Damage multiplier by distance flown: full damage until [Tracers] FullDamageUntil % of the range, then linear to 0 at the range.
        private static float Falloff(ref Shot s, float dist)
        {
            float x = dist / s.Range;
            float k = _fullUntil;
            if (x <= k) return 1f;
            return Mathf.Clamp01((1f - x) / (1f - k));
        }


        internal static float RangeOf(Kind k)
        {
            switch (k)
            {
                case Kind.Pistol: return Mathf.Max(1f, GunplaySettings.PistolRange.Value);
                case Kind.Smg: return Mathf.Max(1f, GunplaySettings.SmgRange.Value);
                case Kind.Sniper: return Mathf.Max(1f, GunplaySettings.SniperRange.Value);
                case Kind.Shotgun: return Mathf.Max(1f, GunplaySettings.ShotgunRange.Value);
                case Kind.Crossbow: return Mathf.Max(1f, GunplaySettings.CrossbowRange.Value);
                default: return Mathf.Max(1f, GunplaySettings.RifleRange.Value);
            }
        }


        private const float SimHz = 60f;
        // where the bullet is drawn this frame: between Prev and Pos by the share of the last step already elapsed (Pos itself before the first
        // step), and how far it has travelled there
        private static void RenderOf(ref Shot s, out Vector3 pos, out float travelled)
        {
            pos = s.Pos; travelled = s.Travelled;
            if (s.StepLen <= 0f || s.Ahead <= 0f) return;
            float f = Mathf.Clamp01(1f - s.Ahead / s.StepLen);
            pos = Vector3.LerpUnclamped(s.Prev, s.Pos, f);
            travelled = Mathf.Max(0f, s.Travelled - (1f - f) * Vector3.Distance(s.Prev, s.Pos));
        }


        private static void Step(ref Shot s, float dt)
        {
            float move = Mathf.Min(s.Speed * dt, s.Range - s.Travelled);
            if (move <= 0f) { s.Alive = false; return; }
            // NPC bullets have a thickness: the player's body is two capsules only 0.34-0.40 m wide (head at the camera), so a
            // hairline that visibly passes through your face misses the collider by centimetres. Player bullets stay a hairline.
            float radius = s.Gun == null ? _npcRadius : 0f;
            // NPC bullet at the player: the virtual hitbox decides the hit on the player, physics only the obstructions in front of it
            CapsuleCollider vcap = s.Gun == null && s.Target != null ? s.Cap : null;
            if (vcap != null && !vcap.enabled) { vcap = PlayerCapsule(s.Target); if (vcap != null) s.Cap = vcap; }     // the game swapped capsules (crouch): re-find, once
            float vt = float.MaxValue; bool vhead = false;
            if (vcap != null && !PlayerHit(s.Pos, s.Dir, move, radius, s.Target, vcap, out vt, out vhead)) vt = float.MaxValue;
            int n = radius > 0f
                ? Physics.SphereCastNonAlloc(s.Pos, radius, s.Dir, _hits, move, s.Layers, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(s.Pos, s.Dir, _hits, move, s.Layers, QueryTriggerInteraction.Collide);
            if (n >= _hits.Length)
            {
                // buffer full: NonAlloc returns the first N found, not the nearest N - take the guaranteed-nearest hit alone
                RaycastHit one;
                n = Physics.Raycast(s.Pos, s.Dir, out one, move, s.Layers, QueryTriggerInteraction.Ignore) ? 1 : 0;
                if (n == 1) _hits[0] = one;
            }
            else if (n > 1) Array.Sort(_hits, 0, n, HitDistance.Instance);
            for (int k = 0; k < n; k++)
            {
                var h = _hits[k];
                var col = h.collider;
                if (col == null || !Counts(col)) continue;
                var tr = col.transform;
                if (vcap != null && tr.IsChildOf(s.Target.transform)) continue;      // the real capsules: the virtual hitbox handles the player
                if (vcap != null && h.distance > vt) break;                          // the player is hit before this obstruction
                if (s.Passed > 0 && WentThrough(ref s, tr)) continue;                  // a part it already went through (its other colliders, backfaces)
                if (s.Gun != null)
                {
                    if (Ignored(tr, s.ShooterRoot != null ? s.ShooterRoot.transform : null, s.Player)) continue;
                    float pd = s.Travelled + h.distance;
                    float pf = Falloff(ref s, pd) * s.DmgMult * s.Gun.DamageMultiplier;
                    if (Penetrates(ref s, col, h, (s.Gun.Damage != null ? s.Gun.Damage.Value : 0f) * pf)) continue;
                    if (s.Bolt) LandBolt(ref s, h.point, h.normal, tr, col is TerrainCollider || Creature(tr) != null || IsCorpse(col.gameObject));
                    PlayerHit(ref s, h, pf);
                    MetalSparks(col, h.point, h.normal);
                    HitWorld(ref s, col, h.point, (s.Gun.Damage != null ? s.Gun.Damage.Value : 0f) * pf);   // vehicle part rules
                    s.Pos = h.point; s.Travelled = pd; s.Alive = false;
                    return;
                }
                if (s.ShooterRoot != null && tr.IsChildOf(s.ShooterRoot.transform)) continue;        // own body / own car
                bool onTarget = s.Target != null && tr.IsChildOf(s.Target.transform);
                bool detectable = (s.DetectLayers & (1 << col.gameObject.layer)) != 0;
                if (!onTarget && detectable) continue;      // other creatures don't stop a vanilla shot either
                float dist = s.Travelled + h.distance;
                float falloff = Falloff(ref s, dist) * s.DmgMult;
                if (!onTarget && Penetrates(ref s, col, h, s.Damage * falloff)) continue;
                if (s.Bolt) LandBolt(ref s, h.point, h.normal, tr, col is TerrainCollider || Creature(tr) != null || IsCorpse(col.gameObject));
                if (onTarget) HitTarget(ref s, h.point, s.Damage * falloff, col.isTrigger ? col.gameObject : s.Target, dist);   // a head trigger: its own Bodypart (x2)
                else if (s.Impact != null && IsCorpse(col.gameObject))     // (1.5.2) an NPC's bullet in a corpse: the shooter's own blood impact, not dust
                {
                    UnityEngine.Object.Instantiate(s.Impact, h.point, Quaternion.identity);   // as HitTarget does
                }
                else { WorldImpact(col, h.point, h.normal, s.Dir); HitWorld(ref s, col, h.point, s.Damage * falloff); }
                s.Pos = h.point;
                s.Travelled = dist;
                s.Alive = false;
                return;
            }
            if (vcap != null && vt < float.MaxValue)
            {
                float dist = s.Travelled + vt;
                float falloff = Falloff(ref s, dist) * s.DmgMult;
                float mult = vhead ? _headMult : 1f;
                if (s.Bolt) LandBolt(ref s, s.Pos + s.Dir * vt, -s.Dir, vhead && s.Head != null ? s.Head.transform : s.Target.transform, true);
                if (vhead && Core.Log.Verbose) GunplayLog.Main("Tracers: headshot on " + s.Target.name + " at " + dist.ToString("0.0") + " m");
                HitTarget(ref s, s.Pos + s.Dir * vt, s.Damage * falloff * mult, vhead && s.Head != null ? s.Head : s.Target, dist);
                s.Pos += s.Dir * vt;
                s.Travelled = dist;
                s.Alive = false;
                return;
            }
            s.Pos += s.Dir * move;
            s.Travelled += move;
            if (s.Travelled >= s.Range - 1e-3f) s.Alive = false;
        }

        // ---------- the player's hitbox for NPC bullets ----------
        // The game's player collider is two slim physics capsules (r 0.17 / 0.20) with the camera on their axis and a trigger sphere
        // for the head. Tracers aimed at the player use their own shapes instead, built each frame from the tallest real capsule so a
        // crouch is followed: a body capsule (feet to neck, [Tracers] PlayerBodyRadius) and a head sphere ([Tracers] PlayerHeadRadius)
        // at the top. A bullet hits whichever it reaches first, once.
        // the object that carries the body capsule: the target itself or its nearest ancestor (the player's "head" child -> Player)
        private static GameObject BodyRootOf(GameObject target)
        {
            for (var tr = target.transform; tr != null; tr = tr.parent)
                if (tr.CompareTag("Player") && PlayerCapsule(tr.gameObject) != null) return tr.gameObject;
            return null;    // NPCs, animals: their real colliders
        }

        private static readonly Dictionary<int, GameObject> _heads = new Dictionary<int, GameObject>();
        private static GameObject HeadOf(GameObject root)
        {
            GameObject h;
            int id = root.GetInstanceID();
            if (_heads.TryGetValue(id, out h) && h != null) return h;
            h = null;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr != root.transform && tr.name == "head")
                    foreach (var f in tr.GetComponents<PlayMakerFSM>())
                        if (f.FsmName == "Bodypart") { h = tr.gameObject; break; }
            _heads[id] = h;
            return h;
        }

        private static CapsuleCollider PlayerCapsule(GameObject target)
        {
            CapsuleCollider best = null;
            foreach (var c in target.GetComponents<CapsuleCollider>())
                if (c.enabled && !c.isTrigger && (best == null || c.height > best.height)) best = c;
            return best;
        }

        // segment p0 + d*t (0..len) vs the virtual shapes; returns the nearest hit distance along the bullet
        private static bool PlayerHit(Vector3 p0, Vector3 d, float len, float thick, GameObject target, CapsuleCollider cap, out float t, out bool head)
        {
            t = 0f; head = false;
            var tr = cap.transform;
            Vector3 up = cap.direction == 1 ? tr.up : cap.direction == 0 ? tr.right : tr.forward;
            float sc = Mathf.Abs(cap.direction == 1 ? tr.lossyScale.y : cap.direction == 0 ? tr.lossyScale.x : tr.lossyScale.z);
            Vector3 center = tr.TransformPoint(cap.center);
            float half = Mathf.Max(0f, cap.height * sc * 0.5f);
            Vector3 feet = center - up * half, top = center + up * half;
            float headR = _headR, bodyR = _bodyR;
            Vector3 headC = top - up * headR;
            Vector3 animatedHead;
            if (Services.Animation != null && Services.Animation.TryPlayerJumpHead(target, out animatedHead)) headC = animatedHead;
            Vector3 neck = headC - up * headR;
            // body capsule: its rounded top ends exactly at the neck, so the head zone belongs to the head alone
            Vector3 bodyLo = feet + up * bodyR, bodyHi = neck - up * bodyR;
            if (Vector3.Dot(bodyHi - bodyLo, up) < 0f) bodyHi = bodyLo;         // crouched very low: a ball
            float th, tb;
            bool hh = RaySphere(p0, d, len, headC, headR + thick, out th);
            bool hb = SegCapsule(p0, d, len, bodyLo, bodyHi, bodyR + thick, out tb);
            if (!hh && !hb) return false;
            if (hh && (!hb || th <= tb)) { t = th; head = true; } else t = tb;
            return true;
        }

        private static bool RaySphere(Vector3 p0, Vector3 d, float len, Vector3 c, float r, out float t)
        {
            t = 0f;
            Vector3 m = p0 - c;
            float b = Vector3.Dot(m, d), cc = Vector3.Dot(m, m) - r * r;
            if (cc > 0f && b > 0f) return false;
            float disc = b * b - cc;
            if (disc < 0f) return false;
            t = -b - Mathf.Sqrt(disc);
            if (t < 0f) t = 0f;            // starts inside
            return t <= len;
        }

        // closest approach between the bullet segment and the capsule axis a..b (Ericson, Real-Time Collision Detection 5.1.9)
        private static bool SegCapsule(Vector3 p0, Vector3 d, float len, Vector3 a, Vector3 b, float r, out float t)
        {
            Vector3 p1 = p0 + d * len;
            Vector3 d1 = p1 - p0, d2 = b - a, rr = p0 - a;
            float A = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, rr);
            float s, u;
            if (A <= 1e-8f && e <= 1e-8f) { s = u = 0f; }
            else if (A <= 1e-8f) { s = 0f; u = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, rr);
                if (e <= 1e-8f) { u = 0f; s = Mathf.Clamp01(-c / A); }
                else
                {
                    float bb = Vector3.Dot(d1, d2), den = A * e - bb * bb;
                    s = den != 0f ? Mathf.Clamp01((bb * f - c * e) / den) : 0f;
                    u = (bb * s + f) / e;
                    if (u < 0f) { u = 0f; s = Mathf.Clamp01(-c / A); }
                    else if (u > 1f) { u = 1f; s = Mathf.Clamp01((bb - c) / A); }
                }
            }
            Vector3 c1 = p0 + d1 * s, c2 = a + d2 * u;
            if ((c1 - c2).sqrMagnitude > r * r) { t = 0f; return false; }
            // back up from the closest approach to the surface along the bullet (entry point), never before the segment start
            float along = s * len;
            float gap = Mathf.Sqrt(Mathf.Max(0f, r * r - (c1 - c2).sqrMagnitude));
            t = Mathf.Max(0f, along - gap);
            return true;
        }

        private sealed class HitDistance : IComparer<RaycastHit>
        {
            public static readonly HitDistance Instance = new HitDistance();
            public int Compare(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); }
        }

        // exactly the vanilla attack state: impact effect at the point, <target>/Bodypart.Damage = damage, SendEvent Damage to the target
        private static void HitTarget(ref Shot s, Vector3 point, float damage, GameObject target, float dist)
        {
            if (s.Impact != null) UnityEngine.Object.Instantiate(s.Impact, point, Quaternion.identity);
            if (target == null || Mathf.Abs(damage) < 0.01f) return;
            if (col_isHead(target) && !IsPlayerObj(target)) damage *= HeadFactor(target);
            var fsms = target.GetComponents<PlayMakerFSM>();
            foreach (var f in fsms)
                if (f.FsmName == "Bodypart")
                {
                    var v = f.FsmVariables.GetFsmFloat("Damage");
                    if (v != null) v.Value = damage;
                }
            float before = Core.Log.Verbose ? HealthOf(target) : 0f;
            foreach (var f in fsms) if (s.EventFsm == null || f.FsmName == s.EventFsm) f.SendEvent(s.EventName);
            Bridge.Hurt(target, s.ShooterRoot);
            if (Core.Log.Verbose)
                Core.Log.Info("[Guns] Hit: " + (s.ShooterRoot != null ? s.ShooterRoot.name : "?") + " -> " + RootOf(target.transform).name + "/" + target.name + " at "
                    + dist.ToString("0.0") + " m, damage " + damage.ToString("0.0")
                    + (float.IsNaN(before) ? "" : ", Health " + before.ToString("0.0") + " -> " + HealthOf(target).ToString("0.0")));
        }


        private static void HitWorld(ref Shot s, Collider col, Vector3 point, float damage)
        {
            Transform part = null;
            for (var t = col.transform; t != null; t = t.parent)
                if (t.CompareTag("vehPart")) { part = t; break; }
            if (part == null) return;

            Core.Services.RaiseVehiclePartHit(part.gameObject, s.ShooterRoot ?? s.Player);
            if (!GunplaySettings.VehicleDamage.Value) return;
            float pct = Mathf.Abs(damage) / Mathf.Max(1f, GunplaySettings.VehicleDamagePer1.Value);   // [Tracers] VehicleDamagePer1 bullet damage = 1 % condition
            bool wheel = IsWheel(col.transform, part);
            if (wheel) pct *= Mathf.Max(0f, GunplaySettings.WheelDamageMultiplier.Value);
            if (pct <= 0f) return;
            if (s.Gun != null) Bridge.PartHit(part.gameObject, point, pct);
            bool broke = false;
            foreach (var f in part.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f.FsmName != "Condition" && f.FsmName != "Repair") continue;
                if (f.Fsm == null || !f.Fsm.Initialized) continue;
                if (OwningPart(f.transform) != part) continue;     // not a nested part's FSM
                var v = f.FsmVariables.GetFsmFloat("Condition");
                if (v == null) continue;
                float before = v.Value;
                v.Value = Mathf.Max(0f, v.Value - pct);
                if (before > 0f && v.Value <= 0f) broke = true;
            }
            if (wheel && broke && GunplaySettings.WheelPopOff.Value) PopOff(part, s.Dir);
        }

        // A wheel shot to 0 jumps off the car: the wrench's de_Attach recipe (layer Item + tag vehPartRemoved -> the part's CheckTag FSM
        // unparents it and adds a Rigidbody a frame or two later), then a kick up and along the bullet. Works with Apocapatrol's crews too
        // (they switch the de_Attach FSMs off, but this doesn't go through them). If CheckTag never frees it, it is freed by hand.
        // ---------- (1.5.0) penetration of car parts ----------
        // A bullet hitting a car part rolls, by gun type, whether it goes through: glass windshields, grid windshields / the wire plate (the
        // gaps), doors / hoods / trunks (sheet metal) and bolted-on metal plates (armour). Through: sparks, the part takes its condition
        // damage, the bullet flies on with less damage (glass/grid x0.8, sheet x0.6, armour x0.4) and can hit whoever is behind; at most 2
        // parts. Engines, radiators, bumpers, wheels, frames stop every bullet. Replaces knocking plates off (wheels still pop off at 0).
        private enum Mat { None, Glass, Grid, Sheet, Armour }
        //                                   Pistol Smg Rifle Sniper Shotgun Crossbow  (% to pass)
        private static readonly float[] PassGlass  = { 80f, 85f, 95f, 100f, 60f, 70f };
        private static readonly float[] PassGrid   = { 50f, 50f, 55f, 60f, 40f, 30f };
        private static readonly float[] PassSheet  = { 30f, 35f, 70f, 90f, 10f, 20f };
        private static readonly float[] PassArmour = { 0f, 5f, 20f, 50f, 0f, 0f };
        private static readonly Dictionary<string, Mat> _partMat = new Dictionary<string, Mat>();
        private static Mat MatOf(string partName)
        {
            Mat m;
            if (_partMat.TryGetValue(partName, out m)) return m;
            string n = partName.ToLowerInvariant();
            int k = n.IndexOf(" ("); if (k > 0) n = n.Substring(0, k);
            if (n.StartsWith("windshield_1_glass")) m = Mat.Glass;
            else if (n.StartsWith("windshield_2_grid") || n.StartsWith("wire_plate")) m = Mat.Grid;
            else if (n.StartsWith("metal_plate")) m = Mat.Armour;
            else if (n.Contains("door") || n.Contains("hood") || n.Contains("trunk")) m = Mat.Sheet;   // door_car_*, junker_door_FL, poloska_door_engine, *_hood*, *_trunk
            else m = Mat.None;
            _partMat[partName] = m;
            return m;
        }

        private static bool WentThrough(ref Shot s, Transform t)
        {
            var part = OwningPart(t);
            if (part == null) return false;
            int id = part.GetInstanceID();
            return id == s.Passed0 || id == s.Passed1;
        }

        private static bool Penetrates(ref Shot s, Collider col, RaycastHit h, float damage)
        {
            if (s.Passed >= 2) return false;
            var part = OwningPart(col.transform);
            if (part == null) return false;
            Mat m = MatOf(part.name);
            if (s.Bolt && m != Mat.Glass) return false;
            if (m == Mat.None) return false;
            float[] t = m == Mat.Glass ? PassGlass : m == Mat.Grid ? PassGrid : m == Mat.Sheet ? PassSheet : PassArmour;
            int ki = (int)s.Kind; if (ki < 0 || ki >= t.Length) ki = 0;
            if (UnityEngine.Random.Range(0f, 100f) >= t[ki]) return false;
            MetalSparks(col, h.point, h.normal);
            HitWorld(ref s, col, h.point, damage);           // the part still takes its condition damage
            s.DmgMult *= m == Mat.Armour ? 0.4f : m == Mat.Sheet ? 0.6f : 0.8f;
            int id = part.GetInstanceID();
            if (s.Passed == 0) s.Passed0 = id; else s.Passed1 = id;
            s.Passed++;
            if (Core.Log.Verbose) Core.Log.Info("[Guns] Hit: " + (s.Orb ? "monster orb" : s.Kind + " bullet") + " goes through " + part.name + " (" + m + "), damage x" + s.DmgMult.ToString("0.00"));
            return true;
        }

        // a rigidbody that belongs to a car (an attached part: door, hood, trunk on its hinge; the car body itself) - bullets don't push it
        private static bool OnCar(Collider col, Rigidbody rb)
        {
            if (OwningPart(col.transform) != null) return true;
            if (rb.GetComponent<Joint>() != null && rb.transform.parent != null) return true;
            var root = col.transform.root;
            int id = root.GetInstanceID();
            bool car;
            if (!_carRoots.TryGetValue(id, out car)) { car = root.GetComponent("VehicleController") != null; _carRoots[id] = car; }
            return car;
        }

        private static void PopOff(Transform part, Vector3 dir) { PopOff(part, dir, "shot off its car (condition 0)"); }
        private static void PopOff(Transform part, Vector3 dir, string why)
        {
            part.gameObject.layer = 9;
            try { part.tag = "vehPartRemoved"; } catch (Exception e) { GunplayLog.Main("Tracers: wheel tag: " + e.Message); }
            Vector3 d = dir; d.y = 0f; d = d.sqrMagnitude > 0.001f ? d.normalized : Vector3.zero;
            _popped.Add(new KeyValuePair<GameObject, Vector3>(part.gameObject, d * 2.5f + Vector3.up * 4f));
            _forceFree.Add(part.gameObject);
            _poppedFrame = Time.frameCount + 2;
            if (Core.Log.Verbose) GunplayLog.Main("Tracers: " + part.name + " " + why);
        }

        // a wheel / tyre part: the hit collider or any object up to (and including) its part is named like one
        private static bool IsWheel(Transform t, Transform part)
        {
            for (; t != null; t = t.parent)
            {
                string n = t.name;
                if (n.IndexOf("steering", StringComparison.OrdinalIgnoreCase) >= 0) return false;     // a steering wheel is not a wheel
                if (n.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("tire", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("tyre", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (t == part) break;
            }
            return false;
        }
    }
}
