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
    /// <summary>The PlayerBody module entry (old module; Wave B moves its parts to the Presentation/Vehicles/HUD/... entries).</summary>
    public sealed partial class PlayerBodyModule : ModuleBase
    {
        public const string SourceVersion = "2.3.9";      // ModAPI.PluginVersion (consumers compare against Apocaplayer versions)
        internal static PlayerBodyModule Instance;

        /// <summary>Module id: config section prefix, Harmony id suffix, asset folder.</summary>
        public override string Id { get { return "PlayerBody"; } }
        /// <summary>Name shown in the F3/F6 module lists.</summary>
        public override string DisplayName { get { return "Player body & animation"; } }
        /// <summary>Standalone mods this module replaces (ReplacementGuard disables them).</summary>
        public override string[] ConflictsWith { get { return new[] { "com.denis.apocalypter.apocaplayer", "com.denis.apocalypter.apocaclimber" }; } }

        // ------------------------------------------------------------------ state shared with the ported code
        /// <summary>True between Init and Shutdown (replaces the old Plugin.Enabled.Value checks; Core removes the patches/jobs when off).</summary>
        internal static bool Running;
        internal new static string Dir;            // BepInEx\plugins\WildWasteland\PlayerBody\
        internal static string DataDir;        // BepInEx\config\WildWasteland\PlayerBody\
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
        /// <summary>Binds every entry, in the old order (see the partial files for the entries of each target).</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            Instance = this;
            // ---- F6 (settings.cfg): one toggle per feature (feature order), then the player options
            BindCharacterChoice(cfg);          // [PlayerBody] Character                              (Presentation/Character)
            BindCameraView(cfg);               // [PlayerBody] ThirdPerson, ToggleObserving, DynamicCrosshair (Presentation/Camera)
            BindCharacterBody(cfg);            // [PlayerBody] BodyFirstPerson .. SmoothAimDownSights  (Presentation/Character)
            BindCameraPickAssist(cfg);         // [PlayerBody] PickAssist                             (Presentation/Camera)
            BindTraversal(cfg);                // [PlayerBody / Climbing]                             (Presentation/Traversal)
            BindCameraCulling(cfg);            // [PlayerBody / Camera culling]                       (Presentation/Camera)
            BindHud(cfg);                      // [PlayerBody / Vehicles] status / hotkey hints       (HUD)
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
            BindCharacterFirstPersonTools(cfg);// [PlayerBody / First person]
            BindSeatsTools(cfg);               // [PlayerBody / Car seat]
            BindCameraCullingTools(cfg);       // [PlayerBody / Camera culling]
            BindTraversalTools(cfg);           // [PlayerBody / Climbing]
            BindCharacterTools(cfg);           // [PlayerBody / Animation|Animation (fallback clips)|Model|ModAPI] + live reactions
        }

        /// <summary>Part step, implemented in PlayerBodyModule.Character.cs (Presentation/Character).</summary>
        partial void BindCharacterChoice(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void BindCameraView(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Character.cs (Presentation/Character).</summary>
        partial void BindCharacterBody(ModuleConfig cfg);
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
        /// <summary>Part step, implemented in PlayerBodyModule.Character.cs (Presentation/Character).</summary>
        partial void BindCharacterFirstPersonTools(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Seats.cs (Presentation/Seats).</summary>
        partial void BindSeatsTools(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void BindCameraCullingTools(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void BindTraversalTools(ModuleConfig cfg);
        /// <summary>Part step, implemented in PlayerBodyModule.Character.cs (Presentation/Character).</summary>
        partial void BindCharacterTools(ModuleConfig cfg);

        // ================================================================== lifecycle
        /// <summary>Module Init: data, hooks/patches and jobs of every part, in the old order (job order = run order within a phase).</summary>
        public override void Init()
        {
            Instance = this;
            Dir = base.Dir;
            DataDir = Assets.ModuleConfigDir(Id);
            Core.TunedSettings.InstallProfiles("PlayerBody");
            Running = true;
            InitPlayer();                      // BodyRunner.ResetAll (clean state, also after a runtime off -> on)
            InitCompatibility();               // ChaseCamera type adapter (+ its discovery job), Weapons Pack shadow patch
            InitVehicles();                    // ignition row keys, Car / steering patches, "Ignition control" job
            InitCharacter();                   // weapon poses, aim lift, female pain sounds, ADS / pain patches
            InitTraversal();                   // climbing / ladder clips
            InitCamera();                      // third-person hooks (GetButtonDown before Crawling's), sway, MouseLook, pick assist
            InitGunplay();                     // blast lance from the seat
            InitPlayerCrawling();              // Low Crawl hooks + "PlayerBody.Crawling" (EarlyUpdate, before the climbing jobs)
            InitTraversalJobs();               // the old ClimbRunner loops ([DefaultExecutionOrder(-1000)])
            InitPlayerJobs();                  // the old Runner Update / LateUpdate
            InitCharacterJobs();               // the old ModApiRunner LateUpdate (after "Body late")
            InitSeatsJobs();                   // steering-wheel arms (LateUpdateLast, before the NPC traversal poses)
            InitTraversalNpcJobs();            // NPC traversal poses (LateUpdateLast)
            InitHud();                         // the one Gui job (editor hints + vehicle HUD)
            InitCameraEndOfFrame();            // end-of-frame camera reset coroutine

            // keys polled in context (third person / climbing / driving); registered so [Keys] conflicts are reported
            foreach (var k in new[] { ObserveKey, ClimbKey, CassetteKey, VolumeDownKey, VolumeUpKey }) Hotkeys.Register(this, k, NoOp, Never);

            InitCharacterService();            // Services.Animation
            Core.Log.Always("[PlayerBody] Player body & animation (Apocaplayer " + SourceVersion + ") ready; assets in " + Dir);
        }

        /// <summary>Part step, implemented in PlayerBodyModule.Player.cs (Presentation/Player).</summary>
        partial void InitPlayer();
        /// <summary>Part step, implemented in PlayerBodyModule.Compatibility.cs (Compatibility).</summary>
        partial void InitCompatibility();
        /// <summary>Part step, implemented in PlayerBodyModule.Vehicles.cs (Vehicles).</summary>
        partial void InitVehicles();
        /// <summary>Part step, implemented in PlayerBodyModule.Character.cs (Presentation/Character).</summary>
        partial void InitCharacter();
        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void InitTraversal();
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void InitCamera();
        /// <summary>Part step, implemented in PlayerBodyModule.Gunplay.cs (Gunplay).</summary>
        partial void InitGunplay();
        /// <summary>Part step, implemented in PlayerBodyModule.Player.cs (Presentation/Player).</summary>
        partial void InitPlayerCrawling();
        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void InitTraversalJobs();
        /// <summary>Part step, implemented in PlayerBodyModule.Player.cs (Presentation/Player).</summary>
        partial void InitPlayerJobs();
        /// <summary>Part step, implemented in PlayerBodyModule.Character.cs (Presentation/Character).</summary>
        partial void InitCharacterJobs();
        /// <summary>Part step, implemented in PlayerBodyModule.Seats.cs (Presentation/Seats).</summary>
        partial void InitSeatsJobs();
        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void InitTraversalNpcJobs();
        /// <summary>Part step, implemented in PlayerBodyModule.Hud.cs (HUD).</summary>
        partial void InitHud();
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void InitCameraEndOfFrame();
        /// <summary>Part step, implemented in PlayerBodyModule.Character.cs (Presentation/Character).</summary>
        partial void InitCharacterService();

        /// <summary>Hotkeys.Register action for context-polled keys (they are only registered for the conflict report).</summary>
        static void NoOp() { }
        /// <summary>Hotkeys.Register condition that never fires (see NoOp).</summary>
        static bool Never() { return false; }

        /// <summary>Module Shutdown: undoes every part's game-state changes, in the old order.</summary>
        public override void Shutdown()
        {
            ShutdownCompatibility();           // Weapons Pack shadows, ChaseCamera adapter
            ShutdownCharacterService();        // Services.Animation = null
            ShutdownCharacterSave();           // weapon poses / aim lift written
            ShutdownTraversal();               // climb / ladder / NPC traversals stopped
            ShutdownPlayer();                  // BodyRunner.TearDown: the game's own player is back
            ShutdownVehicles();                // ignition row removed
            ShutdownCharacter();               // ADS tweens restored, ModAPI characters and service cleared
            ShutdownCamera();                  // end-of-frame loop stopped, sway / third-person caches cleared
            Running = false;
        }

        /// <summary>Part step, implemented in PlayerBodyModule.Compatibility.cs (Compatibility).</summary>
        partial void ShutdownCompatibility();
        /// <summary>Part step, implemented in PlayerBodyModule.Character.cs (Presentation/Character).</summary>
        partial void ShutdownCharacterService();
        /// <summary>Part step, implemented in PlayerBodyModule.Character.cs (Presentation/Character).</summary>
        partial void ShutdownCharacterSave();
        /// <summary>Part step, implemented in PlayerBodyModule.Traversal.cs (Presentation/Traversal).</summary>
        partial void ShutdownTraversal();
        /// <summary>Part step, implemented in PlayerBodyModule.Player.cs (Presentation/Player).</summary>
        partial void ShutdownPlayer();
        /// <summary>Part step, implemented in PlayerBodyModule.Vehicles.cs (Vehicles).</summary>
        partial void ShutdownVehicles();
        /// <summary>Part step, implemented in PlayerBodyModule.Character.cs (Presentation/Character).</summary>
        partial void ShutdownCharacter();
        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void ShutdownCamera();

        /// <summary>Scene load: every part drops its scene references, in the old order.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneCameraRestart();              // the end-of-frame loop again if Core recreated its runner
            ScenePlayer();                     // BodyRunner.OnSceneLoaded
            SceneCharacter();                  // ModAPI characters, service, ADS tween sweep
            SceneCamera();                     // third-person caches, sway sweep
        }

        /// <summary>Part step, implemented in PlayerBodyModule.Camera.cs (Presentation/Camera).</summary>
        partial void SceneCameraRestart();
        /// <summary>Part step, implemented in PlayerBodyModule.Player.cs (Presentation/Player).</summary>
        partial void ScenePlayer();
        /// <summary>Part step, implemented in PlayerBodyModule.Character.cs (Presentation/Character).</summary>
        partial void SceneCharacter();
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
