using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    // How ground NPCs move while they have a target ("the brain").
    //
    // Vanilla (asset dump 2026-10-01, every human and ground animal): while the Detection FSM has a detectedObj the Attack FSM sits in its
    // "trigger" state and the Movement FSM runs the NPC forward at 5-6 m/s (SetVelocity local z, every frame). Nothing turns the body toward
    // the target: the two LookAt actions in trigger aim the AttackRaycast children (the melee / burst sensors), the body's yaw comes only from
    // the "Rotate" FSM (random +-180 deg/s for 1-3 s every 1-3 s), the Attack FSM's run/rotate states (random again) and two 1.2 m bumper
    // rays that spin it +-120 deg/s. The "Unstuck" FSM (position check every 0.3 s) answers a blocked NPC with a random spin and a 40 N hop.
    // The NPC faces the target only during the melee swing (SmoothLookAt), the burst (an instant LookAt) and the run to a Hide point. So a
    // vanilla chase is a random walk that happens to reach you; the "headless chicken".
    //
    // Here, per NPC (humans and ground animals: a root object with the Movement/Attack/Detection/Unstuck FSMs and a Rigidbody with gravity;
    // flyers have gravity off and are left alone, as is anything parented under something else, which is how Apocapatrol seats a crew in a
    // car - a bailed-out crew is a fresh root object and gets a brain at once):
    // - the body turns at [Brain] TurnRate deg/s (never snaps), toward the steered heading while moving, toward the target while holding;
    // - feelers: FeelerCount sweeps over +-FeelerAngle around the direction to the target (melee: body-wide capsule sweeps, side commitment and
    //   wall following, see Steer; gunmen: plain rays unless ShooterPathing), FeelerLength / MeleeFeelerLength m; a blocked heading from a
    //   recent stuck is avoided for StuckMemorySeconds;
    // - a gunman (Tracers knows the gun) stops where it can shoot - line of sight and within [NpcAim] EngagePercent of the gun's reach - and
    //   holds there with the gun up (AimPose: the shooting animation frozen on its first frame), kneeling with CrouchChance % (no crouch
    //   animation exists: legs and hips re-posed by code in LateUpdate, capsule shortened), facing the target; the game's own burst logic
    //   and the Aim pacing do the shooting; every HoldRecheck it
    //   rolls AdvanceChance % to run at the target for AdvanceSeconds; it moves again when the line of sight is lost or the target walks out
    //   of the engage distance. Melee NPCs run at the target with the feelers.
    // - stuck (the Unstuck FSM's detector is kept, its spin + hop are not): a gunman with a line of sight and the target in reach holds and
    //   shoots from there; otherwise it backs up for StuckBackupSeconds, remembers the heading as blocked and leaves on the clearer side;
    //   StuckGiveUpCount stucks within 10 s -> it stands for a second (facing the target), then tries again. Every wait x [Brain] ReactionTime %.
    // - the random turns and bumper rays are switched off while a brain is in charge; Enabled = false gives the vanilla behaviour back.
    // Cost: one Think per NPC every 0.1 s (<= 5 brains) .. 0.5 s (> 20), staggered; a moving NPC casts FeelerCount (+1 drop check) rays
    // per Think, a holding gunman one line-of-sight ray; the per-frame work is one RotateTowards per NPC. NPCs beyond MaxDistance run vanilla.
    //
    // Hooks (Harmony prefixes; every one returns at once unless the FSM belongs to an NPC with an active brain):
    //   SetVelocity.DoSetVelocity (Movement FSM) - the pedal: forward, zero or backing;      Rotate.DoRotate (Rotate + Attack FSMs) - no random yaw;
    //   Raycast.DoRaycast (Attack, the 1.2 m bumper rays) - report clear;                 LookAt.DoLookAt (Attack, owner) - our turn rate instead;
    //   SendEvent.OnEnter (Attack "Animal_Run" while holding -> "Animal_Idle"; Unstuck "Animal_rotateRandom" -> our stuck handling);
    //   AddForce.DoAddForce (Unstuck) - no hop.
    internal static partial class Brain
    {
        /// <summary>What the brain does with an engaged NPC: off (vanilla), chase, hold a shooting position, advance, back up, rest, look around at a ghost, stand aside while Idle walks it, or take cover.</summary>
        internal enum Mode { Off, Chase, Hold, Advance, BackUp, Rest, Search, Walk, Cover }   // Walk (1.6.0): Idle walks it to a ghost, the brain stands aside; Cover (1.1.0): runs to cover, fights from there

        internal sealed class Npc
        {
            public GameObject Owner; public Transform T; public Rigidbody Rb; public Collider Col;
            public bool FollowerDriven;
            public float FollowerReloadUntil;
            public PlayMakerFSM Attack, Movement;
            public FsmGameObject Target;              // Detection.detectedObj
            public bool Ranged; public WeaponRanges.Kind Kind; public PlayMakerFSM MovementFsm; public float MadeAt, NextRangedCheck;
            public Animator Anim; public string AimState; public bool Frozen;   // the shooting animation held on its first frame = aiming
            public object Body; public float RigTried; public bool Strafing, RigQueued;     // (1.2.0) this gunman's Apocaplayer ModAPI character (ApBody): the player's clips and logic
            public int ReloadRounds; public bool ReloadShown, Cocks, WasBurst;     // (1.2.0) one-round-at-a-time reloads, the reload shown on the body, pump / bolt guns, the last frame's burst
            public Transform Weapon; public string WeaponKey;                       // the gun model in its hand and the pose table's key for it
            public int Shots, Magazine; public bool Reloading; public float ReloadUntil, ReloadLen; public int ReloadSoundsPlayed;   // (1.1.2) the magazine: shots since the last reload, its size (0 = never reloads)   // refs-ok: field name, not the Gunplay type
            public bool WeaponDirty; public float SpeedCap;                         // the game toggled something of this NPC (its gun models) -> re-find the gun; max ground speed for the clip that plays (no sliding)
            public float StrafeDev;                                                  // (1.1.1) chasing with clips: the heading's angle off the facing when the body keeps facing the target and the legs strafe (0 = turn as usual)
            public bool Crouched; public float Drop;
            public CapsuleCollider Capsule; public float CapHeight; public Vector3 CapCenter;
            public Mode Mode; public float ModeUntil;
            public float NextTick, NextRecheck, Stagger;
            public float Heading; public bool HasHeading;   // steered world yaw, degrees
            public float BlockedYaw, BlockedUntil;
            public int Side, ClearLooks; public float SideUntil; public bool Flipped;     // pathing: the committed way around an obstacle
            public float FreeLeft, FreeRight;                                             // the last fan's free lengths per half
            public Vector3 Waypoint; public bool HasWaypoint; public float WaypointUntil, NextScout;   // a scouted corner with a clear line to the target
            public string NavReason = "";
            public bool ToGhost, OnNav, WasOnNav; public float LookYaw, NextLookTurn, NavOffUntil;                       // Senses: going to a ghost / looking around at it
            public float BestDist = float.MaxValue, NoProgressSince;
            public int Stucks; public float FirstStuck, LastHop = -10f;
            public int NavStucks; public float NavStuckSince, LastStep = -10f;
            public float CmdSpeed, BlkSince, BlkDriven, BlkLastThink; public Vector3 BlkPos;     // blocked check: driven vs covered distance    // map-route walking: stucks in a row, the last step-over
            public float LosLostAt = -1f; public bool Los; public float Dist;
            public int LosLooks;                       // consecutive thinks with line of sight (Hold needs 2: no flapping on a fence bar)
            public float CrouchRolledAt = -10f; public bool CrouchRoll;   // the kneel roll of this hold episode (kept 5 s across short Chase gaps)
            public float FaceTargetUntil;              // Aim wants a burst: face the target instead of the steered heading for a moment
            public float TickScale = 1f;               // thinks less often far from the camera
            public float SpeedScale = 1f;              // < 1: queues behind a friend going the same way
            public int AvoidSide; public float AvoidUntil;            // passing a friend: the side, kept a moment (no left-right dither)
            public int FriendBumps; public float FriendBumpsSince;
            public Vector3 MakeWayDir; public float MakeWayUntil, MakeWaySpeed = 1.8f; public bool MakeWayLeft;    // standing in a friend's way: a short step aside; hit: a quick sidestep
            public readonly List<Vector3> Trail = new List<Vector3>(); public readonly List<byte> TrailKind = new List<byte>();   // [Debug] NavTrace
            public float NextTrail, NextTraceLog, NextTraceDump; public Vector3 TraceGoal; public string LegBlock = "";
            // (1.1.0) cover: the spot, when the stay ends, arrived there, started by the game's own "hide" (a fast target) or by low health
            public Vector3 CoverPos; public float CoverUntil, CoverSince, NextCoverCheck, CoverCooldownUntil; public bool CoverArrived, CoverFromHide;
            public GameObject DogCoverThreat; public float NextDogCoverCheck, DogCoverRetry;
            public float FrightUntil, NextFrightCover, SawFrightUntil; public bool BurningFear; public Vector3 FrightFrom;
            public FsmGameObject HideVar;            // the Attack FSM's "Hide" variable (its FindClosest result)
            public GameObject VehicleHide; public float NextVehicleHideCheck;
            public FsmFloat HpVar; public float HpMax;   // the Health FSM's Health, and the most it was seen at
            public bool Travel; public float RunZ;
            public float Boost = 1f, BoostUntil;
            public bool WasEngaged;                        // (perf) engaged last frame: a fresh engagement gets a random think phase
            public WeaponRanges.HandCache Hands;           // (perf) hand bones / gun candidates, scanned once (WeaponOf)
            public bool GunPoses; public float GunPosesAt = -10f; public string GunPosesKey;   // (perf) ApBody.HasPoses(WeaponKey), re-read on a new key or after 1 s
            public Fsm MultyFsm; public FsmInt MultyVar;   // (perf) the Damage Ranged FSM's "multyAttack" counter
            public bool Halted;                            // (1.8.0) DogBite: in reach of its target - stands (and DogBite turns it)          // (1.7.0) a nest alarm: runs this many times faster until then       // (1.4.0) a hunter under the ground (Burrow): paused; the fastest pedal the game gave it (its run speed)
        }

        private static readonly Dictionary<int, Npc> _npcs = new Dictionary<int, Npc>();
        /// <summary>INpcApi.SetFollowerDriven: a follower driver (Quests hire) moves this NPC instead of the brain (the brain lets go and keeps its magazine count only while driven).</summary>
        internal static void SetFollowerDriven(GameObject npc, bool enabled, float reloadUntil)
        {
            Npc n;
            if (npc == null || !_npcs.TryGetValue(npc.GetInstanceID(), out n)) return;
            if (enabled && !n.FollowerDriven && n.Mode != Mode.Off) SetMode(n, Mode.Off, "quest follower");
            n.FollowerDriven = enabled;
            n.FollowerReloadUntil = reloadUntil;
            if (!enabled) { n.Shots = 0; n.Reloading = false; n.ReloadShown = false; }
        }
        private static readonly HashSet<int> _ignored = new HashSet<int>();
        private static readonly List<int> _dead = new List<int>();
        private static float _nextCount, _interval = 0.1f;
        private static int _active, _created, _engagedFrame;
        // layers (TagManager): 0 Default, 6 Player, 8 Car, 9 Item, 10 Actor, 11 Door, 14 Ground, 16 SeeTrough, 18 PhysicsLock
        private static readonly int GroundMask = (1 << 0) | (1 << 8) | (1 << 11) | (1 << 14) | (1 << 16);   // + Ground: for the drop check
        private static readonly int LosMask = GroundMask;          // line of sight for the shooting decision: a hill crest hides the target too
        // pathing feelers: everything solid except creatures, the player, weapons and the non-world layers; Ground counts only where it is
        // steep (a rock, a prop placed on that layer), gentle terrain ahead is not an obstacle
        private static readonly int PathMask = ~((1 << 1) | (1 << 2) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 7) | (1 << 10) | (1 << 12) | (1 << 13) | (1 << 15) | (1 << 17) | (1 << 19) | (1 << 22));
        private static float[] _angles = new float[0];
        private static readonly float[] _scores = new float[32];
        private static readonly float[] _blocks = new float[32];

        /// <summary>Scene load: rigs and animated bodies released, every brain, pending spawn and reload-sound cache dropped.</summary>
        public static void OnSceneLoaded() { ClearRigs(); foreach (var kv in _npcs) if (kv.Value.Body != null) ApBody.Detach(kv.Value.Body); _npcs.Clear(); _ignored.Clear(); _arrive.Clear(); _active = 0; ReloadSounds.Clear(); _pending.Clear(); _pendingAt.Clear(); ApBody.Reset(); }   // refs-ok: REFACTOR-REQUEST B07-13 (Presentation ApBody/ReloadSounds: NPC animation framework hooks missing from ICharacterPresentation)

        /// <summary>(WW) module shutdown: every brain lets go (the game's pedal, facing, Unstuck and animation again), kneeling gunmen stand up, frozen aim poses play on, bodies animated by the player animation framework are handed back</summary>
        internal static void Shutdown()
        {
            ClearRigs();
            foreach (var kv in _npcs)
            {
                var n = kv.Value;
                if (n.Owner == null) { if (n.Body != null) { try { ApBody.Detach(n.Body); } catch (Exception) { } n.Body = null; } continue; }   // refs-ok: REFACTOR-REQUEST B07-13 (Presentation ApBody/ReloadSounds: NPC animation framework hooks missing from ICharacterPresentation)
                try { if (n.Mode != Mode.Off) SetMode(n, Mode.Off, "module off"); } catch (Exception e) { NpcaiRuntime.Warn("Brain shutdown of " + n.Owner.name + ": " + e.Message); }
                try { Unfreeze(n); Crouch(n, false); if (n.Anim != null && n.Anim.speed == 0f) n.Anim.speed = 1f; } catch (Exception) { }
                n.Halted = false; n.Travel = false; n.BoostUntil = 0f;
                if (n.Body != null) { try { ApBody.Detach(n.Body); } catch (Exception) { } n.Body = null; }   // refs-ok: REFACTOR-REQUEST B07-13 (Presentation ApBody/ReloadSounds: NPC animation framework hooks missing from ICharacterPresentation)
            }
            _npcs.Clear(); _pending.Clear(); _pendingAt.Clear(); _ignored.Clear();
        }

        /// <summary>(WW) floating origin: the scene-space points a brain keeps across thinks</summary>
        internal static void Shift(Vector3 d)
        {
            foreach (var kv in _npcs)
            {
                var n = kv.Value;
                n.Waypoint += d; n.BlkPos += d; n.CoverPos += d; n.TraceGoal += d; n.FrightFrom += d;
                for (int i = 0; i < n.Trail.Count; i++) n.Trail[i] += d;
            }
        }

        /// <summary>Number of NPCs with a brain.</summary>
        internal static int Count { get { return _npcs.Count; } }
        /// <summary>Number of brains engaged with a target this second.</summary>
        internal static int EngagedCount { get { return _active; } }
        /// <summary>Current think interval, s (scales with engaged NPCs when [Brain] ScaleWithActors).</summary>
        internal static float Interval { get { return _interval; } }

        /// <summary>The brain runs: module on and [NPCAI] EnableNpcPathfinding on.</summary>
        internal static bool On { get { return NpcaiRuntime.Running && NpcaiSettings.BrainEnabled.Value; } }
        /// <summary>[Brain] ReactionTime %: every wait of the brain (think interval, back-up, rest, side lock, no-progress, memory, LOS tolerance, hold rechecks) is multiplied by this; 100 = the defaults, 50 = twice as quick, 500 = five times slower</summary>
        internal static float R { get { return Mathf.Clamp(NpcaiSettings.ReactionTime.Value, 1f, 500f) / 100f; } }

        // ---------- per frame ----------
        /// <summary>Job "NPCAI.Brain" (every frame): registration of spawned gunmen, the engaged count and think interval, capped per-NPC thinks and the per-frame body turn toward the heading / target.</summary>
        public static void Tick()
        {
            if (_npcs.Count == 0) return;
            float now = Time.time, dt = Time.deltaTime;
            if (now >= _nextCount)
            {
                _nextCount = now + 1f;
                _dead.Clear();
                int n = 0;
                var cam = Core.GameRefs.MainCamera; Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
                foreach (var kv in _npcs)
                {
                    var np = kv.Value;
                    if (np.Owner == null) { if (np.Body != null) ApBody.Detach(np.Body); _dead.Add(kv.Key); }   // refs-ok: REFACTOR-REQUEST B07-13 (Presentation ApBody/ReloadSounds: NPC animation framework hooks missing from ICharacterPresentation)
                    else if (np.Mode != Mode.Off) n++;
                    if (np.Owner != null && cam != null)
                    {
                        // out of the player's sight the finer steering is wasted: think half / a quarter as often
                        float cd = (np.T.position - camPos).sqrMagnitude;
                        np.TickScale = cd > 80f * 80f ? 4f : cd > 40f * 40f ? 2f : 1f;
                    }
                }
                foreach (var k in _dead) _npcs.Remove(k);
                _active = n;
                _interval = (NpcaiSettings.ScaleWithActors.Value ? (n <= 5 ? 0.1f : n <= 10 ? 0.2f : n <= 20 ? 0.3f : 0.5f) : 0.1f) * R;
            }
            if (ApBody.Available) RegisterPending();        // spawned gunmen get their rig before they ever engage   // refs-ok: REFACTOR-REQUEST B07-13 (Presentation ApBody/ReloadSounds: NPC animation framework hooks missing from ICharacterPresentation)
            bool on = On;
            float turn = Mover.TurnStep(dt);
            // (perf) per-frame think cap: the engaged NPCs' average thinks per frame + 1; one over the cap keeps its overdue NextTick and
            // thinks next frame (an alert no longer runs every overdue think in one frame)
            int engagedPrev = _engagedFrame; _engagedFrame = 0;
            int thinks = 0, thinkCap = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(_active, engagedPrev) * dt / Mathf.Max(0.01f, _interval)) + 1);
            foreach (var kv in _npcs)
            {
                var n = kv.Value;
                if (n.Owner == null) continue;
                if (n.FollowerDriven) continue;
                // #63: a seated simple convoy bandit is skipped here, before Drive() could suspend its body; one registered in the moment it was
                // spawned (before Patrols seated it) kept the player animation framework on, which then animated the car's motion as a run
                if(SimpleBandits.Has(n.Owner)&&!SimpleBandits.Fighting(n.Owner)){if(n.Body!=null&&!ApBody.Suspended(n.Body))ApBody.Suspend(n.Body,true);continue;}   // refs-ok: REFACTOR-REQUEST B07-13 (Presentation ApBody/ReloadSounds: NPC animation framework hooks missing from ICharacterPresentation)
                if (!on)
                {
                    if (n.Mode != Mode.Off) SetMode(n, Mode.Off, "brain off");
                    continue;
                }
                if (n.Travel) continue;                                       // (1.4.0) diving / travelling underground (Burrow): nothing to steer
                if (n.Crouched && n.T.parent != null) Crouch(n, false);     // seated by Apocapatrol after all: stand up
                if (now < n.MakeWayUntil && n.Rb != null && n.T.parent == null && n.Mode != Mode.Off)   // a friend needs past: a short step aside
                {
                    Vector3 v = n.MakeWayDir * (n.Body != null && n.SpeedCap > 0f ? Mathf.Min(n.MakeWaySpeed, n.SpeedCap) : n.MakeWaySpeed); v.y = n.Rb.velocity.y; n.Rb.velocity = v;
                }
                string state;
                bool engaged = Engaged(n, out state);
                if (now < n.FrightUntil && n.T.parent == null && (n.HpVar == null || n.HpVar.Value >= 1f)) engaged = true;
                if (n.Ranged && n.Body == null && now < n.MadeAt + 60f) TryRig(n);
                if (n.Body != null) Drive(n, state ?? AttackState(n), now, dt);                     // (1.2.0) Apocaplayer's ModAPI: this frame's situation, engaged or not
                if (n.Reloading) ReloadSounds.Tick(n, now);                                           // (1.1.2) the reload's sounds, rig or not   // refs-ok: REFACTOR-REQUEST B07-13 (Presentation ApBody/ReloadSounds: NPC animation framework hooks missing from ICharacterPresentation)
                if (!engaged)
                {
                    n.WasEngaged = false;
                    if (n.Mode != Mode.Off) SetMode(n, Mode.Off, "target lost");
                    continue;
                }
                _engagedFrame++;
                float step = _interval * n.TickScale;
                // (perf) a fresh engagement (or a timer left far behind while skipped) starts at a random phase within one base interval:
                // a camp alerted by one shot no longer thinks in one frame and stays phase-locked
                if (!n.WasEngaged || now - n.NextTick > 2f * step + 0.1f) { n.WasEngaged = true; n.NextTick = now + UnityEngine.Random.value * _interval; }
                if (now >= n.NextTick && thinks < thinkCap)
                {
                    thinks++;
                    n.NextTick += step; if (n.NextTick < now) n.NextTick = now + step;     // keeps its phase
                    try { Think(n, now); }
                    catch (Exception e)
                    {
                        NpcaiRuntime.Error("Brain: " + e);
                        try { SetMode(n, Mode.Off, "error"); } catch (Exception) { n.Mode = Mode.Off; try { Unfreeze(n); Crouch(n, false); } catch (Exception) { } }
                        continue;
                    }
                }
                if (n.Mode == Mode.Off || n.Mode == Mode.BackUp || n.Mode == Mode.Walk) continue;     // Walk: Idle turns the body
                bool cover = n.Mode == Mode.Cover, coverHold = cover && n.CoverArrived;
                if (n.Body == null && n.Frozen && !ChaseState(state) && !(cover && state == "hide")) Unfreeze(n);      // the burst (or a melee swing, a hide run): let the animation play
                bool steering = (n.Mode == Mode.Chase || n.Mode == Mode.Advance || (cover && !coverHold)) && n.HasHeading && (state != "attack_melee" || now < n.FrightUntil) && (state != "hide" || cover);
                if (!ChaseState(state) && state != "attack_ranged" && !steering && !coverHold) continue;   // melee swing, hide run ...: the game's own facing
                // (1.4.6) a moving NPC is turned to its steered heading in every Attack state but the swing / hide: the run velocity is ours in all
                // of them, and an unturned body used to run on along its old facing (an idle raider: toward home, away from the way out)
                var target = n.Target.Value ?? (now < n.FrightUntil ? Core.GameRefs.Player : null);
                if (target == null) continue;
                Vector3 to;
                n.StrafeDev = 0f;
                if (n.Mode == Mode.Search)
                {
                    if (Idle.SearchWalking(n.Owner)) continue;      // walking its search round ([Idle]): the round turns the body
                    if (now >= n.NextLookTurn)
                    {
                        n.NextLookTurn = now + UnityEngine.Random.Range(1f, 1.8f) * R;
                        n.LookYaw = n.T.eulerAngles.y + (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(80f, 160f);
                    }
                    to = Quaternion.Euler(0f, n.LookYaw, 0f) * Vector3.forward;
                }
                else if (n.Mode == Mode.Hold || n.Mode == Mode.Rest || coverHold || state == "attack_ranged" || now < n.FaceTargetUntil)
                    to = target.transform.position - n.T.position;
                else if (n.HasHeading)
                {
                    to = Quaternion.Euler(0f, n.Heading, 0f) * Vector3.forward;
                    // (1.1.1) with strafe clips a small detour (a friend, a post, a dodge: heading within StrafeAngle of the target) is walked
                    // sideways facing the target instead of turning the body; a real turn (a wall, a corner) turns it as before
                    n.StrafeDev = 0f;
                    if (n.Body != null && (n.Mode == Mode.Chase || n.Mode == Mode.Advance) && n.Dist < 30f)
                    {
                        Vector3 tt = target.transform.position - n.T.position; tt.y = 0f;
                        if (tt.sqrMagnitude > 0.01f)
                        {
                            float dev = Mathf.DeltaAngle(Quaternion.LookRotation(tt, Vector3.up).eulerAngles.y, n.Heading);
                            if (Mathf.Abs(dev) <= NpcaiSettings.StrafeAngle.Value) { to = tt; n.StrafeDev = dev; }
                        }
                    }
                }
                else to = target.transform.position - n.T.position;
                to.y = 0f;
                if (to.sqrMagnitude < 0.0001f) continue;
                n.T.rotation = Quaternion.RotateTowards(n.T.rotation, Quaternion.LookRotation(to, Vector3.up), turn);
            }
        }

        /// <summary>the Attack FSM's chase states: "trigger" (every human and most animals), "trigger 2" (Nightwalker; probe 2026-10-06) and the random-yaw "run" the game enters from it. The brain drives the pedal and the facing only in these.</summary>
        private static bool ChaseState(string s) { return s == "trigger" || s == "run" || s == "trigger 2"; }

        /// <summary>the brain is in charge only while the Attack FSM runs with a target, the NPC stands on its own (not seated in a car) and is near the Attack FSM's state when it exists (engaged or not; null when it is not initialised)</summary>
        private static string AttackState(Npc n)
        {
            var fsm = n.Attack != null ? n.Attack.Fsm : null;
            return fsm != null && fsm.Initialized ? fsm.ActiveStateName : null;
        }

        // CreateObject postfix (Senses): a spawned NPC is registered as soon as its FSMs are up, so its gun is in the right hand from the
        // first frames, not only when it first engages
        // (perf/fix) only what Make can accept is queued (a root object with a Rigidbody: CreateObject never parents); an entry leaves
        // 5 s after it was first checked, and a full queue drops its oldest entry (an inactive item used to stay for good and, at 200,
        // kept new NPCs out); a dropped NPC still registers on its first Movement SetVelocity as before
        private static readonly List<GameObject> _pending = new List<GameObject>();
        private static readonly List<float> _pendingAt = new List<float>();
        /// <summary>CreateObject spawn of a possible NPC (root Rigidbody): queued so gunmen get their animation body before they engage.</summary>
        internal static void Spawned(GameObject made)
        {
            if (made == null || made.transform.parent != null || made.GetComponent<Rigidbody>() == null) return;
            if (_pending.Count >= 200) { _pending.RemoveAt(0); _pendingAt.RemoveAt(0); }
            _pending.Add(made); _pendingAt.Add(-1f);
        }
        /// <summary>Registers the queued spawns whose FSMs are ready (gunmen get their animation body early); drops stale entries.</summary>
        private static void RegisterPending()
        {
            if (_pending.Count == 0) return;
            float now = Time.time;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var go = _pending[i];
                if (_pendingAt[i] < 0f) _pendingAt[i] = now;      // the clock starts when the queue is first worked (the animation framework may load later)
                if (go == null || now - _pendingAt[i] > 5f) { _pending.RemoveAt(i); _pendingAt.RemoveAt(i); continue; }
                if (!go.activeInHierarchy) continue;
                var n = Of(go);
                if (n != null || !_retry) { _pending.RemoveAt(i); _pendingAt.RemoveAt(i); }       // registered, or not an NPC of ours
            }
        }

        /// <summary>True when the NPC fights: Attack FSM on with a detected target; returns the Attack state name.</summary>
        private static bool Engaged(Npc n, out string state)
        {
            state = null;
            if (n.T.parent != null || n.Attack == null || !n.Attack.enabled || n.Target == null) return false;
            var fsm = n.Attack.Fsm;
            if (fsm == null || !fsm.Initialized || fsm.Finished) return false;
            if (n.Target.Value == null) return false;
            state = fsm.ActiveStateName;
            return true;
        }

        /// <summary>One think of an engaged NPC: fright and simple-bandit orders first, then target distance, line of sight, cover, hold / chase / advance / back-up / rest decisions and the steering toward the target or along a map route.</summary>
        private static void Think(Npc n, float now)
        {
            if (FrightThink(n, now)) return;
            if(SimpleBandits.Has(n.Owner)&&!Senses.IsGhostTarget(n.Owner)){SimpleThink(n,now);return;}
            var target = n.Target.Value;
            if (target == null) { if (n.Mode != Mode.Off) SetMode(n, Mode.Off, "no target"); return; }
            Vector3 tp = target.transform.position;
            Vector3 d3 = tp - n.T.position; d3.y = 0f;
            float d = d3.magnitude;
            n.Dist = d;
            int kind = Senses.KindOf(n.Owner);           // 0 vanilla / seen target, 1 seen target, 2 going to a ghost, 3 searching at it
            string ast = n.Attack.Fsm.ActiveStateName;
            if (n.Mode != Mode.Cover) CompanionCoverCheck(n, now);
            // (1.1.0) in cover: stays there (a wounded raider does not come out after a ghost), see CoverThink
            if (n.Mode == Mode.Cover) { CoverThink(n, now, ast, kind, tp); return; }
            // (1.6.0) a ghost to walk to: Idle walks the raider there the way it walks home (map to the exit, then straight); the brain only
            // stands aside (no pedal, no turning, no stuck handling) - at any distance. Idle hands it back after 3 failed tries.
            if (kind == 2 && Idle.WalksToGhost(n.Owner))
            {
                n.ToGhost = true;
                if (n.Mode != Mode.Walk) SetMode(n, Mode.Walk, "Idle walks it to the ghost");
                return;
            }
            if (d > NpcaiSettings.MaxDistance.Value) { if (n.Mode != Mode.Off) SetMode(n, Mode.Off, "far"); return; }
            if (!n.Ranged && now < n.MadeAt + 10f && now >= n.NextRangedCheck)     // a gun that was not in the hand at the spawn frame
            {
                n.NextRangedCheck = now + 1f;
                WeaponRanges.Kind gk;
                if (WeaponRanges.GunKindOf(n.Owner, out gk)) { n.Ranged = true; n.Kind = gk; SetupRanged(n); }
            }
            if (kind == 3) { if (n.Mode != Mode.Search) { n.NextLookTurn = 0f; SetMode(n, Mode.Search, "looks around"); } return; }
            n.ToGhost = kind == 2;
            if (n.Mode == Mode.Off || n.Mode == Mode.Search || n.Mode == Mode.Walk) SetMode(n, Mode.Chase, !WildWasteland.Core.Log.L2 ? "" : (n.ToGhost ? "ghost" : "target") + " at " + d.ToString("0") + " m");
            // (1.1.0) cover: the game's own hide (a target faster than 6 m/s: FindClosest tag Hide, then it only turned toward it and ran
            // blind) and low health - both walked there with the map and the feelers, then held facing the target
            if (!n.ToGhost && n.Mode != Mode.BackUp && n.Mode != Mode.Rest) CoverCheck(n, now, ast, kind, tp);
            if (n.Mode == Mode.Cover) { CoverThink(n, now, ast, kind, tp); return; }

            if (Tracing && now >= n.NextTrail && n.Trail.Count < 1200)
            {
                n.NextTrail = now + 0.5f; n.TraceGoal = tp;
                n.Trail.Add(n.T.position); n.TrailKind.Add((byte)(n.Mode == Mode.BackUp || n.Mode == Mode.Rest ? 2 : n.OnNav ? 0 : 1));
            }
            if (n.Mode == Mode.BackUp) { if (now < n.ModeUntil) return; SetMode(n, Mode.Chase, "backed up"); }
            if (n.Mode == Mode.Rest) { if (now < n.ModeUntil) return; SetMode(n, Mode.Chase, "rested"); }

            if (n.ToGhost)
            {
                // a ghost is a place, not a target: go there (melee style, around things), then look around
                if (d <= ArriveFor(n.Owner)) { Senses.ArrivedAt(n.Owner); n.NextLookTurn = 0f; SetMode(n, Mode.Search, "at the ghost"); return; }
                if (n.Mode == Mode.Hold || n.Mode == Mode.Advance) SetMode(n, Mode.Chase, "ghost");
            }
            else if (n.Ranged)
            {
                bool los = LineOfSight(n, target, tp);
                if (los) { n.Los = true; n.LosLostAt = -1f; n.LosLooks++; }
                else { if (n.Los) { n.Los = false; n.LosLostAt = now; } n.LosLooks = 0; }
                float reach = WeaponRanges.RangeOf(n.Kind);
                float engage = reach * Mathf.Clamp(NpcaiSettings.EngagePercent.Value, 1f, 100f) / 100f;
                bool good = los && d <= engage;
                if (n.Mode == Mode.Hold)
                {
                    bool lost = (!los && now - n.LosLostAt > 0.3f * R) || d > engage * 1.1f;
                    if (lost) { SetMode(n, Mode.Chase, !WildWasteland.Core.Log.L2 ? "" : los ? "target at " + d.ToString("0") + " m" : "no line of sight"); }
                    else if (now >= n.NextRecheck)
                    {
                        n.NextRecheck = now + UnityEngine.Random.Range(global::WildWasteland.Modules.NPC.Aim.HoldRecheckMin, global::WildWasteland.Modules.NPC.Aim.HoldRecheckMax) * R;   // refs-ok: own type NPC.Aim (the Brain.Aim method hides the simple name)
                        // advances more when the target has not fired for 8 s (reloading, hiding), never when hurt (below CoverBelow)
                        float adv = NpcaiSettings.AdvanceChance.Value;
                        if (Time.time - Senses.LastPlayerShotAt > 8f) adv *= 2.5f;
                        if (n.HpVar != null && n.HpMax > 0f && NpcaiSettings.CoverBelow.Value > 0f && n.HpVar.Value < n.HpMax * NpcaiSettings.CoverBelow.Value / 100f) adv = 0f;
                        if (UnityEngine.Random.Range(0f, 100f) < adv && d > 3f)
                        {
                            n.ModeUntil = now + UnityEngine.Random.Range(NpcaiSettings.AdvanceMin.Value, Mathf.Max(NpcaiSettings.AdvanceMin.Value, NpcaiSettings.AdvanceMax.Value));
                            SetMode(n, Mode.Advance, "feels like it");
                        }
                        else return;
                    }
                    else return;
                }
                if (n.Mode == Mode.Advance)
                {
                    if (now >= n.ModeUntil || !los || d < 3f)
                    {
                        if (good) { SetMode(n, Mode.Hold, !WildWasteland.Core.Log.L2 ? "" : "at " + d.ToString("0") + " m" + (now >= n.ModeUntil ? "" : ", enough")); return; }
                        SetMode(n, Mode.Chase, !WildWasteland.Core.Log.L2 ? "" : los ? "target at " + d.ToString("0") + " m" : "no line of sight");
                    }
                }
                else if (n.Mode == Mode.Chase && good && n.LosLooks >= 2)        // two thinks in a row: a fence bar flickering across the line doesn't stop and start the hold
                {
                    n.NextRecheck = now + UnityEngine.Random.Range(global::WildWasteland.Modules.NPC.Aim.HoldRecheckMin, global::WildWasteland.Modules.NPC.Aim.HoldRecheckMax) * R;   // refs-ok: own type NPC.Aim (the Brain.Aim method hides the simple name)
                    SetMode(n, Mode.Hold, !WildWasteland.Core.Log.L2 ? "" : "line of sight at " + d.ToString("0") + " m (engages within " + engage.ToString("0") + ")");
                    return;
                }
            }
            MoveToward(n, tp, d3, d, now);
        }

        /// <summary>moving (Chase, Advance, Cover on the way): the map's waypoint or the feelers toward tp, d3 = flat vector to it, d its length</summary>
        private static void MoveToward(Npc n, Vector3 tp, Vector3 d3, float d, float now)
        {
            if (Blocked(n, now)) return;
            // inside a baked camp / building / cave the structure's own map says the way (out through the right exit, around its walls)
            Vector3 navNext = Vector3.zero; float pathLeft = 0f;
            bool hopLeg = false;
            bool asked = now >= n.NavOffUntil;
            n.OnNav = asked && Nav.Next(n.Owner, n.T.position, tp, out navNext, out pathLeft);
            string trace = null; bool vetoed = false;
            if (Tracing && now >= n.NextTraceLog) trace = asked ? Nav.TraceInfo() : "";
            if (n.OnNav)
            {
                hopLeg = Nav.HopAhead(n.T.position, navNext);         // the route climbs a low lip / kerb (a hop edge) right ahead
                // (WW) a POI route already went round everything solid: no leg gate (it used to drop the route at the ladder / a prop corner)
                if (!PoiNav.Guiding(n.Owner) && !NavLegClear(n, navNext, hopLeg)) { n.OnNav = false; vetoed = true; }
                else if (hopLeg) TryHop(n, now, true);
            }
            if (trace != null)
            {
                n.NextTraceLog = now + 1f;
                Vector3 vel = n.Rb != null ? n.Rb.velocity : Vector3.zero; vel.y = 0f;
                Vector3 fp = n.Col != null ? n.Col.bounds.min : n.T.position;
                NpcaiRuntime.Deep("Trace: " + n.Owner.name + " " + n.Mode + " at " + n.T.position.x.ToString("0.0") + "," + n.T.position.z.ToString("0.0") + " feet y " + fp.y.ToString("0.0")
                    + " speed " + vel.magnitude.ToString("0.0") + " goal " + d.ToString("0") + " m | "
                    + (!asked ? "MAP OFF " + (n.NavOffUntil - now).ToString("0") + " s more"
                       : n.OnNav ? "MAP " + trace + (hopLeg ? " HOP" : "") + " wp " + Vector3.Distance(new Vector3(navNext.x, 0f, navNext.z), new Vector3(n.T.position.x, 0f, n.T.position.z)).ToString("0.0") + " m away"
                       : vetoed ? "MAP VETOED (" + n.LegBlock + ") " + trace
                       : "NO MAP: " + (Nav.LastReason.Length > 0 ? Nav.LastReason : "outside every map") + " | " + trace)
                    + (n.HasWaypoint ? " | corner " + Vector3.Distance(n.Waypoint, n.T.position).ToString("0.0") + " m" : "") + (n.Side != 0 ? " | side " + n.Side : ""));
            }
            if (WildWasteland.Core.Log.L2 && !n.OnNav && Nav.LastReason != n.NavReason)
            {
                n.NavReason = Nav.LastReason;
                if (n.NavReason.Length > 0) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " no map route: " + n.NavReason + Nav.Probe(n.T.position));
            }
            if (n.OnNav != n.WasOnNav) { n.WasOnNav = n.OnNav; n.BestDist = float.MaxValue; n.NoProgressSince = now; }   // map path length and straight distance don't compare
            if (n.OnNav) NavProgress(n, pathLeft, now);         // may switch the map off for a moment (full feelers then)
            else if (!Progress(n, d, now)) return;
            Vector3 goal = d3; float gd = d;
            if (n.OnNav)
            {
                goal = navNext - n.T.position; goal.y = 0f; gd = Mathf.Max(0.5f, goal.magnitude);
                n.HasWaypoint = false;
            }
            else if (n.HasWaypoint)
            {
                Vector3 w = n.Waypoint - n.T.position; w.y = 0f;
                float wd = w.magnitude;
                if (wd < 1.2f || now > n.WaypointUntil) { n.HasWaypoint = false; if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + (wd < 1.2f ? " reached the corner" : " gives up the corner")); }
                else if (DirectClear(n, tp, d)) { n.HasWaypoint = false; if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " has a clear line, drops the corner"); }
                else { goal = w; gd = wd; }
            }
            if (n.OnNav) NavSteer(n, goal, now);                // on a map route: straight at the waypoint, short front feelers, steps over low stuff
            else Steer(n, goal, gd, now, d3);
            if (Tracing && n.HasHeading && now + 1f - n.NextTraceLog < 0.05f)      // right after a trace line: where the feelers actually point
            {
                float want = Mathf.Atan2(goal.x, goal.z) * Mathf.Rad2Deg;
                float off = Mathf.DeltaAngle(want, n.Heading);
                if (Mathf.Abs(off) > 30f) NpcaiRuntime.Deep("Trace: " + n.Owner.name + " feelers turn " + off.ToString("0") + " deg off the " + (n.OnNav ? "map waypoint" : n.HasWaypoint ? "corner" : "straight line"));
            }
        }

    }
}
