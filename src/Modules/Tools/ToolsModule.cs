using WildWasteland.Core;
using WildWasteland.Core.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.Tools
{
    /// <summary>
    /// Owns authoring tools and their scheduled work. LOD export belongs here independently of optimization presets;
    /// the remaining editor integrations are supplied by B16 (README 6.10).
    /// </summary>
    public sealed partial class ToolsModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Tools"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Tools"; } }

        /// <summary>No entries yet (Core binds [Tools] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            NavigationTools.Bind(cfg); BindPatrols(cfg);
            _poiEditorKey = cfg.Hotkey("POI editor", UnityEngine.InputSystem.Key.None, "Open the nearest custom POI layout and marker editor (Debug and Developer mode).");
        }

        /// <summary>Authoring work starts only from an explicit tool action.</summary>
        public override void Init()
        {
            _poiEditor = new PoiEditor(); _poiEditor.Install(this, _poiEditorKey);
            InitNavigation(); InitPatrols();
        }

        /// <summary>Stops an in-progress export when Tools is disabled.</summary>
        public override void Shutdown() { ShutdownPatrols(); ShutdownNavigation(); GeneratedLodExport.Stop(); if (_poiEditor != null) _poiEditor.Close(true); _poiEditor = null; }

        /// <summary>Releases captured mesh references before a different save is loaded.</summary>
        public override void OnLoadBegin(string slot) { ClosePatrolEditors(); ResetNavigation(); GeneratedLodExport.Stop(); if (_poiEditor != null) _poiEditor.Close(true); }

        /// <summary>Stops an export whose source scene has been replaced.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ClosePatrolEditors(); ResetNavigation(); GeneratedLodExport.Stop(); if (_poiEditor != null) _poiEditor.Close(true); }

        /// <summary>Registers the explicit loaded-mesh export authoring page.</summary>
        private PoiEditor _poiEditor;
        private BepInEx.Configuration.ConfigEntry<UnityEngine.InputSystem.Key> _poiEditorKey;
        private readonly TownTemplatePage _townTemplates = new TownTemplatePage();
        private readonly List<BepInEx.Configuration.ConfigEntryBase> _gasSettings = new List<BepInEx.Configuration.ConfigEntryBase>();
        /// <summary>Adjusts the active authoring camera when the native world origin moves.</summary>
        public override void OnOriginShift(UnityEngine.Vector3 delta) { PoiNavSetup.Shift(delta); PoiNavEditor.Shift(delta); if (_poiEditor != null) _poiEditor.Shift(delta); }
        /// <summary>Draws the POI layout authoring entry point.</summary>
        private void DrawPoiEditor(ToolLayout layout) { if (_poiEditor != null) _poiEditor.DrawPage(layout); else layout.Entry(_poiEditorKey); }
        /// <summary>Draws POI-owned test-pad state and its actual tuning entries.</summary>
        private void DrawGasStations(ToolLayout layout)
        {
            var api = ModApi.Get<IGasStationAuthoring>();
            if (api == null) { layout.Text("Enable POI to inspect and configure gas-station test pads."); return; }
            layout.Value("Status", api.Status); layout.Value("Loaded", api.LoadedCount.ToString());
            api.Settings(_gasSettings); foreach (var entry in _gasSettings) layout.Entry(entry);
        }
        /// <summary>Contributes independent POI, town, navigation, patrol and recorder authoring pages.</summary>
        public override IEnumerable<ToolPage> ToolPages()
        {
            yield return new ActionPage(this, ToolGroups.Debug, "Generated LOD export", DrawLodExport) { IsTool = true };
            yield return new WeatherPreviewPage(this);
            yield return new ActionPage(this,ToolGroups.Vehicles,"Patrol editor",DrawPatrolEditor){IsTool=true};
            yield return new ActionPage(this,ToolGroups.Vehicles,"Template recorder",DrawRecorder){IsTool=true};
            yield return new ActionPage(this, ToolGroups.Debug, "POI pathfinding", PoiNavEditor.DrawPage) { DebugOnly = true, IsTool = true };
            yield return new ActionPage(this, ToolGroups.Debug, "Pathfinding baked data", DrawNavigationData) { DebugOnly = true, IsTool = true };
            yield return new ActionPage(this, ToolGroups.Debug, "POI editor", DrawPoiEditor) { IsTool = true, DebugOnly = true };
            yield return new ActionPage(this, ToolGroups.Debug, "Gas stations", DrawGasStations) { IsTool = true, DebugOnly = true };
            yield return new ActionPage(this, ToolGroups.General, "Town templates", _townTemplates.Draw) { IsTool = true };
        }

        /// <summary>Shows capture progress and explicit start/stop actions without changing optimization settings.</summary>
        private void DrawLodExport(ToolLayout layout)
        {
            layout.Text(GeneratedLodExport.Status);
            layout.Text("Exports loaded, readable mesh originals for the offline LOD generator. Wait for completion, close the game, then run Generate.cmd.");
            if (GeneratedLodExport.Running)
            {
                if (layout.Button("Stop loaded-model export")) GeneratedLodExport.Stop();
            }
            else if (layout.Button("Export loaded models for LOD generator")) GeneratedLodExport.Start(this);
        }
    }
}


