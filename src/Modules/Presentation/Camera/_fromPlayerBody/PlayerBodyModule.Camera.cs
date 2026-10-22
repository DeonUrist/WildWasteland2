using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (split from PlayerBody/PlayerBodyModule.cs in Wave A): the camera rig - third person, observing / orbit,
    /// dynamic crosshair, pick assist, camera culling (occlusion cutaway), FP weapon sway hook, end-of-frame camera reset. Target:
    /// Presentation camera (B06). AimDownSightsTime is bound here because it sits in the [PlayerBody / Camera] section; it belongs to the
    /// FP weapon-motion unit (AimTransition, B05).
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        // ------------------------------------------------------------------ F6 (main cfg)
        internal static ConfigEntry<bool> ThirdPersonOnFoot, ToggleMiddleMouse, DynamicCrosshair, PickAssistOn;
        internal static ConfigEntry<bool> OcclusionPrototype, OcclusionInVehicle;
        internal static ConfigEntry<Key> ObserveKey;
        internal static KeyCode ObserveKeyCode { get { return KeyMap.Code(ObserveKey.Value); } }

        // ------------------------------------------------------------------ F7 (tools.cfg)
        internal static ConfigEntry<bool> EnableMMB;
        internal static ConfigEntry<float> ThirdDistance, ThirdHeight, ThirdShoulder, ThirdCarDistance, ThirdCarHeight, OrbitSpeed;
        internal static ConfigEntry<float> PickAssistRadius, AdsTime;
        internal static ConfigEntry<float> OcclusionOpacity, OcclusionRadius;

        static Coroutine _eof;
        static Core.Runner _eofHost;

        // ================================================================== config
        /// <summary>Binds [PlayerBody] ThirdPerson, ToggleObserving and DynamicCrosshair.</summary>
        partial void BindCameraView(ModuleConfig cfg)
        {
            ThirdPersonOnFoot = cfg.Player("", "ThirdPerson", true, "The game's Change Camera key switches to a camera behind your character, on foot and in cars (replaces the game's own car camera).");
            ToggleMiddleMouse = cfg.Player("", "ToggleObserving", true, "Third person, observing key ([Keys] Observe). Off: hold the key to orbit the camera around the character; it returns behind the character when you let go. On: a press starts observing and the camera stays where you left it until the next press.");
            DynamicCrosshair = cfg.Player("", "DynamicCrosshair", true, "Third person with a weapon drawn: the crosshair sits where your shots and melee hits really land (the eye ray from the head). Without a weapon, or Off: the crosshair stays centred and the camera turns toward the aim point.");
        }

        /// <summary>Binds [PlayerBody] PickAssist.</summary>
        partial void BindCameraPickAssist(ModuleConfig cfg)
        {
            PickAssistOn = cfg.Player("", "PickAssist", true, "Third person on foot: an item, part or switch close to the crosshair on screen is picked even if the eye ray misses it.");
        }

        /// <summary>Binds [PlayerBody / Camera culling] CameraCulling and CameraCullingInVehicles.</summary>
        partial void BindCameraCulling(ModuleConfig cfg)
        {
            OcclusionPrototype = cfg.Player("Camera culling", "CameraCulling", false, "Third person: keep the camera distance and make geometry between camera and body semi-transparent (prototype; roofs and vehicles get a larger window; not inside caves). Off = the camera moves in front of obstacles.");
            OcclusionInVehicle = cfg.Player("Camera culling", "CameraCullingInVehicles", false, "Third person while driving: also use the camera culling (needs CameraCulling on).");
        }

        /// <summary>Binds [Keys] "Observe (third person)".</summary>
        partial void BindCameraKeys(ModuleConfig cfg)
        {
            ObserveKey = cfg.Hotkey("Observe (third person)", Key.LeftAlt, "Third person: orbit the camera around the character (held, or press on / press off with ToggleObserving). None = no key.");
        }

        /// <summary>Binds [PlayerBody / Camera] (tools.cfg): distances, heights, orbit, MMB, ADS time, pick-assist radius.</summary>
        partial void BindCameraTools(ModuleConfig cfg)
        {
            const string Cam = "Camera";
            ThirdDistance = cfg.Tool(Cam, "ThirdPersonDistance", 3.26f, "Third person on foot: camera distance, m (the mouse wheel changes and remembers it).", new AcceptableValueRange<float>(0.8f, 8f));
            ThirdCarDistance = cfg.Tool(Cam, "ThirdPersonCarDistance", 5.38f, "Third person in a car: camera distance, m (mouse wheel).", new AcceptableValueRange<float>(2f, 15f));
            ThirdHeight = cfg.Tool(Cam, "ThirdHeight", 0.25f, "Third person on foot: camera height above the eyes, m.");
            ThirdShoulder = cfg.Tool(Cam, "ThirdShoulder", 0.45f, "Third person on foot: sideways offset (over the right shoulder), m.");
            ThirdCarHeight = cfg.Tool(Cam, "ThirdCarHeight", 1.1f, "Third person in a car: camera height above the eyes, m.");
            OrbitSpeed = cfg.Tool(Cam, "OrbitSpeed", 3f, "Third person: degrees per mouse step while the observing key orbits the camera.");
            EnableMMB = cfg.Tool(Cam, "EnableMMB", false, "Third person: the middle mouse button also orbits the camera (off: the game rotates a held item with it).");
            AdsTime = cfg.Tool(Cam, "AimDownSightsTime", 0.2f, "First person (SmoothAimDownSights): seconds the gun takes between hip and sights; 0 = the game's own (instant).");
            PickAssistRadius = cfg.Tool(Cam, "PickAssistRadius", 0.06f, "Third person (PickAssist): an item / part / switch this close to the cursor on screen (share of the screen height) is picked even if the eye ray misses it.");
        }

        /// <summary>Binds [PlayerBody / Camera culling] (tools.cfg): cutaway opacity and radius.</summary>
        partial void BindCameraCullingTools(ModuleConfig cfg)
        {
            const string Cull = "Camera culling";
            OcclusionOpacity = cfg.Tool(Cull, "OcclusionOpacity", .20f, "Opacity of blocking geometry inside the cutaway window: 0 = clear, 0.2 = faintly visible.", new AcceptableValueRange<float>(0f, .9f));
            OcclusionRadius = cfg.Tool(Cull, "OcclusionRadius", .55f, "Width around the character cleared by the cutaway, in metres. The rest of a large object remains visible.", new AcceptableValueRange<float>(.2f, 1.5f));
        }

        // ================================================================== lifecycle
        /// <summary>Init: third-person hooks (Core dispatchers, before Crawling's GetButtonDown prefix), FP sway, MouseLook skip, pick assist.</summary>
        partial void InitCamera()
        {
            Hooks.GetButtonDownUpdate.Prefix(this, ThirdPerson.BeforeGetButtonDown);
            Hooks.ActivateGameObjectDo.Prefix(this, ThirdPerson.BeforeActivateGameObject);
            Hooks.SmoothLookAtDo.Prefix(this, FirstPersonSway.BeforeSmoothLookAt);
            Core.Patches.Patch(this, Core.Patches.Method(typeof(HutongGames.PlayMaker.Actions.MouseLook), "OnUpdate"),
                prefix: Core.Patches.Method(typeof(ThirdPerson), "BeforeMouseLook"));
            PickAssist.Patch(this);
        }

        /// <summary>Init: starts the end-of-frame camera reset loop.</summary>
        partial void InitCameraEndOfFrame()
        {
            StartEndOfFrame();
        }

        /// <summary>The WaitForEndOfFrame loop lives on Core's runner; started again if Core had to recreate the runner.</summary>
        static void StartEndOfFrame()
        {
            if (_eof != null && _eofHost != null && _eofHost == Core.Runner.Instance) return;
            _eofHost = Core.Runner.Instance;
            _eof = Scheduler.StartCoroutine(BodyRunner.EndOfFrameLoop());
        }

        /// <summary>Shutdown: stops the end-of-frame loop and clears the sway / third-person caches.</summary>
        partial void ShutdownCamera()
        {
            if (_eof != null && _eofHost != null && _eofHost == Core.Runner.Instance) Scheduler.StopCoroutine(_eof);
            _eof = null; _eofHost = null;
            FirstPersonSway.Clear();
            ThirdPerson.ClearCaches();
        }

        /// <summary>Scene load (first): the end-of-frame loop again if the runner was recreated.</summary>
        partial void SceneCameraRestart()
        {
            StartEndOfFrame();
        }

        /// <summary>Scene load: third-person FSM caches cleared, FP sway records swept.</summary>
        partial void SceneCamera()
        {
            ThirdPerson.ClearCaches();
            FirstPersonSway.Sweep();
        }
    }
}
