using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    // A faction. The game uses the Unity tag of an NPC as its faction (Scrapyard, Coyotes) and marks camps with
    // EnemySpawn_<Faction> child objects. factions.txt can add allies and overrides on top of that.
    internal class Faction
    {
        public string Name = "";
        public string Key = "";
        public List<string> Npcs = new List<string>();
        public List<string> Enemies = new List<string>();
        public List<string> Pois = new List<string>();
        public List<string> Allies = new List<string>();
        public List<string> War = new List<string>();      // factions this one is at war with
        public bool HostileSet;
        public bool Hostile;                                // at war with the player: its people give no quests

        static bool HasWord(List<string> list, string name)
        {
            string n = Util.Norm(name);
            if (n.Length == 0) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Length > 0 && n.Contains(list[i])) return true;
            return false;
        }

        public bool HasEnemy(string name) { return HasWord(Enemies, name) || HasWord(Npcs, name); }

        // exact object name (Camp_11), or a prefix with a trailing * (Camp_1*)
        public bool HasPoi(string poiName)
        {
            string n = Util.Norm(poiName);
            for (int i = 0; i < Pois.Count; i++)
            {
                string p = Pois[i];
                if (p.Length == 0) continue;
                if (p.EndsWith("*", StringComparison.Ordinal)) { if (n.StartsWith(p.Substring(0, p.Length - 1), StringComparison.Ordinal)) return true; }
                else if (n == p) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Quests factions: factions.txt model and parser (hot reload by mtime), lookup by key, camp ownership overrides (saved with the quests),
    /// kill-quest protection and POI eligibility. Wave A split: the faction queries (OfObject/OfPerson/HomeOf/OwnerOfPoi/Friendly/AtWar,
    /// HumanFactions) live in Core/Game/_fromQuests/Factions.Query.cs for B01 (Core.Factions provider).
    /// </summary>
    internal static partial class Factions
    {
        static readonly List<Faction> file = new List<Faction>();                       // from factions.txt
        static readonly Dictionary<string, Faction> byKey = new Dictionary<string, Faction>();
        static DateTime fileStamp = DateTime.MinValue;

        // camps taken over by quests: camp key (name@x,z, origin-relative) -> faction name. Saved with the quests.
        public static readonly Dictionary<string, string> Overrides = new Dictionary<string, string>();

        public static string KeyOf(Poi p)
        {
            Vector3 r = Game.Rel(p.Pos);
            return p.Name + "@" + Mathf.RoundToInt(r.x / 10f) * 10 + "," + Mathf.RoundToInt(r.z / 10f) * 10;
        }

        public static float HomeRadius = 120f;          // a person without a faction tag counts as living at the nearest POI within this distance
        public static string[] PeopleWords = new string[0];

        public static int Count { get { return file.Count; } }

        // Re-reads factions.txt when it has been saved since the last look, so edits apply without restarting.
        public static void Refresh(string path, Action<string> warn)
        {
            DateTime stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (stamp == fileStamp) return;
            fileStamp = stamp;
            Load(path, warn);
            byKey.Clear();
        }

        public static void Load(string path, Action<string> warn)
        {
            file.Clear();
            if (!File.Exists(path)) return;
            Faction cur = null;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    cur = new Faction();
                    cur.Name = line.Substring(1, line.Length - 2).Trim();
                    cur.Key = Util.Norm(cur.Name);
                    file.Add(cur);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq < 0 || cur == null) { warn("factions.txt line " + (i + 1) + ": expected [Faction] or key = value."); continue; }
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                List<string> items = Split(line.Substring(eq + 1));
                switch (key)
                {
                    case "npcs": cur.Npcs = items; break;
                    case "enemies": cur.Enemies = items; break;
                    case "pois": cur.Pois = items; break;
                    case "allies": cur.Allies = items; break;
                    case "war": cur.War = items; break;
                    case "hostile": cur.Hostile = items.Count > 0 && (items[0] == "yes" || items[0] == "true" || items[0] == "1"); cur.HostileSet = true; break;
                    default: warn("factions.txt line " + (i + 1) + ": unknown key '" + key + "' (use npcs, enemies, pois, allies, war, hostile)."); break;
                }
            }
        }

        static List<string> Split(string v)
        {
            var res = new List<string>();
            string[] parts = v.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string s = Util.Norm(parts[i]);
                if (s.Length > 0) res.Add(s);
            }
            return res;
        }

        // The faction with this name (made on demand; picks up its entry in factions.txt if there is one).
        public static Faction Get(string name)
        {
            string k = Util.Norm(name);
            if (k.Length == 0) return null;
            Faction f;
            if (byKey.TryGetValue(k, out f)) return f;
            for (int i = 0; i < file.Count; i++) if (file[i].Key == k) { f = file[i]; break; }
            if (f == null) { f = new Faction(); f.Name = name; f.Key = k; }
            // what the game's story gives us: Scrapyard is hostile to the player and at war with the Coyotes
            if (k == "scrapyard") { if (!f.HostileSet) f.Hostile = true; if (f.War.Count == 0) f.War.Add("coyotes"); }
            if (k == "coyotes" && f.War.Count == 0) f.War.Add("scrapyard");
            byKey[k] = f;
            return f;
        }

        public static Faction ByKey(string key) { return Get(key); }

        // true when `giver` must not send the player after this one. Animals and mutants are always fair game. People are
        // only targets when their faction is hostile to the player or at war with the giver's faction: a giver never sends
        // you after its own side or its allies, and nobody sends you after traders, wanderers or a neutral faction.
        // `go` (may be null) lets the game itself say whether a person without a faction is hostile to the player.
        public static bool Protected(Faction giver, string tag, string enemyName, Vector3 enemyWorld, GameObject go)
        {
            Faction ef = OfObject(tag, enemyName, enemyWorld);
            if (ef == null)                                             // animals and mutants are fair game; a person with no faction
                return IsPerson(enemyName) && !Game.IsPlayerEnemy(go);  // only when the game marks them hostile to the player
            if (giver != null && Friendly(giver, ef)) return true;     // own side or an ally
            if (ef.Hostile) return false;                               // hostile to the player (Scrapyard by default)
            if (giver != null && AtWar(giver, ef)) return false;       // at war with the giver's faction
            return true;                                                // a neutral faction
        }

        // a point of interest may be used unless it belongs to a faction that is not friendly with the giver's
        public static bool PoiAllowed(Faction giver, Poi p, List<EnemyInfo> unused)
        {
            if (giver == null) return true;
            Faction owner = OwnerOfPoi(p);
            return owner == null || Friendly(giver, owner);
        }
        internal static void Reset() { file.Clear(); byKey.Clear(); Overrides.Clear(); fileStamp = DateTime.MinValue; }
    }
}
