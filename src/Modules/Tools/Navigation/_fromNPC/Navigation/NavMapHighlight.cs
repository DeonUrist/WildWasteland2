using System;
using System.Collections.Generic;
using System.IO;

namespace WildWasteland.Modules.NPC
{
    // Temporary collection aid. Disable HighlightUnbakedMapPois to restore normal colors; no map data or marker ownership changes.
    internal static partial class Nav
    {
        private static readonly HashSet<string> _capturedMapTypes = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _highlightNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly char[] _invalidMapNameChars = System.IO.Path.GetInvalidFileNameChars();
        private static bool _highlightIndexReady, _highlightIndexValid, _highlightActive;

        private static string HighlightMapName(string name)
        {
            string result;
            if (_highlightNames.TryGetValue(name, out result)) return result;
            int paren = name.IndexOf('('), space = name.IndexOf(' ');
            int end = paren < 0 ? space : space < 0 ? paren : Math.Min(paren, space);
            result = end < 0 ? name : name.Substring(0, end);
            foreach (char c in _invalidMapNameChars) if (result.IndexOf(c) >= 0) result = result.Replace(c, '_');
            if (result.Length > 80) result = result.Substring(0, 80);
            _highlightNames.Add(name, result); return result;
        }

        internal static bool HighlightMissingPoiBake(string name)
        {
            return NpcModule.HighlightUnbakedMapPois != null && NpcModule.HighlightUnbakedMapPois.Value
                && _highlightIndexReady && _highlightIndexValid && !string.IsNullOrEmpty(name)
                && !_capturedMapTypes.Contains(HighlightMapName(name));
        }

        private static void ResetMapHighlight()
        {
            _capturedMapTypes.Clear(); _highlightNames.Clear();
            _highlightIndexReady = _highlightIndexValid = _highlightActive = false;
        }

        private static void RememberCapturedMap(Structure s)
        {
            if (s.MapFile != null) AddCapturedMap(s.MapFile);
        }

        private static void AddCapturedMap(string file)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(file); int dash = name.LastIndexOf('-');
            if (dash <= 0 || name.Length - dash != 17) return;
            for (int i = dash + 1; i < name.Length; i++) if (!Uri.IsHexDigit(name[i])) return;
            _capturedMapTypes.Add(name.Substring(0, dash));
        }

        private static void TickMapHighlight()
        {
            bool enabled = NpcModule.HighlightUnbakedMapPois != null && NpcModule.HighlightUnbakedMapPois.Value;
            if (!enabled)
            {
                if (_highlightActive) Core.Log.Always("[NPC] Nav: unbaked POI map tint off; normal colors restored");
                _highlightActive = false; return;
            }
            if (_highlightActive && _highlightIndexReady) return;
            _highlightActive = true; _highlightIndexReady = true; _highlightIndexValid = false; _capturedMapTypes.Clear();
            try
            {
                // Read only on scene/enable/reset, never in the marker draw loop. New successful saves/loads update the set directly.
                foreach (string dir in new[] { CustomMapDir, VanillaMapDir,
                    System.IO.Path.Combine(Core.Assets.ModuleDir("NPC"), "NavMaps"),
                    System.IO.Path.Combine(NpcModule.DataDir, "NavMaps", "VanillaExport") })
                    if (Directory.Exists(dir)) foreach (string file in Directory.GetFiles(dir, "*.wwnav", SearchOption.TopDirectoryOnly)) AddCapturedMap(file);
                const string prefix = "NPC.NavMaps.Vanilla.";
                foreach (string resource in WildWasteland.Core.DiskResources.Names)
                    if (resource.StartsWith(prefix, StringComparison.Ordinal) && resource.EndsWith(".wwnav", StringComparison.OrdinalIgnoreCase))
                        AddCapturedMap(resource.Substring(prefix.Length));
                _highlightIndexValid = true;
                Core.Log.Always("[NPC] Nav: unbaked POI map tint on; " + _capturedMapTypes.Count + " POI types have saved bakes");
            }
            catch (Exception e) { NpcModule.Log.LogWarning("Nav: unbaked POI map tint disabled: bake index could not be read: " + e.Message); }
        }
    }
}
