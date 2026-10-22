using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WildWasteland.Core;

namespace WildWasteland.Modules.Tools
{

    public sealed class AuthoringGroup
    {
        public string Id = "", Name = "";
        public string Faction="Scrapyard";
        public float MinKm, Chance;
        public int MinBosses;
        public bool UniformCargo;
        public List<string> Templates = new List<string>();
        public List<string> AllowedCargo = new List<string>();
        public List<string> MerchantTypes = new List<string>();
        /// <summary>Checks current progression and chance gates for the authored spawn group.</summary>
        public bool Eligible(float km, int bosses) { return Chance > 0 && km >= MinKm && bosses >= MinBosses; }

        /// <summary>Selects the configured outcome using the caller random source.</summary>
        public static AuthoringGroup Roll(IEnumerable<AuthoringGroup> source, float km, int bosses, Func<double> random, string avoid = null)
        {
            var list = source.Where(g => g.Eligible(km, bosses)).ToList();
            if (!string.IsNullOrEmpty(avoid) && list.Count > 1) list.RemoveAll(g => string.Equals(g.Id, avoid, StringComparison.OrdinalIgnoreCase));
            double total = list.Sum(g => (double)g.Chance);
            if (total <= 0) return null;
            double roll = random() * total;
            foreach (var group in list) { roll -= group.Chance; if (roll < 0) return group; }
            return list[list.Count - 1];
        }
    }

    public sealed class PatrolEditorData
    {
        public List<AuthoringGroup> Patrols = new List<AuthoringGroup>(), Convoys = new List<AuthoringGroup>(), Merchants=new List<AuthoringGroup>();
        public List<LootProfile> CarLoot = new List<LootProfile>(), TruckLoot = new List<LootProfile>();
        public List<string> TemplateOrder = new List<string>();   // the template lists' order in the editor (names; unlisted ones follow, defaults first, by name)

        /// <summary>Parses current JSON authoring data and validates its supported fields.</summary>
        public static PatrolEditorData FromJson(string text)
        {
            var root = Json.Parse(text) as Dictionary<string, object>;
            if (root == null) throw new FormatException("Expected editor JSON object");
            var result = new PatrolEditorData();
            result.Patrols = Groups(root, "patrols"); result.Convoys = Groups(root, "convoys");
            result.Merchants=Groups(root,"merchants");
            result.CarLoot = root.ContainsKey("carLoot")?Loot(root, "carLoot"):new List<LootProfile>(); result.TruckLoot = root.ContainsKey("truckLoot")?Loot(root, "truckLoot"):new List<LootProfile>();
            object order;
            if (root.TryGetValue("templateOrder", out order) && order is List<object>) result.TemplateOrder = ((List<object>)order).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var g in result.Convoys.Where(g => g.AllowedCargo == null))
                g.AllowedCargo = g.Id.EndsWith("-truck", StringComparison.OrdinalIgnoreCase) ? new List<string> { "truck-empty" } : result.TruckLoot.Where(p => p.Id != "truck-empty").Select(p => p.Id).ToList();
            return result;
        }

        /// <summary>Enumerates and validates current JSON object rows.</summary>
        private static IEnumerable<Dictionary<string, object>> Rows(Dictionary<string, object> root, string key)
        {
            object value;
            if (!root.TryGetValue(key, out value) || !(value is List<object>)) throw new FormatException("Missing array: " + key);
            foreach (var row in (List<object>)value)
            {
                var obj = row as Dictionary<string, object>;
                if (obj == null) throw new FormatException("Invalid object in " + key);
                yield return obj;
            }
        }

