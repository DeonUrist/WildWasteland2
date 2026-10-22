using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using Player = WildWasteland.Modules.PlayerBody.PlayerBodyModule;
using HudSettings = WildWasteland.Modules.HUD.HudModule;
using Patrols = WildWasteland.Modules.Patrols.PatrolsModule;
using CleanHudSettings = WildWasteland.Modules.CleanHUD.CleanHudModule;

namespace WildWasteland.Core.UI
{
    /// <summary>Debug/Developer pages of the settings window (Wave A split): entry filters, logging switches, switches block, scheduler table, Harmony list. B03 takes the logging/perf parts over.</summary>
    internal static partial class CorePages
    {
        static bool DebugSetting(ConfigEntryBase e)
        {
            if (e.Definition.Section.StartsWith("System /", System.StringComparison.Ordinal)) return false;
            if (MapFogSetting(e)) return false;
            if (VerboseSetting(e) || ReferenceEquals(e, Plugin.DeveloperMode)) return false;
            if (ReferenceEquals(e, CleanHudSettings.ShowNeeds) || ReferenceEquals(e, CleanHudSettings.ShowHealthNumbers)
                || ReferenceEquals(e, CleanHudSettings.DamageDirection)
                || ReferenceEquals(e, CleanHudSettings.SimpleMapIcons)
                || ReferenceEquals(e, CleanHudSettings.ShowInsanity) || ReferenceEquals(e, CleanHudSettings.Scale)
                || ReferenceEquals(e, CleanHudSettings.ShowConvoys) || ReferenceEquals(e, CleanHudSettings.ShowLocations)
                || ReferenceEquals(e, CleanHudSettings.AmbiguousPatrols) || ReferenceEquals(e, CleanHudSettings.AmbiguousLocations)
                || ReferenceEquals(e, CleanHudSettings.ShowCompass) || ReferenceEquals(e, CleanHudSettings.MapEnabled)
                || ReferenceEquals(e, CleanHudSettings.MapMarkers) || ReferenceEquals(e, CleanHudSettings.MapRoutes)
                || ReferenceEquals(e, CleanHudSettings.MapTrafficMarkers)
                || ReferenceEquals(e, CleanHudSettings.DrivingHudOnlyThirdPerson)) return false;
            if (ReferenceEquals(e, Player.Character) || ReferenceEquals(e, Player.BodyFirstPerson) || ReferenceEquals(e, Player.FullBodyOnlyOnFoot)
                || ReferenceEquals(e, Player.VehicleHotkeyHint) || ReferenceEquals(e, Player.VehicleStatusHint)
                || ReferenceEquals(e, Player.EmptyHandArms) || ReferenceEquals(e, Player.FirstPersonArmsOnlyOnFoot) || ReferenceEquals(e, Player.OcclusionPrototype)
                || ReferenceEquals(e, Player.ThirdPersonOnFoot) || ReferenceEquals(e, Player.ClimbingOn) || ReferenceEquals(e, Patrols.FuryRoad)
                || ReferenceEquals(e, Patrols.ConvoyEnabled) || ReferenceEquals(e, Patrols.FootPatrols) || ReferenceEquals(e, HudSettings.SelectionDot)
                || ReferenceEquals(e, HudSettings.Crosshair) || ReferenceEquals(e, HudSettings.HitMarker) || ReferenceEquals(e, HudSettings.HeadshotSound)
                || ReferenceEquals(e, HudSettings.DamageNumbers) || ReferenceEquals(e, WildWasteland.Modules.Guns.GunsModule.ToggleIronsights)
                || ReferenceEquals(e, WildWasteland.Modules.NPC.NpcModule.ShoutKey) || ReferenceEquals(e, WildWasteland.Modules.NPC.NpcModule.ShoutModifier)) return false;
            foreach (var m in ModuleHost.Entries) if (ReferenceEquals(e, m.Config.Enabled)) return false;
            // tools.cfg holds the fine tuning (offsets, poses, AI and animation constants): it stays in the file and keeps working, but the
            // Debug page only shows its debug switches (logs, overlays, editors)
            if (ReferenceEquals(e.ConfigFile, Plugin.Tools)) return ToolsDebugSwitch(e);
            return true;
        }

