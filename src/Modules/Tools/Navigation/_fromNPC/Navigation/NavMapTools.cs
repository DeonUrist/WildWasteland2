using System;
using System.Collections.Generic;
using System.IO;

namespace WildWasteland.Modules.NPC
{
    internal static partial class Nav
    {
        private static readonly string[] VanillaPoiNames =
        {
            "Camp_1", "Camp_2", "Camp_3", "Camp_4", "Camp_5", "Camp_6", "Camp_7", "Camp_8",
            "Camp_9", "Camp_10", "Camp_11", "Camp_12", "Camp_13", "Camp_14", "Camp_15", "Camp_16",
            "Building_1", "Building_2", "Building_3", "Building_4", "Building_5", "Building_6", "Building_7",
            "Cave_1", "Cave_2", "Cave_3", "Cave_4", "Cave_5", "Cave_6", "Cave_7",
            "Wreck_1", "Wreck_2", "Wreck_3", "Wreck_4", "Wreck_5", "Wreck_6", "Wreck_7", "Wreck_8", "Starter_Area"
        };

        // Explicit debug action. Only generated files in these two exact directories are removed; no recursive deletion.
        internal static void ResetCustomMaps()
        {
            var removed = new HashSet<Structure>();
            for (int i = _structures.Count - 1; i >= 0; i--)
            {
                var s = _structures[i];
                if (s.LoadedVanilla || (s.MapJob != null && s.MapJob.Vanilla)) continue;
                CloseMap(s); removed.Add(s);
                if (_baking == s) _baking = null;
                if (_needed == s) _needed = null;
                if (s.Root != null)
                {
                    _known.Remove(s.Root.GetInstanceID());
                    var colliders = s.MapColliders ?? s.Root.GetComponentsInChildren<UnityEngine.Collider>(true);
                    foreach (var c in colliders) if (c != null) _floorCols.Remove(c.GetInstanceID());
                }
                _structures.RemoveAt(i);
            }
            for (int i = _maskQueue.Count - 1; i >= 0; i--) if (removed.Contains(_maskQueue[i])) _maskQueue.RemoveAt(i);
            PickReset(); _compRing.Clear(); _outPlans.Clear(); _outMemo.Clear(); _exits.Clear(); _relaxedFields.Clear(); _debug.Clear();
            ForgetTemplates(false);
            _sweep.Clear(); _sweepAdded = 0; _swept = false; _nextSweep = _nextPick = 0f;
            Needed = _baking != null && _baking == _needed; _urgent = false;
            int deleted = 0, failed = 0;
            DeleteCustomMaps(CustomMapDir, ref deleted, ref failed);
            DeleteCustomMaps(System.IO.Path.Combine(Core.Assets.ModuleDir("NPC"), "NavMaps"), ref deleted, ref failed);
            _highlightIndexReady = false;
            MapActionStatus = "Reset custom pathfinding: " + deleted + " cached file(s) deleted, " + removed.Count + " live map(s) queued for rediscovery. Vanilla maps retained."
                + (failed == 0 ? "" : " " + failed + " file operation(s) failed; see the log.");
            Core.Log.Always("[NPC] Nav: " + MapActionStatus);
        }

        private static void DeleteCustomMaps(string dir, ref int deleted, ref int failed)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (string pattern in new[] { "*.wwnav", "*.wwnav.*.tmp" })
                    foreach (string file in Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                    {
                        try { File.Delete(file); deleted++; }
                        catch (Exception e) { failed++; NpcModule.Log.LogWarning("Nav: could not delete custom map " + file + ": " + e.Message); }
                    }
            }
            catch (Exception e) { failed++; NpcModule.Log.LogWarning("Nav: could not reset custom maps in " + dir + ": " + e.Message); }
        }

        // Capture candidates from a clean vanilla setup. Reviewed files go into NPC/Embedded/NavMaps/Vanilla in the repo.
        internal static void ExportVanillaMaps()
        {
            string export = System.IO.Path.Combine(NpcModule.DataDir, "NavMaps", "VanillaExport");
            var expected = new HashSet<string>(VanillaPoiNames, StringComparer.Ordinal);
            var found = new HashSet<string>(StringComparer.Ordinal);
            var copied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int failed = 0;
            try
            {
                Directory.CreateDirectory(export);
                const string prefix = "NPC.NavMaps.Vanilla.";
                var assembly = typeof(Nav).Assembly;
                foreach (string resource in WildWasteland.Core.DiskResources.Names)
                {
                    if (!resource.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    string name = resource.Substring(prefix.Length), poi = VanillaFileName(name, expected);
                    if (poi == null) continue;
                    try
                    {
                        using (var input = WildWasteland.Core.DiskResources.Open(resource))
                        using (var output = new FileStream(System.IO.Path.Combine(export, name), FileMode.Create, FileAccess.Write)) input.CopyTo(output);
                        copied.Add(name); found.Add(poi);
                    }
                    catch (Exception e) { failed++; NpcModule.Log.LogWarning("Nav: could not export " + resource + ": " + e.Message); }
                }
                foreach (string dir in new[] { VanillaMapDir, CustomMapDir, System.IO.Path.Combine(Core.Assets.ModuleDir("NPC"), "NavMaps") })
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (string file in Directory.GetFiles(dir, "*.wwnav", SearchOption.TopDirectoryOnly))
                    {
                        string name = System.IO.Path.GetFileName(file), poi = VanillaFileName(name, expected);
                        if (poi == null || copied.Contains(name)) continue;
                        try
                        {
                            File.Copy(file, System.IO.Path.Combine(export, name), true);
                            copied.Add(name); found.Add(poi);
                        }
                        catch (Exception e) { failed++; NpcModule.Log.LogWarning("Nav: could not export " + file + ": " + e.Message); }
                    }
                }
                var missing = new List<string>();
                foreach (string name in VanillaPoiNames) if (!found.Contains(name)) missing.Add(name);
                MapActionStatus = "Exported " + copied.Count + " candidate map file(s), covering " + found.Count + "/" + VanillaPoiNames.Length
                    + " vanilla POIs, to " + export + ". Capture with custom POI mods disabled; verify before shipping."
                    + (failed == 0 ? "" : " " + failed + " file(s) failed; see the log.");
                Core.Log.Always("[NPC] Nav: " + MapActionStatus);
                if (missing.Count > 0) Core.Log.Always("[NPC] Nav: vanilla capture missing: " + string.Join(", ", missing.ToArray()));
            }
            catch (Exception e)
            {
                MapActionStatus = "Vanilla map export failed: " + e.Message;
                NpcModule.Log.LogWarning("Nav: " + MapActionStatus);
            }
        }

        private static string VanillaFileName(string name, HashSet<string> expected)
        {
            int dash = name.LastIndexOf('-');
            if (dash <= 0 || name.Length - dash != 23 || !name.EndsWith(".wwnav", StringComparison.OrdinalIgnoreCase)) return null;
            string poi = name.Substring(0, dash); if (!expected.Contains(poi)) return null;
            for (int i = dash + 1; i < dash + 17; i++) if (!Uri.IsHexDigit(name[i])) return null;
            return poi;
        }
    }
}
