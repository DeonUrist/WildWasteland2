using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace WildWasteland.Core.UI
{
    /// <summary>Pages owned by Core: module overview (on/off), performance, Harmony inventory, core settings.</summary>
    /// <remarks>
    /// Wave A split (refactor): this file is the Core shell (registration, reset, Modules page, debug-page plumbing). Module
    /// rows live in partial files under src/Core/UI/_pages/ (CorePages.Presentation/HUD/Gunplay/NPCAI/Patrols/POI/Survival/Debug.cs),
    /// owned by B01 in Wave B, which turns them into SettingsShell contributions. Drawing order is unchanged.
    /// </remarks>
    internal static partial class CorePages
    {
        // Contribution points (implemented in src/Core/UI/_pages/*.cs; an absent part draws nothing).
        static partial void FindPoiSwitch();
        static partial void AddResetEntriesHud(List<ConfigEntryBase> entries);
        static partial void PrepareHudPreviews();
        static partial void WorldPatrols(ToolLayout l);
        static partial void WorldPoi(ToolLayout l);
        static partial void WorldWeather(ToolLayout l);
        static partial void NavigationSettings(ToolLayout l);
        static partial void HudGunplay(ToolLayout l);
        static partial void HudNpc(ToolLayout l);
        static partial void DebugSectionHud(ToolLayout l, IModule owner);
        static partial void DebugSectionSurvival(ToolLayout l, IModule owner);
        static partial void DebugSectionPatrols(ToolLayout l, IModule owner);
        static partial void DebugSwitchesPatrols(ToolLayout l, IModule owner);

        internal static ToolPage GamePage, WorldPage, HudPage, SystemPage, DebugPage, DeveloperPage;
        static readonly List<ToolPage> _debugPages = new List<ToolPage>();
        static ConfigPage _settings;
        internal static void AddDebugPage(ToolPage page) { _debugPages.Add(page); }

        internal static void Register()
        {
            GamePage = new GameSettingsPage("PLAYER", DrawPlayer);
            WorldPage = new GameSettingsPage("WORLD", DrawWorld);
            HudPage = new GameSettingsPage("HUD", DrawHud);
            ToolWindow.Add(GamePage); ToolWindow.Add(WorldPage); ToolWindow.Add(HudPage);
            SystemPage = new GameSettingsPage("SYSTEM", SaveLibraryPage.DrawSystem);
            ToolWindow.Add(SystemPage); ToolWindow.Add(SaveLibraryPage.Instance);
            ToolWindow.Add(new ActionPage(null, ToolGroups.General, "Modules", Modules));
            ToolWindow.Add(WildWasteland.Modules.Performance.PerformancePage.Instance);   // #133: separate Performance tab
            DebugPage = new ActionPage(null, ToolGroups.General, "Debug", Debug) { DebugOnly = true };
            DeveloperPage = new ActionPage(null, ToolGroups.General, "Developer", Developer) { DebugOnly = true };
            ToolWindow.Add(DebugPage); ToolWindow.Add(DeveloperPage);
            _settings = new ConfigPage(null, ToolGroups.General, "Other settings", Plugin.Settings, d => true) { Intro = "Remaining gameplay values and switches." };
            FindPoiSwitch();
        }

        static string F(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }

        internal static void ResetSettings()
        {
            var excluded = new HashSet<ConfigEntryBase>();
            foreach (var m in ModuleHost.Entries)
                if (m.Module.Id != "Performance" && m.Module.Id != "POI") excluded.Add(m.Config.Enabled);
            var entries = new List<ConfigEntryBase>();
            foreach (var file in new[] { Plugin.Settings, Plugin.Tools })
                foreach (var key in file.Keys) if (!excluded.Contains(file[key])) entries.Add(file[key]);
            entries.Add(Plugin.EnableDebug);
            AddResetEntriesHud(entries);
            var files = new Dictionary<ConfigFile, bool>();
            foreach (var e in entries) if (!files.ContainsKey(e.ConfigFile)) files.Add(e.ConfigFile, e.ConfigFile.SaveOnConfigSet);
            foreach (var file in files.Keys) file.SaveOnConfigSet = false;
            try
            {
                var preset = WildWasteland.Modules.Performance.PerformanceModule.PresetEntry;
                preset.BoxedValue = preset.DefaultValue;
                foreach (var e in entries) e.BoxedValue = e.DefaultValue;
            }
            finally
            {
                foreach (var pair in files) pair.Key.SaveOnConfigSet = pair.Value;
                foreach (var file in files.Keys) file.Save();
            }
            Ledger.ClearNumbers(); ToolLayout.CancelRebind(); GUI.FocusControl(null);
            Log.Always("[Core] Settings reset: Game, Performance, Debug and Developer restored to defaults");
        }

        sealed class GameSettingsPage : ToolPage
        {
            readonly System.Action<ToolLayout> _draw;
            internal GameSettingsPage(string title, System.Action<ToolLayout> draw) { Group = ToolGroups.General; Title = title; _draw = draw; }
            /// <summary>Prepares the HUD previews (body pictures, crosshair sprites) when a game page opens, never per frame.</summary>
            public override void OnShow() { PrepareHudPreviews(); }
            public override void Draw(ToolLayout l) { _draw(l); }
        }

        static void Switch(ToolLayout l, string label, ConfigEntry<bool> entry)
        {
            bool value = l.Choices(label, entry.Value ? 1 : 0, "Off", "On") == 1;
            if (value != entry.Value) entry.Value = value;
        }

        /// <summary>WORLD page: patrol mode, POI switch, weather map areas, then map/compass settings (module rows in _pages).</summary>
        static void DrawWorld(ToolLayout l)
        {
            l.Heading("World");
            WorldPatrols(l);
            WorldPoi(l);
            WorldWeather(l);
            NavigationSettings(l);
        }

        static void Modules(ToolLayout l)
        {
            l.Text("Turn feature groups on or off. Changes apply immediately.");
            foreach (var e in ModuleHost.Entries)
            {
                bool enabled = GUI.enabled; GUI.enabled = enabled && e.State != ModuleState.OffConflict;
                if (e.Config != null) l.Toggle(e.Config.Enabled, e.Module.DisplayName);
                GUI.enabled = enabled;
                if (e.State == ModuleState.OffConflict) l.Text("Standalone replacement: " + e.Conflict, Ledger.Danger);
                else if (e.State == ModuleState.InitFailed || e.State == ModuleState.DisabledAfterErrors) l.Text("Could not start. Switch off and on to retry: " + e.LastError, Ledger.Danger);
            }
        }

        static string CategoryTitle(IModule owner) { return owner == null ? "Core" : owner.Id + " / " + owner.DisplayName + (ModuleHost.IsRunning(owner) ? "" : " (module off)"); }

        static void DrawDebugPage(ToolLayout l, ToolPage p)
        {
            l.SearchSection(p.Group + " / " + p.Title);
            l.Heading(p.Group + " / " + p.Title);
            l.Prefix = p.Group + "/" + p.Title + "/";
            bool enabled = GUI.enabled;
            bool off = p.Owner != null && !ModuleHost.IsRunning(p.Owner);
            if (off) l.Text(p.Owner.DisplayName + " is off. Settings remain editable; live tools need the module enabled.");
            if (off && !p.ShowWhenModuleOff && !(p is ConfigPage)) GUI.enabled = false;
            try { p.Draw(l); } catch (System.Exception ex) { l.Text("Page error: " + ex.Message, Ledger.Danger); Log.Error("Debug page " + p.Title, ex); }
            finally { GUI.enabled = enabled; }
        }

    }
}
