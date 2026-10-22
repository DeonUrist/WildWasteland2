using System;
using System.Collections.Generic;
using System.IO;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    /// <summary>Immutable, validated generation rules loaded once before terrain workers start.</summary>
    internal sealed class TownTemplate
    {
        internal string Id, DisplayName;
        internal TownFaction Faction;
        internal int Weight = 1, MinLots = 5, MaxLots = 8;
        internal bool Walls = true, RequireMerchant = true, UseGlobalLotRange;
        internal string[] AllowedPois = new string[0];
        internal string[] ExternalParkingPois = new[] { "TownsGasStation" };
        internal int PopulationMin = 10, PopulationMax = 20, LootMin = 5, LootMax = 10;
        internal Dictionary<string, object> Data;
        /// <summary>Parses one authoring document and rejects incomplete or unsafe generation input.</summary>
        internal static TownTemplate Parse(string id, string json)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64) throw new FormatException("Template id");
            foreach (char c in id) if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') throw new FormatException("Template id");
            var d = Json.ParseObject(json); if (d == null) throw new FormatException("Template object required");
            TownFaction faction;
            if (!Enum.TryParse(Text(d, "Faction", id), true, out faction) || !Enum.IsDefined(typeof(TownFaction), faction)) throw new FormatException("Template faction");
            var t = new TownTemplate { Id = id, DisplayName = Text(d, "DisplayName", id), Faction = faction,
                Weight = Number(d, "Weight", 1, 0, 1000), MinLots = Number(d, "MinLots", 5, 3, 32), MaxLots = Number(d, "MaxLots", 8, 3, 32),
                Walls = Flag(d, "Walls", true), RequireMerchant = Flag(d, "RequireMerchant", true),
                UseGlobalLotRange = !d.ContainsKey("MinLots") && !d.ContainsKey("MaxLots"),
                AllowedPois = Strings(d, "AllowedPois", new string[0]), ExternalParkingPois = Strings(d, "ExternalParkingPois", new[] { "TownsGasStation" }),
                PopulationMin = Number(d, "PopulationMinPercent", 10, 0, 500), PopulationMax = Number(d, "PopulationMaxPercent", 20, 0, 500),
                LootMin = Number(d, "LootMinPercent", 5, 0, 500), LootMax = Number(d, "LootMaxPercent", 10, 0, 500), Data = d };
            if (t.MinLots > t.MaxLots || t.PopulationMin > t.PopulationMax || t.LootMin > t.LootMax) throw new FormatException("Template ranges are reversed");
            d["Id"] = id; d["Faction"] = faction.ToString(); return t;
        }
        /// <summary>Reads an optional text field.</summary>
        private static string Text(Dictionary<string, object> d, string key, string fallback)
        { object v; if (!d.TryGetValue(key, out v)) return fallback; var s = v as string; if (s == null || s.Length > 256) throw new FormatException(key); return s; }
        /// <summary>Reads a bounded integer without truncating fractions.</summary>
        private static int Number(Dictionary<string, object> d, string key, int fallback, int min, int max)
        { object v; if (!d.TryGetValue(key, out v)) return fallback; if (!(v is double)) throw new FormatException(key); double n = (double)v; if (double.IsNaN(n) || n < min || n > max || n != Math.Truncate(n)) throw new FormatException(key); return (int)n; }
        /// <summary>Reads an optional boolean.</summary>
        private static bool Flag(Dictionary<string, object> d, string key, bool fallback)
        { object v; if (!d.TryGetValue(key, out v)) return fallback; if (!(v is bool)) throw new FormatException(key); return (bool)v; }
        /// <summary>Reads a bounded list of prefab/custom POI names.</summary>
        private static string[] Strings(Dictionary<string, object> d, string key, string[] fallback)
        { object v; if (!d.TryGetValue(key, out v)) return fallback; var a = v as List<object>; if (a == null || a.Count > 256) throw new FormatException(key); var result = new string[a.Count]; for (int i = 0; i < a.Count; i++) { result[i] = a[i] as string; if (string.IsNullOrEmpty(result[i]) || result[i].Length > 256) throw new FormatException(key); } return result; }
    }
    /// <summary>POI template authoring store; publishes replacement arrays so workers never observe a mutable dictionary.</summary>
    internal sealed class TownTemplates
    {
        private readonly Dictionary<string, TownTemplate> _templates = new Dictionary<string, TownTemplate>(StringComparer.Ordinal);
        internal TownTemplate[] Snapshot = new TownTemplate[0];
        /// <summary>Template files are authoring data rather than per-slot sidecars.</summary>
        private static string DirectoryPath { get { return Path.Combine(Assets.ModuleConfigDir("POI"), "Templates"); } }
        /// <summary>Loads four built-in faction templates and validated local overrides.</summary>
        internal void Load()
        {
            _templates.Clear(); Directory.CreateDirectory(DirectoryPath);
            foreach (TownFaction faction in Enum.GetValues(typeof(TownFaction)))
            {
                string id = faction.ToString();
                var data = new Dictionary<string, object> { { "Id", id }, { "Faction", id }, { "DisplayName", id }, { "Weight", 1 },
                    { "Walls", faction == TownFaction.Coyotes || faction == TownFaction.Scrapyard }, { "RequireMerchant", faction == TownFaction.Coyotes },
                    { "AllowedPois", new object[0] }, { "ExternalParkingPois", new object[] { "TownsGasStation" } }, { "PopulationMinPercent", 10 }, { "PopulationMaxPercent", 20 }, { "LootMinPercent", 5 }, { "LootMaxPercent", 10 } };
                _templates[id] = TownTemplate.Parse(id, Json.Write(data));
            }
            foreach (string file in Directory.GetFiles(DirectoryPath, "*.json"))
                try { string id = Path.GetFileNameWithoutExtension(file); _templates[id] = TownTemplate.Parse(id, File.ReadAllText(file)); }
                catch (Exception error) { Log.Warn("[POI] Template rejected " + Path.GetFileName(file) + ": " + error.Message); }
            Publish();
        }
        /// <summary>Copies template ids for the editor.</summary>
        internal void Ids(List<string> into) { into.Clear(); into.AddRange(_templates.Keys); into.Sort(StringComparer.Ordinal); }
        /// <summary>Returns normalized JSON for the selected template.</summary>
        internal bool Get(string id, out string json) { TownTemplate t; json = null; if (!_templates.TryGetValue(id, out t)) return false; json = Json.Write(t.Data); return true; }
        /// <summary>Validates, atomically saves, and publishes a replacement generation snapshot.</summary>
        internal bool Save(string id, string json)
        {
            try { var t = TownTemplate.Parse(id, json); Directory.CreateDirectory(DirectoryPath); string path = Path.Combine(DirectoryPath, id + ".json"); File.WriteAllText(path + ".tmp", Json.Write(t.Data)); AtomicFile.Replace(path + ".tmp", path, path + ".bak"); _templates[id] = t; Publish(); return true; }
            catch (Exception error) { Log.Warn("[POI] Template not saved: " + error.Message); return false; }
        }
        /// <summary>Builds an independent worker-visible array after a load or edit.</summary>
        private void Publish() { Snapshot = new List<TownTemplate>(_templates.Values).ToArray(); Array.Sort(Snapshot, (a, b) => string.CompareOrdinal(a.Id, b.Id)); }
    }
}