        static bool ToolsDebugSwitch(ConfigEntryBase e)
        {
            if (e.SettingType != typeof(bool)) return false;
            string sec = e.Definition.Section, key = e.Definition.Key;
            if (sec.EndsWith("/ Debug", System.StringComparison.Ordinal) || sec.EndsWith("/ Editors", System.StringComparison.Ordinal)) return true;
            return key == "Log" || key.IndexOf("Verbose", System.StringComparison.Ordinal) >= 0 || key == "DebugMap" || key == "Real patrol names" || key == "hide route type";
        }

        static bool VerboseSetting(ConfigEntryBase e)
        {
            if (e.SettingType != typeof(bool)) return false;
            if (ReferenceEquals(e, WildWasteland.Modules.NPC.NpcModule.ShowNav)) return true;
            string key = e.Definition.Key;
            return key.IndexOf("Verbose", System.StringComparison.OrdinalIgnoreCase) >= 0 || key == "Log" || key.EndsWith("Log", System.StringComparison.OrdinalIgnoreCase);
        }

        static void Logging(ToolLayout l, ConfigFile file, IModule owner)
        {
            foreach (var key in file.Keys)
            {
                var e = file[key];
                if (BelongsTo(e, owner) && !ReferenceEquals(e, Plugin.VerboseLog) && VerboseSetting(e))
                    l.Toggle((ConfigEntry<bool>)e, ReferenceEquals(e, WildWasteland.Modules.NPC.NpcModule.ShowNav)
                        ? "Show navigation, ghosts and NPC states in the world" : key.Section + " / " + key.Key);
            }
        }

        static bool RetainedDebugSetting(ConfigEntryBase e)
        {
            if (WeatherTuning(e) || MapFogSetting(e) || VerboseSetting(e) || ReferenceEquals(e, Patrols.MenuKey) || ReferenceEquals(e, Patrols.TemplateDumpKey)) return true;
            return e.SettingType == typeof(bool) && DebugSetting(e) && !ReferenceEquals(e, Player.WeaponAdjust)
                && !ReferenceEquals(e, Patrols.TurretSpotEditor) && !ReferenceEquals(e, Patrols.BusPassengerPoseEditorEnabled);
        }

        static bool DeveloperSetting(ConfigEntryBase e) { return DebugSetting(e) && !RetainedDebugSetting(e); }

        static IModule _categoryOwner;
        static bool BelongsTo(ConfigEntryBase e, IModule owner)
        {
            if (ReferenceEquals(e, Patrols.MenuKey) || ReferenceEquals(e, Patrols.TemplateDumpKey)) return owner != null && owner.Id == "Patrols";
            string section = e.Definition.Section; int cut = section.IndexOf(" / ", System.StringComparison.Ordinal);
            foreach (var m in ModuleHost.Entries)
                if (section == m.Module.Id || (cut == m.Module.Id.Length && string.CompareOrdinal(section, 0, m.Module.Id, 0, cut) == 0)) return ReferenceEquals(owner, m.Module);
            return owner == null;
        }
        static bool RemainingDeveloperSetting(ConfigEntryBase e) { return DeveloperSetting(e) && BelongsTo(e, _categoryOwner); }
        static bool HasDebugSettings(ConfigFile file, IModule owner)
        {
            foreach (var key in file.Keys) if (BelongsTo(file[key], owner) && RetainedDebugSetting(file[key])) return true;
            return false;
        }

        static void Debug(ToolLayout l)
        {
            l.DeduplicateEntries = true;
            EnsureSwitches();
            for (int i = -1; i < ModuleHost.Entries.Count; i++)
            {
                var owner = i < 0 ? null : ModuleHost.Entries[i].Module;
                if (owner != null && !HasDebugSettings(Plugin.Settings, owner) && !HasDebugSettings(Plugin.Tools, owner)) continue;
                if (!l.Category("Debug/" + (owner != null ? owner.Id : "Core"), CategoryTitle(owner))) continue;
                DebugSectionHud(l, owner);
                DebugSectionSurvival(l, owner);
                DebugSectionPatrols(l, owner);
                l.Heading("Logging");
                if (owner == null)
                {
                    l.Toggle(Plugin.VerboseLog, "VerboseLog");
                    l.Text("General verbose logging. Module log switches retain their own behavior; NPC traces also work with general logging off.");
                }
                Logging(l, Plugin.Settings, owner); Logging(l, Plugin.Tools, owner);
                l.EntryFilter = RetainedDebugSetting;
                l.Heading("Switches");
                DebugSwitchesPatrols(l, owner);
                foreach (var s in _switches) if (ReferenceEquals(s.Module, owner)) l.Toggle(s.Entry, l.PageLabel(s.Entry) ?? s.Label);
                l.EntryFilter = null;
                if (owner == null) l.Toggle(Plugin.DeveloperMode, "Developer mode");
            }
        }

