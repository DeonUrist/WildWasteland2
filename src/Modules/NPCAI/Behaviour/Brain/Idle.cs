using System;
using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    // [Idle] EnableIdleBehavior: what camp raiders do when nothing is going on (prototype, 1.3.0). Separate from the fight logic: it only
    // drives an NPC while the senses have it Idle (no target, no ghost, not searching) and nothing is detected - the moment it sees or
    // hears anything it lets go (animation and velocity handed back) and the senses / brain take over as before.
    //
    // Who: raiders spawned by a camp's EnemySpawn_Scrapyard_* spawner (incl. boss and bodyguards; the _Attack raid spawners excluded),
    // Coyotes from EnemySpawn_Coyotes_* with [Idle] Coyotes. Home = the exact spawn point ("spawn (n)" under the spawner's
    // spawn_locations_enemy* sibling) the CreateObject used; after a load (restored NPCs are not re-spawned) the nearest free spawn
    // point of the right faction in the structure it stands in, the floor under it.
    //
    // 1. Return home: idle for [Idle] ReturnDelay (15 s) since it last had a target / ghost / search and > 2.5 m from home -> runs back
    //    (camp map routes when on a baked map, else straight with three body feelers). An attempt fails after 2 s without getting
    //    closer; then 5 s standing, then the next; after 10 failed attempts home is forgotten. Any loaded NPC, any distance.
    // 2. Base walk: standing at home, within [Idle] WalkRadius (200 m) of the camera, on a baked map: 1-4 patrol points (Nav.PatrolPoints:
    //    clearly reachable on a straight line, elbow room, body sweep). Stand 10-25 s, walk to one, stand 4-10 s looking around, walk
    //    back. Every leg is swept again before it starts; a bump drops that point for 5 min. No points = it just stands, as vanilla.
    // 3. Search walk: while the senses have it searching (a spot it went to check, nothing there), it walks short rounds from where the
    //    search began instead of standing: 1-3 points made like the camp walk's (Nav.SearchPoints: the camp map when it stands on one,
    //    otherwise physics only), out to a point and back through the start, the brain's look-around in the pauses. When the search
    //    ends it heads home at once (ReturnDelay counts only after a fight that did not end in a search).
    // Gait: humans have only idle / run / attack clips - "walking" is the run clip at half speed (2.5 m/s); full run when far from home.
    internal static partial class Idle
    {
        /// <summary>What an Idle walk is for: home, a patrol point and back, a search round, a ghost, or a travel order.</summary>
        private enum Leg { None, Home, ToPoint, Back, SearchOut, SearchBack, Investigate, Travel }

        private sealed class Ctl
        {
            public Senses.Agent A; public Rigidbody Rb; public Animator Anim;
            public bool Resolved, Excluded; public float NextResolve;
            public Vector3 Home; public bool HasHome, Forgotten; public int SpotId;
            public float BusyAt, NextThink, WaitUntil, LegStart, ProgressAt, BestLeft;
            public int Tries; public Leg Leg; public Vector3 Goal; public int PointIdx = -1;
            public Vector3 Steer; public float Speed; public bool Moving, AtHome;
            public List<Vector3> Points; public int PointsState;   // 0 not tried, -1 map not baked yet, 1 done
            public float NextPoints; public float[] DroppedUntil; public int PointsTries;
            public float LookYaw, NextLook; public bool Turning, PendingBack; public float LastHop = -10f;
            public bool NoHome, LastWasSearch, Fresh;
            public PlayMakerFSM Movement; public bool MovementLooked; public float NextAnimCheck;
            public int SNext; public bool SLooked;      // (1.4.10) the round: next point to walk, looked around at the end
            public bool InSearch, SAtOrigin; public Vector3 SOrigin; public List<Vector3> SPts; public float[] SDropped; public int SLast = -1;
            public bool Inv; public int InvGhost = -1, InvFails, SkipGhost = -1; public Vector3 SkipPos; public bool InvOnMap; public float BestStraight, LastMapLeft, LastStraightLeft;   // (1.6.0) the walk to a ghost
            // (WW perf) think scale by camera distance (x1 / x2 / x4, as the brain's TickScale), the thinking state the timer belongs to
            // (0 none, 1 ghost walk, 2 search, 3 idle), the feeler fan's last answer and when to ask again, the next run-clip check
            public float Scale = 1f; public byte ThinkKind; public float NextFan, FanWant, FanOff, NextRunCheck;
            public bool Follower, Merchant; public Order Ord;     // (crew AI) driven by a quest follower system (never travels); the travel order of its Travel leg
        }

        // (crew AI) Services.Npc.Travel: another module (Patrols: crews back to their car, foot patrols) sends an NPC to a scene point. Walked
        // as the way home is (camp / cave map to the exit, then straight with the feeler fan; cars are obstacles), paused while the NPC has a
        // target / ghost / search (the brain and the senses as usual) and resumed when it is idle again. State: 1 walking (or paused),
        // 2 within Radius, 3 gave up (got nowhere ReturnTries times in a row) or nobody drives it (no senses agent for 5 s, a quest follower).
        internal sealed class Order { public GameObject Npc; public Vector3 Point; public float Radius, Touched, Best = float.MaxValue; public int State = 1, Tries; }
        private static readonly Dictionary<int, Order> _travel = new Dictionary<int, Order>();
        private static bool _idleWas = true;

        /// <summary>A camp spawn point (an NPC's home) and whether it belongs to a peaceful Coyote town.</summary>
        private sealed class Spot { public Transform T; public bool Coyotes; }

        private static readonly Dictionary<int, Ctl> _ctl = new Dictionary<int, Ctl>();
        /// <summary>INpcApi.SetFollowerDriven (Idle part): a follower driver moves the NPC; any Idle walk stops.</summary>
        internal static void SetFollowerDriven(GameObject npc, bool enabled)
        {
            Ctl c;
            if (npc != null && _ctl.TryGetValue(npc.GetInstanceID(), out c))
            { if (enabled && !c.Excluded) Stop(c, true); c.Excluded = enabled || c.Merchant; c.Follower = enabled; }
        }

        /// <summary>INpcApi.Travel: walks the NPC to a scene point (the order survives combat; null cancels).</summary>
        internal static void Travel(GameObject npc, Vector3? point, float radius)
        {
            if (npc == null) return;
            int id = npc.GetInstanceID();
            Ctl c;
            if (!point.HasValue)
            {
                if (_travel.Remove(id) && _ctl.TryGetValue(id, out c) && c.Leg == Leg.Travel) Stop(c, true);
                return;
            }
            Order o;
            if (!_travel.TryGetValue(id, out o) || !ReferenceEquals(o.Npc, npc)) { o = new Order { Npc = npc }; _travel[id] = o; }
            else if (o.State != 1 || Flat(o.Point - point.Value) > 4f) { o.Tries = 0; o.Best = float.MaxValue; }   // a new goal: tries count again
            o.Point = point.Value; o.Radius = Mathf.Max(0.3f, radius); o.State = 1; o.Touched = Time.time;
            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Idle: " + npc.name + " sent to travel " + Flat(npc.transform.position - o.Point).ToString("0") + " m (arrive within " + o.Radius.ToString("0.0") + " m)");
        }

        /// <summary>INpcApi.TravelState: 0 none, 1 walking (3 when nobody drives it for 5 s), 2 arrived, 3 failed.</summary>
        internal static int TravelState(GameObject npc)
        {
            Order o;
            if (npc == null || !_travel.TryGetValue(npc.GetInstanceID(), out o) || !ReferenceEquals(o.Npc, npc)) return 0;
            if (o.State == 1 && (!Senses.On || Time.time - o.Touched > 5f)) return 3;      // nobody drives it (no senses agent, a quest follower, the senses off)
            return o.State;
        }
        private static readonly Dictionary<int, Spot> _spawnOf = new Dictionary<int, Spot>();        // NPC instance id -> its spawn point
        private static readonly Dictionary<int, List<Spot>> _spots = new Dictionary<int, List<Spot>>();   // structure root id -> spawn points
        private static readonly HashSet<int> _claimed = new HashSet<int>();                          // spawn point ids already someone's home
        private static readonly List<int> _dead = new List<int>();
        private static int _stagger;
        // (WW perf, T2/N3) thinks: every ThinkEvery x Scale s per NPC on its own phase (next += interval, a random phase whenever it (re)enters
        // a thinking state - an alert no longer lines a whole camp up on one frame); at most ceil(thinkers x dt / ThinkEvery) + 1 per frame,
        // the rest a frame later
        private const float ThinkEvery = 0.2f;
        private static int _thinkers, _thinkCap = 1, _thinksNow;
        private static readonly int RunHash = Animator.StringToHash("run");

        internal static bool On { get { return NpcaiSettings.IdleEnabled != null && NpcaiSettings.IdleEnabled.Value && Senses.On; } }

        /// <summary>Scene load: every controller, spawn point, claim and travel order dropped.</summary>
        public static void OnSceneLoaded() { _ctl.Clear(); _spawnOf.Clear(); _spots.Clear(); _claimed.Clear(); _travel.Clear(); }

        /// <summary>(WW) module shutdown: every walk stops (idle clip, velocity zeroed) - the game's own idle again</summary>
        internal static void Shutdown()
        {
            foreach (var kv in _ctl) { var c = kv.Value; if (c.A != null && c.A.Owner != null) { try { Stop(c, true); } catch (Exception) { } } }
            _ctl.Clear(); _claimed.Clear(); _spawnOf.Clear(); _spots.Clear(); _travel.Clear();
        }

        /// <summary>(WW) floating origin: homes, legs, patrol / search points are scene space</summary>
        internal static void Shift(Vector3 d)
        {
            foreach (var kv in _ctl)
            {
                var c = kv.Value;
                c.Home += d; c.Goal += d; c.Steer += d; c.SOrigin += d; c.SkipPos += d;
                if (c.Points != null) for (int i = 0; i < c.Points.Count; i++) c.Points[i] += d;
                if (c.SPts != null) for (int i = 0; i < c.SPts.Count; i++) c.SPts[i] += d;
            }
            foreach (var kv in _travel) kv.Value.Point += d;      // (crew AI) travel goals are scene space too
        }

        /// <summary>Number of Idle controllers.</summary>
        internal static int Count { get { return _ctl.Count; } }
        internal static int Walking { get { int n = 0; foreach (var kv in _ctl) if (kv.Value.Moving) n++; return n; } }

    }
}
