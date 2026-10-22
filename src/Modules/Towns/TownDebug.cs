using System;
using System.Reflection;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Towns
{
    internal static class TownDebug
    {
        private delegate bool Category(ToolLayout layout, string key, string title);
        private static Category _category;
        private static TownSettings _settings;
        private static TownsModule _owner;
        private static FieldInfo _guid;
        internal static void Install(TownsModule owner, TownSettings settings)
        {
            _settings = settings;
            _owner = owner;
            _category = (Category)Delegate.CreateDelegate(typeof(Category), Patches.Method(typeof(ToolLayout), "Category"));
            var pages = typeof(ToolWindow).Assembly.GetType("WildWasteland.Core.UI.CorePages", true);
            if (!Patches.Patch(owner, Patches.Method(pages, "Developer"), postfix: typeof(TownDebug).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Static)))
                throw new InvalidOperationException("Towns DEBUG controls could not be registered.");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var ui = assembly.GetType("Apocasetter.SettingsUI", false); if (ui == null) continue;
                var entry = assembly.GetType("Apocasetter.ModEntry", false); _guid = entry == null ? null : entry.GetField("Guid");
                if (_guid != null)
                {
                    Patches.Patch(owner, Patches.Method(ui, "DrawSettings"), prefix: Patches.Method(typeof(TownDebug), "OldSettings"));
                    Patches.Patch(owner, Patches.Method(ui, "DrawSectionTabs"), prefix: Patches.Method(typeof(TownDebug), "OldTabs"));
                }
                break;
            }
        }
        private static void Draw(ToolLayout l)
        {
            if (!_category(l, "Developer/Towns", "Towns")) return;
            l.Heading("Town size"); l.Entry(_settings.Min); l.Entry(_settings.Max);
            l.Text("New towns use these building bounds. Existing towns keep their saved building count and layout.");
            foreach (string section in new[] { "Towns / Generation", "Towns / Debug", "Towns / Assets" })
            {
                l.Heading(section == "Towns / Generation" ? "Placement and terrain" : section == "Towns / Debug" ? "Loot and population" : "Assets (restart required)");
                foreach (var key in _settings.File.Keys)
                    if (key.Section == section && !ReferenceEquals(_settings.File[key], _settings.Min) && !ReferenceEquals(_settings.File[key], _settings.Max)) l.Entry(_settings.File[key]);
            }
            l.Text("One percentage roll per town, spread evenly across buildings. 100% uses native totals; 500% is five times. Existing objects and completed spawners are retained.");
            l.Heading("Current towns"); _owner.Draw(l);
        }
        private static bool IsTowns(object entry) { return entry != null && (string)_guid.GetValue(entry) == "com.denis.apocalypter.wildwasteland.towns"; }
        private static bool OldSettings(object m)
        {
            if (!IsTowns(m)) return true;
            UnityEngine.GUILayout.Label("Towns settings have moved to Wild Wasteland: Debug > Developer > Towns.");
            UnityEngine.GUILayout.Label("Use F3 and enable Debug to access building counts, placement, loot and population.");
            return false;
        }
        private static bool OldTabs(object m, ref float __result)
        { if (!IsTowns(m)) return true; __result = 0; return false; }
    }
}
