using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Brain part: mode changes, line of sight, map-leg checks and the brain-facing queries for Idle / Burrow / Pounce / DogBite.</summary>
    internal static partial class Brain
    {
        // ---------- modes ----------
        /// <summary>Changes the brain mode: animation clip and pose side effects, trail bookkeeping and the level-2 mode log.</summary>
        private static void SetMode(Npc n, Mode m, string why)
        {
            Mode was = n.Mode;
            n.Mode = m;
            if (m != was && m != Mode.Chase && m != Mode.Advance && m != Mode.Cover) PoiNav.StopGuiding(n.Owner);
            if (m != Mode.Cover) n.DogCoverThreat = null;
            if (m == Mode.Cover && was != Mode.Cover) Senses.SetHiding(n.Owner, true);          // the senses: keep the target, take no ghosts
            else if (was == Mode.Cover && m != Mode.Cover) Senses.SetHiding(n.Owner, false);
            if (Tracing && m != was)
            {
                if ((m == Mode.Chase || m == Mode.Advance) && (was == Mode.Off || was == Mode.Search)) { n.Trail.Clear(); n.TrailKind.Clear(); n.NextTrail = 0f; n.NextTraceDump = 0f; }
                else if ((m == Mode.Search || m == Mode.Off) && was != Mode.Hold) { n.NextTraceDump = 0f; TraceDump(n, m == Mode.Search ? "reached the goal" : "chase over"); }
                else if (m == Mode.Rest) TraceDump(n, "rests");
            }
            if (m != Mode.Chase && m != Mode.Advance) n.StrafeDev = 0f;
            if (m == Mode.Chase || m == Mode.Advance) { n.HasHeading = false; if (was != Mode.Chase && was != Mode.Advance && was != Mode.BackUp) { n.BestDist = float.MaxValue; n.NoProgressSince = Time.time; n.Flipped = false; n.Side = 0; n.HasWaypoint = false; } }
            bool wasStill = was == Mode.Hold || was == Mode.Rest || was == Mode.Search || was == Mode.Walk, still = m == Mode.Hold || m == Mode.Rest || m == Mode.Search || m == Mode.Walk;
            if (m == Mode.Off) { if (wasStill) Move(n, n.Target != null && n.Target.Value != null); else if (n.Target == null || n.Target.Value == null) Move(n, false); }   // (1.4.9) back to the game's run only with a target left - no target: idle, not a run on the spot; (crew AI) also when released from a chase with no target left (nothing drives that run any more)
            else if (still && !wasStill) Move(n, false);
            else if (!still && wasStill) Move(n, true);
            if (still) n.OnNav = false;
            if (m == Mode.Hold)
            {
                Aim(n);
                if (was != Mode.Hold && n.Ranged && NpcaiSettings.CrouchChance.Value > 0f)
                {
                    float t = Time.time;
                    if (t - n.CrouchRolledAt > 5f) { n.CrouchRoll = UnityEngine.Random.Range(0f, 100f) < NpcaiSettings.CrouchChance.Value; n.CrouchRolledAt = t; }
                    if (n.CrouchRoll) Crouch(n, true);
                }
            }
            else { Unfreeze(n); Crouch(n, false); }
            if (WildWasteland.Core.Log.L2 && (m != was))
                NpcaiRuntime.Deep("Brain: " + n.Owner.name + " " + was + " -> " + m + " (" + why + ", " + n.Dist.ToString("0") + " m" + (n.Ranged ? ", " + n.Kind : "") + ")");
        }

        /// <summary>Movement FSM: Animal_Run (run animation + forward velocity) or Animal_Idle (idle animation, velocity zero), only while the Attack FSM is in its chase state (during a burst / swing the Attack FSM re-sends Animal_Run on its way back; see BeforeSendEvent).</summary>
        private static void Move(Npc n, bool run)
        {
            if (n.Movement == null || n.Attack == null || n.Attack.Fsm == null) return;
            string s = n.Attack.Fsm.ActiveStateName;
            if (!ChaseState(s) && s != "hide" && Time.time >= n.FrightUntil) return;
            n.Movement.SendEvent(run ? "Animal_Run" : "Animal_Idle");
        }

        /// <summary>ActivateGameObject(s).OnEnter postfix: the game toggled an object of this NPC (the WeaponType FSM's gun models, the melee machete ...): the rig re-finds the held weapon on its next frame. One dictionary lookup, only when the game toggles something.</summary>
        public static void AfterActivate(FsmStateAction __instance)
        {
            if (_npcs.Count == 0 || !NpcaiRuntime.Running) return;
            try
            {
                var fsm = __instance.Fsm;
                if (fsm == null) return;
                var n = Get(fsm.GameObject);
                if (n == null || n.Body == null || n.WeaponDirty) return;
                // (perf) only a toggle under (or above) a hand bone, or of the held gun, can change what the hand holds; a muzzle flash or
                // another prop elsewhere on the body no longer re-scans the gun. ActivateGameObjects (several targets): as before.
                var one = __instance as ActivateGameObject;
                if (one != null)
                {
                    var go = fsm.GetOwnerDefaultTarget(one.gameObject);
                    if (go != null && n.Hands != null)
                    {
                        var gt = go.transform;
                        if (n.Weapon != null && gt != n.Weapon && gt.IsChildOf(n.Weapon)) return;      // a muzzle flash of the held gun: still held
                        if (gt != n.Weapon && !Core.HeldWeaponModels.UnderHand(n.Hands, gt)) return;
                    }
                }
                n.WeaponDirty = true;
            }
            catch (Exception) { }
        }

        // a map waypoint is taken only when the first metre toward it is free for the body (walls, rock, baked props - not loose items or cars,
        // the feelers steer round those); otherwise this look runs without the map, as before maps existed
        private static readonly int NavGateMask = PathMask & ~((1 << 8) | (1 << 9));
        /// <summary>True when the leg to the next map waypoint is clear for the body (no hop leg).</summary>
        private static bool NavLegClear(Npc n, Vector3 next) { return NavLegClear(n, next, false); }
        /// <summary>True when the leg to the next map waypoint is clear for the body; a hop leg allows a low lip.</summary>
        private static bool NavLegClear(Npc n, Vector3 next, bool hopLeg)
        {
            if (n.Col == null) return true;
            var b = n.Col.bounds;
            float r = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z), 0.12f, 0.35f);
            float lo = b.min.y + 0.25f + r, hi = b.min.y + b.size.y * 0.7f - r; if (hi < lo) hi = lo;
            Vector3 p1 = new Vector3(b.center.x, lo, b.center.z), p2 = new Vector3(b.center.x, hi, b.center.z);
            Vector3 d = next - n.T.position; d.y = 0f;
            float len = Mathf.Min(1f, d.magnitude);
            if (len < 0.05f) return true;
            RaycastHit h;
            if (!Physics.CapsuleCast(p1, p2, r, d.normalized, out h, len, NavGateMask, QueryTriggerInteraction.Ignore)) return true;
            if (h.collider.transform.root == n.T) return true;
            if (Nav.IsFloor(h.collider, h.normal, h.point.y, b.min.y)) return true;
            if (hopLeg && h.point.y - b.min.y <= Nav.HopStep + 0.1f) return true;   // the lip the map hops over, not a wall
            if (h.point.y - b.min.y <= 0.6f && !Physics.Raycast(new Vector3(b.center.x, b.min.y + 0.65f, b.center.z), d.normalized, h.distance + r + 0.3f, NavGateMask, QueryTriggerInteraction.Ignore))
                return true;                                                          // ankle-high clutter with nothing at the knee: stepped over
            n.LegBlock = h.collider.name + " [" + LayerMask.LayerToName(h.collider.gameObject.layer) + "] " + (h.point.y - b.min.y).ToString("0.00") + " m above the feet, " + h.distance.ToString("0.00") + " m ahead" + (hopLeg ? ", hop leg" : "");
            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " map waypoint blocked by " + n.LegBlock + ", steers without the map");
            return false;
        }

        /// <summary>The Unstuck FSM's stuck verdict: a gunman with a clear shot holds, else back up and avoid the heading; StuckGiveUpCount stucks in 10 s rest a second.</summary>
        private static void OnStuck(Npc n)
        {
            float now = Time.time;      // (crew AI) convoy crews on foot walk with MoveToward now: the full stuck handling (SimpleThink keeps BackUp / Rest)
            if (n.Mode == Mode.Hold || n.Mode == Mode.Rest || n.Mode == Mode.Search || n.Mode == Mode.Walk || n.Mode == Mode.BackUp || n.Mode == Mode.Off) return;   // standing still (or Idle's walk): the Unstuck FSM's "not moving" is no stuck
            if (n.Travel || n.Halted || Senses.Blown(n.Owner)) return;          // pushed by a tornado / under the ground: not stuck
            if (now - n.FriendBumpsSince > 6f) { n.FriendBumpsSince = now; n.FriendBumps = 0; }
            if (FriendInTheWay(n) && ++n.FriendBumps <= 3)   // bumped into another NPC: pass it, keep the map (3 times in 6 s at most: a wall next to a friend is still a wall)
            {
                if (n.AvoidSide == 0 || now >= n.AvoidUntil) n.AvoidSide = UnityEngine.Random.value < 0.5f ? -1 : 1;
                n.AvoidUntil = now + 1.2f; n.NoProgressSince = now;
                return;
            }
            if (n.OnNav)
            {
                // stuck on a map route: hop / step back and keep the route; 3 times within 8 s -> full feelers for 3 s, then the map again
                if (now - n.NavStuckSince > 8f) { n.NavStuckSince = now; n.NavStucks = 0; }
                n.NavStucks++;
                n.BlockedYaw = n.T.eulerAngles.y; n.BlockedUntil = now + Mathf.Max(0f, NpcaiSettings.BlockedMemory.Value) * R;
                if (WildWasteland.Core.Log.L2) LogAhead(n);
                if (n.NavStucks < 3)
                {
                    if (TryHop(n, now, true)) return;
                    n.ModeUntil = now + 0.4f * R;
                    SetMode(n, Mode.BackUp, WildWasteland.Core.Log.L2 ? "stuck on a map route at " + n.T.position.x.ToString("0.0") + "," + n.T.position.z.ToString("0.0") + ", a step back, keeps the route" : "");
                    return;
                }
                n.NavStucks = 0; n.NavOffUntil = now + 3f * R; n.OnNav = false;
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " stuck 3x on a map route at " + n.T.position.x.ToString("0.0") + "," + n.T.position.z.ToString("0.0") + ", full feelers for " + (3f * R).ToString("0") + " s");
            }
            if (now - n.FirstStuck > StuckWindow * R) { n.FirstStuck = now; n.Stucks = 0; }
            n.Stucks++;
            var target = n.Target != null ? n.Target.Value : null;
            if (n.Ranged && !n.ToGhost && target != null && n.Dist <= WeaponRanges.RangeOf(n.Kind) && LineOfSight(n, target, target.transform.position))
            {
                n.NextRecheck = now + global::WildWasteland.Modules.NPC.Aim.HoldRecheckMax * R;   // refs-ok: own type NPC.Aim (the Brain.Aim method hides the simple name)
                SetMode(n, Mode.Hold, "stuck, shoots from here");
                return;
            }
            if (n.Stucks >= Mathf.Max(1, NpcaiSettings.StuckGiveUpCount.Value))
            {
                n.Stucks = 0;
                n.ModeUntil = now + RestSeconds * R;
                if (n.ToGhost && Senses.CannotReach(n.Owner)) { n.NextLookTurn = 0f; SetMode(n, Mode.Search, "stuck on the way to the ghost"); return; }
                SetMode(n, Mode.Rest, "stuck for good, rests");
                return;
            }
            float yaw = n.T.eulerAngles.y;
            n.BlockedYaw = yaw; n.BlockedUntil = now + Mathf.Max(0f, NpcaiSettings.StuckMemorySeconds.Value) * R;
            n.HasWaypoint = false; n.NextScout = 0f;
            // the body hit something the feelers did not see (or saw too late): commit to the freer side now and keep it through the back-up
            if (n.Side == 0) n.Side = n.FreeRight >= n.FreeLeft ? 1 : -1;
            n.SideUntil = now + SideLock * 2f * R; n.ClearLooks = 0;
            if (WildWasteland.Core.Log.L2) LogAhead(n);
            if (TryHop(n, now, true)) return;       // stuck: a low edge is hopped even when its face is sloped (only gentle terrain is not)
            n.ModeUntil = now + Mathf.Max(0.1f, NpcaiSettings.StuckBackupSeconds.Value) * R;
            SetMode(n, Mode.BackUp, WildWasteland.Core.Log.L2 ? "stuck " + n.Stucks + "x, backs up" : "");
        }

        /// <summary>Stuck on something low (a rock lip, a kerb, a pipe on the floor) with nothing above it: the game's own Unstuck would hop here (we block its AddForce while steering) - give one small hop forward instead of backing up. At most one hop per 2 s.</summary>
        private static bool TryHop(Npc n, float now) { return TryHop(n, now, false); }
        /// <summary>A small hop over a low lip ahead (low ray blocked, knee ray free), at most once per 2 s.</summary>
        private static bool TryHop(Npc n, float now, bool lip)
        {
            if (n.Rb == null || n.Col == null || now - n.LastHop < 2f) return false;
            var b = n.Col.bounds;
            Vector3 fwd = n.T.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) return false; fwd.Normalize();
            Vector3 low = new Vector3(b.center.x, b.min.y + 0.1f, b.center.z), knee = new Vector3(b.center.x, b.min.y + 0.55f, b.center.z);
            float reach = Mathf.Min(b.extents.x, b.extents.z) + 0.45f;
            RaycastHit h;
            if (!Physics.Raycast(low, fwd, out h, reach, PathMask, QueryTriggerInteraction.Ignore) || h.collider.transform.root == n.T) return false;
            // a slope the body walks up is no edge - but a lip (stuck at it, or the map's route hops it) is hopped unless it is gentle terrain
            bool gentle = h.normal.y >= Nav.WalkNormal || Nav.IsFloor(h.collider, h.normal, h.point.y, b.min.y);
            if (gentle && (!lip || h.collider.gameObject.layer == 14)) return false;
            if (Physics.Raycast(knee, fwd, reach + 0.2f, PathMask, QueryTriggerInteraction.Ignore)) return false;      // something higher: not a hop
            n.LastHop = now;
            n.Rb.AddForce(Vector3.up * 3.2f + fwd * 1.5f, ForceMode.VelocityChange);
            if (n.Body != null && !ApBody.Suspended(n.Body)) ApBody.Jump(n.Body);   // refs-ok: REFACTOR-REQUEST B07-13 (Presentation ApBody/ReloadSounds: NPC animation framework hooks missing from ICharacterPresentation)
            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " hops over a low edge (" + h.collider.name + ")");
            return true;
        }

        /// <summary>The horizontal direction from a touching collider to the body. Collider.ClosestPoint only works for convex shapes: for a non-convex MeshCollider (cave rock, building walls) and terrain it returns the query point itself, so those use ComputePenetration against the NPC's own (convex) collider instead.</summary>
        private static bool TouchDir(Npc n, Collider c, Vector3 origin, out Vector3 away, out float contactY)
        {
            away = Vector3.zero; contactY = origin.y;
            var mc = c as MeshCollider;
            bool convex = mc != null ? mc.convex : (c is BoxCollider || c is SphereCollider || c is CapsuleCollider);   // terrain (and anything else) -> penetration
            if (convex)
            {
                Vector3 cp = c.ClosestPoint(origin);
                contactY = cp.y;
                away = origin - cp; away.y = 0f;
                if (away.sqrMagnitude < 0.0001f) return false;
                away.Normalize();
                return true;
            }
            if (n.Col == null) return false;
            Vector3 dir; float dist;
            if (!Physics.ComputePenetration(n.Col, n.T.position, n.T.rotation, c, c.transform.position, c.transform.rotation, out dir, out dist)) return false;
            contactY = origin.y - 0.5f;             // a wall pressed against the body: never counted as the floor under the feet
            if (Mathf.Abs(dir.y) > 0.8f) { contactY = dir.y > 0f ? n.Col.bounds.min.y : origin.y; }   // pushed up = it is under the feet
            away = dir; away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) return false;
            away.Normalize();
            return true;
        }

        /// <summary>(1.6.0) Idle: the brain has stood aside for its ghost walk (Mode Walk) - only then does Idle move the body</summary>
        internal static bool IsWalk(GameObject owner)
        {
            if (!On) return false;
            var n = Get(owner);
            return n != null && n.Mode == Mode.Walk;
        }

        /// <summary>Aim decided to fire but the body is turned away (steering around something): face the target for a moment</summary>
        internal static void FaceTarget(GameObject owner, float seconds)
        {
            var n = Get(owner);
            if (n != null) n.FaceTargetUntil = Time.time + seconds;
        }

        /// <summary>(1.4.0) Burrow: a hunter dives (held still, no thinking) / is up again (the stuck and progress judgements start over)</summary>
        internal static bool IsRanged(GameObject owner) { var n = Get(owner); return n != null && n.Ranged; }
        /// <summary>Burrow / Pounce take the NPC over (or give it back): no pedal, no thinking, no stuck judgement while travelling.</summary>
        internal static bool SetTravel(GameObject owner, bool on)
        {
            var n = Get(owner);
            if (n == null) return false;
            n.Travel = on; n.CmdSpeed = 0f;
            float now = Time.time;
            if (on) { if (n.Rb != null && !n.Rb.isKinematic) { Vector3 v = n.Rb.velocity; v.x = 0f; v.z = 0f; n.Rb.velocity = v; } }
            else { BlkReset(n, now); n.NoProgressSince = now; n.BestDist = float.MaxValue; n.HasWaypoint = false; n.MakeWayUntil = 0f; }
            return true;
        }

        /// <summary>(1.8.0) DogBite: a dog in reach of its target stands still (no running circles round it) / runs again</summary>
        internal static void Halt(GameObject owner, bool on)
        {
            var n = Get(owner);
            if (n == null) return;
            n.Halted = on;
            if (!on) { BlkReset(n, Time.time); n.NoProgressSince = Time.time; }
        }

        /// <summary>(1.7.0) Nests: an alarmed spider runs `mult` times faster for `seconds`</summary>
        internal static void Boost(GameObject owner, float mult, float seconds)
        {
            var n = Get(owner);
            if (n == null) return;
            n.Boost = Mathf.Clamp(mult, 0.1f, 5f); n.BoostUntil = Time.time + Mathf.Max(0f, seconds);
        }

        /// <summary>(1.4.0) the run speed the game's Movement FSM gives this NPC (0 = not seen yet)</summary>
        internal static float RunSpeed(GameObject owner) { var n = Get(owner); return n != null ? n.RunZ : 0f; }

    }
}
