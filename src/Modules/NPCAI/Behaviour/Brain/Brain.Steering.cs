using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Brain part: steering - walking a map route and the feeler fan, corner scouting, blocked and stuck handling, hops.</summary>
    internal static partial class Brain
    {
        // ---------- walking a map route (1.4.5) ----------
        // The map already knows the way around walls, so on a route the body walks it the way the idle return does: straight at the waypoint,
        // the first free of 5 headings (0, +-30, +-60) by a short body sweep from above the knee (1.2 m; low clutter is stepped over, not
        // steered around), no side commitment, no corner scouting, no left/right flips. Ankle-high things (a rock lip, debris, a kerb) ahead
        // are stepped over with a small lift.
        private static readonly float[] NavFan = { 0f, -30f, 30f, -60f, 60f };
        private static readonly int NavFeelMask = PathMask & ~(1 << 9);      // walls, rock, props, cars - not loose items
        /// <summary>Steering on a map route: straight at the waypoint with a short front check, the blocked heading skipped for BlockedMemory s.</summary>
        private static void NavSteer(Npc n, Vector3 toWp, float now)
        {
            float want = Mathf.Atan2(toWp.x, toWp.z) * Mathf.Rad2Deg, chosen = want;
            n.Side = 0; n.Flipped = false; n.HasWaypoint = false;
            if (n.Col != null && !PoiNav.Guiding(n.Owner))   // (WW) a POI route: straight at its waypoint (the route is walkable; a stuck body is slid on)
            {
                var b = n.Col.bounds;
                float r = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z), 0.15f, 0.35f);
                float lo = b.min.y + 0.6f + r, hi = Mathf.Max(lo, b.min.y + b.size.y * 0.85f - r);
                Vector3 p1 = new Vector3(b.center.x, lo, b.center.z), p2 = new Vector3(b.center.x, hi, b.center.z);
                Transform troot = n.Target.Value != null ? n.Target.Value.transform.root : null;
                bool avoid = now < n.BlockedUntil;
                for (int i = 0; i < NavFan.Length; i++)
                {
                    if (avoid && Mathf.Abs(Mathf.DeltaAngle(want + NavFan[i], n.BlockedYaw)) < 25f) continue;   // blocked that way a moment ago
                    Vector3 dir = Quaternion.Euler(0f, want + NavFan[i], 0f) * Vector3.forward;
                    RaycastHit h;
                    if (!Physics.CapsuleCast(p1, p2, r, dir, out h, 1.2f, NavFeelMask, QueryTriggerInteraction.Ignore)
                        || h.collider.transform.root == n.T || (troot != null && h.collider.transform.root == troot)
                        || Nav.IsFloor(h.collider, h.normal, h.point.y, b.min.y))
                    { chosen = want + NavFan[i]; break; }
                }
            }
            n.Heading = chosen; n.HasHeading = true;
            StepOver(n, now);
        }

        /// <summary>something ankle-high right ahead (a lip, debris, a kerb) and nothing at the knee: a small lift over it (at most every 0.6 s)</summary>
        private static void StepOver(Npc n, float now)
        {
            if (n.Rb == null || n.Col == null || now - n.LastStep < 0.6f || now - n.LastHop < 0.6f) return;
            var b = n.Col.bounds;
            Vector3 fwd = n.T.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) return; fwd.Normalize();
            float reach = Mathf.Min(b.extents.x, b.extents.z) + 0.35f;
            Vector3 low = new Vector3(b.center.x, b.min.y + 0.08f, b.center.z), knee = new Vector3(b.center.x, b.min.y + 0.55f, b.center.z);
            RaycastHit h;
            if (!Physics.Raycast(low, fwd, out h, reach, NavFeelMask, QueryTriggerInteraction.Ignore) || h.collider.transform.root == n.T) return;
            if (h.normal.y >= Nav.WalkNormal) return;                           // ground rising gently: the body walks up it
            if (Physics.Raycast(knee, fwd, reach + 0.25f, NavFeelMask, QueryTriggerInteraction.Ignore)) return;   // higher than a step: not this
            n.LastStep = now;
            Vector3 v = n.Rb.velocity;
            if (v.y < 2.4f) n.Rb.AddForce(Vector3.up * (2.4f - Mathf.Max(0f, v.y)) + fwd * 0.6f, ForceMode.VelocityChange);
            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " steps over " + h.collider.name + " (" + (h.point.y - b.min.y).ToString("0.00") + " m)");
        }

        /// <summary>progress on a map route (path length left): 2.5 s without getting nearer -&gt; a hop; 5 s -&gt; full feelers for 3 s, then the map again</summary>
        private static void NavProgress(Npc n, float pathLeft, float now)
        {
            if (PoiNav.Guiding(n.Owner)) { n.BestDist = pathLeft; n.NoProgressSince = now; return; }   // (WW) no hops / map-off on a POI route
            if (Senses.Blown(n.Owner) || pathLeft < n.BestDist - 0.5f) { n.BestDist = Mathf.Min(n.BestDist, pathLeft); n.NoProgressSince = now; return; }
            float stall = now - n.NoProgressSince;
            if (stall >= 5f * R)
            {
                n.NavOffUntil = now + 3f * R; n.OnNav = false; n.NoProgressSince = now;
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " gets nowhere on its map route, full feelers for " + (3f * R).ToString("0") + " s");
            }
            else if (stall >= 2.5f * R) TryHop(n, now, true);
        }

        // ---------- feelers ----------
        // Pathing (everyone, 1.1.0; the plain rays of 0.7.0 are gone): FeelerCount capsule sweeps (radius ~0.3 m, knee to chest, so a
        // post, a tyre, a rock or a fence bar anywhere across the body counts) over +-FeelerAngle around the direction to the target, on the
        // bumper layers plus Item. Each direction is scored by the progress it makes toward the target over the feeler length (a long free
        // detour beats a short free straight line only when it pays), minus a penalty for ending near a wall. The moment the straight line is
        // blocked the NPC commits to a side (the freer half of the fan; a tie goes with the obstacle's facing) for SideLock seconds and until the
        // straight line has been clear twice in a row: the other half of the fan is penalised, so it can only choose how hard to turn, never
        // dither left-right into the thing. Boxed in (every feeler short), it follows the obstacle's tangent on the committed side. No progress
        // toward the target for 5 s flips the side once, then it rests (Rest mode) and starts over.
        private static readonly float[] _free = new float[32];
        private static readonly Vector3[] _normals = new Vector3[32];
        private static readonly Collider[] _touch = new Collider[16];
        private static readonly Vector3[] _touchAway = new Vector3[16];   // per touching collider: the horizontal direction from it to the body (unit), or zero

        /// <summary>Feeler steering toward the target: a fan of FeelerCount casts over +-FeelerAngle, side commitment and wall following for melee, plain rays for gunmen, the drop check and remembered stuck headings.</summary>
        private static void Steer(Npc n, Vector3 toTarget, float dist, float now, Vector3 toReal)
        {
            int count = Mathf.Clamp(NpcaiSettings.FeelerCount.Value, 3, 31);
            if (count % 2 == 0) count++;
            float half = Mathf.Clamp(NpcaiSettings.FeelerAngle.Value, 10f, 170f);
            if (_angles.Length != count)
            {
                _angles = new float[count];
                for (int i = 0; i < count; i++) _angles[i] = -half + half * 2f * i / (count - 1);   // symmetric, 0 in the middle
            }
            float len = Mathf.Max(0.5f, n.Ranged && !n.ToGhost ? NpcaiSettings.FeelerLength.Value : NpcaiSettings.MeleeFeelerLength.Value);
            if (dist < len && !n.OnNav) len = Mathf.Max(0.5f, dist);     // close to the target: don't "see" it as a wall (a map waypoint is no object)
            Vector3 origin = n.Col != null ? n.Col.bounds.center : n.T.position;
            float targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            Transform troot = n.Target.Value != null ? n.Target.Value.transform.root : null;
            bool blockedMem = now < n.BlockedUntil;
            Vector3 right = Quaternion.Euler(0f, targetYaw, 0f) * Vector3.right;
            int mask = PathMask;
            float radius = 0.3f;
            Vector3 p1 = origin, p2 = origin;
            // the swept capsule: a little wider than the body, from ankle height (5 cm) to chest, so a brazier, a tyre or a fence bar that
            // the body would clip registers (small animals: whatever fits between their collider's top and bottom)
            if (n.Col != null)
            {
                var b = n.Col.bounds;
                radius = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z) * 1.15f, 0.12f, 0.45f);
                float lo = b.min.y + Mathf.Min(0.05f, b.size.y * 0.05f) + radius, hi = b.min.y + b.size.y * 0.7f - radius;   // from 5 cm above the feet: a brazier lip, a kerb
                if (hi < lo) hi = lo;
                p1 = new Vector3(b.center.x, lo, b.center.z); p2 = new Vector3(b.center.x, hi, b.center.z);
            }
            RaycastHit hit;
            float feetY = n.Col != null ? n.Col.bounds.min.y : n.T.position.y - 1f;
            int centre = count / 2;
            // a sweep ignores whatever overlaps its start, so the thing the body is pressed against would be invisible: block the
            // directions toward anything already touching the swept capsule
            int touching = Physics.OverlapCapsuleNonAlloc(p1, p2, radius, _touch, mask, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < touching; k++)
            {
                var c = _touch[k]; _touchAway[k] = Vector3.zero;
                if (c == null || c.transform.root == n.T || (troot != null && c.transform.root == troot)) { _touch[k] = null; continue; }
                Vector3 away; float awayY;
                if (!TouchDir(n, c, origin, out away, out awayY)) { _touch[k] = null; continue; }
                if (Nav.IsFloorCollider(c) && awayY <= feetY + 0.3f) { _touch[k] = null; continue; }      // the ground under the feet (terrain, a cave floor)
                _touchAway[k] = away;
            }
            for (int i = 0; i < count; i++)
            {
                float yaw = targetYaw + _angles[i];
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float free = len; Vector3 normal = Vector3.zero;
                for (int k = 0; k < touching; k++)
                {
                    if (_touch[k] == null) continue;
                    Vector3 away = _touchAway[k];
                    if (away.sqrMagnitude < 0.0001f) continue;
                    if (Vector3.Angle(dir, -away) < 70f) { free = 0f; normal = away; }
                }
                if (free <= 0f) { _free[i] = 0f; _normals[i] = normal; _blocks[i] = 1f; continue; }
                if (Physics.CapsuleCast(p1, p2, radius, dir, out hit, len, mask, QueryTriggerInteraction.Ignore)
                    && (troot == null || hit.collider.transform.root != troot) && hit.collider.transform.root != n.T
                    && !Nav.IsFloor(hit.collider, hit.normal, hit.point.y, feetY))      // gentle ground ahead is not a wall (terrain or a cave floor rising)
                { free = Mathf.Max(0f, hit.distance); normal = hit.normal; }
                _free[i] = free; _normals[i] = normal; _blocks[i] = 1f - free / len;
            }

            {
                bool straightBlocked = _free[centre] < len * 0.9f;
                { float fl0 = 0f, fr0 = 0f; for (int i = 0; i < count; i++) { if (_angles[i] < 0f) fl0 += _free[i]; else if (_angles[i] > 0f) fr0 += _free[i]; } n.FreeLeft = fl0; n.FreeRight = fr0; }
                if (straightBlocked && !n.HasWaypoint && !n.OnNav && now >= n.NextScout)
                {
                    if (Scout(n, origin, p1, p2, radius, mask, toReal, troot, now)) { n.Side = 0; n.Flipped = false; n.NextScout = now + 0.5f * R; }
                    else n.NextScout = now + 2.5f * R;        // nothing found: boxed in - don't burn 80 casts twice a second on it
                }
                if (n.HasWaypoint) { n.Side = 0; n.Flipped = false; }
                else if (n.Side == 0)
                {
                    if (straightBlocked)
                    {
                        float fl = 0f, fr = 0f;
                        for (int i = 0; i < count; i++) { if (_angles[i] < 0f) fl += _free[i]; else if (_angles[i] > 0f) fr += _free[i]; }
                        int side;
                        if (fr > fl * 1.15f) side = 1;
                        else if (fl > fr * 1.15f) side = -1;
                        else
                        {
                            float facing = Vector3.Dot(_normals[centre], right);     // the obstacle's face leans right -> go right
                            side = Mathf.Abs(facing) > 0.05f ? (facing > 0f ? 1 : -1) : (UnityEngine.Random.value < 0.5f ? -1 : 1);
                        }
                        n.Side = side; n.SideUntil = now + SideLock * R; n.ClearLooks = 0;
                        if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " goes around on the " + (side > 0 ? "right" : "left") + " (free L " + fl.ToString("0.0") + " / R " + fr.ToString("0.0") + ")");
                    }
                }
                else
                {
                    if (!straightBlocked) n.ClearLooks++; else n.ClearLooks = 0;
                    if (n.ClearLooks >= 2 && now >= n.SideUntil) { n.Side = 0; n.Flipped = false; }
                }
            }

            int best = -1; float bestScore = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                float yaw = targetYaw + _angles[i];
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                Vector3 reach = dir * _free[i];
                float dAfter = (toTarget - reach).magnitude;
                float score = (dist - dAfter) / len - _blocks[i] * 1.0f;        // net progress toward the target, less for ending at a wall
                if (n.Side != 0 && Mathf.Sign(_angles[i]) == -n.Side && _angles[i] != 0f) score -= 1.0f;
                if (blockedMem && Mathf.Abs(Mathf.DeltaAngle(yaw, n.BlockedYaw)) < 50f) score -= 2f;
                if (n.HasHeading && Mathf.Abs(Mathf.DeltaAngle(yaw, n.Heading)) < 12f) score += 0.1f;   // no flicker between near-equal rays
                _scores[i] = score;
                if (score > bestScore) { bestScore = score; best = i; }
            }
            if (NpcaiSettings.DropCheck.Value && best >= 0)
            {
                // a short look for ground 1.5 m along the winner; a cliff or roof edge there -> the next best direction
                for (int tries = 0; tries < 3 && best >= 0; tries++)
                {
                    Vector3 dir = Quaternion.Euler(0f, targetYaw + _angles[best], 0f) * Vector3.forward;
                    Vector3 ahead = origin + dir * Mathf.Min(1.5f, len);
                    if (Physics.Raycast(ahead, Vector3.down, 4f, GroundMask, QueryTriggerInteraction.Ignore)) break;
                    _scores[best] = float.MinValue;
                    int nb = -1; float nbs = float.MinValue;
                    for (int i = 0; i < count; i++) if (_scores[i] > nbs) { nbs = _scores[i]; nb = i; }
                    best = nb;
                }
            }
            if (best < 0) best = centre;
            float heading = targetYaw + _angles[best];

            if (n.Side != 0)
            {
                // boxed in (every feeler short): follow the obstacle's surface on the committed side, pushed off it a little when very close
                bool boxed = true;
                for (int i = 0; i < count; i++) if (_free[i] > len * 0.6f) { boxed = false; break; }
                if (boxed && _normals[centre] != Vector3.zero)
                {
                    Vector3 nrm = _normals[centre]; nrm.y = 0f;
                    if (nrm.sqrMagnitude > 0.001f)
                    {
                        nrm.Normalize();
                        Vector3 tangent = Vector3.Cross(Vector3.up, nrm);
                        if (Vector3.Dot(tangent, right * n.Side) < 0f) tangent = -tangent;
                        Vector3 follow = tangent + nrm * (_free[centre] < 1f ? 0.5f : 0.15f);
                        heading = Mathf.Atan2(follow.x, follow.z) * Mathf.Rad2Deg;
                        // a corner: the way along the wall is blocked too -> turn around now, don't wait for the no-progress timer
                        RaycastHit ch;
                        if (Physics.CapsuleCast(p1, p2, radius, tangent, out ch, 1.2f, mask, QueryTriggerInteraction.Ignore) && ch.collider.transform.root != n.T && (troot == null || ch.collider.transform.root != troot))
                        {
                            n.Side = -n.Side; n.SideUntil = now + SideLock * 2f * R; n.ClearLooks = 0; n.NextScout = 0f;
                            heading = Mathf.Atan2(-tangent.x, -tangent.z) * Mathf.Rad2Deg;
                            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " dead end, turns around");
                        }
                    }
                }
            }
            heading = AvoidFriends(n, heading, origin, p1, p2, radius, mask, now);
            n.Heading = heading;
            n.HasHeading = true;
        }

        /// <summary>Other NPCs are not in the feelers' mask (Actor layer): without this they ran into each other, the bump counted as "stuck" and switched the map off for 10 s. Now, for every NPC within 3 m: - ahead (+-60 deg) and going the same way (a brain NPC moving within 45 deg of this heading): queue behind it - slow down, never push; - ahead and standing / coming the other way, within 2.5 m: pass it on one side (the side away from it; dead ahead: decided by instance id so two NPCs pick opposite sides), the side kept 1 s; if that way is blocked, the other side; neither: wait; a brain NPC standing still (hold / rest / search) within 1.6 m steps aside for 0.6 s; - beside within 1 m: a small push apart.</summary>
        private static float AvoidFriends(Npc n, float heading, Vector3 origin, Vector3 p1, Vector3 p2, float radius, int mask, float now)
        {
            n.SpeedScale = 1f;
            Vector3 me = n.T.position;
            Vector3 fwd = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            var tgt = n.Target.Value; Transform troot = tgt != null ? tgt.transform.root : null;
            float push = 0f; bool passing = false;
            string myTag = NpcaiSettings.FriendsPassThrough.Value ? Senses.TagOf(n.Owner) : null;
            foreach (var a in Senses.AllAgents)
            {
                if (a.Owner == null || a.T == n.T || a.T.parent != null || (troot != null && a.T == troot)) continue;
                if (myTag != null && a.Tag == myTag) continue;      // same faction: walks through them (Passthrough)
                Vector3 rel = a.T.position - me;
                if (Mathf.Abs(rel.y) > 2f) continue;
                rel.y = 0f;
                float d = rel.magnitude;
                if (d > 3f || d < 0.01f) continue;
                float ang = Vector3.SignedAngle(fwd, rel, Vector3.up);      // > 0: it is on the right
                if (Mathf.Abs(ang) > 60f)
                {
                    if (d < 1f) push += (ang > 0f ? -1f : 1f) * 12f * (1f - d);
                    continue;
                }
                Npc o; _npcs.TryGetValue(a.Owner.GetInstanceID(), out o);
                bool oMoving = o != null && o.Rb != null && o.Mode != Mode.Off && o.Mode != Mode.Hold && o.Mode != Mode.Rest && o.Mode != Mode.Search && o.Mode != Mode.Walk
                               && new Vector3(o.Rb.velocity.x, 0f, o.Rb.velocity.z).sqrMagnitude > 1f;
                if (oMoving && o.HasHeading && Mathf.Abs(Mathf.DeltaAngle(o.Heading, heading)) < 45f)
                {
                    float s = Mathf.Clamp((d - 0.9f) / 1.5f, 0.2f, 1f);      // queue: never push the one in front
                    if (s < n.SpeedScale) n.SpeedScale = s;
                    continue;
                }
                if (d > 2.5f) continue;
                int side;
                if (now < n.AvoidUntil && n.AvoidSide != 0) side = n.AvoidSide;
                else if (Mathf.Abs(ang) < 6f) side = n.Owner.GetInstanceID() < a.Owner.GetInstanceID() ? 1 : -1;
                else side = ang > 0f ? -1 : 1;
                n.AvoidSide = side; n.AvoidUntil = now + 1f; passing = true;
                push += side * Mathf.Lerp(55f, 20f, d / 2.5f);
                if (o != null && !oMoving && d < 1.6f && (o.Mode == Mode.Hold || o.Mode == Mode.Rest || o.Mode == Mode.Search) && !Idle.SearchWalking(o.Owner))
                {
                    o.MakeWayDir = Vector3.Cross(Vector3.up, fwd) * -side; o.MakeWaySpeed = 1.8f;     // away from the side the passer takes
                    o.MakeWayUntil = now + 0.6f;
                }
            }
            if (push == 0f) return heading;
            push = Mathf.Clamp(push, -70f, 70f);
            float h = heading + push;
            if (!passing || WayFree(n, h, origin, p1, p2, radius, mask)) return h;
            float other = heading - push;                                   // that side is a wall: the other side
            if (WayFree(n, other, origin, p1, p2, radius, mask)) { n.AvoidSide = -n.AvoidSide; return other; }
            n.SpeedScale = Mathf.Min(n.SpeedScale, 0.2f);                   // neither: wait for it to move
            return heading;
        }

        /// <summary>True when a body-wide cast along a heading is free for the feeler length (and there is ground ahead with DropCheck).</summary>
        private static bool WayFree(Npc n, float yaw, Vector3 origin, Vector3 p1, Vector3 p2, float radius, int mask)
        {
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            RaycastHit h;
            if (!Physics.CapsuleCast(p1, p2, radius, dir, out h, 1.2f, mask, QueryTriggerInteraction.Ignore)) return true;
            if (h.collider.transform.root == n.T) return true;
            float feetY = n.Col != null ? n.Col.bounds.min.y : n.T.position.y - 1f;
            return Nav.IsFloor(h.collider, h.normal, h.point.y, feetY);
        }

        /// <summary>a bump into another NPC is no reason to call itself stuck</summary>
        private static bool FriendInTheWay(Npc n)
        {
            Vector3 me = n.T.position, fwd = n.T.forward; fwd.y = 0f;
            string myTag = NpcaiSettings.FriendsPassThrough.Value ? Senses.TagOf(n.Owner) : null;
            foreach (var a in Senses.AllAgents)
            {
                if (a.Owner == null || a.T == n.T || a.T.parent != null) continue;
                if (myTag != null && a.Tag == myTag) continue;      // same faction: no bump possible (Passthrough)
                Vector3 rel = a.T.position - me;
                if (Mathf.Abs(rel.y) > 2f) continue;
                rel.y = 0f;
                if (rel.sqrMagnitude > 1.3f * 1.3f) continue;
                if (Vector3.Angle(fwd, rel) < 75f) return true;
            }
            return false;
        }

        // The L-corner problem: a 2.5 m fan cannot tell which end of a wall leads to the target, and the wrong side is a dead end. When the
        // straight line is blocked the NPC scouts: 12 long sweeps (ScoutLength m) around the direction to the target; the end of each free
        // stretch is a candidate corner, kept only if the line from there to the target is clear; the corner with the shortest path
        // (there + from there) becomes a waypoint the feelers steer to until it is reached, the straight line clears, or it times out.
        private static readonly float[] _scoutAngles = { -15f, 15f, -40f, 40f, -65f, 65f, -90f, 90f, -115f, 115f, -140f, 140f };
        private const float ScoutLength = 10f;

        /// <summary>Scouts a corner with a clear line to the target (a waypoint kept for a while) when the direct way is blocked.</summary>
        private static bool Scout(Npc n, Vector3 origin, Vector3 p1, Vector3 p2, float radius, int mask, Vector3 toReal, Transform troot, float now)
        {
            float yaw0 = Mathf.Atan2(toReal.x, toReal.z) * Mathf.Rad2Deg;
            float dReal = toReal.magnitude;
            float feetY = n.Col != null ? n.Col.bounds.min.y : n.T.position.y - 1f;
            Vector3 chestOff = p2 - origin;        // scouting at chest height
            Vector3 targetChest = origin + toReal + Vector3.up * 0.3f;
            bool blockedMem = now < n.BlockedUntil;
            float bestCost = float.MaxValue; Vector3 bestEnd = Vector3.zero; float bestAngle = 0f;
            RaycastHit hit;
            for (int k = 0; k < _scoutAngles.Length; k++)
            {
                float yaw = yaw0 + _scoutAngles[k];
                if (blockedMem && Mathf.Abs(Mathf.DeltaAngle(yaw, n.BlockedYaw)) < 30f) continue;
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float free = ScoutLength;
                if (Physics.CapsuleCast(p1, p2, radius, dir, out hit, ScoutLength, mask, QueryTriggerInteraction.Ignore)
                    && (troot == null || hit.collider.transform.root != troot) && hit.collider.transform.root != n.T
                    && !Nav.IsFloor(hit.collider, hit.normal, hit.point.y, feetY))
                    free = hit.distance;
                // walk the free stretch from near to far: the first point with a clear line to the target is the corner
                float reach = free - radius - 0.3f;
                for (float along = 1.5f; along <= reach; along += 2.5f)
                {
                    Vector3 end = origin + dir * along;
                    Vector3 toT = targetChest - (end + chestOff);
                    if (Physics.Raycast(end + chestOff, toT.normalized, out hit, toT.magnitude, mask, QueryTriggerInteraction.Ignore)
                        && (troot == null || hit.collider.transform.root != troot) && hit.collider.transform.root != n.T
                        && !Nav.IsFloor(hit.collider, hit.normal, hit.point.y, feetY)) continue;
                    float cost = along + (origin + toReal - end).magnitude;
                    if (cost < bestCost) { bestCost = cost; bestEnd = end; bestAngle = _scoutAngles[k]; }
                    break;      // farther points on this ray only add path
                }
            }
            if (bestCost == float.MaxValue) return false;
            n.Waypoint = bestEnd; n.HasWaypoint = true; n.WaypointUntil = now + 8f * R;
            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " scouts a corner " + (bestAngle < 0f ? "left" : "right") + " " + Mathf.Abs(bestAngle) + " deg, " + (bestEnd - origin).magnitude.ToString("0.0") + " m away (path " + bestCost.ToString("0.0") + " m, straight " + dReal.ToString("0.0") + ")");
            return true;
        }

        /// <summary>the straight line to the target is free for the body (one sweep)</summary>
        private static bool DirectClear(Npc n, Vector3 tp, float dist)
        {
            if (n.Col == null) return true;
            var b = n.Col.bounds;
            float radius = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z) * 1.15f, 0.12f, 0.45f);
            float lo = b.min.y + Mathf.Min(0.05f, b.size.y * 0.05f) + radius, hi = b.min.y + b.size.y * 0.7f - radius;
            if (hi < lo) hi = lo;
            Vector3 p1 = new Vector3(b.center.x, lo, b.center.z), p2 = new Vector3(b.center.x, hi, b.center.z);
            Vector3 dir = tp - n.T.position; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return true;
            dir.Normalize();
            RaycastHit hit;
            Transform troot = n.Target.Value != null ? n.Target.Value.transform.root : null;
            if (!Physics.CapsuleCast(p1, p2, radius, dir, out hit, Mathf.Max(0.1f, dist - 0.5f), PathMask, QueryTriggerInteraction.Ignore)) return true;
            if (troot != null && hit.collider.transform.root == troot) return true;
            if (hit.collider.transform.root == n.T) return true;
            return Nav.IsFloor(hit.collider, hit.normal, hit.point.y, b.min.y);
        }

        /// <summary>moving modes: is the NPC getting anywhere? (called from Think) - 5 s without coming nearer flips the side once, then rests</summary>
        private static bool Progress(Npc n, float d, float now)
        {
            if (Senses.Blown(n.Owner)) { n.NoProgressSince = now; n.BestDist = Mathf.Min(n.BestDist, d); return true; }   // pushed by a tornado: no judgement
            if (d < n.BestDist - 0.5f) { n.BestDist = d; n.NoProgressSince = now; return true; }
            if (now - n.NoProgressSince < NoProgressSeconds * R) return true;
            n.NoProgressSince = now;
            n.HasWaypoint = false; n.NextScout = 0f;
            if (!n.Flipped)
            {
                n.Flipped = true;
                n.Side = n.Side != 0 ? -n.Side : (UnityEngine.Random.value < 0.5f ? -1 : 1);
                n.SideUntil = now + SideLock * 2f * R; n.ClearLooks = 0;
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " gets nowhere, tries the " + (n.Side > 0 ? "right" : "left"));
                return true;
            }
            n.Flipped = false; n.Side = 0; n.BestDist = float.MaxValue;
            n.ModeUntil = now + RestSeconds * R;
            if (n.ToGhost && Senses.CannotReach(n.Owner)) { n.NextLookTurn = 0f; SetMode(n, Mode.Search, "gets nowhere toward the ghost"); return false; }
            SetMode(n, Mode.Rest, "gets nowhere, rests");
            return false;
        }

        private const float SideLock = 1.2f, NoProgressSeconds = 2.5f, RestSeconds = 1f, StuckWindow = 10f;

        /// <summary>True when the line from the NPC's eyes to the target's chest is clear of walls and terrain.</summary>
        private static bool LineOfSight(Npc n, GameObject target, Vector3 tp)
        {
            Vector3 eye = n.Col != null ? n.Col.bounds.center + Vector3.up * n.Col.bounds.extents.y * 0.6f : n.T.position + Vector3.up * 1.5f;
            Vector3 chest = tp + Vector3.up * 1f;
            RaycastHit hit;
            if (!Physics.Linecast(eye, chest, out hit, LosMask, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.transform.root == target.transform.root;
        }

    }
}
