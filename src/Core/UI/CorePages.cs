using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WildWasteland.Core.UI
{
    /// <summary>Core-owned settings shell: module overview, reset policy and pages contributed by their feature owners.</summary>
    internal static class CorePages
    {
        internal static ToolPage GamePage, WorldPage, HudPage, SystemPage, DebugPage, DeveloperPage;
        private static readonly List<ToolPage> Pages = new List<ToolPage>();

        /// <summary>Tool pages exposed to Debug without depending on a Debug module type.</summary>
        internal static IEnumerable<ToolPage> ModulePages { get { return Pages; } }

        /// <summary>Collects a module's authored tool page for the Debug-owned category presentation.</summary>
        internal static void AddDebugPage(ToolPage page) { if (page != null && !Pages.Contains(page)) Pages.Add(page); }

        /// <summary>Creates shared page hosts; each feature contributes its rows through SettingsShell.</summary>
        internal static void Register()
        {
            GamePage = new ShellPage("PLAYER", SettingsShell.Page.Player);
            WorldPage = new ShellPage("GAMEPLAY", SettingsShell.Page.World);
            HudPage = new ShellPage("HUD", SettingsShell.Page.Hud);
            SystemPage = new ShellPage("SYSTEM", SettingsShell.Page.System);
            DebugPage = new ShellPage("Debug", SettingsShell.Page.Debug) { DebugOnly = true };
            DeveloperPage = new ShellPage("Developer", SettingsShell.Page.Developer) { DebugOnly = true };
            ToolWindow.Add(GamePage); ToolWindow.Add(WorldPage); ToolWindow.Add(HudPage); ToolWindow.Add(SystemPage);
            ToolWindow.Add(new ActionPage(null, ToolGroups.General, "Modules", Modules));
            ToolWindow.Add(Optimization.PerformancePage.Instance);
            ToolWindow.Add(DebugPage); ToolWindow.Add(DeveloperPage);
        }

        /// <summary>Restores gameplay/debug/developer values while preserving module masters and batching config writes.</summary>
        internal static void ResetSettings()
        {
            var excluded = new HashSet<ConfigEntryBase>();
            foreach (var module in ModuleHost.Entries)
                if (module.Module.Id != "POI" && module.Config != null) excluded.Add(module.Config.Enabled);
            var entries = new List<ConfigEntryBase>();
            foreach (var file in new[] { Plugin.Settings, Plugin.Tools })
                foreach (var key in file.Keys) if (!excluded.Contains(file[key])) entries.Add(file[key]);
            if (Plugin.EnableDebug != null) entries.Add(Plugin.EnableDebug);
            var contributed = new List<ConfigEntryBase>();
            foreach (SettingsShell.Page page in System.Enum.GetValues(typeof(SettingsShell.Page)))
            {
                SettingsShell.Shared.ResettableEntries(page, contributed);
                for (int i = 0; i < contributed.Count; i++)
                    if (!excluded.Contains(contributed[i]) && !entries.Contains(contributed[i])) entries.Add(contributed[i]);
            }
            var files = new Dictionary<ConfigFile, bool>();
            foreach (var entry in entries) if (!files.ContainsKey(entry.ConfigFile)) files.Add(entry.ConfigFile, entry.ConfigFile.SaveOnConfigSet);
            foreach (var file in files.Keys) file.SaveOnConfigSet = false;
            try
            {
                var preset = Optimization.OptimizationRuntime.PresetEntry;
                if (preset != null) preset.BoxedValue = preset.DefaultValue;
                foreach (var entry in entries) entry.BoxedValue = entry.DefaultValue;
            }
            finally
            {
                foreach (var file in files) file.Key.SaveOnConfigSet = file.Value;
                foreach (var file in files.Keys) file.Save();
            }
            Ledger.ClearNumbers(); ToolLayout.CancelRebind(); GUI.FocusControl(null);
            Log.Always(null, "Settings reset: Game, Performance, Debug and Developer restored to defaults");
        }

        /// <summary>One shared page whose rows and preparation remain owned by contributing modules.</summary>
        private sealed class ShellPage : ToolPage
        {
            private readonly SettingsShell.Page _page;
            /// <summary>Creates a host for one settings contribution page.</summary>
            internal ShellPage(string title, SettingsShell.Page page) { Group = ToolGroups.General; Title = title; _page = page; }
            /// <summary>Prepares only the resources requested by contributors to this page.</summary>
            public override void OnShow() { SettingsShell.Shared.Prepare(_page); }
            /// <summary>Draws contributed rows in their authored order.</summary>
            public override void Draw(ToolLayout layout) { SettingsShell.Shared.Draw(_page, layout); }
        }

        /// <summary>Draws module masters with conflict/failure status, retaining the normal runtime off/on controls.</summary>
        private static void Modules(ToolLayout layout)
        {
            layout.Text("Turn feature groups on or off. Changes apply immediately.");
            foreach (var entry in ModuleHost.Entries)
            {
                bool enabled = GUI.enabled; GUI.enabled = enabled && entry.State != ModuleState.OffConflict;
                if (entry.Config != null) layout.Toggle(entry.Config.Enabled, entry.Module.DisplayName);
                GUI.enabled = enabled;
                if (entry.State == ModuleState.OffConflict) layout.Text("Standalone replacement: " + entry.Conflict, Ledger.Danger);
                else if (entry.State == ModuleState.InitFailed || entry.State == ModuleState.DisabledAfterErrors)
                    layout.Text("Could not start. Switch off and on to retry: " + entry.LastError, Ledger.Danger);
            }
        }
    }
}