        /// <summary>Parses authored spawn groups and their faction and eligibility fields.</summary>
        private static List<AuthoringGroup> Groups(Dictionary<string, object> root, string key)
        {
            var result = new List<AuthoringGroup>();
            foreach (var row in Rows(root, key))
            {
                var g = new AuthoringGroup { Id = Json.Text(row, "id", ""), Name = Json.Text(row, "name", ""), MinKm = Number(row, "minKm", 0, 10000), MinBosses = (int)Number(row, "minBosses", 0, 7), Chance = Number(row, "chance", 0, 100), UniformCargo = Json.Bool(row, "uniformCargo", false) };
                g.Faction=PatrolEditor.Source.NormalizeFaction(Json.Text(row,"faction","Scrapyard"));
                object values;
                if(row.TryGetValue("merchantTypes",out values)&&values is List<object>)g.MerchantTypes=((List<object>)values).OfType<string>().Where(EditorMerchants.Known).Distinct().ToList();
                if(key=="merchants")g.MerchantTypes=EditorMerchants.Selection(g.MerchantTypes).ToList();
                if (row.TryGetValue("templates", out values) && values is List<object>) g.Templates = ((List<object>)values).OfType<string>().ToList();
                g.AllowedCargo = row.TryGetValue("allowedCargo", out values) && values is List<object> ? ((List<object>)values).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList() : key == "convoys" ? null : new List<string>();
                if (g.Id.Length == 0 || result.Any(x => x.Id.Equals(g.Id, StringComparison.OrdinalIgnoreCase))) throw new FormatException("Missing or duplicate group id");
                result.Add(g);
            }
            return result;
        }

        /// <summary>Reads the current editor cargo metadata.</summary>
        private static List<LootProfile> Loot(Dictionary<string, object> root, string key)
        {
            var result = new List<LootProfile>();
            foreach (var row in Rows(root, key))
            {
                var p = new LootProfile { Id = Json.Text(row, "id", ""), Name = Json.Text(row, "name", ""), TextureKey = Json.Text(row, "textureKey", ""), Default = Json.Bool(row, "default", false) };
                p.SpawnChance = row.ContainsKey("spawnChance") ? Number(row, "spawnChance", 0, 100) : DefaultCargoChance(p.Id);
                foreach (var item in Rows(row, "items"))
                {
                    string id = Json.Text(item, "id", "");
                    if (id.Length == 0 || p.Items.Any(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase))) throw new FormatException("Missing or duplicate loot item");
                    p.Items.Add(new LootItem { Id = id, Chance = Number(item, "chance", 0, 100000) });
                }
                if (p.Id.Length == 0 || result.Any(x => x.Id.Equals(p.Id, StringComparison.OrdinalIgnoreCase))) throw new FormatException("Missing or duplicate loot id");
                result.Add(p);
            }
            return result;
        }

        /// <summary>Reads and clamps an authored number after rejecting invalid numeric values.</summary>
        private static float Number(Dictionary<string, object> row, string key, float min, float max)
        {
            float n = Json.Num(row, key, min);
            if (float.IsInfinity(n) || float.IsNaN(n)) throw new FormatException("Invalid " + key);
            return Math.Max(min, Math.Min(max, n));
        }
        /// <summary>Returns the shipped default cargo chance for the current editor data.</summary>
        private static float DefaultCargoChance(string id)
        {
            switch (id)
            {
                case "truck-food": return 18; case "truck-water": return 14;
                case "truck-gasoline": case "truck-diesel": case "truck-medicine": case "truck-weapons": return 11;
                case "truck-drugs": case "truck-mechanic": case "truck-corpses": return 7;
                case "truck-rats": return 3; case "truck-empty": return 0; default: return 10;
            }
        }

        /// <summary>Serializes current authoring data in its stable field order.</summary>
        public string ToJson()
        {
            Func<AuthoringGroup, object> group = g => Json.Object("id", g.Id, "name", g.Name,"faction",g.Faction, "minKm", g.MinKm, "minBosses", g.MinBosses, "chance", g.Chance, "uniformCargo", g.UniformCargo, "templates", g.Templates, "allowedCargo", g.AllowedCargo,"merchantTypes",Merchants.Contains(g)?EditorMerchants.Selection(g.MerchantTypes).ToList():g.MerchantTypes);
            Func<LootProfile, object> loot = p => Json.Object("id", p.Id, "name", p.Name, "textureKey", p.TextureKey, "default", p.Default, "spawnChance", p.SpawnChance, "items", p.Items.Select(i => Json.Object("id", i.Id, "chance", i.Chance)).ToList());
            return Json.Write(Json.Object("schema", 3, "patrols", Patrols.Select(group).ToList(), "convoys", Convoys.Select(group).ToList(),"merchants",Merchants.Select(group).ToList(), "carLoot", CarLoot.Select(loot).ToList(), "truckLoot", TruckLoot.Select(loot).ToList(), "templateOrder", TemplateOrder));
        }
    }
}
