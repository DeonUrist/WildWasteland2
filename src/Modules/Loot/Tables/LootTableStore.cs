using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using WildWasteland.Core;

namespace WildWasteland.Modules.Loot
{
    /// <summary>Authoritative current loot tables, POI/lot assignments and cargo metadata in LootTables.json. Runtime contributions overlay file tables without a second persisted source.</summary>
    internal sealed class LootTableStore
    {
        private const int Schema = 1;
        private readonly Dictionary<string, LootTable> _file = new Dictionary<string, LootTable>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _order = new List<string>();
        private readonly Dictionary<string, LootTable> _runtime = new Dictionary<string, LootTable>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _poi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _lots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string _path;

        /// <summary>Increments when tables or assignments change.</summary>
        internal int Revision { get; private set; }

        /// <summary>Number of tables (file + runtime overlay ids).</summary>
        internal int Count { get { int n = _file.Count; foreach (var id in _runtime.Keys) if (!_file.ContainsKey(id)) n++; return n; } }

        /// <summary>Path of LootTables.json (null before <see cref="Load"/>).</summary>
        internal string FilePath { get { return _path; } }

        /// <summary>Reads LootTables.json from <paramref name="dir"/> (an invalid file is preserved as LootTables.invalid-*.json and
        /// replaced), seeding shipped current Loot defaults only when a file must be created.</summary>
        internal void Load(string dir)
        {
            _path = Path.Combine(dir, "LootTables.json");
            _file.Clear(); _order.Clear(); _poi.Clear(); _lots.Clear();
            bool dirty = false;
            if (File.Exists(_path))
            {
                try { Parse(File.ReadAllText(_path)); }
                catch (Exception e)
                {
                    _file.Clear(); _order.Clear(); _poi.Clear(); _lots.Clear();
                    try { File.Copy(_path, AuthoringFiles.UniquePath(dir, "LootTables.invalid-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture), ".json")); }
                    catch (Exception copy) { Log.Warn(LootModule.Instance, "could not preserve the invalid LootTables.json: " + copy.Message); }
                    Log.Warn(LootModule.Instance, "LootTables.json invalid; preserved a copy and rebuilt it: " + e.Message);
                    dirty = true;
                }
            }
            else dirty = true;
            if (dirty && _file.Count == 0)
            {
                using(var stream=DiskResources.Open("Loot.CargoDefaults.json"))
                using(var reader=new StreamReader(stream))
                { var defaults=Json.ParseObject(reader.ReadToEnd());ImportProfiles(defaults,"carLoot","car");ImportProfiles(defaults,"truckLoot","truck"); }
            }

            if (dirty) Save();
            Revision++;
            if (Log.L1) Log.Main(LootModule.Instance, "loot tables: " + _file.Count + " in " + _path);
        }

        /// <summary>Drops runtime tables (module shutdown).</summary>
        internal void ClearRuntime()
        {
            if (_runtime.Count == 0) return;
            _runtime.Clear(); Revision++;
        }

        /// <summary>All table ids, file tables in file order then runtime-only ids (cleared first).</summary>
        internal void TableIds(List<string> into)
        {
            into.Clear();
            into.AddRange(_order);
            foreach (var id in _runtime.Keys) if (!_file.ContainsKey(id)) into.Add(id);
        }

        /// <summary>Entries of a table (cleared first; runtime tables win over file tables); false when unknown.</summary>
        internal bool TryGetTable(string tableId, List<LootEntry> into)
        {
            into.Clear();
            var table = Find(tableId);
            if (table == null) return false;
            into.AddRange(table.Entries);
            return true;
        }

        /// <summary>Table id assigned to a POI type (native PoiType name or custom POI id), or null.</summary>
        internal string TableFor(string poiType)
        {
            string id;
            return poiType != null && _poi.TryGetValue(poiType, out id) ? id : null;
        }

        /// <summary>Table id assigned to lot <paramref name="lot"/> of a town template, or null.</summary>
        internal string TableForLot(string townTemplateId, int lot)
        {
            string id;
            return townTemplateId != null && _lots.TryGetValue(LotKey(townTemplateId, lot), out id) ? id : null;
        }

        /// <summary>Rolls a table (see <see cref="LootTable"/> roll rule) into item keys (cleared first); false when unknown.</summary>
        internal bool Roll(string tableId, System.Random random, List<string> itemKeys)
        {
            itemKeys.Clear();
            var table = Find(tableId);
            if (table == null || random == null) return false;
            for (int i = 0; i < table.Entries.Count; i++)
            {
                var e = table.Entries[i];
                if (string.IsNullOrEmpty(e.ItemKey)) continue;
                int copies = Copies(e.Weight, random);
                int min = Math.Max(1, e.Min), max = Math.Max(min, e.Max);
                for (int c = 0; c < copies; c++)
                {
                    int n = min == max ? min : random.Next(min, max + 1);
                    for (int k = 0; k < n; k++) itemKeys.Add(e.ItemKey);
                }
            }
            return true;
        }

        /// <summary>Registers or replaces a runtime table of another module (overlays a file table with the same id; not saved).</summary>
        internal void Register(IModule owner, string tableId, IList<LootEntry> entries)
        {
            if (string.IsNullOrEmpty(tableId)) throw new ArgumentException("Empty loot table id");
            var table = new LootTable { Id = tableId, Name = tableId, Owner = owner };
            if (entries != null) for (int i = 0; i < entries.Count; i++) table.Entries.Add(entries[i]);
            _runtime[tableId] = table;
            Revision++;
            if (Log.L2) Log.Deep(LootModule.Instance, "loot table " + tableId + " registered by " + (owner != null ? owner.Id : "?") + " (" + table.Entries.Count + " entries)");
        }

        /// <summary>Runtime table first, then the file table; null when unknown.</summary>
        internal LootTable Find(string tableId)
        {
            if (string.IsNullOrEmpty(tableId)) return null;
            LootTable table;
            if (_runtime.TryGetValue(tableId, out table)) return table;
            return _file.TryGetValue(tableId, out table) ? table : null;
        }

        /// <summary>Copies for a chance in percent: floor(percent/100) plus one with the remainder as probability (Patrols LootProfile.RollCount).</summary>
        private static int Copies(double percent, System.Random random)
        {
            if (double.IsNaN(percent) || double.IsInfinity(percent) || percent <= 0) return 0;
            double count = Math.Min(100000d, percent) / 100d;
            int whole = (int)Math.Floor(count);
            return whole + (count > whole && random.NextDouble() < count - whole ? 1 : 0);
        }

        /// <summary>Assignment key of a town template lot.</summary>
        private static string LotKey(string template, int lot) { return template + "/" + lot.ToString(CultureInfo.InvariantCulture); }

        /// <summary>Location of the Patrols editor file: config/WildWasteland/Patrols/ first, then the Patrols plugin folder
        /// (where the Patrols module keeps it today); null when neither exists.</summary>
        /// <summary>Adds the profiles of one PatrolsAndLoot.json list whose table id is missing; returns the number added. A car and a
        /// truck profile with the same id: the second one gets the id "kind:id".</summary>
        private int ImportProfiles(Dictionary<string, object> root, string key, string kind)
        {
            var rows = Json.Arr(root, key);
            if (rows == null) return 0;
            int added = 0;
            foreach (var value in rows)
            {
                var row = value as Dictionary<string, object>;
                string id = Json.Text(row, "id", "");
                if (row == null || id.Length == 0) continue;
                LootTable existing;
                if (_file.TryGetValue(id, out existing))
                {
                    if (string.Equals(existing.Kind, kind, StringComparison.OrdinalIgnoreCase)) continue;
                    id = kind + ":" + id;
                    if (_file.ContainsKey(id)) continue;
                }
                var table = new LootTable
                {
                    Id = id, Kind = kind, Name = Json.Text(row, "name", ""), TextureKey = Json.Text(row, "textureKey", ""),
                    Default = Json.Bool(row, "default", false), SpawnChance = Clamp(Json.Num(row, "spawnChance", 10f), 0f, 100f),
                };
                var items = Json.Arr(row, "items");
                if (items != null) foreach (var itemValue in items)
                {
                    var item = itemValue as Dictionary<string, object>;
                    string itemId = Json.Text(item, "id", "");
                    if (itemId.Length == 0) continue;
                    table.Entries.Add(new LootEntry { ItemKey = itemId, Weight = Clamp(Json.Num(item, "chance", 0f), 0f, 100000f), Min = 1, Max = 1 });
                }
                Add(table);
                added++;
            }
            return added;
        }

        /// <summary>Copies editable file cargo rows without exposing mutable authoritative tables.</summary>
        internal void Profiles(List<LootProfile> cars,List<LootProfile> trucks)
        {
            cars.Clear();trucks.Clear();foreach(var id in _order){var table=_file[id];if(table.Kind!="car"&&table.Kind!="truck")continue;var p=new LootProfile {Id=table.Id,Name=table.Name,TextureKey=table.TextureKey,Default=table.Default,SpawnChance=table.SpawnChance};foreach(var item in table.Entries)p.Items.Add(new LootItem {Id=item.ItemKey,Chance=item.Weight});(table.Kind=="car"?cars:trucks).Add(p);}
        }
        /// <summary>Validates and publishes all edited cargo rows, restoring in-memory authority if publication fails.</summary>
        internal void SaveProfiles(IList<LootProfile> cars,IList<LootProfile> trucks)
        {
            var replacement=new List<LootTable>();var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(bool truck in new[]{false,true})foreach(var p in truck?trucks:cars)
            {
                if(p==null||string.IsNullOrWhiteSpace(p.Id)||!ids.Add(p.Id))throw new FormatException("Missing or duplicate cargo profile");
                var old=Find(p.Id);var table=new LootTable {Id=p.Id,Name=p.Name,Kind=truck?"truck":"car",TextureKey=p.TextureKey,Default=p.Default,SpawnChance=Clamp(p.SpawnChance,0,100)};
                var items=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var row in p.Items){if(row==null||string.IsNullOrWhiteSpace(row.Id)||!items.Add(row.Id)||float.IsNaN(row.Chance)||float.IsInfinity(row.Chance))throw new FormatException("Invalid or duplicate cargo item");var entry=new LootEntry {ItemKey=row.Id,Weight=Clamp(row.Chance,0,100000),Min=1,Max=1};if(old!=null)foreach(var prior in old.Entries)if(prior.ItemKey==row.Id){entry.Min=prior.Min;entry.Max=prior.Max;break;}table.Entries.Add(entry);}replacement.Add(table);
            }
            foreach(var table in _file.Values)if(table.Kind!="car"&&table.Kind!="truck"&&ids.Contains(table.Id))throw new FormatException("Cargo profile conflicts with a world loot table");
            var previous=new Dictionary<string,LootTable>(_file,StringComparer.OrdinalIgnoreCase);var order=new List<string>(_order);
            try{foreach(var id in new List<string>(_order))if(_file[id].Kind=="car"||_file[id].Kind=="truck"){_file.Remove(id);_order.Remove(id);}foreach(var table in replacement)Add(table);Save(true);Revision++;}
            catch{_file.Clear();foreach(var pair in previous)_file.Add(pair.Key,pair.Value);_order.Clear();_order.AddRange(order);throw;}
        }

        /// <summary>Clamps a parsed number (NaN -> min).</summary>
        private static float Clamp(float value, float min, float max) { return float.IsNaN(value) ? min : Math.Max(min, Math.Min(max, value)); }

        /// <summary>Adds a file table (keeps file order).</summary>
        private void Add(LootTable table) { _file[table.Id] = table; _order.Add(table.Id); }

        /// <summary>Parses LootTables.json (schema 1); throws on structural errors or duplicate ids.</summary>
        private void Parse(string text)
        {
            var root = Json.ParseObject(text);
            if (root == null) throw new FormatException("Expected a JSON object");
            int schema = Json.Int(root, "schema", 0);
            if (schema != Schema) throw new FormatException("Unsupported schema " + schema);
            var tables = Json.Arr(root, "tables");
            if (tables == null) throw new FormatException("Missing array: tables");
            foreach (var value in tables)
            {
                var row = value as Dictionary<string, object>;
                if (row == null) throw new FormatException("Invalid table object");
                var table = new LootTable
                {
                    Id = Json.Text(row, "id", ""), Name = Json.Text(row, "name", ""), Kind = Json.Text(row, "kind", ""),
                    TextureKey = Json.Text(row, "textureKey", ""), Default = Json.Bool(row, "default", false),
                    SpawnChance = Clamp(Json.Num(row, "spawnChance", 10f), 0f, 100f),
                };
                if (table.Id.Length == 0 || _file.ContainsKey(table.Id)) throw new FormatException("Missing or duplicate table id");
                var entries = Json.Arr(row, "entries");
                if (entries != null) foreach (var entryValue in entries)
                {
                    var entry = entryValue as Dictionary<string, object>;
                    if (entry == null) throw new FormatException("Invalid entry in " + table.Id);
                    int min = Math.Max(0, Json.Int(entry, "min", 1));
                    table.Entries.Add(new LootEntry
                    {
                        ItemKey = Json.Text(entry, "item", ""), Weight = Clamp(Json.Num(entry, "weight", 0f), 0f, 100000f),
                        Min = min, Max = Math.Max(min, Json.Int(entry, "max", min)),
                    });
                }
                Add(table);
            }
            ReadMap(Json.Obj(root, "poiTables"), _poi);
            ReadMap(Json.Obj(root, "lotTables"), _lots);
        }

        /// <summary>Copies string members of an assignment object.</summary>
        private static void ReadMap(Dictionary<string, object> source, Dictionary<string, string> into)
        {
            if (source == null) return;
            foreach (var pair in source) { var id = pair.Value as string; if (!string.IsNullOrEmpty(id)) into[pair.Key] = id; }
        }

        /// <summary>Writes LootTables.json through a same-directory atomic replace (D17).</summary>
        private void Save(bool fail=false)
        {
            var tables = new List<object>();
            foreach (var id in _order)
            {
                var t = _file[id];
                var entries = new List<object>();
                foreach (var e in t.Entries) entries.Add(Json.Object("item", e.ItemKey, "weight", e.Weight, "min", e.Min, "max", e.Max));
                tables.Add(Json.Object("id", t.Id, "name", t.Name, "kind", t.Kind, "textureKey", t.TextureKey, "default", t.Default, "spawnChance", t.SpawnChance, "entries", entries));
            }
            var poi = new Dictionary<string, object>(); foreach (var pair in _poi) poi[pair.Key] = pair.Value;
            var lots = new Dictionary<string, object>(); foreach (var pair in _lots) lots[pair.Key] = pair.Value;
            try { AuthoringFiles.AtomicWrite(_path, Json.Write(Json.Object("schema", Schema, "tables", tables, "poiTables", poi, "lotTables", lots))); }
            catch (Exception e) { if(fail)throw;Log.Warn(LootModule.Instance, "LootTables.json not written: " + e.Message); }
        }
    }
}
