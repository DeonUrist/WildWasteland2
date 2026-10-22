using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// World part of <see cref="PresentationModule"/> (Wave B owner B06): the camera rig (third person, observing, dynamic crosshair, pick
    /// assist, camera culling, end-of-frame view reset), the traversal engine (ledge climbing, ladders, NPC traversal animations), seat poses
    /// (player driver's seat and steering-wheel arms, crew / bus / turret / rider / follower poses) and faction vehicle skins. Owns their config
    /// (<c>[Presentation / Camera|Camera culling|Climbing|Car seat|Seat pose|Turret pose|Vehicle skins]</c>), hooks, jobs, F7 pages and the
    /// ModAPI providers <see cref="ICameraRig"/>, <see cref="ITraversal"/>, <see cref="ISeatPoses"/>, <see cref="IVehicleSkins"/>.
    /// The main part (B05) calls each partial method after its own work (Shutdown: World first).
    /// </summary>
    public sealed partial class PresentationModule
    {
        /// <summary>The module instance, set at bind time; owner of the World part's hooks, jobs, logs and events.</summary>
        internal static PresentationModule WorldOwner;
        /// <summary>True between the World part's Init and Shutdown (the camera / traversal / seat parts act only while set).</summary>
        internal static bool WorldRunning;

        static CameraRigApi _cameraRig;
        static TraversalApi _traversal;
        static SeatPosesApi _seats;
        static VehicleSkinsApi _skins;

        // ================================================================== config helpers

        /// <summary>Binds a settings.cfg entry in <c>[Presentation / sub]</c> whose default is the captured tuning default of the old section (D12: values are not migrated).</summary>
        internal static ConfigEntry<T> WorldPlayer<T>(ModuleConfig cfg, string sub, string oldSection, string key, T def, string desc)
        {
            return TunedSettings.Bind(cfg.Main, cfg.Section(sub), key, TunedSettings.Default(cfg.Main, oldSection, key, def), desc);
        }

        /// <summary>Binds a tools.cfg entry in <c>[Presentation / sub]</c> whose default is the captured tuning default of the old section (D12: values are not migrated).</summary>
        internal static ConfigEntry<T> WorldTool<T>(ModuleConfig cfg, string sub, string oldSection, string key, T def, string desc)
        {
            return TunedSettings.Bind(cfg.Tools, cfg.Section(sub), key, TunedSettings.Default(cfg.Tools, oldSection, key, def), desc);
        }

        /// <summary>Like <see cref="WorldTool{T}(ModuleConfig,string,string,string,T,string)"/> with an acceptable-value range or list.</summary>
        internal static ConfigEntry<T> WorldTool<T>(ModuleConfig cfg, string sub, string oldSection, string key, T def, string desc, AcceptableValueBase range)
        {
            return TunedSettings.Bind(cfg.Tools, cfg.Section(sub), key, TunedSettings.Default(cfg.Tools, oldSection, key, def), new ConfigDescription(desc, range));
        }

        // ================================================================== lifecycle

        /// <summary>World part: binds camera, traversal, car seat, seat pose and vehicle skin entries (old relative order).</summary>
        partial void BindWorld(ModuleConfig cfg)
        {
            WorldOwner = this;
            CameraSettings.Bind(cfg);
            TraversalSettings.Bind(cfg);
            CarSeatSettings.Bind(cfg);
            SeatPoseSettings.Bind(cfg);
            VehicleSkinSettings.Bind(cfg);
        }

        /// <summary>
        /// World part start: climbing / ladder clips, camera hooks and patches, vehicle skin textures, then the jobs in the old order
        /// (climbing and ladders in EarlyUpdate / EarlyFixedUpdate, steering-wheel arms then NPC traversal poses in LateUpdateLast) and the
        /// end-of-frame camera reset loop. Context-polled keys are registered for the [Keys] conflict report.
        /// </summary>
        partial void InitWorld()
        {
            WorldOwner = this;
            WorldRunning = true;
            ClimbingController.Init();
            LadderController.Init();
            // hooks run for every GetButtonDown / ActivateGameObject / SmoothLookAt of every FSM: each handler starts with a cheap filter
            Hooks.GetButtonDownUpdate.Prefix(this, ThirdPerson.BeforeGetButtonDown);
            Hooks.SmoothLookAtDo.Prefix(this, FirstPersonSway.BeforeSmoothLookAt);
            Hooks.DoMousePick.Postfix(this, PickAssist.AfterDoMousePick);
            Patches.Patch(this, Patches.Method(typeof(HutongGames.PlayMaker.Actions.MouseLook), "OnUpdate"), prefix: Patches.Method(typeof(ThirdPerson), "BeforeMouseLook"));
            Paint.Configure(VehicleSkinSettings.TextureDir());
            Paint.Preload();

            Scheduler.EveryFrame(this, "Presentation.Climbing", ClimbingController.TickInstance, Phase.EarlyUpdate);
            Scheduler.EveryFrame(this, "Presentation.Climbing (physics)", ClimbingController.FixedTickInstance, Phase.EarlyFixedUpdate);
            Scheduler.EveryFrame(this, "Presentation.Ladders", LadderController.TickInstance, Phase.EarlyUpdate);
            Scheduler.EveryFrame(this, "Presentation.Ladders (physics)", LadderController.FixedTickInstance, Phase.EarlyFixedUpdate);
            Scheduler.EveryFrame(this, "Presentation.WheelArms", BodyRunner.WheelTick, Phase.LateUpdateLast);
            Scheduler.EveryFrame(this, "Presentation.NPC traversal poses", NpcTraversal.LateTick, Phase.LateUpdateLast);
            CameraEndOfFrame.Start();
            Hotkeys.Register(this, CameraSettings.ObserveKey, NoKeyAction, NeverPressed);
            Hotkeys.Register(this, TraversalSettings.ClimbKey, NoKeyAction, NeverPressed);
            if (Log.L1) Log.Main(this, "World part ready: camera rig, traversal, seat poses, vehicle skins");
        }

        /// <summary>Hotkeys.Register action for keys polled in context (registered only for the conflict report).</summary>
        static void NoKeyAction() { }

        /// <summary>Hotkeys.Register condition that never fires (see <see cref="NoKeyAction"/>).</summary>
        static bool NeverPressed() { return false; }

        /// <summary>World part stop: running climbs, ladders and NPC traversals end, the end-of-frame loop stops, camera caches are cleared.</summary>
        partial void ShutdownWorld()
        {
            try { ClimbingController.StopInstance(); } catch (Exception e) { Log.Warn(this, "climbing: " + e.Message); }
            try { LadderController.StopInstance(); } catch (Exception e) { Log.Warn(this, "ladders: " + e.Message); }
            try { NpcTraversal.Clear(); } catch (Exception e) { Log.Warn(this, "NPC traversals: " + e.Message); }
            if (_traversal != null) _traversal.Clear();
            CameraEndOfFrame.Stop();
            FirstPersonSway.Clear();
            ThirdPerson.ClearCaches();
            WorldRunning = false;
        }

        /// <summary>World part scene load: the end-of-frame loop again if Core recreated its runner, third-person FSM caches and sway records swept.</summary>
        partial void SceneLoadedWorld(Scene scene, LoadSceneMode mode)
        {
            CameraEndOfFrame.Start();
            ThirdPerson.ClearCaches();
            FirstPersonSway.Sweep();
            if (_traversal != null) _traversal.Clear();
        }

        /// <summary>World part world ready: builds the shared ladder pose clip now (under the loading screen) instead of on the first ladder (README 8#6).</summary>
        partial void WorldReadyWorld()
        {
            if (TraversalSettings.Ladders != null && TraversalSettings.Ladders.Value) PoseClip.Shared("ClimbingLadder");
        }

        /// <summary>World part floating-origin jump: the world-space climb / ladder paths move with the world.</summary>
        partial void OriginShiftWorld(Vector3 delta)
        {
            ClimbingController.ShiftInstance(delta);
            LadderController.ShiftInstance(delta);
        }

        /// <summary>World part ModAPI: provides the camera rig, traversal, seat pose and vehicle skin services.</summary>
        partial void ProvideApisWorld()
        {
            ModApi.Provide<ICameraModInterop>(this,new CameraModInterop());
            if (_cameraRig == null) _cameraRig = new CameraRigApi();
            if (_traversal == null) _traversal = new TraversalApi();
            if (_seats == null) _seats = new SeatPosesApi();
            if (_skins == null) _skins = new VehicleSkinsApi();
            ModApi.Provide<ICameraRig>(this, _cameraRig);
            ModApi.Provide<ITraversal>(this, _traversal);
            ModApi.Provide<ISeatPoses>(this, _seats);
            ModApi.Provide<IVehicleSkins>(this, _skins);
        }

        /// <summary>World part save begin: nothing to do (no world-part state is saved).</summary>
        partial void SaveBeginWorld(string slot) { }

        /// <summary>World part save end: nothing to do.</summary>
        partial void SaveEndWorld(string slot) { }

        /// <summary>World part load begin: nothing to do (scene load clears the caches).</summary>
        partial void LoadBeginWorld(string slot) { }

        /// <summary>World part settings rows: none (the PLAYER page rows for climbing / third person / occlusion are drawn by the main part).</summary>
        partial void SettingsWorld(SettingsShell shell) { }

        /// <summary>World part F7 pages: Camera and Climbing (group "Player body &amp; animation", old page order).</summary>
        partial void ToolPagesWorld(List<ToolPage> pages)
        {
            pages.Add(new ActionPage(this, ToolGroups.Player, "Camera", CameraPage.Draw));
            pages.Add(new ActionPage(this, ToolGroups.Player, "Climbing", ClimbingPage.Draw));
        }

        /// <summary>The traversal provider of the World part (null before ProvideApis); used by the ladder controller for NPC occupants.</summary>
        internal static TraversalApi Traversal { get { return _traversal; } }
    }
}