        static void Developer(ToolLayout l)
        {
            l.DeduplicateEntries = true;
            for (int i = -1; i < ModuleHost.Entries.Count; i++)
            {
                var owner = i < 0 ? null : ModuleHost.Entries[i].Module;
                if (!l.Category("Developer/" + (owner != null ? owner.Id : "Core"), CategoryTitle(owner))) continue;
                if (owner != null && owner.Id == "Performance")
                {
                    l.EntryFilter = null;
                    WildWasteland.Modules.Performance.PerformancePage.Instance.DrawDeveloper(l);
                }
                l.EntryFilter = DeveloperSetting;
                foreach (var p in _debugPages) if (ReferenceEquals(p.Owner, owner) && p.IsTool) DrawDebugPage(l, p);
                foreach (var p in _debugPages) if (ReferenceEquals(p.Owner, owner) && !p.IsTool) DrawDebugPage(l, p);
                _categoryOwner = owner; l.EntryFilter = RemainingDeveloperSetting;
                l.SearchSection("Other settings"); l.Prefix = "Developer/";
                _settings.Draw(l);
                l.EntryFilter = null;
                if (owner == null)
                {
                    l.SearchSection("Core diagnostics");
                    if (l.Button("Write startup summary to the log", true, false, 360)) Log.Always(ModuleHost.Summary());
                }
                l.SearchSection("Harmony patches"); Harmony(l, owner);
            }
        }

        // Switches block (RC12): every bool the Debug page shows further down, grouped by module, drawn once here (deduplication hides
        // the lower copies). The sorted list is rebuilt only when either file's entry count changes, not per OnGUI event.
        sealed class SwitchItem { internal ConfigEntry<bool> Entry; internal string Label, Section; internal int Order, Index; internal IModule Module; internal string Heading, HeadingOff; }
        sealed class SwitchOrder : IComparer<SwitchItem>
        {
            public int Compare(SwitchItem a, SwitchItem b)
            {
                int c = a.Order.CompareTo(b.Order); if (c != 0) return c;
                c = string.CompareOrdinal(a.Section, b.Section); if (c != 0) return c;
                c = string.Compare(a.Label, b.Label, System.StringComparison.OrdinalIgnoreCase); if (c != 0) return c;
                return a.Index.CompareTo(b.Index);
            }
        }
        static readonly List<SwitchItem> _switches = new List<SwitchItem>();
        static readonly SwitchOrder _switchOrder = new SwitchOrder();
        static int _switchCount = -1;

        static void BuildSwitches()
        {
            _switches.Clear();
            int index = 0;
            AddSwitches(Plugin.Settings, ref index); AddSwitches(Plugin.Tools, ref index);
            _switches.Sort(_switchOrder);
            for (int i = 0; i < _switches.Count; i++)   // same label twice in one module: name the sub-section
                for (int j = i + 1; j < _switches.Count && _switches[j].Order == _switches[i].Order; j++)
                    if (string.Equals(_switches[i].Label, _switches[j].Label, System.StringComparison.OrdinalIgnoreCase) && _switches[i].Section != _switches[j].Section)
                    { _switches[i].Label += " (" + _switches[i].Section + ")"; _switches[j].Label += " (" + _switches[j].Section + ")"; }
        }

