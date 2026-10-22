using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Config entries of the Presentation traversal engine (ledge climbing, ladders). Bound by <see cref="PresentationModule"/> (World part)
    /// in <c>[Presentation / Climbing]</c> (settings.cfg switches, tools.cfg tuning) and the <c>[Keys]</c> entry "Climb (alternative key)".
    /// D12: no value migration; the captured tuning defaults of <c>[PlayerBody / Climbing]</c> are used as defaults.
    /// </summary>
    internal static class TraversalSettings
    {
        internal const string Section = "Climbing";
        const string Old = "PlayerBody / Climbing";
        internal static ConfigEntry<bool> ClimbingOn, JumpTriggersClimbing, Ladders;
        internal static ConfigEntry<Key> ClimbKey;
        internal static ConfigEntry<float> ClimbMinHeight, ClimbMaxHeight, ClimbReach, ClimbWaistLimit, ClimbSpeed;
        internal static ConfigEntry<float> LadderSpeed, LadderSlideSpeed, LadderSlidePose, LadderOffset, LadderEyeForward, LadderNeckBack;

        /// <summary>The alternative climb key as a legacy KeyCode.</summary>
        internal static KeyCode ClimbKeyCode { get { return KeyMap.Code(ClimbKey.Value); } }

        /// <summary>Binds the climbing / ladder switches, the climb key, then the tools.cfg tuning (old order).</summary>
        internal static void Bind(ModuleConfig cfg)
        {
            ClimbingOn = PresentationModule.WorldPlayer(cfg, Section, Old, "Climbing", true, "Climb onto reachable solid ledges on foot.");
            JumpTriggersClimbing = PresentationModule.WorldPlayer(cfg, Section, Old, "JumpTriggersClimbing", true, "The game's Jump key climbs onto a reachable ledge or catches one within arm's reach during a jump. Otherwise it jumps normally.");
            Ladders = PresentationModule.WorldPlayer(cfg, Section, Old, "Ladders", true, "Ladders: weapon put away, centred on the ladder with a climbing animation (W / S up and down, Jump lets go). Off = the game's own ladder.");
            ClimbKey = cfg.Hotkey("Climb (alternative key)", Key.None, "Optional separate climb key (the game's Jump key also climbs with JumpTriggersClimbing). None = no key.");

            ClimbMinHeight = PresentationModule.WorldTool(cfg, Section, Old, "MinimumHeight", .45f, "Climbing: minimum ledge height above the feet, metres.", new AcceptableValueRange<float>(.36f, 1f));
            ClimbMaxHeight = PresentationModule.WorldTool(cfg, Section, Old, "MaximumHeight", 2.5f, "Climbing: maximum ledge height above the feet, metres.", new AcceptableValueRange<float>(1f, 2.6f));
            ClimbReach = PresentationModule.WorldTool(cfg, Section, Old, "Reach", .85f, "Climbing: maximum distance to the obstacle face, metres.", new AcceptableValueRange<float>(.35f, 1.2f));
            ClimbWaistLimit = PresentationModule.WorldTool(cfg, Section, Old, "WaistHeightLimit", 1.25f, "Climbing: higher ledges use the high-climb animation.", new AcceptableValueRange<float>(.8f, 1.5f));
            ClimbSpeed = PresentationModule.WorldTool(cfg, Section, Old, "AnimationSpeed", 1.5f, "Climbing: climb playback speed.", new AcceptableValueRange<float>(.6f, 2.5f));
            LadderSpeed = PresentationModule.WorldTool(cfg, Section, Old, "LadderSpeed", 2.2f, "Ladders: climbing speed up, metres per second (the animation follows the rungs at any speed; the game's own ladder: 2).", new AcceptableValueRange<float>(.3f, 4f));
            LadderSlideSpeed = PresentationModule.WorldTool(cfg, Section, Old, "LadderSlideSpeed", 6f, "Ladders: going down is a slide (hands and feet on the rails); its top speed, metres per second.", new AcceptableValueRange<float>(1f, 12f));
            LadderSlidePose = PresentationModule.WorldTool(cfg, Section, Old, "LadderSlidePose", 0f, "Ladders: the climbing animation frame held while sliding down (0..1 of one cycle).", new AcceptableValueRange<float>(0f, 1f));
            LadderEyeForward = PresentationModule.WorldTool(cfg, Section, Old, "LadderEyeForward", .16f, "Ladders: first-person eye forward of the head towards the ladder, metres (so the camera does not see its own neck).", new AcceptableValueRange<float>(0f, .4f));
            LadderNeckBack = PresentationModule.WorldTool(cfg, Section, Old, "LadderNeckBack", 12f, "Ladders: the neck tilted back while climbing, degrees.", new AcceptableValueRange<float>(-30f, 45f));
            LadderOffset = PresentationModule.WorldTool(cfg, Section, Old, "LadderOffset", 0f, "Ladders: moves the body away from (+) or towards (-) the ladder, metres; 0 = the balls of the feet over the rungs.", new AcceptableValueRange<float>(-.3f, .3f));
        }
    }
}
