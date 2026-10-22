using System;
using BepInEx.Configuration;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Owns optional third-party hooks, generation facts and vehicle-ownership queries. Early guards are installed
    /// before Core optimization starts; B17 supplies the remaining bridge integrations (README 6.12).
    /// </summary>
    public sealed class CompatibilityModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Compatibility"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Compatibility"; } }

        private readonly EngineSimBridge _engineSim = new EngineSimBridge();
        private ConfigEntry<bool> _engineEnabled;
        private bool _running;
        /// <summary>Binds per-mod switches; session takeover remains mandatory and independent of these switches.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            _engineEnabled = cfg.Player("EngineSim", "Enabled", true, "Schedule Engine Sim through Wild Wasteland and provide player and crew ignition.");
            _engineEnabled.SettingChanged += EngineChanged;
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
        /// <summary>Reseeds shared scene discovery and clears ignition requests.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (_running && _engineEnabled.Value) _engineSim.OnSceneLoaded(scene, mode); }

        /// <summary>Exposes the per-mod Engine Sim switch in the shared settings shell.</summary>
        public override void Settings(SettingsShell shell) { shell.Row(this, SettingsShell.Page.System, "Compatibility", _engineEnabled, "Engine Sim"); }

        /// <summary>Installs detected SigmaMod and Vistas hooks under this module's lifetime.</summary>
        public override void Init() { SigmaFsmStates.Install(this); VistasWorldGen.Init(this); _running = true; if (_engineEnabled.Value) _engineSim.Init(); }

        /// <summary>Registers the detected third-party vehicle owner before Core optimization starts.</summary>
        public override void ProvideApis()
        {
            if (VistasWorldGen.Present) ModApi.Provide<IWorldGenFacts>(this, new VistasFacts());
            var query = EngineSimVehicleOwnership.Create();
            var ownership = ModApi.Get<IVehicleOwnership>();
            if (query != null && ownership != null) ownership.Register(this, query);
        }

        /// <summary>Detaches external listeners and clears runtime state before Core removes this module's hooks.</summary>
        public override void Shutdown() { _running = false; _engineSim.Shutdown(); VistasWorldGen.Shutdown(); SigmaFsmStates.Clear(); }

        /// <summary>Publishes generation facts without exposing reflective third-party bridge state.</summary>
        private sealed class VistasFacts : IWorldGenFacts
        {
            /// <summary>True while the detected World Amp bridge is installed.</summary>
            public bool Present { get { return VistasWorldGen.Present; } }
            /// <summary>Last place name reported by the native entry callback.</summary>
            public string CurrentPlace { get { return VistasWorldGen.CurrentPlace; } }
            /// <summary>Native place classification or landmark caption accompanying the entry callback.</summary>
            public string CurrentPlaceKind { get { return VistasWorldGen.CurrentPlaceKind; } }
            /// <summary>Revision changed whenever captured roads are replaced or cleared.</summary>
            public int RoadRevision { get { return VistasWorldGen.RoadRevision; } }
            /// <summary>Third-party assembly identity and terrain/road settings used by route-cache consumers.</summary>
            public string RoadIdentity { get { return VistasWorldGen.RoadIdentity; } }
            /// <summary>Configured banner height, with the native fallback when the setting is unavailable.</summary>
            public float BannerHeight { get { return VistasWorldGen.BannerHeight; } }
            /// <summary>Copies the retained road polylines into the consumer's reusable list.</summary>
            public void RoadLines(System.Collections.Generic.List<UnityEngine.Vector3[]> into)
            { into.Clear(); foreach (var line in VistasWorldGen.RoadLines) into.Add(line); }
        }
    }
}

