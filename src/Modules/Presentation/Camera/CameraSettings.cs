using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Config entries of the Presentation camera rig (third person, observing / orbit, dynamic crosshair, pick assist, camera culling).
    /// Bound by <see cref="PresentationModule"/> (World part) in <c>[Presentation / Camera]</c> and <c>[Presentation / Camera culling]</c>
    /// (settings.cfg and tools.cfg) plus the <c>[Keys]</c> entry "Observe (third person)". D12: no value migration; the captured tuning
    /// defaults of the old <c>[PlayerBody ...]</c> sections are used as defaults.
    /// </summary>
    internal static class CameraSettings
    {
        internal const string Section = "Camera", CullingSection = "Camera culling";
        internal static ConfigEntry<bool> ThirdPersonOnFoot, ToggleMiddleMouse, DynamicCrosshair, PickAssistOn;
        internal static ConfigEntry<bool> OcclusionPrototype, OcclusionInVehicle, EnableMMB;
        internal static ConfigEntry<Key> ObserveKey;
        internal static ConfigEntry<float> ThirdDistance, ThirdHeight, ThirdShoulder, ThirdCarDistance, ThirdCarHeight, OrbitSpeed;
        internal static ConfigEntry<float> PickAssistRadius, AdsTime, OcclusionOpacity, OcclusionRadius;

        /// <summary>The Observe key as a legacy KeyCode (polled with Input.GetKey).</summary>
        internal static KeyCode ObserveKeyCode { get { return KeyMap.Code(ObserveKey.Value); } }

        /// <summary>Binds every camera entry (player switches, the Observe key, then the tools.cfg tuning), in the old display order.</summary>
        internal static void Bind(ModuleConfig cfg)
        {
            ThirdPersonOnFoot = PresentationModule.WorldPlayer(cfg, Section, "PlayerBody", "ThirdPerson", true, "The game's Change Camera key switches to a camera behind your character, on foot and in cars (replaces the game's own car camera).");
            ToggleMiddleMouse = PresentationModule.WorldPlayer(cfg, Section, "PlayerBody", "ToggleObserving", true, "Third person, observing key ([Keys] Observe). Off: hold the key to orbit the camera around the character; it returns behind the character when you let go. On: a press starts observing and the camera stays where you left it until the next press.");
            DynamicCrosshair = PresentationModule.WorldPlayer(cfg, Section, "PlayerBody", "DynamicCrosshair", true, "Third person with a weapon drawn: the crosshair sits where your shots and melee hits really land (the eye ray from the head). Without a weapon, or Off: the crosshair stays centred and the camera turns toward the aim point.");
            PickAssistOn = PresentationModule.WorldPlayer(cfg, Section, "PlayerBody", "PickAssist", true, "Third person on foot: an item, part or switch close to the crosshair on screen is picked even if the eye ray misses it.");
            OcclusionPrototype = PresentationModule.WorldPlayer(cfg, CullingSection, "PlayerBody / Camera culling", "CameraCulling", false, "Third person: keep the camera distance and make geometry between camera and body semi-transparent (prototype; roofs and vehicles get a larger window; not inside caves). Off = the camera moves in front of obstacles.");
            OcclusionInVehicle = PresentationModule.WorldPlayer(cfg, CullingSection, "PlayerBody / Camera culling", "CameraCullingInVehicles", false, "Third person while driving: also use the camera culling (needs CameraCulling on).");
            ObserveKey = cfg.Hotkey("Observe (third person)", Key.LeftAlt, "Third person: orbit the camera around the character (held, or press on / press off with ToggleObserving). None = no key.");

            const string Old = "PlayerBody / Camera";
            ThirdDistance = PresentationModule.WorldTool(cfg, Section, Old, "ThirdPersonDistance", 3.26f, "Third person on foot: camera distance, m (the mouse wheel changes and remembers it).", new AcceptableValueRange<float>(0.8f, 8f));
            ThirdCarDistance = PresentationModule.WorldTool(cfg, Section, Old, "ThirdPersonCarDistance", 5.38f, "Third person in a car: camera distance, m (mouse wheel).", new AcceptableValueRange<float>(2f, 15f));
            ThirdHeight = PresentationModule.WorldTool(cfg, Section, Old, "ThirdHeight", 0.25f, "Third person on foot: camera height above the eyes, m.");
            ThirdShoulder = PresentationModule.WorldTool(cfg, Section, Old, "ThirdShoulder", 0.45f, "Third person on foot: sideways offset (over the right shoulder), m.");
            ThirdCarHeight = PresentationModule.WorldTool(cfg, Section, Old, "ThirdCarHeight", 1.1f, "Third person in a car: camera height above the eyes, m.");
            OrbitSpeed = PresentationModule.WorldTool(cfg, Section, Old, "OrbitSpeed", 3f, "Third person: degrees per mouse step while the observing key orbits the camera.");
            EnableMMB = PresentationModule.WorldTool(cfg, Section, Old, "EnableMMB", false, "Third person: the middle mouse button also orbits the camera (off: the game rotates a held item with it).");
            AdsTime = PresentationModule.WorldTool(cfg, Section, Old, "AimDownSightsTime", 0.2f, "First person (SmoothAimDownSights): seconds the gun takes between hip and sights; 0 = the game's own (instant).");
            PickAssistRadius = PresentationModule.WorldTool(cfg, Section, Old, "PickAssistRadius", 0.06f, "Third person (PickAssist): an item / part / switch this close to the cursor on screen (share of the screen height) is picked even if the eye ray misses it.");
            OcclusionOpacity = PresentationModule.WorldTool(cfg, CullingSection, "PlayerBody / Camera culling", "OcclusionOpacity", .20f, "Opacity of blocking geometry inside the cutaway window: 0 = clear, 0.2 = faintly visible.", new AcceptableValueRange<float>(0f, .9f));
            OcclusionRadius = PresentationModule.WorldTool(cfg, CullingSection, "PlayerBody / Camera culling", "OcclusionRadius", .55f, "Width around the character cleared by the cutaway, in metres. The rest of a large object remains visible.", new AcceptableValueRange<float>(.2f, 1.5f));
        }
    }
}