        static void AddSwitches(ConfigFile file, ref int index)
        {
            if (file == null) return;
            foreach (var key in file.Keys)
            {
                var e = file[key] as ConfigEntry<bool>;
                index++;
                if (e == null || !DebugSetting(e)) continue;
                string sec = key.Section; int cut = sec.IndexOf(" / ", System.StringComparison.Ordinal);
                string prefix = cut >= 0 ? sec.Substring(0, cut) : sec;
                string label = ReferenceEquals(e, CleanHudSettings.Enabled) ? "Clean HUD" : ReferenceEquals(e, CleanHudSettings.MenuEnabled) ? "Clean menus" : ReferenceEquals(e, CleanHudSettings.CleanFont) ? "CleanFont" : Readable(key.Key);
                var item = new SwitchItem { Entry = e, Label = label, Section = sec, Order = -1, Index = index, Heading = "Core", HeadingOff = "Core" };
                for (int m = 0; m < ModuleHost.Entries.Count; m++)
                {
                    var me = ModuleHost.Entries[m];
                    if (me.Module.Id != prefix) continue;
                    item.Order = m; item.Module = me.Module; item.Heading = me.Module.DisplayName; item.HeadingOff = me.Module.DisplayName + " (module off)";
                    break;
                }
                _switches.Add(item);
            }
        }

        /// <summary>"TurretSpotEditor" -> "Turret spot editor"; keys with spaces are kept as they are.</summary>
        static string Readable(string key)
        {
            if (string.IsNullOrEmpty(key) || key.IndexOf(' ') >= 0) return key;
            var sb = new System.Text.StringBuilder(key.Length + 8);
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                bool split = i > 0 && char.IsUpper(c) && (char.IsLower(key[i - 1]) || (i + 1 < key.Length && char.IsLower(key[i + 1]) && char.IsUpper(key[i - 1])));
                if (split) sb.Append(' ');
                sb.Append(split && i + 1 < key.Length && char.IsLower(key[i + 1]) ? char.ToLowerInvariant(c) : c);
            }
            return sb.ToString();
        }

        static void EnsureSwitches()
        {
            int count = (Plugin.Settings != null ? Plugin.Settings.Count : 0) + (Plugin.Tools != null ? Plugin.Tools.Count : 0);
            if (count != _switchCount) { _switchCount = count; BuildSwitches(); }
        }

        static readonly Dictionary<string, double[]> _sum = new Dictionary<string, double[]>();

        internal static void Performance(ToolLayout l)
        {
            l.Text("Managed time of every Wild Wasteland job, measured by the scheduler. Budget: average <= 0.5 ms per frame, worst <= 2 ms. Times while this window is open are from the paused game; close it and reopen to read live numbers.");
            l.Value("Frames measured", Scheduler.Frames.ToString(CultureInfo.InvariantCulture));
            l.Value("WW average / worst frame (ms)", F(Scheduler.AvgFrameMs) + " / " + F(Scheduler.MaxFrameMs));
            if (l.Button("Reset statistics", true, false, 220)) Scheduler.ResetStats();
            _sum.Clear();
            foreach (var j in Scheduler.AllJobs())
            {
                string owner = j.Owner != null ? j.Owner.Id : "Core";
                double[] s; if (!_sum.TryGetValue(owner, out s)) { s = new double[2]; _sum[owner] = s; }
                s[0] += j.TotalMs; if (j.MaxMs > s[1]) s[1] = j.MaxMs;
            }
            l.Heading("Per module (avg ms/frame, worst job ms)");
            double frames = Mathf.Max(1, Scheduler.Frames);
            foreach (var kv in _sum) l.Value(kv.Key, F(kv.Value[0] / frames) + "   worst " + F(kv.Value[1]));
            l.Heading("Jobs (avg ms/frame, last, worst, calls, errors)");
            foreach (var j in Scheduler.AllJobs())
                l.Value(j.ToString(), F(j.TotalMs / frames) + "  " + F(j.LastMs) + "  " + F(j.MaxMs) + "  " + j.Calls + (j.Exceptions > 0 ? "  ERR " + j.Exceptions : "") + (j.Enabled ? "" : "  (off)"));
        }

        static void Harmony(ToolLayout l, IModule module)
        {
            string owner = module != null ? module.Id : "Core";
            l.Heading("Harmony patches");
            if (module == null)
            {
                l.Text("Shared PlayMaker targets go through Core dispatchers; module patches are listed inside their module category.");
                l.Value("Conflicts", Patches.Conflicts.ToString(CultureInfo.InvariantCulture));
                l.Text(Hooks.Summary());
            }
            foreach (var r in Patches.Records)
            {
                if (r.Owner != owner) continue;
                l.Text((r.Ok ? "" : "FAILED ") + r.Kind + "  " + r.Target + (r.Ok ? "" : "  - " + r.Error), r.Ok ? (Color?)null : Ledger.Danger);
            }
        }
    }
}
