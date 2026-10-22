using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Modules.NPC;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Owns optional third-party hooks, generation facts and vehicle-ownership queries. Early guards are installed
    /// before Core optimization starts; B17 supplies the remaining bridge integrations (README 6.12).
    /// </summary>
    public sealed partial class CompatibilityModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Compatibility"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Compatibility"; } }

        private readonly EngineSimBridge _engineSim = new EngineSimBridge();
        private ConfigEntry<bool> _engineEnabled, _languageEnabled, _caveEnabled;
        private CaveDarknessBridge _cave;
        private Job _caveDiscovery;
        private ApocaLanguageBridge _language;
        private bool _running;
        private readonly List<OptionalBridge> _bridges = new List<OptionalBridge>();

        /// <summary>True between Init and Shutdown (bridge switches react live only then).</summary>
        internal bool IsRunning { get { return _running; } }

        /// <summary>Declares the per-mod bridges in start order (README 6.12: one folder and one switch per third-party mod).</summary>
        public CompatibilityModule()
        {
            _bridges.Add(new OptionalBridge(this, "SigmaMod", "SigmaMod", "Prepare native FSM states before SigmaMod reads inactive FSMs (only patched when SigmaMod is loaded).", SigmaFsmStates.Install, SigmaFsmStates.Clear));
            _bridges.Add(new OptionalBridge(this, "VistasWorldGen", "Vistas World Gen", "Use Vistas World Gen roads, places and terrain with Wild Wasteland towns, traffic, HUD banners and weather.", VistasBridge.Start, VistasBridge.Shutdown));
            _bridges.Add(new OptionalBridge(this, "BloodFX", "BloodFX", "Recover the optional BloodFX updater after scene destruction and suppress its F8 debug hit.", BloodFxCompat.Start, BloodFxCompat.Shutdown));
            _bridges.Add(new OptionalBridge(this, "ApocaDustStorm", "ApocaDustStorm", "War Rig tanks shelter from ApocaDustStorm storms, and its dust storms dim NPC sight and hearing like sandstorms.", ApocaDustStormBridge.Start, ApocaDustStormBridge.Shutdown));
            _bridges.Add(new OptionalBridge(this, "Apocaspawner", "Apocaspawner", "List the War Rig and the Stew Pot in Apocaspawner and scale its crossbow damage like Wild Wasteland crossbows.", ApocaspawnerBridge.Start, ApocaspawnerBridge.Shutdown));
            _bridges.Add(new OptionalBridge(this, "Apocapocket", "Apocapocket", "Show Apocapocket's extra slots on the Wild Wasteland HUD and provide them to Wild Wasteland modules.", ApocapocketBridge.Start, ApocapocketBridge.Shutdown));
            _bridges.Add(new OptionalBridge(this, "Apocatalker", "Apocatalker", "Show Apocatalker conversations in the Wild Wasteland dialogue window.", ApocatalkerBridge.Start, ApocatalkerBridge.Shutdown));
            _bridges.Add(new OptionalBridge(this, "ChaseCamera", "ChaseCamera", "Drive ChaseCamera with the Wild Wasteland player body and show its speed, RPM and gear on the Wild Wasteland HUD.", ChaseCameraBridge.Start, ChaseCameraBridge.Shutdown));
            _bridges.Add(new OptionalBridge(this, "DogCompanion", "Dog Companion", "Drive Dog Companion's dog with Wild Wasteland canine animation, combat and vehicle seating (the [NPC] DogCompanionCompat switch still applies).", DogCompanionCompat.Initialize, DogCompanionCompat.Stop));
            _bridges.Add(new OptionalBridge(this, "WeaponsPack", "Weapons Pack", "Connect optional Weapons Pack behavior (NPC fear, chainsaw kills, held-model shadows, HUD icons, slash sound) through Wild Wasteland services.", WeaponsPackBridge.Start, WeaponsPackBridge.Shutdown));
        }

        /// <summary>Binds per-mod switches; session takeover remains mandatory and independent of these switches.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            foreach (var bridge in _bridges) bridge.Bind(cfg);
            _engineEnabled = cfg.Player("EngineSim", "Enabled", true, "Schedule Engine Sim through Wild Wasteland and provide player and crew ignition.");
            _engineEnabled.SettingChanged += EngineChanged;
            _languageEnabled = cfg.Player("ApocaLanguage", "Enabled", true, "Use the optional language mod's language choices in the Wild Wasteland menu.");
            _languageEnabled.SettingChanged += LanguageChanged;
            _caveEnabled = cfg.Player("CaveDarkness", "Enabled", true, "Use the optional cave mod's ambient light for NPC perception.");
            _caveEnabled.SettingChanged += CaveChanged;
        }
        /// <summary>Restarts only Engine Sim resources when its player switch changes.</summary>
        private void EngineChanged(object sender, EventArgs args)
        {
            if (!_running) return;
            _engineSim.Shutdown();
            if (!_engineEnabled.Value) return;
            try { _engineSim.Init(); }
            catch (Exception error) { _engineSim.Shutdown(); Log.Error(this, "Engine Sim bridge could not start; standalone callbacks retained", error); }
        }
        /// <summary>Reconciles only the language service when its individual switch changes.</summary>
        private void LanguageChanged(object sender, EventArgs args) { if (_running) ProvideLanguage(); }
        /// <summary>Registers a fresh optional language provider or withdraws only this module's existing provider.</summary>
        private void ProvideLanguage()
        {
            if (_languageEnabled.Value) { if (_language == null) _language = new ApocaLanguageBridge(); ModApi.Provide<ILanguageMenu>(this, _language); }
            else { ModApi.Withdraw<ILanguageMenu>(this); _language = null; }
        }
        /// <summary>Reconciles the optional cave field independently from global daylight policy.</summary>
        private void CaveChanged(object sender, EventArgs args) { if (_running) ProvideCave(); }
        /// <summary>Owns cave discovery maintenance and the numeric cave field provider.</summary>
        private void ProvideCave()
        {
            if (_caveDiscovery != null) { _caveDiscovery.Remove(); _caveDiscovery = null; }
            ModApi.Withdraw<ICaveAmbient>(this); _cave = null;
            if (!_caveEnabled.Value) return;
            _cave = new CaveDarknessBridge(); _cave.Refresh();
            _caveDiscovery = Scheduler.Interval(this, "Compatibility.CaveDarkness.Discovery", 5f, _cave.Refresh);
            ModApi.Provide<ICaveAmbient>(this, _cave);
        }
        /// <summary>Reseeds shared scene discovery and clears ignition requests.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_running) return;
            if (_engineEnabled.Value) _engineSim.OnSceneLoaded(scene, mode);
            WeaponsPackBridge.OnSceneLoaded();
            ApocatalkerBridge.OnSceneLoaded();
        }

        /// <summary>Contributes every per-mod switch to the System page's Compatibility group.</summary>
        public override void Settings(SettingsShell shell) { foreach (var bridge in _bridges) bridge.Settings(shell); shell.Row(this, SettingsShell.Page.System, "Compatibility", _engineEnabled, "Engine Sim"); shell.Row(this, SettingsShell.Page.System, "Compatibility", _languageEnabled, "ApocaLanguage"); shell.Row(this, SettingsShell.Page.System, "Compatibility", _caveEnabled, "Cave Darkness"); }

        /// <summary>Starts every enabled per-mod bridge (SigmaMod and Vistas first, as before) and Engine Sim.</summary>
        public override void Init() { _running = true; foreach (var bridge in _bridges) bridge.Start(); if (_engineEnabled.Value) _engineSim.Init(); }

        /// <summary>Registers the detected third-party vehicle owner before Core optimization starts.</summary>
        public override void ProvideApis()
        {
            ProvideLanguage(); ProvideCave();
            var query = EngineSimVehicleOwnership.Create();
            var ownership = ModApi.Get<IVehicleOwnership>();
            if (query != null && ownership != null) ownership.Register(this, query);
        }

        /// <summary>Detaches external listeners and clears runtime state before Core removes this module's hooks.</summary>
        public override void Shutdown() { _running = false; for (int i = _bridges.Count - 1; i >= 0; i--) _bridges[i].Stop(); if (_caveDiscovery != null) _caveDiscovery.Remove(); _caveDiscovery = null; ModApi.Withdraw<ICaveAmbient>(this); _cave = null; ModApi.Withdraw<ILanguageMenu>(this); _language = null; _engineSim.Shutdown(); }
    }
}

