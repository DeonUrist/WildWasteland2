using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.PlayerBody
{
    // Player body & animation (ported from Apocaplayer 2.3.9, which had absorbed Apocaclimber): a full character body under the
    // first-person camera and in the driver's seat, third person on foot and in cars, the Mixamo animation framework (ModAPI, also
    // used by NPC and Patrols through Core.Services.Animation), climbing, vehicle hotkeys / HUD and first-person polish.
    // The old Plugin's config statics live in the partial files under the same names; Apocaplayer's own Runner / ClimbRunner /
    // ModApiRunner loops are scheduler jobs (BodyRunner), its Harmony patches are Core hooks or module patches (see Init).
    //
    // (Wave A refactor) This file is the old entry's core: identity, shared state, logging and the lifecycle SEQUENCE. Everything
    // else lives in partial files placed in the target module folders (PlayerBodyModule.<Target>.cs under src/Modules/<Target>/
    // .../_fromPlayerBody/): config entries + binding, Init/Shutdown/scene parts. The partial methods are called below in exactly the
    // old order (config bind order = display order; job registration order = run order within a phase; hook order per dispatcher).
    // (D12) The Apocaplayer/Apocaclimber first-run config migrations, the config/Apocaplayer sidecar copy, the legacy ignition-key
    // read and the LadderSpeed old-default bump are gone (fresh start).
    // (Wave B, B05) The character body, the animation framework, the player driver (BodyRunner, Low Crawl) and their config moved to
    // PresentationModule ([Presentation] sections). What is left is a shell that still runs the parts other Wave B packages own in this
    // class (camera, traversal, seats: B06; HUD hints: B10; developer editors: B16; compatibility: B17); Wave C deletes it.
    /// <summary>The PlayerBody module shell: runs the parts other packages still own in this class until Wave C deletes it.</summary>
    public sealed partial class PlayerBodyModule : ModuleBase
    {
        /// <summary>The Apocaplayer version the ModAPI mirrors (kept for the ChaseCamera type adapter).</summary>
        public const string SourceVersion = "2.3.9";
        /// <summary>The shell instance (hook/job owner of the remaining parts).</summary>
        internal static PlayerBodyModule Instance;
        /// <summary>The config of the shell, kept for the parts bound after Presentation (see <see cref="BindAfterPresentation"/>).</summary>
        static ModuleConfig _lateCfg;

        /// <summary>Module id: config section prefix, Harmony id suffix, asset folder.</summary>
        public override string Id { get { return "PlayerBody"; } }
        /// <summary>Name shown in the F3/F6 module lists.</summary>
        public override string DisplayName { get { return "Player body (legacy parts: camera, climbing, vehicle hints)"; } }
        /// <summary>Standalone mods this module replaces (ReplacementGuard disables them).</summary>
        public override string[] ConflictsWith { get { return new[] { "com.denis.apocalypter.apocaplayer", "com.denis.apocalypter.apocaclimber" }; } }

        // ------------------------------------------------------------------ state shared with the ported code
        /// <summary>True between Init and Shutdown (replaces the old Plugin.Enabled.Value checks; Core removes the patches/jobs when off).</summary>
        internal static bool Running;
        /// <summary>BepInEx\plugins\WildWasteland\PlayerBody\ (assets of the remaining parts).</summary>
        internal new static string Dir;
        /// <summary>BepInEx\config\WildWasteland\PlayerBody\ (data of the remaining parts, e.g. wheel grips).</summary>
        internal static string DataDir;
        /// <summary>True while Core verbose logging is on.</summary>
        internal static bool VerboseOn { get { return Core.Log.Verbose; } }

        // ------------------------------------------------------------------ logging (old Plugin.Log / Verbose / Warn)
        /// <summary>Verbose-level log line with the [PlayerBody] prefix.</summary>
        internal static void Info(string s) { if (Core.Log.Verbose) Core.Log.Info("[PlayerBody] " + s); }
        /// <summary>Verbose-level log line with the [PlayerBody] prefix (same as Info).</summary>
        internal static void Verbose(string s) { if (Core.Log.Verbose) Core.Log.Info("[PlayerBody] " + s); }
        /// <summary>Warning with the [PlayerBody] prefix (rate-limited by Core).</summary>
        internal static void Warn(string s) { Core.Log.Warn("[PlayerBody] " + s); }
        /// <summary>Error with the [PlayerBody] prefix.</summary>
        internal static void Error(string s) { Core.Log.Error("[PlayerBody] " + s); }
        /// <summary>Error with the [PlayerBody] prefix and the exception text.</summary>
        internal static void Error(Exception e) { Core.Log.Error("[PlayerBody] " + e); }

        /// <summary>A path relative to the module's plugin folder (absolute paths are returned as they are).</summary>
        internal static string ModPath(string rel)
        {
            rel = (rel ?? "").Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.IsPathRooted(rel) ? rel : Path.Combine(Dir ?? Assets.ModuleDir("PlayerBody"), rel);
        }

        // ================================================================== config
        /// <summary>Binds the entries of the remaining parts in the old order; the HUD part (it reacts to the Presentation ReplaceDriver entry)
        /// is bound by <see cref="BindAfterPresentation"/> once Presentation has bound its entries (Presentation is registered after this shell).</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            Instance = this;
            _lateCfg = cfg;
            // ---- F6 (settings.cfg)
            BindCameraView(cfg);               // [PlayerBody] ThirdPerson, ToggleObserving, DynamicCrosshair (Presentation/Camera)
            BindCameraPickAssist(cfg);         // [PlayerBody] PickAssist                             (Presentation/Camera)
            BindTraversal(cfg);                // [PlayerBody / Climbing]                             (Presentation/Traversal)
            BindCameraCulling(cfg);            // [PlayerBody / Camera culling]                       (Presentation/Camera)
            BindVehicles(cfg);                 // [PlayerBody / Vehicles] IgnitionKey                 (Vehicles)
            BindGunplay(cfg);                  // [PlayerBody / Vehicles] LanceIgnoresOwnCar          (Gunplay)
            // ---- [Keys]: gameplay keys keep Apocaplayer's defaults (polled in context: third person, climbing, driving)
            BindCameraKeys(cfg);               // Observe (third person)
            BindTraversalKeys(cfg);            // Climb (alternative key)
            BindVehiclesKeys(cfg);             // Cassette start/stop, volume down / up
            BindDeveloperKeys(cfg);            // weapon pose editor keys (GunPose.BindEditorKeys)
            // ---- F7 (tools.cfg)
            BindDeveloperTools(cfg);           // [PlayerBody / Editors]
            BindCameraTools(cfg);              // [PlayerBody / Camera]
            BindSeatsTools(cfg);               // [PlayerBody / Car seat]
            BindCameraCullingTools(cfg);       // [PlayerBody / Camera culling]
            BindTraversalTools(cfg);           // [PlayerBody / Climbing]
        }

        /// <summary>Called by PresentationModule.BindConfig after its entries exist: binds the HUD part ([PlayerBody / Vehicles] hints and
        /// the TAB-picture reaction to the Presentation ReplaceDriver entry). No-op before this shell bound its own config.</summary>
        internal static void BindAfterPresentation()
        {
            var cfg = _lateCfg; _lateCfg = null;
            if (cfg != null && Instance != null) Instance.BindHud(cfg);
        }

        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void BindCameraView(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void BindCameraPickAssist(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void BindTraversal(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void BindCameraCulling(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Hud.cs (HUD).</summary>
        partial void BindHud(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Vehicles.cs (Vehicles).</summary>
        partial void BindVehicles(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Gunplay.cs (Gunplay).</summary>
        partial void BindGunplay(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void BindCameraKeys(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void BindTraversalKeys(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Vehicles.cs (Vehicles).</summary>
        partial void BindVehiclesKeys(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Developer.cs (Developer).</summary>
        partial void BindDeveloperKeys(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Developer.cs (Developer).</summary>
        partial void BindDeveloperTools(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void BindCameraTools(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Seats.cs (Presentation/Seats).</summary>
        partial void BindSeatsTools(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void BindCameraCullingTools(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void BindTraversalTools(ModuleConfig cfg);

        // ================================================================== lifecycle
        /// <summary>Module Init: data, hooks/patches and jobs of the remaining parts, in the old order (job order = run order within a phase).
        /// The character / player parts now start in PresentationModule (registered right after this shell).</summary>
        public override void Init()
        {
            Instance = this;
            Dir = base.Dir;
            DataDir = Assets.ModuleConfigDir(Id);
            Core.TunedSettings.InstallProfiles("PlayerBody");
            Running = true;
            InitCompatibility();               // ChaseCamera type adapter (+ its discovery job), Weapons Pack shadow patch
            InitVehicles();                    // ignition row keys, Car / steering patches, "Ignition control" job
            InitTraversal();                   // climbing / ladder clips
            InitCamera();                      // third-person hooks (GetButtonDown before Crawling's), sway, MouseLook, pick assist
            InitGunplay();                     // blast lance from the seat
            InitTraversalJobs();               // the old ClimbRunner loops ([DefaultExecutionOrder(-1000)])
            InitSeatsJobs();                   // steering-wheel arms (LateUpdateLast, before the NPC traversal poses)
            InitTraversalNpcJobs();            // NPC traversal poses (LateUpdateLast)
            InitHud();                         // the one Gui job (editor hints + vehicle HUD)
            InitCameraEndOfFrame();            // end-of-frame camera reset coroutine

            // keys polled in context (third person / climbing); registered so [Keys] conflicts are reported. The cassette / volume keys are
            // registered by Vehicles now (B12-8).
            foreach (var k in new[] { ObserveKey, ClimbKey }) Hotkeys.Register(this, k, NoOp, Never);
        }

        /// <summary>Part step, implemented in PlayerBodyModule.Compatibility.cs (Compatibility).</summary>
        partial void InitCompatibility();
        /// <summary>Part step, implemented in PlayerBodyModule.Vehicles.cs (Vehicles).</summary>
        partial void InitVehicles();
        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void InitTraversal();
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void InitCamera();
        /// <summary>Part step, implemented in PlayerBodyModule.Gunplay.cs (Gunplay).</summary>
        partial void InitGunplay();
        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void InitTraversalJobs();
        /// <summary>Part step, implemented in PlayerBodyModule.Seats.cs (Presentation/Seats).</summary>
        partial void InitSeatsJobs();
        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void InitTraversalNpcJobs();
        /// <summary>Part step, implemented in PlayerBodyModule.Hud.cs (HUD).</summary>
        partial void InitHud();
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void InitCameraEndOfFrame();

        /// <summary>Hotkeys.Register action for context-polled keys (they are only registered for the conflict report).</summary>
        static void NoOp() { }
        /// <summary>Hotkeys.Register condition that never fires (see NoOp).</summary>
        static bool Never() { return false; }

        /// <summary>Module Shutdown: undoes the remaining parts' game-state changes, in the old order.</summary>
        public override void Shutdown()
        {
            ShutdownCompatibility();           // Weapons Pack shadows, ChaseCamera adapter
            ShutdownTraversal();               // climb / ladder / NPC traversals stopped
            ShutdownVehicles();                // ignition row removed
            ShutdownCamera();                  // end-of-frame loop stopped, sway / third-person caches cleared
            Running = false;
        }

        /// <summary>Part step, implemented in PlayerBodyModule.Compatibility.cs (Compatibility).</summary>
        partial void ShutdownCompatibility();
        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void ShutdownTraversal();
        /// <summary>Part step, implemented in PlayerBodyModule.Vehicles.cs (Vehicles).</summary>
        partial void ShutdownVehicles();
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void ShutdownCamera();

        /// <summary>Scene load: the remaining parts drop their scene references (the body / characters reset in PresentationModule).</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneCameraRestart();              // the end-of-frame loop again if Core recreated its runner
            SceneCamera();                     // third-person caches, sway sweep
        }

        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void SceneCameraRestart();
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void SceneCamera();

        // the climb path is kept in world space for the duration of a climb: move it with the world (else a cancel would put the player
        // back 1000 m away). The death ragdoll is a scene root object, which the NWH FloatingOrigin moves itself; everything else is
        // re-derived from the Player / camera every frame (ThirdPerson moves its cached view along already).
        /// <summary>Floating-origin jump: the traversal part moves its world-space climb path.</summary>
        public override void OnOriginShift(Vector3 delta) { OriginShiftTraversal(delta); }

        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void OriginShiftTraversal(Vector3 delta);

        // ================================================================== F7
        /// <summary>F7 pages (PlayerBodyPages, split per target).</summary>
        public override IEnumerable<ToolPage> ToolPages() { return PlayerBodyPages.Create(this); }
    }
}
