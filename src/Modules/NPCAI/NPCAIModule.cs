using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Modules.Guns;
using WildWasteland.Modules.NPC;
using WildWasteland.Modules.Patrols;
using WildWasteland.Modules.Quests;
using WildWasteland.Modules.POI;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>
    /// NPCAI module entry (README 3.1, 6.2): NPC senses, brain, idle behaviour, animals, hazards, population rules, bosses and followers.
    /// Main owner B07 (Behaviour/Population); the Navigation part (grid nav, PoiNav, town routing) is owned by B08 and implemented in
    /// <c>src/Modules/NPCAI/Navigation/NPCAIModule.Navigation.cs</c> through the "Navigation" partial methods. The jobs keep the order of
    /// the old NPCAI.Runner (Senses -> Nav -> Brain -> Idle -> Burrow -> Pounce -> Webs -> Nests -> Ambush -> DogBite); the Navigation part
    /// registers its jobs right after Senses. The old NPC entry (NpcModule) is a shell for the parts other packages still own.
    /// </summary>
    public sealed partial class NPCAIModule : ModuleBase
    {
        /// <summary>GUID of the standalone NPCAI plugin this module replaces (both stay off while it is installed).</summary>
        public const string StandaloneGuid = "com.denis.apocalypter.npcai";
        static readonly string[] _conflicts = { StandaloneGuid };

        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "NPCAI"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "NPC AI"; } }

        /// <summary>The standalone NPCAI plugin replaces this module as before.</summary>
        public override string[] ConflictsWith { get { return _conflicts; } }

        bool _subscribed;

        /// <summary>Binds every entry of the module (main part, then Navigation).</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            NpcaiRuntime.Module = this;
            NpcaiSettings.Bind(cfg);
            BindNavigation(cfg);
        }

        /// <summary>Starts the module; a failed start is undone by Shutdown before the exception reaches ModuleHost.</summary>
        public override void Init()
        {
            try { Start(); }
            catch (Exception)
            {
                // a half-started module must not leave providers / event handlers / changed NPCs behind (Core only removes patches, jobs, hooks)
                try { Shutdown(); } catch (Exception) { }
                throw;
            }
        }

        /// <summary>Init body: registries reset, events, hooks, patches, then every job in the old NPCAI.Runner order.</summary>
        void Start()
        {
            NpcaiRuntime.Module = this;
            NpcaiRuntime.DataDir = Assets.ModuleConfigDir(Id);
            ResetAll();                                    // re-runnable after Shutdown: every registry starts empty

            Services.CrewBailedOut += OnCrewBailedOut;     // ex Senses.HookApocapatrol (Harmony on Apocapatrol.Patrol.BailOut / Explode.Blast)
            Services.VehicleExploded += OnVehicleExploded;
            SaveBus.StateChanged += Senses.OnSaveLoadState;   // ex Senses.Persist.Tick polling the SaveLoadGame FSM
            _subscribed = true;

            InstallHooks();
            InstallPatches();
            SeatedLosGuard.Install(this);                  // seated crews' target-less LOS actions (ex Patrols; after Senses on the same dispatchers)
            Bosses.Install(this);
            NpcWorld.Install(this);
            Brain.InstallHides(this);                      // "Hide" cover objects from a WorldIndex watch (no 10 s tag scan)                        // Health / ID FSM tracking for the follower and walker code (ex Quests QuestWorld)                          // human boss health: FsmIndex "BossUI" tracking (ex Guns entry)
            InteriorPopulation.Install(this);              // interior / native-POI population rules (ex Towns entry): hooks + jobs

            // the old NPCAI.Runner, in its order (load-bearing: Brain/Idle body hand-over, DogAnimations restore in Update / sample in LateUpdate)
            Scheduler.EveryFrame(this, "NPCAI.Senses", Senses.Tick);
            InitNavigation();                              // B08: "Nav" here (between Senses and Brain, as before), Passthrough, PoiNav jobs
            Scheduler.EveryFrame(this, "NPCAI.Brain", Brain.Tick);
            Scheduler.EveryFrame(this, "NPCAI.Idle", Idle.Tick);
            Scheduler.EveryFrame(this, "NPCAI.Burrow", Burrow.Tick);
            Scheduler.EveryFrame(this, "NPCAI.Pounce", Pounce.Tick);
            Scheduler.EveryFrame(this, "NPCAI.Webs", Webs.Tick);
            Scheduler.EveryFrame(this, "NPCAI.Nests", Nests.Tick);
            Scheduler.Interval(this, "NPCAI.Ambush", 0.5f, Ambush.Tick, Phase.Update, true);
            Scheduler.EveryFrame(this, "NPCAI.DogBite", DogBite.Tick);
            Scheduler.Interval(this, "NPCAI.Aim.Sweep", 10f, Aim.Sweep);
            Scheduler.Interval(this, "NPCAI.Bosses", 0.5f, Bosses.Job);

            // [Keys] Shout: Core owns the input (modifier blocks the game's buttons), NPCAI reacts to the PlayerShout event
            new PlayerShoutInput(this, NpcaiSettings.ShoutKey, NpcaiSettings.ShoutModifier, NpcaiSettings.ShoutBlocksButtons, NpcaiSettings.ShoutCooldown, NpcaiSettings.PlayerShoutRange, IsRunning);
            ModEvents.Subscribe<Core.Events.PlayerShout>(this, Senses.OnPlayerShout);
            ModEvents.Subscribe<Core.Events.NpcFrightened>(this, Brain.OnFrightened);   // Weapons Pack fire / chainsaw fear (B17-2)

            NpcaiRuntime.Running = true;
            NpcaiRuntime.Main("NPC Overhaul 1.9.15 port started");
        }

        /// <summary>Shout input gate: the module runs.</summary>
        static bool IsRunning() { return NpcaiRuntime.Running; }

        /// <summary>Stops the module: every NPC is handed back to the game (Navigation part first, then the behaviour in reverse dependency order).</summary>
        public override void Shutdown()
        {
            // first: every handler / On property sees the module off (FSM events sent below re-enter our hooks synchronously)
            NpcaiRuntime.Running = false;

            if (_subscribed)
            {
                Services.CrewBailedOut -= OnCrewBailedOut;
                Services.VehicleExploded -= OnVehicleExploded;
                SaveBus.StateChanged -= Senses.OnSaveLoadState;
                _subscribed = false;
            }
            Step("Population", InteriorPopulation.ReleaseDeferred);   // deferred native spawn events run before the hooks go (ex Towns shutdown)
            Step("Navigation", ShutdownNavigation);
            Step("Pounce", Pounce.Shutdown);
            Step("Burrow", Burrow.Shutdown);
            Step("DogBite", DogBite.Shutdown);          // + DogAnimations + Dog Companion drivers
            Step("Nests", Nests.Shutdown);
            Step("Webs", Webs.Shutdown);
            Step("Idle", Idle.Shutdown);
            Step("Brain", Brain.Shutdown);
            Step("Aim", Aim.Shutdown);
            Step("Senses", Senses.Shutdown);
            ResetAll();
        }

        /// <summary>Runs one shutdown/shift step; an exception is logged and the next step still runs.</summary>
        void Step(string what, Action a)
        {
            try { a(); } catch (Exception e) { Log.Error(this, "shutdown " + what, e); }
        }

        /// <summary>Clears every behaviour registry / cache (the old Plugin.SceneLoaded chain). Init and Shutdown call it too.</summary>
        static void ResetAll()
        {
            Aim.OnSceneLoaded(); Brain.OnSceneLoaded(); Senses.OnSceneLoaded(); Idle.OnSceneLoaded();
            Burrow.OnSceneLoaded(); Pounce.OnSceneLoaded(); Webs.OnSceneLoaded(); Nests.OnSceneLoaded(); Ambush.OnSceneLoaded(); DogBite.OnSceneLoaded();
            Bosses.OnSceneLoaded(); NpcWorld.Clear();
        }

        /// <summary>Scene load: every behaviour registry reset, then the Navigation part.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ResetAll(); SceneLoadedNavigation(scene, mode); }

        /// <summary>First isPlay after load/new game.</summary>
        public override void OnWorldReady() { WorldReadyNavigation(); }

        /// <summary>Native save started.</summary>
        public override void OnSaveBegin(string slot)
        {
            try { Senses.Persist.Save(slot); Nests.SaveWorld(slot); SaveBeginNavigation(slot); }
            catch (Exception e) { SaveBus.ReportFailure(this, "NPC AI world state save failed: " + e.Message); throw; }
        }

        /// <summary>Native save finished.</summary>
        public override void OnSaveEnd(string slot) { SaveEndNavigation(slot); }

        /// <summary>Native load started.</summary>
        public override void OnLoadBegin(string slot) { Nests.LoadWorld(slot); LoadBeginNavigation(slot); }

        /// <summary>
        /// Floating-origin shift (delta = old - new offset). The game's FloatingOrigin moves every scene root; the subsystems kept scene-space
        /// positions across frames, so they are translated here (plain Vector3 / Bounds data) or dropped where a rebuild is cheap.
        /// </summary>
        public override void OnOriginShift(Vector3 delta)
        {
            _shift = delta;
            Step("Senses.Shift", ShiftSenses);
            Step("Brain.Shift", ShiftBrain);
            Step("Idle.Shift", ShiftIdle);
            Step("Burrow.Shift", ShiftBurrow);
            Step("Pounce.Shift", ShiftPounce);
            Step("Nests.Shift", ShiftNests);
            Step("Webs.Shift", ShiftWebs);
            OriginShiftNavigation(delta);
        }

        static Vector3 _shift;
        /// <summary>Origin shift of the senses (ghosts, last-seen points).</summary>
        static void ShiftSenses() { Senses.Shift(_shift); }
        /// <summary>Origin shift of the brains (goals, trails).</summary>
        static void ShiftBrain() { Brain.Shift(_shift); }
        /// <summary>Origin shift of the idle controllers (homes, travel goals).</summary>
        static void ShiftIdle() { Idle.Shift(_shift); }
        /// <summary>Origin shift of the burrowers (dive / rise points).</summary>
        static void ShiftBurrow() { Burrow.Shift(_shift); }
        /// <summary>Origin shift of the pouncers (leap points).</summary>
        static void ShiftPounce() { Pounce.Shift(_shift); }
        /// <summary>Origin shift of the nests (centres, web spots).</summary>
        static void ShiftNests() { Nests.Shift(_shift); }
        /// <summary>Origin shift of the webs.</summary>
        static void ShiftWebs() { Webs.Shift(_shift); }

        /// <summary>Old Patrols event (CrewBailedOut): the crew keeps what its car knew.</summary>
        static void OnCrewBailedOut(GameObject car, GameObject pax, GameObject npc) { if (NpcaiRuntime.Running) Senses.AfterBailOut(car, pax, npc); }
        /// <summary>Old Patrols event (VehicleExploded): the blast is heard.</summary>
        static void OnVehicleExploded(GameObject car) { if (NpcaiRuntime.Running) Senses.AfterBlast(car); }

        /// <summary>Core.Hooks subscriptions; per dispatcher the subscriber order is the old one.</summary>
        void InstallHooks()
        {
            // SendEvent.OnEnter: Brain (Animal_Run while holding / Animal_rotateRandom) then Aim (Activate = the burst) - NPCAI's patch order
            Hooks.SendEventEnter.Prefix(this, Brain.BeforeSendEvent);
            Hooks.SendEventEnter.Prefix(this, Aim.BeforeSendEvent);
            // SetVelocity.DoSetVelocity: Brain (the pedal; registers NPCs); DogAnimations follows (Presentation, NPC shell)
            Hooks.SetVelocityDo.Prefix(this, Brain.BeforeSetVelocity);
            // CreateObject.OnEnter postfixes: Senses (structures, pending rigs, blasts), Idle (camp spawn points)
            Hooks.CreateObjectEnter.Postfix(this, Senses.AfterCreateObject);
            Hooks.CreateObjectEnter.Postfix(this, Idle.AfterCreateObject);
            Hooks.CreateObjectEnter.Postfix(this, Bosses.AfterCreateObject);   // spawned human bosses (ex Guns entry)
            // SetFsmFloat.OnEnter postfix: a real dog bite -> DogBite's lunge (vanilla hits are Gunplay's, subscribed by GunplayModule)
            Hooks.SetFsmFloatEnter.Postfix(this, DogBite.AfterSetFsmFloat);
            // Raycast.DoRaycast = the Attack FSM's bumper rays
            Hooks.RaycastDo.Prefix(this, Brain.BeforeRaycast);
            Hooks.SmoothLookAtDo.Prefix(this, Brain.BeforeSmoothLookAt);
            Hooks.ProcessEvent.Prefix(this, Brain.BeforeDrivingHide);
            Hooks.ActivateGameObjectEnter.Postfix(this, Brain.AfterActivate);
            // SensorToolkit: the Detection FSM (registers agents), the burst LOS
            Hooks.SensorDetectionsDo.Prefix(this, Senses.BeforeGetDetections);
            Hooks.SensorDetectionsDo.Prefix(this, Bosses.BeforeGetDetections);   // boss registration by detection (ex Guns entry)
            Hooks.LosEnter3D.Prefix(this, Senses.BeforeLosResult);
            Hooks.LosUpdate3D.Prefix(this, Senses.BeforeLosResult);
        }

        /// <summary>Module-only patches (targets no other module patches; listed on the F7 "Harmony patches" page).</summary>
        void InstallPatches()
        {
            Patch(typeof(Rotate), "DoRotate", typeof(Brain), "BeforeRotate", true);
            Patch(typeof(LookAt), "DoLookAt", typeof(Brain), "BeforeLookAt", true);
            Patch(typeof(AddForce), "DoAddForce", typeof(Brain), "BeforeAddForce", true);
            Patch(typeof(RandomWait), "OnEnter", typeof(Aim), "BeforeRandomWait", true);
            var multi = AccessTools.TypeByName("HutongGames.PlayMaker.Actions.ActivateGameObjects");      // optional (not in every PlayMaker build)
            if (multi != null) Patch(multi, "OnEnter", typeof(Brain), "AfterActivate", false);
        }

        /// <summary>Patches target.method with patchType.patchMethod as a prefix or postfix through Core.Patches (recorded per module).</summary>
        bool Patch(Type target, string method, Type patchType, string patchMethod, bool prefix)
        {
            var original = AccessTools.Method(target, method);
            var mi = AccessTools.Method(patchType, patchMethod);
            return Core.Patches.Patch(this, original, prefix: prefix ? mi : null, postfix: prefix ? null : mi);
        }

        /// <summary>Registers ModAPI providers: the NPC API, the private diagnostics source for Debug, then the Navigation part.</summary>
        public override void ProvideApis()
        {
            var api = new NpcApi();
            ModApi.Provide<INpcApi>(this, api);
            ModApi.Provide<INpcControl>(this, api);
            ModApi.Provide<ISeatedCombat>(this, new SeatedCombatApi());
            ModApi.Provide<IFollowers>(this, new Hire.FollowersApi());
            ModApi.Provide<IPopulation>(this, new PopulationApi());
            ModApi.Provide<INpcDiagnosticSource>(this, new NpcDiagnosticSource());
            ProvideApisNavigation();
        }

        /// <summary>Contributes the shout key row to the shared HUD settings page, then the Navigation rows.</summary>
        public override void Settings(SettingsShell shell)
        {
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawShoutRow, 170);
            SettingsNavigation(shell);
        }

        /// <summary>Draws the shout key + modifier row.</summary>
        static void DrawShoutRow(ToolLayout l) { l.KeyPair("Shout", NpcaiSettings.ShoutKey, "Shout modifier", NpcaiSettings.ShoutModifier); }

        /// <summary>F7 pages of the module (live values + tuning), then the Navigation pages.</summary>
        public override IEnumerable<ToolPage> ToolPages()
        {
            var pages = new List<ToolPage>();
            NpcPages.Build(this, pages);
            ToolPagesNavigation(pages);
            return pages;
        }

        /// <summary>Navigation part: config binds.</summary>
        partial void BindNavigation(ModuleConfig cfg);
        /// <summary>Navigation part: start (registers its jobs right after the Senses job).</summary>
        partial void InitNavigation();
        /// <summary>Navigation part: stop (first shutdown step).</summary>
        partial void ShutdownNavigation();
        /// <summary>Navigation part: scene loaded.</summary>
        partial void SceneLoadedNavigation(Scene scene, LoadSceneMode mode);
        /// <summary>Navigation part: world ready.</summary>
        partial void WorldReadyNavigation();
        /// <summary>Navigation part: save begin.</summary>
        partial void SaveBeginNavigation(string slot);
        /// <summary>Navigation part: save end.</summary>
        partial void SaveEndNavigation(string slot);
        /// <summary>Navigation part: load begin.</summary>
        partial void LoadBeginNavigation(string slot);
        /// <summary>Navigation part: origin shift.</summary>
        partial void OriginShiftNavigation(Vector3 delta);
        /// <summary>Navigation part: ModAPI providers.</summary>
        partial void ProvideApisNavigation();
        /// <summary>Navigation part: settings rows.</summary>
        partial void SettingsNavigation(SettingsShell shell);
        /// <summary>Navigation part: tool pages.</summary>
        partial void ToolPagesNavigation(List<ToolPage> pages);
    }
}
