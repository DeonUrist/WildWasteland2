using System;
using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Idle part: the per-frame tick, travel orders, the camp / search / ghost-walk thinks and the walk legs.</summary>
    internal static partial class Idle
    {
        // ---------- per frame ----------
        /// <summary>(crew AI) the idle behaviour off ([Idle] EnableIdleBehavior): no camp logic, but travel orders and the convoy crews' run check go on (as long as the senses run)</summary>
        public static void Tick()
        {
            if (!Senses.On) { if (_ctl.Count > 0) { foreach (var kv in _ctl) Stop(kv.Value, true); _ctl.Clear(); _claimed.Clear(); } _idleWas = true; return; }
            bool idleOn = On;
            if (!idleOn && _idleWas && _ctl.Count > 0) { foreach (var kv in _ctl) Stop(kv.Value, true); _ctl.Clear(); _claimed.Clear(); }
            _idleWas = idleOn;
            float now = Time.time, dt = Time.deltaTime;
            if (dt <= 0f) return;
            _thinkCap = Mathf.CeilToInt(_thinkers * dt / ThinkEvery) + 1; _thinkers = 0; _thinksNow = 0;
            foreach (var a in Senses.AllAgents)
            {
                if (a.Owner == null) continue;
                if (Brain.Retreating(a.Owner)) continue;
                int id = a.Owner.GetInstanceID();
                Order ord = null;
                if (_travel.Count > 0 && _travel.TryGetValue(id, out ord) && !ReferenceEquals(ord.Npc, a.Owner)) ord = null;
                bool simple = SimpleBandits.Has(a.Owner);
                if (ord == null)
                {
                    if (simple) { SimpleIdle(a, id, now); continue; }     // convoy crews: no camp logic, only the run check (Idle walks them only with a travel order)
                    if (!idleOn) continue;
                }
                Ctl c;
                if (!_ctl.TryGetValue(id, out c)) { c = Make(a, now); if (simple) c.NextAnimCheck = now + 2f; _ctl[id] = c; }
                if (c.Excluded && (ord == null || c.Follower)) continue;
                if (ord != null) ord.Touched = now;
                else if (c.Leg == Leg.Travel) Stop(c, true);          // its order was cancelled
                if (a.T.parent != null)        // seated in a car (Apocapatrol crews): not ours - no home lookup, no logic, no label
                {
                    c.ThinkKind = 0;
                    if (c.Moving || c.Leg != Leg.None || c.Turning) Stop(c, false);
                    c.InSearch = false; c.Inv = false;
                    continue;
                }
                if (a.Blown)                   // shoved by a tornado: stands as vanilla, nothing counts (no failed tries)
                {
                    c.ThinkKind = 0;
                    if (c.Moving || c.Leg != Leg.None || c.Turning) Stop(c, true);
                    c.InSearch = false; c.PendingBack = false; c.Inv = false;
                    continue;
                }
                if (ord != null && (simple || c.Excluded || !idleOn)) { TravelTick(c, ord, now, dt); continue; }   // only the travel (and the brain for the fight)
                if (!c.Resolved && now >= c.NextResolve) { try { Resolve(c, now); } catch (Exception e) { NpcaiRuntime.Error("Idle: " + e); c.Excluded = true; continue; } if (c.Excluded) continue; }
                bool searching = a.State == Senses.State.Search && a.Target == null && a.T.parent == null;
                // (1.6.0) going to check a ghost: walked like the way home (camp map to its exit, then straight), the brain stands aside
                var gh = a.Ghost;
                bool investigating = InvestigateOn && a.State == Senses.State.Investigate && a.Target == null && gh != null && gh.Obj != null
                                     && !(gh.Id == c.SkipGhost && Flat(gh.Pos - c.SkipPos) < 8f);
                if (c.Inv && !investigating)
                {
                    c.Inv = false;
                    if (c.Leg == Leg.Investigate || c.Moving) Stop(c, a.State != Senses.State.Combat);   // to a search / idle: the idle clip, not the run
                }
                if (investigating)
                {
                    if (c.InSearch) c.InSearch = false;
                    if (!c.Inv)
                    {
                        if (c.Leg != Leg.None || c.Moving || c.Turning) Stop(c, false);
                        c.Inv = true; c.InvGhost = gh.Id; c.InvFails = 0; c.WaitUntil = 0f;
                        if (WildWasteland.Core.Log.L2) Log(c, "walks to ghost #" + gh.Id + " (" + Flat(a.T.position - gh.Pos).ToString("0") + " m)");
                    }
                    else if (c.InvGhost != gh.Id) { c.InvGhost = gh.Id; c.InvFails = 0; c.BestLeft = c.BestStraight = float.MaxValue; c.ProgressAt = now; }   // news: a new spot
                    c.BusyAt = now; c.LastWasSearch = false; c.Fresh = false; c.Tries = 0; c.PendingBack = false; c.AtHome = false;
                    if (Due(c, now, 1))
                    {
                        try { InvestigateThink(c, now); }
                        catch (Exception e) { NpcaiRuntime.Error("Idle ghost walk: " + e); Stop(c, false); c.Inv = false; c.SkipGhost = gh.Id; c.SkipPos = gh.Pos; }
                    }
                    if (c.Leg == Leg.Investigate && !Brain.IsWalk(a.Owner)) { Stop(c, false); c.WaitUntil = 0f; }   // the brain took the body back: no two drivers
                    if (c.Moving) Drive(c, dt, now);
                    continue;
                }
                bool idle = a.State == Senses.State.Idle && a.Target == null && a.Ghost == null && a.T.parent == null
                            && (a.DetectedVar == null || a.DetectedVar.Value == null);
                if (searching)
                {
                    if (!c.InSearch && (c.Leg != Leg.None || c.Moving || c.Turning)) Stop(c, false);
                    c.BusyAt = now; c.LastWasSearch = true; c.Fresh = false; c.Tries = 0; c.PendingBack = false; c.AtHome = false;
                    if (Due(c, now, 2))
                    {
                        try { SearchThink(c, now); } catch (Exception e) { NpcaiRuntime.Error("Idle search: " + e); Stop(c, false); c.InSearch = false; }
                    }
                    if (c.Moving) Drive(c, dt, now);
                    continue;
                }
                if (c.InSearch) { c.InSearch = false; if (c.Moving) Stop(c, idle); c.WaitUntil = 0f; }   // the search is over
                if (!idle)
                {
                    if (c.Leg != Leg.None || c.Moving || c.Turning) Stop(c, false);   // the fight logic takes over: its own animation, not ours
                    c.BusyAt = now; c.LastWasSearch = false; c.Fresh = false; c.Tries = 0; c.WaitUntil = 0f; c.PendingBack = false; c.AtHome = false;
                    c.ThinkKind = 0;
                    continue;
                }
                if (ord != null)
                {
                    if (ord.State == 1 && Due(c, now, 4)) { try { TravelThink(c, ord, now); } catch (Exception e) { NpcaiRuntime.Error("Idle travel: " + e); Stop(c); ord.State = 3; } }
                }
                else if (Due(c, now, 3))
                {
                    try { Think(c, now); } catch (Exception e) { NpcaiRuntime.Error("Idle: " + e); Stop(c); c.Excluded = true; }
                }
                if (!c.Moving && !c.Turning && now >= c.NextAnimCheck) { c.NextAnimCheck = now + 0.5f; RunInPlace(c); }
                if (c.Moving) Drive(c, dt, now);
                else if (c.Turning)
                {
                    float y = Mathf.MoveTowardsAngle(a.T.eulerAngles.y, c.LookYaw, Mover.TurnStep(dt) * 0.5f);
                    a.T.rotation = Quaternion.Euler(0f, y, 0f);
                    if (Mathf.Abs(Mathf.DeltaAngle(y, c.LookYaw)) < 1f) c.Turning = false;
                }
            }
            // forget dead NPCs (and free their spawn points)
            if (_ctl.Count > 0 && (Time.frameCount & 63) == 0)
            {
                _dead.Clear();
                var cam = Core.GameRefs.MainCamera; Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
                foreach (var kv in _ctl)
                {
                    var c = kv.Value;
                    if (c.A.Owner == null) { _dead.Add(kv.Key); if (c.HasHome) _claimed.Remove(c.SpotId); continue; }
                    if (cam != null)
                    {
                        // (WW perf, D7) the brain's camera-distance scale (Brain TickScale): far from the camera the finer steering is wasted
                        float cd = (c.A.T.position - cp).sqrMagnitude;
                        c.Scale = cd > 80f * 80f ? 4f : cd > 40f * 40f ? 2f : 1f;
                    }
                }
                foreach (var k in _dead) { _ctl.Remove(k); _spawnOf.Remove(k); }
            }
            // (crew AI) orders of dead / removed NPCs go
            if (_travel.Count > 0 && (Time.frameCount & 63) == 32)
            {
                _dead.Clear();
                foreach (var kv in _travel) if (kv.Value.Npc == null) _dead.Add(kv.Key);
                foreach (var k in _dead) _travel.Remove(k);
            }
        }

        /// <summary>Builds a controller for an agent (Rigidbody, run clip animator, staggered first think).</summary>
        private static Ctl Make(Senses.Agent a, float now)
        {
            var c = new Ctl { A = a, Rb = a.Owner.GetComponent<Rigidbody>(), BusyAt = -1000f, Fresh = true, NextThink = now + (_stagger++ % 10) * 0.02f };   // never busy yet: no return delay after a load
            string prefab = a.Tag == "Coyotes" ? Senses.PrefabOf(a.Owner) : "";
            c.Merchant = prefab == "Merchant" || prefab == "Mechanic" || prefab == "Organic_Mechanic";
            c.Excluded = c.Resolved = c.Merchant;   // shopkeepers keep their authored spots; explicit travel orders still work.
            var anim = a.T.Find("Anim");
            c.Anim = anim != null ? anim.GetComponent<Animator>() : a.Owner.GetComponentInChildren<Animator>();
            return c;
        }

        /// <summary>(crew AI) a convoy crew member (simple convoy bandit) on foot with nothing to fight: nobody drives its body any more (the brain let go), so the game's Movement FSM left in Run would run it forward for good - the same check as an idle camp NPC's, twice a second (the first 2 s after it registers skipped: a jump off the car)</summary>
        private static void SimpleIdle(Senses.Agent a, int id, float now)
        {
            if (a.T.parent != null || a.Blown || a.Target != null || a.Ghost != null || (a.DetectedVar != null && a.DetectedVar.Value != null)) return;
            Ctl c;
            if (!_ctl.TryGetValue(id, out c)) { c = Make(a, now); c.NextAnimCheck = now + 2f; _ctl[id] = c; }
            if (c.Excluded) return;
            if (c.Moving || c.Leg != Leg.None || c.Turning) Stop(c, true);       // a cancelled travel
            if (now >= c.NextAnimCheck) { c.NextAnimCheck = now + 0.5f; RunInPlace(c); }
        }

        /// <summary>(crew AI) an NPC with a travel order that Idle does not run otherwise (a convoy crew member, an NPC without camp behaviour, the idle behaviour off): it travels while idle; with a target / ghost / search the brain and the senses have it and the walk waits</summary>
        private static void TravelTick(Ctl c, Order ord, float now, float dt)
        {
            var a = c.A;
            bool idle = a.State == Senses.State.Idle && a.Target == null && a.Ghost == null && (a.DetectedVar == null || a.DetectedVar.Value == null);
            if (!idle)
            {
                if (c.Leg != Leg.None || c.Moving || c.Turning) { Stop(c, false); if (WildWasteland.Core.Log.L2) Log(c, "travel paused (" + a.State + ")"); }
                c.ThinkKind = 0; c.WaitUntil = 0f;
                return;
            }
            if (ord.State == 1 && Due(c, now, 4)) { try { TravelThink(c, ord, now); } catch (Exception e) { NpcaiRuntime.Error("Idle travel: " + e); Stop(c); ord.State = 3; } }
            if (!c.Moving && !c.Turning && now >= c.NextAnimCheck) { c.NextAnimCheck = now + 0.5f; RunInPlace(c); }
            if (c.Moving) Drive(c, dt, now);
        }

        /// <summary>Travel order think: start / follow the travel leg, slow to a walk near the goal, report arrival or failure to the order.</summary>
        private static void TravelThink(Ctl c, Order ord, float now)
        {
            Vector3 pos = c.A.T.position;
            if (c.Leg == Leg.Travel)
            {
                if (Flat(ord.Point - c.Goal) > 0.25f)
                {
                    if (Flat(ord.Point - c.Goal) > 4f) { c.BestLeft = float.MaxValue; c.ProgressAt = now; }   // the goal moved: progress counts from here
                    c.Goal = ord.Point;
                }
                LegThink(c, now, pos);
                return;
            }
            if (c.Leg != Leg.None || c.Turning) Stop(c, false);     // a camp leg left over
            if (now < c.WaitUntil) return;
            float left = Flat(pos - ord.Point);
            if (left <= ord.Radius) { ord.State = 2; c.Tries = 0; if (WildWasteland.Core.Log.L2) Log(c, "is there (travel, " + left.ToString("0.0") + " m)"); return; }
            c.Ord = ord;
            float runBeyond = Mathf.Max(0f, NpcaiSettings.TravelRunBeyond.Value);
            StartLeg(c, Leg.Travel, ord.Point, now, left > runBeyond ? 5f : 2.5f);
        }

        /// <summary>a think is due for this NPC (and this frame still has room for it); kind = the thinking state it is in</summary>
        private static bool Due(Ctl c, float now, byte kind)
        {
            _thinkers++;
            if (c.ThinkKind != kind) { c.ThinkKind = kind; if (now >= c.NextThink) c.NextThink = now + UnityEngine.Random.value * ThinkEvery * c.Scale; }
            // a slow (far) thinker walking: a reached waypoint or the goal close ahead asks for the next think now (no overshooting), but
            // never sooner than ThinkEvery after the last one (the near rate)
            if (c.Scale > 1f && c.Moving && c.Leg != Leg.None && now < c.NextThink && now - (c.NextThink - ThinkEvery * c.Scale) >= ThinkEvery)
            {
                Vector3 p = c.A.T.position;
                if (Flat(p - c.Goal) <= 2f || Flat(p - c.Steer) <= 1f) c.NextThink = now;
            }
            if (now < c.NextThink || _thinksNow >= _thinkCap) return false;
            _thinksNow++;
            c.NextThink += ThinkEvery * c.Scale;
            if (c.NextThink < now) c.NextThink = now;
            return true;
        }

        /// <summary>Idle think of a camp NPC: after losing everything it waits ReturnDelay, then runs home; at home it walks a short round over its camp's patrol points.</summary>
        private static void Think(Ctl c, float now)
        {
            if (c.Excluded || !c.HasHome || c.Forgotten) return;
            Vector3 pos = c.A.T.position;
            // after a search: home at once; after a fight that ended some other way: a moment where it is first
            if (!c.LastWasSearch && now - c.BusyAt < Mathf.Max(0f, NpcaiSettings.IdleReturnDelay.Value)) return;

            if (c.Leg != Leg.None) { LegThink(c, now, pos); return; }
            if (now < c.WaitUntil) { LookAround(c, now); return; }
            if (c.PendingBack) { c.PendingBack = false; StartLeg(c, Leg.Back, c.Home, now, 2.5f); return; }   // stood at the patrol point: back home

            float dHome = Flat(pos - c.Home);
            if (dHome > 2.5f || Mathf.Abs(pos.y - c.Home.y) > 2.5f) { c.AtHome = false; StartLeg(c, Leg.Home, c.Home, now, dHome > 25f ? 5f : 2.5f); return; }

            // at home: the base walk, near the camera only
            if (!c.AtHome) { c.AtHome = true; c.WaitUntil = now + (c.Fresh ? UnityEngine.Random.Range(1f, 4f) : UnityEngine.Random.Range(10f, 25f)); c.Fresh = false; return; }   // just loaded: the first round soon
            var cam = Core.GameRefs.MainCamera;
            if (cam == null || Flat(cam.transform.position - pos) > NpcaiSettings.IdleWalkRadius.Value) return;
            if (c.PointsState != 1 && now >= c.NextPoints)
            {
                if (c.Points == null) c.Points = new List<Vector3>();
                int r = Nav.PatrolPoints(c.Home, c.Points, 4, 4f, 15f);
                if (r < 0)       // home's map not baked yet (or home is off the map: a platform high above the camp floor)
                {
                    c.PointsState = -1; c.NextPoints = now + 10f;
                    if (++c.PointsTries >= 30) { c.PointsState = 1; c.Points.Clear(); c.DroppedUntil = new float[0]; if (WildWasteland.Core.Log.L2) Log(c, "no camp map at its home - stands"); }
                    return;
                }
                c.PointsState = 1; c.DroppedUntil = new float[c.Points.Count];
                if (WildWasteland.Core.Log.L2) Log(c, r + " patrol point(s)" + (r == 0 ? " - stands" : ""));
            }
            if (c.PointsState != 1 || c.Points.Count == 0) return;
            int pick = -1, tries = 0;
            while (tries++ < 6) { int i = UnityEngine.Random.Range(0, c.Points.Count); if (now >= c.DroppedUntil[i]) { pick = i; break; } }
            if (pick < 0) { c.WaitUntil = now + 10f; return; }
            if (!Nav.BodyPathClear(c.Home, c.Points[pick])) { c.DroppedUntil[pick] = now + 300f; c.WaitUntil = now + 5f; if (WildWasteland.Core.Log.L2) Log(c, "patrol point " + pick + " blocked now, skipped"); return; }
            c.PointIdx = pick;
            StartLeg(c, Leg.ToPoint, c.Points[pick], now, 2.5f);
        }

        /// <summary>Search think: walks a few points around the searched spot and back (map points when on a camp map), then ends the search early.</summary>
        private static void SearchThink(Ctl c, float now)
        {
            Vector3 pos = c.A.T.position;
            if (!c.InSearch)
            {
                c.InSearch = true; c.SOrigin = pos; c.SAtOrigin = true; c.SLast = -1; c.SNext = 0; c.SLooked = false;
                if (c.SPts == null) c.SPts = new List<Vector3>();
                int r = Nav.SearchPoints(pos, c.SPts, 3, 4f, 12f);
                c.SDropped = new float[c.SPts.Count];
                c.WaitUntil = now + UnityEngine.Random.Range(1.5f, 3f);
                if (WildWasteland.Core.Log.L2) Log(c, "searches: " + c.SPts.Count + " point(s) to walk to" + (r < 0 ? " (off a camp map)" : ""));
                return;
            }
            if (c.Leg != Leg.None) { LegThink(c, now, pos); return; }
            if (now < c.WaitUntil) return;
            if (!c.SAtOrigin) { StartLeg(c, Leg.SearchBack, c.SOrigin, now, 2.5f); return; }
            // (1.4.10) each point of the round once (out and back to the spot), then a short look around and the search is over: home.
            // No point to walk (or none clear any more): it looks around a few seconds where it stands, then home.
            while (c.SNext < c.SPts.Count)
            {
                int i = c.SNext++;
                if (now < c.SDropped[i]) continue;
                if (!Nav.BodyPathClear(c.SOrigin, c.SPts[i])) { c.SDropped[i] = now + 300f; continue; }
                c.SLast = i;
                StartLeg(c, Leg.SearchOut, c.SPts[i], now, 2.5f);
                return;
            }
            if (!c.SLooked) { c.SLooked = true; c.WaitUntil = now + (c.SPts.Count == 0 ? 6f : 2f); return; }
            Log(c, c.SPts.Count == 0 ? "no round to walk, looked around - nothing here" : "walked its round - nothing here");
            Senses.EndSearch(c.A.Owner, c.SPts.Count == 0 ? "looked around, nothing here" : "walked the round, nothing here");
            c.WaitUntil = now + 1f;
        }

        /// <summary>for the brain: this NPC is walking its search round (the brain leaves the body's facing to it while it does) walking home or on a search round (Passthrough keeps friends' bodies apart only once this is over)</summary>
        internal static bool Busy(GameObject owner)
        {
            Ctl c;
            return owner != null && _ctl.TryGetValue(owner.GetInstanceID(), out c) && (c.InSearch || c.Leg == Leg.Home || c.Leg == Leg.Travel || c.Inv);
        }

        /// <summary>(1.6.0) for the brain: Idle has this NPC's walk to a ghost (the brain switches to Mode Walk and stands aside)</summary>
        internal static bool WalksToGhost(GameObject owner)
        {
            Ctl c;
            return On && owner != null && _ctl.TryGetValue(owner.GetInstanceID(), out c) && c.Inv;
        }

        /// <summary>The ghost walk runs while the brain does (the brain stands aside in Walk mode).</summary>
        private static bool InvestigateOn { get { return Brain.On; } }

        /// <summary>the walk to a ghost: starts once the brain has stood aside, follows the ghost when it moves, arrival -&gt; the senses' search</summary>
        private static void InvestigateThink(Ctl c, float now)
        {
            var a = c.A; var g = a.Ghost;
            if (g == null) return;
            if (!Brain.IsWalk(a.Owner)) { c.NextThink = now + 0.05f + 0.1f * UnityEngine.Random.value; return; }      // the brain hasn't let go yet (its next think; own phase per NPC)
            Vector3 goal; if (!Senses.GoalOf(a.Owner, out goal)) goal = g.Pos;    // (1.1.0) the flank point first, then the ghost
            if (c.Leg == Leg.Investigate)
            {
                if (Flat(goal - c.Goal) > 0.25f)
                {
                    if (Flat(goal - c.Goal) > 4f) { c.BestLeft = c.BestStraight = float.MaxValue; c.ProgressAt = now; }   // the spot moved: progress counts from here
                    c.Goal = goal;
                }
                LegThink(c, now, a.T.position);
                return;
            }
            if (now < c.WaitUntil) return;
            c.InvOnMap = false; c.BestStraight = c.LastMapLeft = c.LastStraightLeft = float.MaxValue;
            StartLeg(c, Leg.Investigate, goal, now, 5f);
        }

        /// <summary>True while the NPC walks its search round.</summary>
        internal static bool SearchWalking(GameObject owner)
        {
            Ctl c;
            return owner != null && _ctl.TryGetValue(owner.GetInstanceID(), out c) && c.InSearch && c.Moving;
        }

        /// <summary>Starts a walk leg toward a goal at a speed (and thinks it at once).</summary>
        private static void StartLeg(Ctl c, Leg leg, Vector3 goal, float now, float speed)
        {
            c.Leg = leg; c.Goal = goal; c.LegStart = now; c.ProgressAt = now; c.BestLeft = float.MaxValue; c.Speed = speed;
            if (leg == Leg.Home) { if (WildWasteland.Core.Log.L2) Log(c, "runs home (" + Flat(c.A.T.position - goal).ToString("0") + " m, try " + (c.Tries + 1) + ")"); }
            else if (leg == Leg.Travel && WildWasteland.Core.Log.L2) Log(c, (speed > 2.5f ? "runs" : "walks") + " to its travel goal (" + Flat(c.A.T.position - goal).ToString("0") + " m" + (c.Ord != null && c.Ord.Tries > 0 ? ", try " + (c.Ord.Tries + 1) : "") + ")");
            LegThink(c, now, c.A.T.position);
            if (c.Leg != Leg.None) Go(c, speed);
        }

        /// <summary>Leg think: arrival (flat distance / caller radius / same POI floor), map route next point, hops, slowing near home or the travel goal, and the no-progress patience.</summary>
        private static void LegThink(Ctl c, float now, Vector3 pos)
        {
            float left = Flat(pos - c.Goal);
            bool inv = c.Leg == Leg.Investigate, travel = c.Leg == Leg.Travel;
            float arrive = c.Leg == Leg.Home ? 1.5f : c.Leg == Leg.SearchBack ? 1.2f : inv ? Brain.ArriveFor(c.A.Owner) : travel ? (c.Ord != null ? c.Ord.Radius : 1.5f) : 0.8f;
            // (WW) a travel goal on another floor of a POI with navigation data counts only once the NPC is on that floor
            bool flat = inv || travel && !PoiNav.Inside(c.Goal);
            if (left <= arrive && (flat || Mathf.Abs(pos.y - c.Goal.y) < 2.5f)) { Arrived(c, now); return; }   // a ghost: flat distance, as the brain judged it; a travel goal: the caller's radius, flat
            Vector3 next = c.Goal; float pathLeft = left;
            bool onMap = Nav.On && Nav.Next(c.A.Owner, pos, c.Goal, out next, out pathLeft);
            if (onMap)
            {
                c.Steer = next; left = pathLeft;
                if (PoiNav.Guiding(c.A.Owner)) c.ProgressAt = now;      // (WW) a POI route never gives up: its watchdog slides a stuck body on
                if (Nav.HopAhead(pos, next)) Hop(c, now);             // the map's way crosses a low lip here
            }
            else { c.Steer = c.Goal; }
            if (c.Leg == Leg.Home && left <= 25f && c.Speed > 2.5f) Go(c, 2.5f);      // slows to a walk near home
            else if (travel && c.Speed > 2.5f && left <= NpcaiSettings.TravelRunBeyond.Value) Go(c, 2.5f);
            if (inv)
            {
                // map path left and straight distance are two measures: each counts against its own best (switching between them is no
                // progress by itself); a jump up of one (a new exit, the spot moved) starts that measure again from there
                c.InvOnMap = onMap;
                if (onMap) { if (left > c.LastMapLeft + 2f) c.BestLeft = left; c.LastMapLeft = left; if (left < c.BestLeft - 0.5f) { c.BestLeft = left; c.ProgressAt = now; } }
                else { if (left > c.LastStraightLeft + 2f) c.BestStraight = left; c.LastStraightLeft = left; if (left < c.BestStraight - 0.5f) { c.BestStraight = left; c.ProgressAt = now; } }
            }
            else if (left < c.BestLeft - 0.5f) { c.BestLeft = left; c.ProgressAt = now; }
            float patience = (c.Leg == Leg.Home || inv || travel ? 2f : 1f) * c.Scale;      // (WW perf) thinks Scale x rarer far from the camera: as much patience in thinks
            if (now - c.ProgressAt > patience) Failed(c, now);
        }

        /// <summary>a small hop over a low lip ahead (same impulse as the brain's): low ray hits something, knee-high ray doesn't; once per 2 s</summary>
        private static void Hop(Ctl c, float now)
        {
            if (c.Rb == null || now - c.LastHop < 2f) return;
            var col = c.A.Col; if (col == null) return;
            var b = col.bounds;
            Vector3 fwd = c.A.T.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) return; fwd.Normalize();
            float reach = Mathf.Min(b.extents.x, b.extents.z) + 0.45f;
            Vector3 low = new Vector3(b.center.x, b.min.y + 0.1f, b.center.z), knee = new Vector3(b.center.x, b.min.y + 0.55f, b.center.z);
            RaycastHit h;
            if (!Physics.Raycast(low, fwd, out h, reach, FeelMask | (1 << 14), QueryTriggerInteraction.Ignore) || h.collider.transform.root == c.A.T) return;
            if (Physics.Raycast(knee, fwd, reach + 0.2f, FeelMask | (1 << 14), QueryTriggerInteraction.Ignore)) return;
            c.LastHop = now;
            c.Rb.AddForce(Vector3.up * 3.2f + fwd * 1.5f, ForceMode.VelocityChange);
            Brain.HopAnim(c.A.Owner);                                    // (1.2.0) the jump on an animated body
            if (WildWasteland.Core.Log.L2) Log(c, "hops over a low edge (" + h.collider.name + ")");
        }

        /// <summary>A leg arrived: travel order done, ghost reached (the senses search), search point reached, home or patrol point reached.</summary>
        private static void Arrived(Ctl c, float now)
        {
            var leg = c.Leg;
            Stop(c);
            if (leg == Leg.Travel) { if (c.Ord != null) { c.Ord.State = 2; c.Ord.Tries = 0; } Log(c, "is there (travel)"); return; }
            if (leg == Leg.Investigate) { c.InvFails = 0; bool flank = Senses.IsFlanking(c.A.Owner); if (WildWasteland.Core.Log.L2) Log(c, flank ? "is at its flank point" : "is at the ghost"); Senses.ArrivedAt(c.A.Owner); c.WaitUntil = flank ? now + 0.3f : c.WaitUntil; return; }   // the senses start the search (or a pursuit); after a flank point: the ghost itself
            if (leg == Leg.SearchOut) { c.SAtOrigin = false; c.WaitUntil = now + UnityEngine.Random.Range(2f, 4f); return; }   // the brain looks around
            if (leg == Leg.SearchBack) { c.SAtOrigin = true; c.WaitUntil = now + UnityEngine.Random.Range(1.5f, 3f); return; }
            if (leg == Leg.Home) { c.Tries = 0; c.AtHome = true; c.WaitUntil = now + UnityEngine.Random.Range(10f, 25f); if (WildWasteland.Core.Log.L2) Log(c, "is home"); }
            else if (leg == Leg.ToPoint) { c.WaitUntil = now + UnityEngine.Random.Range(4f, 10f); c.NextLook = now + 1f; c.AtHome = false; c.PendingBack = true; }
            else { c.AtHome = true; c.WaitUntil = now + UnityEngine.Random.Range(10f, 25f); }
        }

        /// <summary>A leg got nowhere: travel retries / give-up, ghost walk retries then hands the ghost to the brain, search point dropped, home retries then forgets the home.</summary>
        private static void Failed(Ctl c, float now)
        {
            var leg = c.Leg;
            Stop(c);
            if (leg == Leg.Travel)
            {
                var o = c.Ord;
                if (o == null) return;
                float left = Flat(c.A.T.position - o.Point);
                if (left < o.Best - 10f) { o.Best = left; o.Tries = 0; }      // 10 m nearer than ever before: bumps on a long way don't add up
                o.Tries++;
                int max = Math.Max(1, NpcaiSettings.IdleReturnTries.Value);
                if (o.Tries >= max) { o.State = 3; if (WildWasteland.Core.Log.L2) Log(c, "gets nowhere on its travel after " + o.Tries + " tries, gives up (" + left.ToString("0") + " m left)"); return; }
                Hop(c, now);
                c.WaitUntil = now + 1f;
                if (WildWasteland.Core.Log.L2) Log(c, "gets nowhere on its travel (try " + o.Tries + "/" + max + "), again");
                return;
            }
            if (leg == Leg.Investigate)
            {
                c.InvFails++;
                var g = c.A.Ghost;
                if (c.InvFails >= 3 && g != null)
                {
                    // no good three times: the brain takes this ghost (feelers, back-ups, hops) - the pre-1.6 walk, never worse than before
                    c.SkipGhost = g.Id; c.SkipPos = g.Pos; c.Inv = false;
                    if (WildWasteland.Core.Log.L2) Log(c, "gets nowhere walking to ghost #" + g.Id + " (3 tries), the brain takes over");
                    return;
                }
                Hop(c, now);
                c.WaitUntil = now + 0.5f;
                if (WildWasteland.Core.Log.L2) Log(c, "gets nowhere walking to the ghost (try " + c.InvFails + "/3), again");
                return;
            }
            if (leg == Leg.SearchOut || leg == Leg.SearchBack)
            {
                if (leg == Leg.SearchOut && c.SLast >= 0 && c.SDropped != null && c.SLast < c.SDropped.Length) c.SDropped[c.SLast] = now + 300f;
                if (leg == Leg.SearchBack) { c.SPts.Clear(); Log(c, "bumped on its search round, stands"); }   // can't get back to the start: no more rounds
                c.SAtOrigin = leg == Leg.SearchBack; c.WaitUntil = now + 1f;
                return;
            }
            if (leg == Leg.Home)
            {
                c.Tries++;
                int max = Math.Max(1, NpcaiSettings.IdleReturnTries.Value);
                if (c.Tries >= max) { c.Forgotten = true; _claimed.Remove(c.SpotId); if (WildWasteland.Core.Log.L2) Log(c, "can't get home after " + c.Tries + " tries, forgets it"); return; }
                c.WaitUntil = now + Mathf.Max(0f, NpcaiSettings.IdleRetrySeconds.Value);
                if (WildWasteland.Core.Log.L2) Log(c, "gets nowhere going home (try " + c.Tries + "/" + max + "), waits");
            }
            else
            {
                if (leg == Leg.ToPoint && c.PointIdx >= 0 && c.DroppedUntil != null && c.PointIdx < c.DroppedUntil.Length) c.DroppedUntil[c.PointIdx] = now + 300f;
                Log(c, "bumped on its walk, goes home");
                c.AtHome = false;          // next think: back home (a normal return)
            }
        }

        /// <summary>At a patrol point: looks around (a random turn every 1.5-3 s).</summary>
        private static void LookAround(Ctl c, float now)
        {
            if (!c.PendingBack || now < c.NextLook) return;       // looks around at the patrol point only (at home it just stands, as vanilla)
            c.NextLook = now + UnityEngine.Random.Range(1.5f, 3f);
            c.LookYaw = c.A.T.eulerAngles.y + UnityEngine.Random.Range(-70f, 70f);
            c.Turning = true;
        }

    }
}
