using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (split from PlayerBody/PlayerBodyModule.cs in Wave A): ledge climbing and ladders (player controllers and
    /// the NPC traversal poses). Target: Presentation traversal (B06).
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        // ------------------------------------------------------------------ F6 (main cfg)
        internal static ConfigEntry<bool> ClimbingOn, JumpTriggersClimbing, Ladders;
        internal static ConfigEntry<Key> ClimbKey;
        /// <summary>The alternative climb key as a legacy KeyCode.</summary>
        internal static KeyCode ClimbKeyCode { get { return KeyMap.Code(ClimbKey.Value); } }

        // ------------------------------------------------------------------ F7 (tools.cfg)
        internal static ConfigEntry<float> ClimbMinHeight, ClimbMaxHeight, ClimbReach, ClimbWaistLimit, ClimbSpeed, LadderSpeed, LadderSlideSpeed, LadderSlidePose, LadderOffset, LadderEyeForward, LadderNeckBack;

        // ================================================================== config
        /// <summary>Binds [PlayerBody / Climbing] Climbing, JumpTriggersClimbing and Ladders.</summary>
        partial void BindTraversal(ModuleConfig cfg)
        {
            ClimbingOn = cfg.Player("Climbing", "Climbing", true, "Climb onto reachable solid ledges on foot.");
            JumpTriggersClimbing = cfg.Player("Climbing", "JumpTriggersClimbing", true, "The game's Jump key climbs onto a reachable ledge or catches one within arm's reach during a jump. Otherwise it jumps normally.");
            Ladders = cfg.Player("Climbing", "Ladders", true, "Ladders: weapon put away, centred on the ladder with a climbing animation (W / S up and down, Jump lets go). Off = the game's own ladder.");
        }

        /// <summary>Binds [Keys] "Climb (alternative key)".</summary>
        partial void BindTraversalKeys(ModuleConfig cfg)
        {
            ClimbKey = cfg.Hotkey("Climb (alternative key)", Key.None, "Optional separate climb key (the game's Jump key also climbs with JumpTriggersClimbing). None = no key.");
        }

        /// <summary>Binds [PlayerBody / Climbing] (tools.cfg): ledge limits and ladder tuning.</summary>
        partial void BindTraversalTools(ModuleConfig cfg)
        {
            const string Clm = "Climbing";
            ClimbMinHeight = cfg.Tool(Clm, "MinimumHeight", .45f, "Climbing: minimum ledge height above the feet, metres.", new AcceptableValueRange<float>(.36f, 1f));
            ClimbMaxHeight = cfg.Tool(Clm, "MaximumHeight", 2.5f, "Climbing: maximum ledge height above the feet, metres.", new AcceptableValueRange<float>(1f, 2.6f));
            ClimbReach = cfg.Tool(Clm, "Reach", .85f, "Climbing: maximum distance to the obstacle face, metres.", new AcceptableValueRange<float>(.35f, 1.2f));
            ClimbWaistLimit = cfg.Tool(Clm, "WaistHeightLimit", 1.25f, "Climbing: higher ledges use the high-climb animation.", new AcceptableValueRange<float>(.8f, 1.5f));
            ClimbSpeed = cfg.Tool(Clm, "AnimationSpeed", 1.5f, "Climbing: climb playback speed.", new AcceptableValueRange<float>(.6f, 2.5f));
            LadderSpeed = cfg.Tool(Clm, "LadderSpeed", 2.2f, "Ladders: climbing speed up, metres per second (the animation follows the rungs at any speed; the game's own ladder: 2).", new AcceptableValueRange<float>(.3f, 4f));
            LadderSlideSpeed = cfg.Tool(Clm, "LadderSlideSpeed", 6f, "Ladders: going down is a slide (hands and feet on the rails); its top speed, metres per second.", new AcceptableValueRange<float>(1f, 12f));
            LadderSlidePose = cfg.Tool(Clm, "LadderSlidePose", 0f, "Ladders: the climbing animation frame held while sliding down (0..1 of one cycle).", new AcceptableValueRange<float>(0f, 1f));
            LadderEyeForward = cfg.Tool(Clm, "LadderEyeForward", .16f, "Ladders: first-person eye forward of the head towards the ladder, metres (so the camera does not see its own neck).", new AcceptableValueRange<float>(0f, .4f));
            LadderNeckBack = cfg.Tool(Clm, "LadderNeckBack", 12f, "Ladders: the neck tilted back while climbing, degrees.", new AcceptableValueRange<float>(-30f, 45f));
            LadderOffset = cfg.Tool(Clm, "LadderOffset", 0f, "Ladders: moves the body away from (+) or towards (-) the ladder, metres; 0 = the balls of the feet over the rungs.", new AcceptableValueRange<float>(-.3f, .3f));
        }

        // ================================================================== lifecycle
        /// <summary>Init: the climbing and ladder controllers load their clips (data, was Plugin.Awake).</summary>
        partial void InitTraversal()
        {
            ClimbingController.Init();
            LadderController.Init();
        }

        /// <summary>Init: the old ClimbRunner loops (EarlyUpdate / EarlyFixedUpdate, after the Low Crawl job).</summary>
        partial void InitTraversalJobs()
        {
            Scheduler.EveryFrame(this, "Climbing", ClimbingController.TickInstance, Phase.EarlyUpdate);
            Scheduler.EveryFrame(this, "Climbing (physics)", ClimbingController.FixedTickInstance, Phase.EarlyFixedUpdate);
            Scheduler.EveryFrame(this, "Ladders", LadderController.TickInstance, Phase.EarlyUpdate);
            Scheduler.EveryFrame(this, "Ladders (physics)", LadderController.FixedTickInstance, Phase.EarlyFixedUpdate);
        }

        /// <summary>Init: the NPC traversal poses (LateUpdateLast, after the steering-wheel arms).</summary>
        partial void InitTraversalNpcJobs()
        {
            Scheduler.EveryFrame(this, "NPC traversal poses", NpcTraversal.LateTick, Phase.LateUpdateLast);
        }

        /// <summary>Shutdown: stops a running climb / ladder and every NPC traversal.</summary>
        partial void ShutdownTraversal()
        {
            try { ClimbingController.StopInstance(); } catch (Exception e) { Warn("climbing: " + e.Message); }
            try { LadderController.StopInstance(); } catch (Exception e) { Warn("ladders: " + e.Message); }
            try { NpcTraversal.Clear(); } catch (Exception e) { Warn("NPC traversals: " + e.Message); }
        }

        /// <summary>Floating-origin jump: the world-space climb / ladder paths move with the world.</summary>
        partial void OriginShiftTraversal(Vector3 delta)
        {
            ClimbingController.ShiftInstance(delta); LadderController.ShiftInstance(delta);
        }
    }
}
