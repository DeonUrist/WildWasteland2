using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.HUD
{
    public sealed partial class HudModule : IVehicleStatusDisplay, ICompassMarkers
    {
        internal const string GUID = "com.denis.apocalypter.cleanhud";
        internal static ConfigEntry<bool> CleanHudEnabled, ShowNeeds, ShowHealthNumbers, ShowInsanity, MenuEnabled, CleanFont, EnableCheats;
        internal static ConfigEntry<float> Scale;
        internal static ConfigEntry<bool> ShowConvoys, ShowLocations;
        internal static ConfigEntry<bool> SimpleMapIcons;
        internal static ConfigEntry<bool> DamageDirection;
        internal static ConfigEntry<bool> ShowAmmo;
        internal static ConfigEntry<bool> VehicleStatusHint, VehicleHotkeyHint;
        internal static ConfigEntry<bool> AmbiguousPatrols, AmbiguousLocations;
        internal static ConfigEntry<bool> ShowCompass, MapEnabled, DrivingHudOnlyThirdPerson;
        internal static ConfigEntry<MapDetail> MapMarkers, MapRoutes, MapTrafficMarkers;
        internal static ConfigEntry<bool> FogOfWar;
        internal static ConfigEntry<float> FogRadius, FogEdge, FogOpacity, BinocularZoom;
        internal static LayoutFile Layout;
        private HudView _hud;
        private Menu _menu;
        private NewGameSetup _newGameSetup;
        private LoadingScreen _loadingScreen;
        private MapMarkers _questMap;
        private float _retry;
        private Job _attach;
        internal static bool Running;
        private static readonly Dictionary<string, float> _throttle = new Dictionary<string, float>();


        /// <summary>Binds HUD feature, layout, map and cockpit presentation settings.</summary>
        private void BindCleanConfig(ModuleConfig cfg)
        {
            VehicleStatusHint = cfg.Player("Vehicles", "VehicleStatusHint", true, "Show ignition off, handbrake and cassette state while driving.");
            VehicleHotkeyHint = cfg.Player("Vehicles", "VehicleHotkeyHint", true, "Show cockpit key hints while driving.");
            Instance = this;
            CleanHudEnabled = cfg.Player("", "Clean HUD", true, "Replace the native HUD. Off restores it immediately.");
            ShowNeeds = cfg.Player("HUD", "ShowNeeds", true, "Show water, food and sleep beneath HP and armor.");
            ShowHealthNumbers = cfg.Player("HUD", "ShowHealthNumbers", true, "Show HP and armor values inside their bars.");
            ShowAmmo = cfg.Player("HUD", "ShowAmmo", true, "Show loaded ammunition, reserve ammunition and grenade counts.");
            ShowInsanity = cfg.Player("HUD", "ShowInsanity", true, "Show insanity in the top right while above zero.");
            DamageDirection = cfg.Player("HUD", "DamageDirection", true, "Show an opaque red arrow toward each attacker for two seconds after a hit. Repeated hits refresh one arrow. Requires NPC AI and Clean HUD.");
            ShowConvoys = cfg.Player("HUD", "ShowConvoys", true, "Compass icons and distances for vehicle groups: merchants yellow, Coyotes green, patrols red and convoys orange.");
            ShowLocations = cfg.Player("HUD", "ShowLocations", true, "Compass icons and distances for native locations within 1 km.");
            SimpleMapIcons = cfg.Player("HUD", "Simple map icons", false, "Use generic location icons on the map and compass instead of location-specific pictures.");
            SimpleMapIcons.SettingChanged += SimpleIconsChanged;
            AmbiguousPatrols = cfg.Player("HUD", "AmbiguousPatrols", true, "Show all compass vehicle groups with white vehicle icons, keeping their distances.");
            AmbiguousLocations = cfg.Player("HUD", "AmbiguousLocations", true, "Show undiscovered compass locations as neutral question marks. Approaching within 100 m or spotting through binoculars remembers their real icons and colors. Quest flags are unchanged.");
            ShowCompass = cfg.Player("HUD", "Compass", true, "Show the compass, including its direction strip and all markers.");
            DrivingHudOnlyThirdPerson = cfg.Player("HUD", "DrivingHudOnlyThirdPerson", true, "Show the gear/RPM/speed HUD only while driving in third person. Off allows either camera view; the source HUD enable switch still applies.");
            MapEnabled = cfg.Player("Map", "Enabled", true, "Enable the native debug map on the game's Map key.");
            MapMarkers = cfg.Player("Map", "Markers", MapDetail.Ambiguous, "Off hides map markers; Ambiguous shows question marks until locations are discovered within 100 m or spotted through binoculars; On shows their icons and colors. Discoveries are saved. Map icons have no name labels.");
            MapTrafficMarkers = cfg.Player("Map", "TrafficMarkersMode", MapDetail.Ambiguous, "World traffic markers: moving dots and names of patrols, convoys, merchants and foot travelers. Off hides them; Ambiguous shows gray Unknown; On shows their colors and names. Independent of location markers; fog of war still applies.");
            MapRoutes = cfg.Player("Map", "Routes", MapDetail.Off, "Route lines of world traffic only (the moving markers are World traffic markers). Off hides them; Ambiguous draws them gray; On in faction colors.");
            FogOfWar = cfg.Player("Map", "FogOfWar", true, "Hide the parts of the map you have not explored yet. Exploring is recorded while this is off.");
            FogRadius = cfg.Player("Map", "FogRevealRadius", 500f, "Base range in meters. Exploration reveals twice this radius; high ground extends it up to threefold (4 km radial cap), with raised binoculars doubling the viewed cone again.", new AcceptableValueRange<float>(50f, 2000f));
            FogEdge = cfg.Player("Map", "FogEdgeFade", 200f, "Width in meters of the soft edge, measured inward from the reveal radius. 0 = hard edge.", new AcceptableValueRange<float>(0f, 1000f));
            FogOpacity = cfg.Player("Map", "FogOpacity", 1f, "1 hides unexplored areas completely; lower values leave them faintly visible.", new AcceptableValueRange<float>(0f, 1f));
            BinocularZoom = cfg.Player("Map", "BinocularZoom", 1f, "Remembered additional binocular magnification. Scroll while binoculars are raised to change it from 1x to 2x.", new AcceptableValueRange<float>(1f, 2f));
            MenuEnabled = cfg.Player("Menu", "Enabled", true, "Replace the title and pause menus with CleanHUD's menus.");
            CleanFont = UiTheme.CleanFont;
            // Shared accent/font entries are bound once by Core UI before module binding.
            EnableCheats = cfg.Player("Menu", "EnableCheats", false, "Add the game's cheat controls to the menu settings.");
            Scale = cfg.Player("HUD", "Scale", 1f, "HUD scale; each panel grows away from its screen edge.", new AcceptableValueRange<float>(0.5f, 1.5f));
            CleanHudEnabled.SettingChanged += HudChanged;
            MenuEnabled.SettingChanged += MenuChanged;
            MapEnabled.SettingChanged += MapChanged;
        }

        /// <summary>Starts clean HUD services, native observers and owner-scoped display jobs.</summary>
        private void InitClean()
        {
            Running = true; _retry = 0; _throttle.Clear();

            MessageHook.Clear(); Bindings.Clear(); Menu.BlockCancelFrame = -1;
            var dir = Core.Assets.ModuleConfigDir("HUD");
            Core.TunedSettings.InstallProfiles("HUD");
            Layout = new LayoutFile(Path.Combine(dir, "layout.json"));
            Layout.Poll(true);
            InitCoreAssets();
            Binoculars.Init(this);
            VistasBanners.Init(this);

            Scheduler.Interval(this, "Vantage terrain", .2f, VantageDiscovery.Tick);
            LocationMarkers.Init();
            MapFog.Init();
            _loadingScreen = new LoadingScreen();
            ModEvents.Subscribe<Core.Events.SavePresentation>(this, SavePresented);

            _newGameSetup = new NewGameSetup(this);
            _menu = new Menu(_loadingScreen, _newGameSetup);
            InitCoreUpdateCheck();
            SaveBus.StateChanged += _loadingScreen.StateChanged;
            Core.ModApi.Provide<IVehicleStatusDisplay>(Core.ModuleHost.Current,this);
            Core.ModApi.Provide<ICompassMarkers>(Core.ModuleHost.Current,this);
            _questMap = new MapMarkers(); Core.ModApi.Provide<IMapMarkers>(Core.ModuleHost.Current,_questMap);

            QuestNotifications.Install(this);
            QuestTracker.Install(this);
            QuestJournal.Install(this);
            Core.Patches.Patch(this, AccessTools.Method(typeof(UiTextSetText), "OnEnter"), postfix: AccessTools.Method(typeof(MessageHook), "Postfix"));
            Hooks.GetButtonDownUpdate.Prefix(this, CancelHook.Prefix);
            Hooks.EnterState.Postfix(this, _loadingScreen.AfterEnterState);
            InitCoreSceneJob();
            Scheduler.Sliced(this, "Weapon icons", 500, Assets.RenderStep);
            Scheduler.Interval(this, "Layout reload", 2f, PollLayout);
            Scheduler.EveryFrame(this, "Inventory window", InventoryStyle.Tick, Phase.LateUpdateLast);
            Scheduler.Interval(this, "Location markers", .5f, LocationMarkers.Tick);
            Scheduler.Interval(this, "Quest map lookup", 1f, _questMap.FindMap);
            Scheduler.EveryFrame(this, "Quest map markers", _questMap.Tick, Phase.LateUpdateLast);
            Scheduler.EveryFrame(this, "Weather map areas", TickWeatherMap, Phase.LateUpdateLast);
            Scheduler.Gui(this, "Player map marker", _questMap.DrawPlayer, _questMap.PlayerShown);
            Scheduler.Interval(this, "Map fog exploration", .2f, MapFog.Paint);
            Scheduler.Sliced(this, "Map fog painting", 500, MapFog.PaintStep);

            Scheduler.EveryFrame(this, "Quest journal", QuestJournal.Tick, Phase.LateUpdateLast);
            Scheduler.EveryFrame(this, "Quest dialogue", QuestDialogue.Tick, Phase.LateUpdateLast);
            _attach = Scheduler.Interval(this, "HUD attachment", 1f, AttachHud, Phase.LateUpdateLast);
            _attach.RunNow();
            Scheduler.EveryFrame(this, "HUD", TickHud, Phase.LateUpdateLast);
            Scheduler.EveryFrame(this, "Menus", _menu.Tick, Phase.LateUpdateLast);
            Scheduler.EveryFrame(this, "Loading screen", _loadingScreen.Tick, Phase.LateUpdateLast);
            Core.Log.Always(null,"[CleanHUD] CleanHUD 1.5.1 integrated; layout: " + Layout.Path);
        }

        /// <summary>Forwards save phase changes to HUD-owned loading presentation.</summary>
        private void SavePresented(ref Core.Events.SavePresentation presentation) { if (_loadingScreen != null) _loadingScreen.Present(ref presentation); }
        private readonly WeatherMap _weatherMap = new WeatherMap();
        /// <summary>Refreshes storm presentation on the map through the typed weather provider.</summary>
        private void TickWeatherMap() { _weatherMap.Tick(_questMap); }
        /// <summary>Reloads user-edited HUD layout data when its polling deadline permits.</summary>
        private void PollLayout() { Layout.Poll(false); }
        /// <summary>Applies changed map visibility while the HUD feature is running.</summary>
        private void MapChanged(object sender, EventArgs args) { if (Running && _questMap != null) _questMap.ApplyEnabled(); }
        /// <summary>Refreshes marker artwork when the simple-icon option changes.</summary>
        private void SimpleIconsChanged(object sender, EventArgs args) { if (Running) LocationMarkers.RefreshIcons(); }
        /// <summary>Retries native HUD attachment within the existing scene readiness and retry gates.</summary>
        private void AttachHud()
        {

            if (!KeyCatalog.ScanStarted) KeyCatalog.StartScan();
            if (!CleanHudEnabled.Value || Time.unscaledTime < _retry) return;
            if (_hud != null && !_hud.Valid) Release();
            if (_hud == null) _hud = HudView.TryCreate(Layout);
        }
        /// <summary>Advances the attached HUD and clean menu while retaining native fallback behavior.</summary>
        private void TickHud()
        {
            if (!CleanHudEnabled.Value) return;
            if (_hud != null && !_hud.Valid) Release();
            if (_hud == null) return;
            try { _hud.Tick(); }
            catch { Release(); _retry = Time.unscaledTime + 5f; throw; }
        }
        /// <summary>Disposes the attached replacement HUD so the native view can be restored.</summary>
        private void Release() { if (_hud != null) { _hud.Dispose(); _hud = null; } }
        /// <summary>Updates cockpit status in the clean HUD, allowing the caller to fall back when unavailable.</summary>
        public bool TryShow(bool ignitionOff, bool handbrakeOn, bool playing, string volume)
        {
            if (!Running || !CleanHudEnabled.Value || _hud == null || !_hud.Valid) return false;
            _hud.SetVehicleStatus(ignitionOff, handbrakeOn, playing, volume);
            return true;
        }
        bool ICompassMarkers.VehiclesEnabled { get { return Running && CleanHudEnabled.Value && ShowCompass.Value && ShowConvoys.Value; } }
        int ICompassMarkers.Add(string owner, Func<Vector3> position, string icon, Color color)
        { return Api.Compass.Add(owner, position, new Api.Marker { Icon = icon, Color = color, ShowDistance = true, MaxDistance = owner == "WildWasteland.VehicleGroups" ? 1000f : 0f }); }
        bool ICompassMarkers.Exists(int id) { return Api.Compass.Exists(id); }
        bool ICompassMarkers.Remove(int id) { return Api.Compass.Remove(id); }
        void ICompassMarkers.Identify(int id) { foreach (var entry in Api.Compass.Entries) if (entry.Id == id) { entry.Identified = true; break; } }
        void ICompassMarkers.RemoveAll(string owner) { Api.Compass.RemoveAll(owner); }
        /// <summary>Applies the clean HUD feature switch and restores native graphics when disabled.</summary>
        private void HudChanged(object sender, EventArgs args)
        {
            if (!Running) return;
            if (!CleanHudEnabled.Value) Release(); else if (_attach != null) _attach.RunNow();
            if (_questMap != null) _questMap.FindMap();
        }
        /// <summary>Applies the menu feature switch and restores native menu controls when disabled.</summary>
        private void MenuChanged(object sender, EventArgs args)
        {
            if (!Running || MenuEnabled.Value) return;
            if (_menu != null) _menu.Teardown();
            if (_loadingScreen != null) _loadingScreen.End();
        }
        /// <summary>Resolves scene loaded clean for the clean HUD feature.</summary>
        private void SceneLoadedClean(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex == 0 && _newGameSetup != null) _newGameSetup.Clear();
            _weatherMap.Clear();
            Binoculars.Reset();
            VantageDiscovery.Reset(); VistasBanners.Reset();
            InventoryStyle.Restore();
            LocationMarkers.Begin(false);
            if (_questMap != null) _questMap.SceneLoaded();  QuestJournal.Hide(); QuestDialogue.Hide();
            Release(); _menu.Teardown(); SceneResetCore(); MessageHook.Clear(); Bindings.Clear(); Assets.SceneLoaded();
            _loadingScreen.SceneLoaded();
            _retry = 0; _attach.RunNow();
        }
        /// <summary>Clears scene-dependent UI and discoveries before loading another save.</summary>
        private void LoadBeginClean(string slot) { VantageDiscovery.Reset(); VistasBanners.Reset(); InventoryStyle.Restore(); LocationMarkers.Begin(false); MapFog.Reset(); QuestJournal.Hide(); _loadingScreen.Begin(); Release(); Bindings.Clear(); MessageHook.Clear(); Assets.SceneLoaded(); }
        /// <summary>Adjusts fog and discovery sampling after the native floating-origin jump.</summary>
        private void OriginShiftClean(Vector3 delta) { MapFog.OriginShifted(); VantageDiscovery.OriginShifted(delta); }
        /// <summary>Loads selected-world discoveries and attaches HUD resources after world loading.</summary>
        private void WorldReadyClean() { LocationMarkers.WorldReady(SaveBus.Slot); WorldReadySaves(); WorldReadyCore(); _attach.RunNow(); }
        /// <summary>Restores native UI, unsubscribes feature handlers and releases owned clean HUD resources.</summary>
        private void ShutdownClean()
        {
            if (_newGameSetup != null) _newGameSetup.Dispose(); _newGameSetup = null;

            _weatherMap.Clear();
            Binoculars.Shutdown();
            VantageDiscovery.Reset(); VistasBanners.Shutdown();

            InventoryStyle.Restore();
            Running = false; Release();
            ShutdownCoreUpdateCheck();

            QuestTracker.Clear();
            QuestJournal.Clear(); QuestDialogue.Dispose(); QuestNotifications.Clear();
            if (ReferenceEquals(Core.ModApi.Get<Core.IMapMarkers>(), _questMap)) Core.ModApi.Withdraw<IMapMarkers>(Core.ModApi.OwnerOf<IMapMarkers>());
            if (_questMap != null) _questMap.Dispose(); _questMap = null;
            if (ReferenceEquals(Core.ModApi.Get<Core.IVehicleStatusDisplay>(), this)) Core.ModApi.Withdraw<IVehicleStatusDisplay>(Core.ModApi.OwnerOf<IVehicleStatusDisplay>());
            if (ReferenceEquals(Core.ModApi.Get<Core.ICompassMarkers>(), this)) Core.ModApi.Withdraw<ICompassMarkers>(Core.ModApi.OwnerOf<ICompassMarkers>());
            LocationMarkers.Shutdown();
            MapFrame.Restore(); MapFog.Shutdown();
            if (_menu != null) _menu.Teardown(); _menu = null;

            if (_loadingScreen != null) { SaveBus.StateChanged -= _loadingScreen.StateChanged; _loadingScreen.Dispose(); _loadingScreen = null; }
            MessageHook.Clear(); Bindings.Clear(); Menu.BlockCancelFrame = -1;
            ShutdownCoreAssets(); Menu.DisposeAssets();
            Api.Compass.Entries.Clear(); Api.Compass.CustomIcons.Clear(); Api.Compass.IconRevision++; _attach = null;
        }
        // ---------------------------------------------------------------- Wave A partial parts (implemented in the target module folders)
        /// <summary>Core/UI theme + scene index: embedded textures/font preload, scene cache reset.</summary>
        partial void InitCoreAssets();
        /// <summary>Core: the GitHub release check coroutine.</summary>
        partial void InitCoreUpdateCheck();
        /// <summary>Core scene index: sliced key discovery job.</summary>
        partial void InitCoreSceneJob();
        /// <summary>Core scene index: reset on scene load.</summary>
        partial void SceneResetCore();
        /// <summary>Core scene index: reset and key discovery at world ready.</summary>
        partial void WorldReadyCore();
        /// <summary>Core: stops the release check coroutine.</summary>
        partial void ShutdownCoreUpdateCheck();
        /// <summary>Core: scene index and theme texture teardown.</summary>
        partial void ShutdownCoreAssets();
        /// <summary>Saves: fog-of-war sidecar read at world ready.</summary>
        partial void WorldReadySaves();

        /// <summary>Enumerates HUD layout and map tooling pages.</summary>
        private IEnumerable<ToolPage> CleanToolPages()
        {
            yield return new ConfigPage(this, ToolGroups.Weapons, "Clean HUD & menus", Config.Main, "HUD / HUD", "HUD / Menu");
        }
        /// <summary>Resolves log for the clean HUD feature.</summary>
        internal static void Log(string s) { Core.Log.Always(null,"[CleanHUD] " + s); }
        /// <summary>Resolves warn for the clean HUD feature.</summary>
        internal static void Warn(string s) { Core.Log.Warn(null,"[CleanHUD] " + s); }
        /// <summary>Logs repeated UI exceptions only after the per-key throttle interval.</summary>
        internal static void Throttled(string key, Exception e)
        {
            float next;
            if (_throttle.TryGetValue(key, out next) && Time.unscaledTime < next) return;
            _throttle[key] = Time.unscaledTime + 10f;
            Core.Log.Error(null,"[CleanHUD] " + key + " failed", e);
        }
    }
}
