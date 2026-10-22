using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Modules.NPC;
using WildWasteland.Modules.PlayerBody;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// Presentation module entry (README 3.1, 6.1): one character pipeline for player, NPCs, female NPCs and animals; traversal, seat poses,
    /// weapon poses, camera rig, vehicle skins. Main owner B05 (Character/Player: the player's body and animation framework, the ModAPI
    /// animation service, Women of Wasteland incl. the female population roll, the NPC body/animal visual parts), World part owner B06
    /// (Camera/Traversal/Seats/VehicleSkins) through the *World partial methods implemented in PresentationModule.World.cs.
    /// Config: [Presentation] / [Presentation / &lt;Sub&gt;] (D12, no migration; entries of the character part in <see cref="CharacterSettings"/>).
    /// Assets: plugins/WildWasteland/Presentation/ (Models, Sounds; D13), data: config/WildWasteland/Presentation/.
    ///
    /// Lifecycle order (keeps the order the parts had inside the old PlayerBody entry, where the World parts sat between the character
    /// data and the character jobs):
    ///   BindConfig: character entries, then World.
    ///   Init: character data + patches (body reset, weapon poses, aim lift, pain sounds, ADS), then <c>InitWorld</c> (camera hooks before
    ///         the crawl hooks, traversal jobs before the body jobs), then the crawl hooks and the body / character / NPC visual jobs,
    ///         Women of Wasteland.
    ///   Shutdown: animation service withdrawn, poses saved, Women of Wasteland, then <c>ShutdownWorld</c>, then the body torn down.
    ///   Other calls: character part first, then World.
    /// </summary>
    public sealed partial class PresentationModule : ModuleBase
    {
        /// <summary>The Apocaplayer version the ModAPI animation service mirrors (consumers compare against Apocaplayer versions).</summary>
        internal const string SourceVersion = "2.3.9";

        static readonly string[] _conflicts = { "com.denis.apocalypter.apocaplayer", "com.denis.apocalypter.apocaclimber" };
        static readonly AnimationService _animation = new AnimationService();
        static readonly CharacterPresentation _characters = new CharacterPresentation();
        static readonly CharacterAppearance _appearance = new CharacterAppearance();
        static readonly WeaponPoseService _weaponPose = new WeaponPoseService();
        static readonly PoseAuthoring _poseAuthoring = new PoseAuthoring();
        static bool _running;

        /// <summary>The module instance (log/hook owner); set at BindConfig so logging works before the first Init.</summary>
        internal static PresentationModule Instance;

        /// <summary>True between Init and Shutdown of the character part (replaces PlayerBodyModule.Running for the character code).</summary>
        internal static bool Running { get { return _running; } }

        /// <summary>BepInEx\plugins\WildWasteland\Presentation\ (models, clips, sounds).</summary>
        internal static string AssetDir { get { return Assets.ModuleDir("Presentation"); } }

        /// <summary>BepInEx\config\WildWasteland\Presentation\ (weapon-poses.txt, aim-lift.txt).</summary>
        internal static string DataDir { get { return Assets.ModuleConfigDir("Presentation"); } }

        /// <summary>The ModAPI animation service object (IAnimationApi) of this module.</summary>
        internal static AnimationService Animation { get { return _animation; } }

        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Presentation"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Presentation (player body, characters & animation)"; } }

        /// <summary>The standalone Apocaplayer / Apocaclimber plugins replace this module (AGENTS 5.8).</summary>
        public override string[] ConflictsWith { get { return _conflicts; } }

        /// <summary>A path relative to the module's plugin folder (absolute paths are returned unchanged).</summary>
        internal static string AssetPath(string rel)
        {
            rel = (rel ?? "").Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.IsPathRooted(rel) ? rel : Path.Combine(AssetDir, rel);
        }

        // ================================================================== config
        /// <summary>Binds every entry of the module (character part, then World), then the PlayerBody shell parts that read character entries.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            Instance = this;
            CharacterSettings.Bind(cfg);
            BindWorld(cfg);
        }

        // ================================================================== lifecycle
        /// <summary>Starts the module: character data and patches, the World part, then the crawl hooks, jobs, NPC visual parts and Women of Wasteland.</summary>
        public override void Init()
        {
            Instance = this;
            _running = true;
            ModApi.Provide<ICharacterHudData>(this, new CharacterHudData());
            InitCharacterData();
            InitWorld();
            InitCharacterJobs();
            InitNpcVisuals();
            WomenFeature.Initialize(this);
            Log.Always(this, "Player body & animation (Apocaplayer " + SourceVersion + ") ready; assets in " + AssetDir);
        }

        /// <summary>Init step: clean body state, default pose profiles, weapon poses, aim lift, pain sounds and the ADS / pain-sound patches.</summary>
        void InitCharacterData()
        {
            BodyRunner.ResetAll();
            DefaultProfiles.Install(DataDir);
            GunPose.Bind();
            AimLift.Load(DataDir);
            Scheduler.StartCoroutine(FemalePain.LoadClips());
            Scheduler.StartCoroutine(Anims.Preload());
            AimTransition.Patch(this);
            FemalePain.Patch(this);
        }

        /// <summary>Init step: the Low Crawl hooks (after the World part's third-person GetButtonDown prefix) and the body / character jobs.</summary>
        void InitCharacterJobs()
        {
            Crawling.Init(this);
            WeaponPoseService.Reset();
            ModEvents.Subscribe<Core.Events.WeaponSwitchPhase>(this, WeaponPoseService.OnSwitchPhase);
            ModEvents.Subscribe<Core.Events.AdsChanged>(this, WeaponPoseService.OnAdsChanged);
            ModEvents.Subscribe<Core.Events.CharacterAction>(this, _characters.OnCharacterAction);
            Scheduler.EveryFrame(this, "Presentation.WeaponLoweringRestore", WeaponPoseService.Restore, Phase.EarlyUpdate);
            Scheduler.EveryFrame(this, "Presentation.BodyUpdate", BodyRunner.Tick, Phase.Update);
            Scheduler.EveryFrame(this, "Presentation.BodyLate", BodyRunner.LateTick, Phase.LateUpdate);
            Scheduler.EveryFrame(this, "Presentation.Characters", BodyRunner.ModApiTick, Phase.LateUpdate);
            Scheduler.EveryFrame(this, "Presentation.VisualOffsets", _characters.LateTick, Phase.LateUpdate);
            Scheduler.EveryFrame(this, "Presentation.WeaponLowering", WeaponPoseService.Pose, Phase.LateUpdateLast);
        }

        /// <summary>Init step: the dog animation bundle hooks, the NPC rig builder and the visual LateUpdate ticks of burrowing / pouncing / biting animals, the carcass and taunt hooks.</summary>
        void InitNpcVisuals()
        {
            DogAnimations.Initialize(this);
            Scheduler.Sliced(this, "Presentation.NpcRigs", 1000, Brain.BuildRig, Phase.LateUpdate);
            Scheduler.EveryFrame(this, "Presentation.BurrowLate", Burrow.LateTick, Phase.LateUpdate);
            Scheduler.EveryFrame(this, "Presentation.PounceLate", Pounce.LateTick, Phase.LateUpdate);
            Scheduler.EveryFrame(this, "Presentation.DogBiteLate", DogBite.LateTick, Phase.LateUpdate);
            Hooks.CreateObjectEnter.Postfix(this, Pounce.AfterCreateObject);
            PatchAction(typeof(AudioPlay), "OnEnter", typeof(Senses), "BeforeAudioPlay", true);
        }

        /// <summary>Patches target.method with patchType.patchMethod as a prefix or postfix through Core.Patches (recorded for this module).</summary>
        internal bool PatchAction(Type target, string method, Type patchType, string patchMethod, bool prefix)
        {
            var original = AccessTools.Method(target, method);
            var mi = AccessTools.Method(patchType, patchMethod);
            return Core.Patches.Patch(this, original, prefix: prefix ? mi : null, postfix: prefix ? null : mi);
        }

        /// <summary>Stops the module: service withdrawn, poses saved, Women of Wasteland, the World part, then the body and the animated characters.</summary>
        public override void Shutdown()
        {
            ModApi.Withdraw<IAnimationApi>(this);
            ModApi.Withdraw<ICharacterPresentation>(this);
            ShutdownStep("saving poses", SavePoses);
            ShutdownStep("Women of Wasteland", WomenFeature.Shutdown);
            ShutdownWorld();
            ShutdownStep("body", BodyRunner.TearDown);
            ShutdownStep("aim transition", AimTransition.RestoreAll);
            ShutdownStep("weapon lowering", WeaponPoseService.Reset);
            _characters.Clear();
            ShutdownStep("ModAPI characters", ModAPI.OnSceneLoaded);
            _animation.Clear();
            ShutdownStep("dog animations", DogAnimations.Shutdown);
            _running = false;
        }

        /// <summary>Shutdown step: writes edited weapon poses and aim lift.</summary>
        static void SavePoses() { GunPose.Flush(); AimLift.SaveIfDirty(); }

        /// <summary>Runs one shutdown step; an exception is logged and the next step still runs.</summary>
        void ShutdownStep(string what, Action a)
        {
            try { a(); } catch (Exception e) { Log.Warn(this, "shutdown " + what + ": " + e.Message); }
        }

        /// <summary>Scene load: body state, ModAPI characters, service handles, ADS tweens and Gungirl caches dropped, then World.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BodyRunner.OnSceneLoaded();
            ModAPI.OnSceneLoaded();
            _animation.Clear();
            _characters.Clear();
            WeaponPoseService.Reset();
            AimTransition.Sweep();
            Gungirl.OnSceneLoaded();
            SceneLoadedWorld(scene, mode);
        }

        /// <summary>First isPlay after load/new game: Gungirl's world scan, then World.</summary>
        public override void OnWorldReady() { Gungirl.BeginScan(); WorldReadyWorld(); }

        /// <summary>Native save started.</summary>
        public override void OnSaveBegin(string slot) { SaveBeginWorld(slot); }

        /// <summary>Native save finished.</summary>
        public override void OnSaveEnd(string slot) { SaveEndWorld(slot); }

        /// <summary>Native load started.</summary>
        public override void OnLoadBegin(string slot) { LoadBeginWorld(slot); }

        /// <summary>Floating-origin shift (delta = old - new offset).</summary>
        public override void OnOriginShift(Vector3 delta) { OriginShiftWorld(delta); }

        /// <summary>Registers the ModAPI providers of the character part (IAnimationApi, ICharacterPresentation, ICharacterAppearance,
        /// IWeaponPose, IPoseAuthoring, ICharacterAssets, INpcAppearance while Women of Wasteland runs), then World's.</summary>
        public override void ProvideApis()
        {
            ModApi.Provide<ICrewAppearance>(this,new CrewAppearance());
            ModApi.Provide<IAnimationApi>(this, _animation);
            ModApi.Provide<ICharacterPresentation>(this, _characters);
            ModApi.Provide<ICharacterAppearance>(this, _appearance);
            ModApi.Provide<IWeaponPose>(this, _weaponPose);
            ModApi.Provide<IPoseAuthoring>(this, _poseAuthoring);
            WomenFeature.ProvideApis(this);
            ProvideApisWorld();
        }

        /// <summary>Contributes the PLAYER page rows (character choice, body/arms switches), then World's rows.</summary>
        public override void Settings(SettingsShell shell)
        {
            shell.Custom(this, SettingsShell.Page.Player, "", CharacterPages.DrawPlayer, 0);
            SettingsWorld(shell);
        }

        /// <summary>F7 pages: "Animation framework", "Body &amp; model", "Female appearance &amp; voices", then World's pages.</summary>
        public override IEnumerable<ToolPage> ToolPages()
        {
            var pages = new List<ToolPage>();
            CharacterPages.Add(this, pages);
            ToolPagesWorld(pages);
            return pages;
        }

        /// <summary>World part: config binds.</summary>
        partial void BindWorld(ModuleConfig cfg);
        /// <summary>World part: start.</summary>
        partial void InitWorld();
        /// <summary>World part: stop.</summary>
        partial void ShutdownWorld();
        /// <summary>World part: scene loaded.</summary>
        partial void SceneLoadedWorld(Scene scene, LoadSceneMode mode);
        /// <summary>World part: world ready.</summary>
        partial void WorldReadyWorld();
        /// <summary>World part: save begin.</summary>
        partial void SaveBeginWorld(string slot);
        /// <summary>World part: save end.</summary>
        partial void SaveEndWorld(string slot);
        /// <summary>World part: load begin.</summary>
        partial void LoadBeginWorld(string slot);
        /// <summary>World part: origin shift.</summary>
        partial void OriginShiftWorld(Vector3 delta);
        /// <summary>World part: ModAPI providers.</summary>
        partial void ProvideApisWorld();
        /// <summary>World part: settings rows.</summary>
        partial void SettingsWorld(SettingsShell shell);
        /// <summary>World part: tool pages.</summary>
        partial void ToolPagesWorld(List<ToolPage> pages);
    }
}
