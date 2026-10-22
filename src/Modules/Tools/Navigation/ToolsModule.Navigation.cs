using System;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Tools
{
    /// <summary>Owns navigation authoring input, rendering, setup copies and provider lifecycle.</summary>
    public sealed partial class ToolsModule
    {
        private INavigationAuthoring _navigation;
        /// <summary>Registers named editor jobs and the exact setup-root provider callback.</summary>
        private void InitNavigation()
        {
            ModApi.Changed += NavigationChanged; BindNavigation();
            Scheduler.EveryFrame(this, "Navigation setup and bake", PoiNavSetup.Tick);
            Scheduler.EveryFrame(this, "Navigation display", PoiNavView.Tick);
            Scheduler.EveryFrame(this, "Navigation editor input", PoiNavEditor.Tick);
            Scheduler.EveryFrame(this, "Navigation editor camera", PoiNavEditor.CameraTick, Phase.LateUpdateLast);
            Scheduler.Gui(this, "Navigation editor", PoiNavEditor.Draw, PoiNavEditor.WantsGui);
            Hotkeys.Register(this, NavigationTools.EditorKey, PoiNavEditor.Toggle, PoiNavEditor.CanToggle, true);
        }
        /// <summary>Releases editor drafts and unsaved test/setup objects before native scene or save replacement.</summary>
        private void ResetNavigation() { PoiNavEditor.Close(true); PoiNavView.Unhook(); PoiNavSetup.Reset(); }
        /// <summary>Rebinds navigation authoring only when its actual provider changes.</summary>
        private void NavigationChanged(Type contract) { if (contract == typeof(INavigationAuthoring)) BindNavigation(); }
        /// <summary>Detaches the previous setup callback and prevents a draft from outliving its source provider.</summary>
        private void BindNavigation()
        {
            var next = ModApi.Get<INavigationAuthoring>(); if (ReferenceEquals(next, _navigation)) return;
            ResetNavigation(); if (_navigation != null) _navigation.RemoveSetupRoots(this);
            _navigation = next; if (_navigation != null) _navigation.RegisterSetupRoots(this, PoiNavSetup.Roots);
        }
        /// <summary>Unsubscribes provider changes and restores all authoring state when Tools stops.</summary>
        private void ShutdownNavigation()
        {
            ModApi.Changed -= NavigationChanged; ResetNavigation();
            if (_navigation != null) _navigation.RemoveSetupRoots(this); _navigation = null;
        }
        /// <summary>Preserves native map reset/export and missing-bake display actions through the typed owner.</summary>
        private void DrawNavigationData(ToolLayout layout)
        {
            var api = NavigationTools.Source;
            if (api == null) { layout.Text("Enable NPC AI to inspect or export its navigation maps."); return; }
            api.HighlightUnbaked = layout.Toggle("Highlight unbaked POIs on map (light purple)", api.HighlightUnbaked);
            if (layout.Button("Reset custom pathfinding baked data", true, false, 430)) api.ResetCustomMaps();
            if (layout.Button("Export vanilla pathfinding maps", true, false, 430)) api.ExportVanillaMaps();
            layout.Text("Reset clears locally baked maps and rebuilds nearby custom maps. Shipped maps and vanilla exports are retained.");
            if (!string.IsNullOrEmpty(api.MapActionStatus)) layout.Text(api.MapActionStatus);
        }
    }
}
