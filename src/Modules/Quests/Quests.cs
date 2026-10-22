using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace WildWasteland.Modules.Quests
{
    internal enum QType { Fetch = 0, Kill = 1, Goto = 2, Talk = 3, Clear = 4, Takeover = 5, Deliver = 6 }

    // One quest, either hand-written (quests.txt) or generated. Positions are stored relative
    // to the MapMagic root because the game shifts the whole world while you travel.
    internal class Quest
    {
        public string Id = "";
        public string Title = "Odd job";
        public string Giver = "*";        // which NPC names may offer it ("*" = anyone, or "merchant,elder")
        public string GiverId = "";       // object name of the one NPC that gave it, e.g. Pup(Clone)936 (empty = any NPC of that type)
        public string GiverName = "";     // the NPC type that actually gave it (the turn-in point)
        public QType Type = QType.Fetch;
        public string Target = "";        // item / enemy / npc token
        public string Label = "";         // nice name for the target
        public int Count = 1;
        public int Progress;
        public int Reward;
        public float Radius = 50f;
        public float MinDistance;         // Talk quests: how far from where you accepted the recipient must be
        public bool HasPos;
        public Vector3 Pos;               // Goto target / Clear centre (origin-relative)
        public Vector3 Start;             // where it was accepted (origin-relative)
        public bool ReturnToGiver = true;
        public bool ReturnExplicit;       // set when quests.txt has a return = line
        public bool CountExplicit;        // set when quests.txt has a count = line
        public bool ObjectiveDone;
        public bool Once = true;
        public float DistMin, DistMax;    // hand-written Goto/Talk: distance range from the giver in metres
        public float Range = -1f;         // hand-written Kill: only enemies within this many metres of the giver (-1 = config default, 0 = anywhere)
        public string NeedPoi = "";      // hand-written, any type: only offered when a point of interest of this kind is near the giver
        public string NeedEnemy = "";    // hand-written, any type: only offered when this enemy is alive near the giver
        public float NeedRange = 500f;    // how near (metres from the giver) for NeedPoi / NeedEnemy
        public int NeedCount = 1;         // how many of NeedEnemy must be alive
        public string Home = "";         // key of the giver's faction when the quest was offered (not read from quests.txt)
        public bool HasGiverPos;
        public Vector3 GiverPos;          // where the giver was last seen (origin-relative)
        public string Offer = "", Accept = "", Remind = "", Done = "";
        public float TimeLeft;            // Deliver: seconds of play left before the goods count as stolen
        public float Minutes;             // hand-written Deliver: time limit in in-game minutes (0 = worked out from the distance)
        public string Place = "";         // Deliver: the destination, e.g. "Coyotes camp"
        public double Deadline;           // in-game hours (Day * 24 + hour) at which the job fails; 0 = no limit (deliveries use TimeLeft)
        public int RepGain;               // reputation with the giver's faction for finishing it (0 = worked out from the reward)
        public string Chain = "";         // name of the quest chain it belongs to (hand-written quests)
        public string After = "";         // hand-written: only offered once these quests (ids, comma list) are done
        public bool Checkup;              // Goto: a visit to a camp the giver's own faction holds (a check-up, pays less)
        public float Hours;               // hand-written: time limit in in-game hours (0 = worked out; never less than the configured minimum)

        public bool NoRecipient;          // Deliver, not saved: you are at the camp and nobody there will take the goods
        public float NoRecipientSince = -1f;

        public Quest Clone() { return (Quest)MemberwiseClone(); }

        public bool GiverMatches(string npcName)
        {
            return Util.NameMatches(Giver, npcName);
        }
    }

    internal static class Util
    {
        public static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ' || c == '_' || c == '-' || c == '.') continue;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        // "merchant, elder" or "*" against an NPC name like "Merchant"
        public static bool NameMatches(string pattern, string npcName)
        {
            if (string.IsNullOrEmpty(pattern) || pattern.Trim() == "*") return true;
            string n = Norm(npcName);
            string[] parts = pattern.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = Norm(parts[i]);
                if (p.Length == 0) continue;
                if (p == "*" || n.Contains(p)) return true;
            }
            return false;
        }

        public static bool SameName(string a, string b)
        {
            return Norm(a) == Norm(b) && Norm(a).Length > 0;
        }

        public static string Compass(float dx, float dz)
        {
            // +Z is treated as north, +X as east
            double deg = Math.Atan2(dx, dz) * 180.0 / Math.PI;
            if (deg < 0) deg += 360.0;
            string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            int idx = (int)Math.Round(deg / 45.0) % 8;
            return names[idx];
        }

        public static string Esc(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("|", "\\p").Replace("\n", "\\n").Replace("\r", "");
        }

        public static string Unesc(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char d = s[++i];
                    if (d == 'p') sb.Append('|');
                    else if (d == 'n') sb.Append('\n');
                    else sb.Append(d);
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }

    // ----------------------------------------------------------------------------------
    // Saved quest state (written into the game's own save slot through Easy Save)
    // ----------------------------------------------------------------------------------
    internal class QuestBook
    {
        public readonly List<Quest> Active = new List<Quest>();
        public readonly HashSet<string> DoneIds = new HashSet<string>();
        public int TotalDone;
        public readonly Dictionary<string, string> Overrides = new Dictionary<string, string>();
        public readonly List<SavedWalk> Walks = new List<SavedWalk>();
        // cooldown stamps in in-game hours * 1000: when each giver (by object name) last gave out a quest, and
        // "camp:<key>" entries for when a camp's garrison last lost someone
        public readonly Dictionary<string, long> GiverStamp = new Dictionary<string, long>();
        // reputation with each faction (key -> value), and when it last changed (in-game hours) for the slow recovery
        public readonly Dictionary<string, int> Rep = new Dictionary<string, int>();
        public readonly Dictionary<string, double> RepStamp = new Dictionary<string, double>();
        // hired help (see Hire.cs): the NPC's object name, when the hire ends (in-game hours), the faction key, what to call it
        public string HireName = "", HireFaction = "", HireKind = "";
        public double HireUntil;

        public void Clear()
        {
            Active.Clear();
            Rep.Clear();
            RepStamp.Clear();
            DoneIds.Clear();
            GiverStamp.Clear();
            TotalDone = 0;
            Overrides.Clear();
            Walks.Clear();
            HireName = ""; HireFaction = ""; HireKind = ""; HireUntil = 0.0;
        }

        static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
        static float PF(string s) { float r; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : 0f; }
        static int PI(string s) { int r; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) ? r : 0; }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("AQ1\n");
            sb.Append("T|").Append(TotalDone).Append('\n');
            foreach (string d in DoneIds) sb.Append("D|").Append(Util.Esc(d)).Append('\n');
            foreach (KeyValuePair<string, long> g in GiverStamp) sb.Append("C|").Append(Util.Esc(g.Key)).Append('|').Append(g.Value).Append('\n');
            foreach (KeyValuePair<string, int> r in Rep)
            {
                double st; RepStamp.TryGetValue(r.Key, out st);
                sb.Append("R|").Append(Util.Esc(r.Key)).Append('|').Append(r.Value).Append('|').Append(st.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }
            foreach (KeyValuePair<string, string> o in Overrides) sb.Append("O|").Append(Util.Esc(o.Key)).Append('|').Append(Util.Esc(o.Value)).Append('\n');
            if (!string.IsNullOrEmpty(HireName))
                sb.Append("H|").Append(Util.Esc(HireName)).Append('|').Append(HireUntil.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(Util.Esc(HireFaction ?? "")).Append('|').Append(Util.Esc(HireKind ?? "")).Append('\n');
            foreach (SavedWalk wk in Walks)
                sb.Append("W|").Append(Util.Esc(wk.Name)).Append('|').Append(F(wk.TargetRel.x)).Append('|').Append(F(wk.TargetRel.y)).Append('|').Append(F(wk.TargetRel.z)).Append('|').Append(Util.Esc(wk.CampKey ?? "")).Append('\n');
            foreach (Quest q in Active)
            {
                sb.Append("Q|")
                  .Append(Util.Esc(q.Id)).Append('|')
                  .Append(Util.Esc(q.Title)).Append('|')
                  .Append(Util.Esc(q.Giver)).Append('|')
                  .Append(Util.Esc(q.GiverName)).Append('|')
                  .Append((int)q.Type).Append('|')
                  .Append(Util.Esc(q.Target)).Append('|')
                  .Append(Util.Esc(q.Label)).Append('|')
                  .Append(q.Count).Append('|')
                  .Append(q.Progress).Append('|')
                  .Append(q.Reward).Append('|')
                  .Append(F(q.Radius)).Append('|')
                  .Append(F(q.MinDistance)).Append('|')
                  .Append(q.HasPos ? 1 : 0).Append('|')
                  .Append(F(q.Pos.x)).Append('|').Append(F(q.Pos.y)).Append('|').Append(F(q.Pos.z)).Append('|')
                  .Append(F(q.Start.x)).Append('|').Append(F(q.Start.y)).Append('|').Append(F(q.Start.z)).Append('|')
                  .Append(q.ReturnToGiver ? 1 : 0).Append('|')
                  .Append(q.ObjectiveDone ? 1 : 0).Append('|')
                  .Append(q.Once ? 1 : 0).Append('|')
                  .Append(Util.Esc(q.Offer)).Append('|')
                  .Append(Util.Esc(q.Accept)).Append('|')
                  .Append(Util.Esc(q.Remind)).Append('|')
                  .Append(Util.Esc(q.Done)).Append('|')
                  .Append(Util.Esc(q.Home)).Append('|')
                  .Append(Util.Esc(q.GiverId)).Append('|')
                  .Append(q.HasGiverPos ? 1 : 0).Append('|')
                  .Append(F(q.GiverPos.x)).Append('|').Append(F(q.GiverPos.y)).Append('|').Append(F(q.GiverPos.z)).Append('|')
                  .Append(F(q.TimeLeft)).Append('|')
                  .Append(Util.Esc(q.Place)).Append('|')
                  .Append(q.Deadline.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                  .Append(q.RepGain).Append('|')
                  .Append(Util.Esc(q.Chain)).Append('|')
                  .Append(q.Checkup ? 1 : 0).Append('\n');
            }
            return sb.ToString();
        }

        // Returns false (and leaves the book untouched) if the data is not a format we know.
        public bool Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data)) return false;
            string[] lines = data.Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != "AQ1") return false;

            var active = new List<Quest>();
            var done = new HashSet<string>();
            var stamps = new Dictionary<string, long>();
            var owners = new Dictionary<string, string>();
            var walks = new List<SavedWalk>();
            var rep = new Dictionary<string, int>();
            var repStamp = new Dictionary<string, double>();
            int total = 0;
            string hireName = "", hireFaction = "", hireKind = "";
            double hireUntil = 0.0;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0) continue;
                string[] p = line.Split('|');
                if (p[0] == "T" && p.Length >= 2) total = PI(p[1]);
                else if (p[0] == "D" && p.Length >= 2) done.Add(Util.Unesc(p[1]));
                else if (p[0] == "C" && p.Length >= 3) { long tk; if (long.TryParse(p[2], out tk)) stamps[Util.Unesc(p[1])] = tk; }
                else if (p[0] == "H" && p.Length >= 5)
                {
                    hireName = Util.Unesc(p[1]);
                    double hu;
                    hireUntil = double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out hu) ? hu : 0.0;
                    hireFaction = Util.Unesc(p[3]);
                    hireKind = Util.Unesc(p[4]);
                }
                else if (p[0] == "O" && p.Length >= 3) owners[Util.Unesc(p[1])] = Util.Unesc(p[2]);
                else if (p[0] == "R" && p.Length >= 3)
                {
                    string rk = Util.Unesc(p[1]);
                    rep[rk] = PI(p[2]);
                    double rs;
                    if (p.Length >= 4 && double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out rs)) repStamp[rk] = rs;
                }
                else if (p[0] == "W" && p.Length >= 5) walks.Add(new SavedWalk { Name = Util.Unesc(p[1]), TargetRel = new Vector3(PF(p[2]), PF(p[3]), PF(p[4])), CampKey = p.Length >= 6 ? Util.Unesc(p[5]) : "" });
                else if (p[0] == "Q" && p.Length >= 27)
                {
                    var q = new Quest();
                    q.Id = Util.Unesc(p[1]);
                    q.Title = Util.Unesc(p[2]);
                    q.Giver = Util.Unesc(p[3]);
                    q.GiverName = Util.Unesc(p[4]);
                    q.Type = (QType)PI(p[5]);
                    q.Target = Util.Unesc(p[6]);
                    q.Label = Util.Unesc(p[7]);
                    q.Count = PI(p[8]);
                    q.Progress = PI(p[9]);
                    q.Reward = PI(p[10]);
                    q.Radius = PF(p[11]);
                    q.MinDistance = PF(p[12]);
                    q.HasPos = PI(p[13]) != 0;
                    q.Pos = new Vector3(PF(p[14]), PF(p[15]), PF(p[16]));
                    q.Start = new Vector3(PF(p[17]), PF(p[18]), PF(p[19]));
                    q.ReturnToGiver = PI(p[20]) != 0;
                    q.ObjectiveDone = PI(p[21]) != 0;
                    q.Once = PI(p[22]) != 0;
                    q.Offer = Util.Unesc(p[23]);
                    q.Accept = Util.Unesc(p[24]);
                    q.Remind = Util.Unesc(p[25]);
                    q.Done = Util.Unesc(p[26]);
                    q.Home = p.Length >= 28 ? Util.Unesc(p[27]) : "";
                    q.GiverId = p.Length >= 29 ? Util.Unesc(p[28]) : "";
                    if (p.Length >= 33 && PI(p[29]) != 0) { q.HasGiverPos = true; q.GiverPos = new Vector3(PF(p[30]), PF(p[31]), PF(p[32])); }
                    if (p.Length >= 35) { q.TimeLeft = PF(p[33]); q.Place = Util.Unesc(p[34]); }
                    if (p.Length >= 39)
                    {
                        double dl;
                        if (double.TryParse(p[35], NumberStyles.Float, CultureInfo.InvariantCulture, out dl)) q.Deadline = dl;
                        q.RepGain = PI(p[36]);
                        q.Chain = Util.Unesc(p[37]);
                        q.Checkup = PI(p[38]) != 0;
                    }
                    active.Add(q);
                }
            }
            Active.Clear();
            Active.AddRange(active);
            DoneIds.Clear();
            foreach (string d in done) DoneIds.Add(d);
            TotalDone = total;
            GiverStamp.Clear();
            foreach (KeyValuePair<string, long> g in stamps) GiverStamp[g.Key] = g.Value;
            Rep.Clear();
            foreach (KeyValuePair<string, int> r in rep) Rep[r.Key] = r.Value;
            RepStamp.Clear();
            foreach (KeyValuePair<string, double> r in repStamp) RepStamp[r.Key] = r.Value;
            Overrides.Clear();
            foreach (KeyValuePair<string, string> o in owners) Overrides[o.Key] = o.Value;
            Walks.Clear(); Walks.AddRange(walks);
            HireName = hireName; HireUntil = hireUntil; HireFaction = hireFaction; HireKind = hireKind;
            return true;
        }
    }

    // ----------------------------------------------------------------------------------
    // quests.txt parser
    // ----------------------------------------------------------------------------------
    internal static class QuestFile
    {
        public static List<Quest> Load(string path, Action<string> warn)
        {
            var result = new List<Quest>();
            if (!File.Exists(path)) { warn("quests.txt not found next to the plugin DLL; only generated quests will be offered."); return result; }

            Quest cur = null;
            int lineNo = 0;
            foreach (string raw in File.ReadAllLines(path))
            {
                lineNo++;
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                if (l.StartsWith("[") && l.EndsWith("]"))
                {
                    Finish(cur, result, warn);
                    cur = new Quest();
                    cur.Id = l.Substring(1, l.Length - 2).Trim();
                    cur.Title = cur.Id;
                    continue;
                }
                if (cur == null) continue;
                int eq = l.IndexOf('=');
                if (eq <= 0) { warn("quests.txt line " + lineNo + ": expected key = value"); continue; }
                string key = l.Substring(0, eq).Trim().ToLowerInvariant();
                string val = l.Substring(eq + 1).Trim();
                if (val.Length >= 2 && val[0] == '"' && val[val.Length - 1] == '"') val = val.Substring(1, val.Length - 2);
                Apply(cur, key, val, lineNo, warn);
            }
            Finish(cur, result, warn);
            return result;
        }

        static void Apply(Quest q, string key, string val, int lineNo, Action<string> warn)
        {
            switch (key)
            {
                case "title": q.Title = val; break;
                case "giver": q.Giver = val.Length == 0 ? "*" : val; break;
                case "type":
                    switch (val.ToLowerInvariant())
                    {
                        case "fetch": q.Type = QType.Fetch; break;
                        case "kill": q.Type = QType.Kill; break;
                        case "goto": q.Type = QType.Goto; break;
                        case "talk": q.Type = QType.Talk; break;
                        case "clear": q.Type = QType.Clear; break;
                        case "takeover": q.Type = QType.Takeover; break;
                        case "deliver": q.Type = QType.Deliver; break;
                        default: warn("quests.txt line " + lineNo + ": unknown type '" + val + "' (use fetch, kill, goto, talk, clear, takeover or deliver)"); break;
                    }
                    break;
                case "target": q.Target = val; break;
                case "label": q.Label = val; break;
                case "count": q.Count = Math.Max(1, ParseInt(val, 1)); q.CountExplicit = true; break;
                case "reward": q.Reward = Math.Max(0, ParseInt(val, 0)); break;
                case "range":
                    {
                        float r = ParseFloat(val, -1f);
                        if (r < 0f) warn("quests.txt line " + lineNo + ": range must be a number of metres (0 = anywhere); the config default is used.");
                        else q.Range = r;
                        break;
                    }
                case "needpoi": q.NeedPoi = val; break;
                case "needenemy": q.NeedEnemy = val; break;
                case "needrange": q.NeedRange = Math.Max(10f, ParseFloat(val, 500f)); break;
                case "needcount": q.NeedCount = Math.Max(1, ParseInt(val, 1)); break;
                case "radius": q.Radius = Math.Max(5f, ParseFloat(val, 50f)); break;
                case "minutes": q.Minutes = Math.Max(0f, ParseFloat(val, 0f)); break;
                case "hours": q.Hours = Math.Max(0f, ParseFloat(val, 0f)); break;
                case "rep": q.RepGain = Math.Max(0, ParseInt(val, 0)); break;
                case "chain": q.Chain = val; break;
                case "after": q.After = val; break;
                case "checkup": q.Checkup = ParseBool(val, false); break;
                case "distance":
                    {
                        string[] parts = val.Split('-');
                        float a = ParseFloat(parts[0], 600f);
                        float b = parts.Length > 1 ? ParseFloat(parts[1], a) : a;
                        q.DistMin = Math.Min(a, b);
                        q.DistMax = Math.Max(a, b);
                        break;
                    }
                case "return": q.ReturnToGiver = ParseBool(val, true); q.ReturnExplicit = true; break;
                case "once": q.Once = ParseBool(val, true); break;
                case "offer": q.Offer = val; break;
                case "accept": q.Accept = val; break;
                case "remind": q.Remind = val; break;
                case "done": q.Done = val; break;
                default: warn("quests.txt line " + lineNo + ": unknown key '" + key + "'"); break;
            }
        }

        static void Finish(Quest q, List<Quest> into, Action<string> warn)
        {
            if (q == null) return;
            if (q.Type == QType.Fetch && q.Target.Length == 0) { warn("quest [" + q.Id + "]: fetch needs a target (item name); skipped."); return; }
            if (q.Type == QType.Deliver && q.Target.Length == 0) { warn("quest [" + q.Id + "]: deliver needs a target (the item's prefab name); skipped."); return; }
            if (q.Type == QType.Kill && q.Target.Length == 0) q.Target = "any";
            if (q.Type == QType.Talk && q.Target.Length == 0) q.Target = "*";
            if (q.Label.Length == 0)
            {
                if (q.Type == QType.Talk) q.Label = "someone";
                else if (q.Type == QType.Goto) q.Label = q.Target.Length > 0 ? q.Target.Replace('_', ' ') : "spot";
                else if (q.Target == "any" || q.Target == "*") q.Label = "hostiles";
                else q.Label = q.Target.TrimStart('=').Replace('_', ' ');
            }
            if (!q.ReturnExplicit) q.ReturnToGiver = q.Type != QType.Talk;       // a go-to job is reported back too
            if (q.Type == QType.Fetch || q.Type == QType.Kill) q.ReturnToGiver = true;       // kills: report back
            if (q.Chain.Length > 0 || q.After.Length > 0) q.Once = true;                   // a step of a chain is done once
            if (q.Type == QType.Goto && q.DistMax <= 0f) { q.DistMin = 150f; q.DistMax = 1800f; }
            if (q.Type == QType.Talk && q.DistMax <= 0f) { q.DistMin = 600f; q.DistMax = 1200f; }
            if (q.Type == QType.Clear && q.Radius < 100f) q.Radius = 250f;
            if (q.Type == QType.Takeover)
            {
                if (q.DistMax <= 0f) { q.DistMin = 200f; q.DistMax = 4000f; }
                if (q.Radius <= 50f) q.Radius = 120f;
                if (!q.CountExplicit) q.Count = 0;                  // 0 = not known until you are close enough to see the camp
                if (q.Title == "Odd job") q.Title = "Take the camp";
                q.ReturnToGiver = true;
            }
            if (q.Type == QType.Talk) q.ReturnToGiver = false;
            if (q.Type == QType.Deliver)
            {
                if (q.DistMax <= 0f) { q.DistMin = 400f; q.DistMax = 3000f; }
                if (q.Radius <= 50f) q.Radius = 120f;
                if (q.Title == "Odd job") q.Title = "Delivery";
                q.ReturnToGiver = false;
            }
            if (q.Reward <= 0) q.Reward = 50;
            if (q.Type == QType.Goto && q.Checkup && q.Title == "Odd job") q.Title = "Check up on the camp";
            Phrases.FillDefaults(q, new System.Random(q.Id.GetHashCode()));
            into.Add(q);
        }

        static int ParseInt(string s, int d) { int r; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) ? r : d; }
        static float ParseFloat(string s, float d) { float r; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : d; }
        static bool ParseBool(string s, bool d)
        {
            string v = s.Trim().ToLowerInvariant();
            if (v == "yes" || v == "true" || v == "1" || v == "on") return true;
            if (v == "no" || v == "false" || v == "0" || v == "off") return false;
            return d;
        }
    }

    // ----------------------------------------------------------------------------------
    // Default dialogue (used when quests.txt leaves a line out, and for generated quests).
    // Placeholders: {n} {item} {target} {reward} {have} {progress} {giver} {dist} {dir} {radius} {place} {minutes}
    // ----------------------------------------------------------------------------------
    internal static class Phrases
    {
        static readonly string[] FetchOffer =
        {
            "I'm short on {item}. Bring me {n} and I'll pay {reward} nuts. In your pack or on the ground next to us, I don't care which.",
            "Supplies ran dry again. {n} {item}, that's all I need. {reward} nuts for your trouble.",
            "You look like someone who finds things. I need {n} {item}. Worth {reward} nuts.",
            "Got a use for {n} {item}. Don't ask what. {reward} nuts, no haggling.",
        };
        static readonly string[] FetchRemind =
        {
            "Still waiting on {n} {item}. You've got {have} with you.",
            "{have} of {n} {item}. Not enough yet.",
        };
        static readonly string[] FetchDone =
        {
            "That'll do. {reward} nuts, as promised.",
            "Good. Here's your {reward} nuts.",
        };

        static readonly string[] KillOffer =
        {
            "{target} have been picking off my people. Put down {n} and come tell me when it's done. {reward} nuts, and a bit extra for every body you drag back.",
            "I'm sick of {target} near here. Kill {n} of them and come tell me. {reward} nuts when it's done; bring me the bodies and I'll add a bonus.",
            "Put {n} {target} in the ground and report back. {reward} nuts, more if you bring the bodies.",
        };
        static readonly string[] KillRemind =
        {
            "{progress} of {n} {target} so far. Keep at it.",
            "Not done yet. {progress} of {n}.",
        };
        static readonly string[] KillDone =
        {
            "That's them. Good work. {reward} nuts.",
            "Hm, nasty ones. {reward} nuts, you earned it.",
        };

        static readonly string[] GotoOffer =
        {
            "Word is there's a {poi} about {dist} m {dir} of here that nobody has looked at. Go see it and come back and tell me. {reward} nuts.",
            "I want eyes on that {poi}, {dist} m {dir} from here. Go look, then report back. {reward} nuts.",
        };
        static readonly string[] GotoRemind =
        {
            "Haven't been to the {poi} yet? It's about {dist} m {dir} of you now.",
            "Still not there. {dist} m to go, {dir}, to the {poi}. Then come back and tell me.",
        };
        static readonly string[] GotoDone =
        {
            "So that's what's out there. {reward} nuts.",
            "Good to know. {reward} nuts for the look.",
        };
        static readonly string[] CheckupOffer =
        {
            "Our {poi} is about {dist} m {dir} of here and I haven't heard from it in a while. Go see that all is well and tell me. It's only a check-up, so {reward} nuts.",
            "Look in on our {poi}, {dist} m {dir}, and report back. Nothing fancy, {reward} nuts.",
        };
        static readonly string[] CheckupRemind =
        {
            "Have you been to our {poi} yet? About {dist} m {dir} of you.",
        };
        static readonly string[] CheckupDone =
        {
            "Good, they're fine. {reward} nuts for the trouble.",
            "Thanks for looking in. {reward} nuts.",
        };

        static readonly string[] TalkOffer =
        {
            "I've got word that needs carrying. Find someone at least {dist} m from here, anyone who'll listen, and say I sent you. {reward} nuts.",
            "Take a message for me. Find another face {dist} m or more from here and tell them {giver} says hello. {reward} nuts.",
        };
        static readonly string[] TalkRemind =
        {
            "Haven't delivered it yet? Find someone at least {dist} m from where you took it.",
        };
        static readonly string[] TalkDone =
        {
            "Word from a {giver}? Thanks for carrying it.",
            "A {giver} sent you? Hm. Fine.",
        };

        static readonly string[] TakeoverOffer =
        {
            "The {target} out there has been a thorn in our side. Clear out {n} of them and I'll move our people in. {reward} nuts for the job.",
            "I want that {target} gone. Break them, hold it, and my crew takes the ground. {reward} nuts.",
            "That {target} sits on good ground. Kill {n} of its people and I'll send folks to claim it. Pays {reward} nuts.",
        };
        static readonly string[] TakeoverRemind =
        {
            "{progress} of theirs down so far. The {target} is still standing.",
            "Not yet. {progress} down. Finish the {target}.",
        };
        static readonly string[] TakeoverDone =
        {
            "Word is my people are already on the road to claim it. {reward} nuts, well earned.",
            "The camp is ours now. Here's your {reward} nuts.",
        };
        static readonly string[] ClearOffer =
        {
            "Hostiles keep gathering around here. Clear out {n} within {radius} m of this spot. {reward} nuts.",
            "Make it safe around here. Kill {n} hostiles within {radius} m of my camp. {reward} nuts.",
        };
        static readonly string[] ClearRemind =
        {
            "{progress} of {n} cleared. Not safe yet.",
        };
        static readonly string[] ClearDone =
        {
            "Quiet at last. {reward} nuts.",
        };

        static readonly string[] DeliverOffer =
        {
            "I've got {n} {item} that need to get to the {place}, about {dist} m {dir} of here. Have them there within {minutes} and they'll pay you {reward} nuts. Don't get any ideas.",
            "Run {n} {item} over to the {place} for me, {dist} m {dir}. You've got {minutes}. {reward} nuts on arrival. Keep them and every one of us will know.",
            "Our people at the {place} are waiting on {n} {item}. It's {dist} m {dir}. {minutes}, {reward} nuts when you hand them over.",
        };
        static readonly string[] DeliverRemind =
        {
            "They're expecting {n} {item}. You've got {have}.",
        };
        static readonly string[] DeliverDone =
        {
            "The {item}? Right on time. Here's {reward} nuts.",
            "Good, they sent it. {reward} nuts, as agreed.",
        };
        static readonly string[] DeliverAccept =
        {
            "It's on the ground in front of me. Clock's running.",
            "Take it, it's right here. Don't be late.",
        };

        static readonly string[] AcceptLines =
        {
            "Good. Don't take all week.",
            "Deal. Don't let me down.",
            "Fine. Come back when it's done.",
        };

        public static readonly string[] Decline =
        {
            "Suit yourself.",
            "Your loss.",
        };

        public static readonly string[] NoWork =
        {
            "Nothing right now.",
            "Not today.",
        };

        public static string Pick(string[] pool, System.Random r)
        {
            return pool[r.Next(pool.Length)];
        }

        public static void FillDefaults(Quest q, System.Random r)
        {
            string[] o, rem, d;
            switch (q.Type)
            {
                case QType.Kill: o = KillOffer; rem = KillRemind; d = KillDone; break;
                case QType.Goto:
                    if (q.Checkup) { o = CheckupOffer; rem = CheckupRemind; d = CheckupDone; }
                    else { o = GotoOffer; rem = GotoRemind; d = GotoDone; }
                    break;
                case QType.Talk: o = TalkOffer; rem = TalkRemind; d = TalkDone; break;
                case QType.Clear: o = ClearOffer; rem = ClearRemind; d = ClearDone; break;
                case QType.Takeover: o = TakeoverOffer; rem = TakeoverRemind; d = TakeoverDone; break;
                case QType.Deliver: o = DeliverOffer; rem = DeliverRemind; d = DeliverDone; if (string.IsNullOrEmpty(q.Accept)) q.Accept = Pick(DeliverAccept, r); break;
                default: o = FetchOffer; rem = FetchRemind; d = FetchDone; break;
            }
            if (string.IsNullOrEmpty(q.Offer)) q.Offer = Pick(o, r);
            if (string.IsNullOrEmpty(q.Remind)) q.Remind = Pick(rem, r);
            if (string.IsNullOrEmpty(q.Done)) q.Done = Pick(d, r);
            if (string.IsNullOrEmpty(q.Accept)) q.Accept = Pick(AcceptLines, r);
        }
    }

    // ----------------------------------------------------------------------------------
    // Random quest generator
    // ----------------------------------------------------------------------------------
    internal static class QuestGen
    {
        // Reward scale, from the game's own Value FSMs (BaseValue = buy base, BaseValue2 = sell base):
        // merchants sell at 1.2-1.6 x BaseValue and buy at 0.6-0.8 x BaseValue2. A fetch job pays about 1.15 x an
        // item's fair value: more than a merchant pays for it, less than buying it from one (so buy-and-deliver
        // never pays). Stacked items (bandage, first aid) are counted per use, so their value is per use.
        const float FetchBonus = 1.15f;
        const float KillBonus = 1.25f;

        // prefab name (without "(Clone)"), display name, fair value in nuts per counted unit, min/max count, weight
        // (how often it is picked: 3 = everyday supplies, 2 = useful gear, 1 = rare and valuable).
        // Value = the item's BaseValue; ammo boxes sit between their sell base (20) and buy base; stacked items
        // (bandage, first aid, cigarettes, sanity pills, weed) are counted per use, so their value is per use.
        class Item
        {
            public string Prefab, Name; public int Value, Min, Max, Weight;
            public Item(string p, string n, int v, int a, int b, int w) { Prefab = p; Name = n; Value = v; Min = a; Max = b; Weight = w; }
        }
        class Foe { public string Token, Name; public int Each, Min, Max; public Foe(string t, string n, int e, int a, int b) { Token = t; Name = n; Each = e; Min = a; Max = b; } }

        static readonly Item[] Items =
        {
            // medicine and vices
            new Item("bandage_1", "Bandage", 16, 2, 5, 3),             // base 40, 2-3 per stack
            new Item("first_aid_1", "First Aid", 23, 2, 4, 3),         // base 70, 3 per kit
            new Item("sanity_pills", "Sanity Pills", 180, 1, 2, 1),    // base 180
            new Item("cigarette_bactrian", "Cigarette", 40, 2, 5, 2),  // base 120, about 3 per pack
            new Item("weed_bag", "Weed", 14, 4, 8, 1),                 // base 110, 8 per bag
            new Item("alcohol_canister", "Alcohol", 50, 1, 3, 2),      // base 50
            // food
            new Item("dogfood_can", "Dogfood", 50, 2, 4, 3),           // base 50
            new Item("meat_mutant_cooked", "Cooked Mutant meat", 3, 4, 8, 2),
            new Item("meat_insect_cooked", "Cooked Insect meat", 4, 4, 8, 2),
            new Item("Rat_cooked", "Cooked Rat", 5, 3, 6, 2),          // cooked meat is worth 3-4
            new Item("plant_tobacco", "Tobacco Plant", 180, 1, 2, 1),  // base 180
            new Item("plant_weed", "Weed Plant", 210, 1, 1, 1),        // base 210
            // fuel and water (valued by their contents; half full counts)
            new Item("Water_Can_Plastic", "Water Can", 90, 1, 2, 3),   // 13 L x 9 nuts/L
            new Item("Diesel_Can", "Diesel Can", 130, 1, 2, 2),        // 20 L x 8 nuts/L
            new Item("Gasoline_Can", "Gasoline Can", 200, 1, 2, 2),    // 20 L x 12 nuts/L
            new Item("MotorOil_Can_Small", "Motor Oil Can", 30, 1, 2, 2), // 5 L x 6 nuts/L
            // ammo
            new Item("ammo_box_9mm", "Ammo 9mm", 32, 1, 3, 3),         // buy base 40
            new Item("ammo_box_22", "Ammo .22", 23, 1, 3, 3),          // buy base 25
            new Item("ammo_box_12gauge", "Ammo 12 gauge", 56, 1, 2, 2),// buy base 80
            new Item("ammo_box_556mm", "Ammo 5.56mm", 50, 1, 2, 2),    // buy base 70
            new Item("ammo_box_762mm", "Ammo 7.62mm", 53, 1, 2, 2),    // buy base 75
            new Item("ammo_box_3006", "Ammo .30-06", 86, 1, 2, 1),     // buy base 130
            new Item("ammo_arrow", "Ammo Arrow", 40, 1, 3, 2),         // base 40
            // tools and gear
            new Item("Toolbox", "Toolbox", 45, 1, 2, 3),               // base 45
            new Item("Repairbox_Small", "Repairbox Small", 90, 1, 2, 2), // base 90
            new Item("armorplate", "Armor Plate", 30, 2, 4, 2),        // base 30
            new Item("bedroll", "Bedroll", 35, 1, 1, 2),               // base 35
            new Item("binocular", "Binocular", 65, 1, 1, 1),           // base 65
            new Item("flashlight1", "Old Flashlight", 75, 1, 1, 1),    // base 75
            new Item("gasoline_stove", "Portable Gasoline Stove", 150, 1, 1, 1),
            new Item("dew_collector_portable", "Portable Dew Collector", 120, 1, 1, 1),
            new Item("bodyarmor_type_0", "Bodyarmor Type-0", 150, 1, 1, 1),
            new Item("backpack_small", "Small Backpack", 230, 1, 1, 1),
            // weapons
            new Item("old_knife", "Old Knife", 20, 1, 2, 2),           // base 20
            new Item("machete", "Machete", 80, 1, 1, 1),               // base 80
            new Item("22_pipe_pistol", "Pipe Pistol (.22lr)", 100, 1, 1, 1),
            new Item("22_pipe_revolver", "Pipe Revolver (.22lr)", 180, 1, 1, 1),
            new Item("crossbow", "Crossbow", 140, 1, 1, 1),
            new Item("slamfire_shotgun", "Slamfire Shotgun (20ga)", 260, 1, 1, 1),
            // car parts the merchants also buy
            new Item("radiator_small", "Small Radiator", 120, 1, 1, 1),
            new Item("radiator_medium", "Medium Radiator", 180, 1, 1, 1),
            new Item("radio_car", "Cassette Player", 65, 1, 1, 1),
            new Item("cassette_80ssynthwave", "Cassette 80's Synthwave Mix", 110, 1, 1, 1),
        };

        // Deliveries: goods the giver hands you (spawned in front of them). Things that are not stacks or containers,
        // so "n of them" is simply n objects.
        static readonly Item[] Goods =
        {
            new Item("dogfood_can", "Dogfood", 50, 2, 4, 3),
            new Item("ammo_box_9mm", "Ammo 9mm", 32, 1, 3, 3),
            new Item("ammo_box_22", "Ammo .22", 23, 2, 4, 2),
            new Item("ammo_box_12gauge", "Ammo 12 gauge", 56, 1, 2, 2),
            new Item("Toolbox", "Toolbox", 45, 1, 2, 3),
            new Item("Repairbox_Small", "Repairbox Small", 90, 1, 1, 2),
            new Item("armorplate", "Armor Plate", 30, 2, 4, 2),
            new Item("alcohol_canister", "Alcohol", 50, 1, 3, 2),
            new Item("bedroll", "Bedroll", 35, 1, 2, 1),
            new Item("binocular", "Binocular", 65, 1, 1, 1),
            new Item("radio_car", "Cassette Player", 65, 1, 1, 1),
        };

        // A friendly camp (the giver's faction or an ally) between minD and maxD from the giver, never the giver's own,
        // and not one an active delivery or a neighbour's offer already points at. Picked at random.
        public static Poi PickDeliveryCamp(Faction giver, Vector3 giverWorld, float minD, float maxD, List<Quest> active, HashSet<string> blocked, System.Random rng)
        {
            if (giver == null) return null;
            var cands = new List<Poi>();
            foreach (Poi p in Game.FindPois(giverWorld, Math.Max(150f, minD), maxD, "camp"))
            {
                if (!p.Name.StartsWith("Camp", StringComparison.OrdinalIgnoreCase)) continue;
                Faction owner = Factions.OwnerOfPoi(p);
                if (owner == null || !Factions.Friendly(giver, owner)) continue;
                if (!HasFriendlyPeople(giver, p.Pos, 120f, true)) continue;          // people are loaded there and none is on the giver's side
                Vector3 rel = Game.Rel(p.Pos);
                bool taken = false;
                for (int i = 0; i < active.Count; i++)
                    if (active[i].Type == QType.Deliver && active[i].HasPos && Game.HDist(active[i].Pos, rel) < 5f) { taken = true; break; }
                if (blocked != null && blocked.Contains("deliver:" + PosKey(rel))) taken = true;
                if (!taken) cands.Add(p);
            }
            return cands.Count == 0 ? null : cands[rng.Next(cands.Count)];
        }

        /// Is a living person who is not hostile to the player and friendly to `giver` within `radius` of `pos`?
        /// When `noneKnownIsFine` and nobody at all is loaded there (far away), the answer is yes: the camp cannot be judged yet.
        public static bool HasFriendlyPeople(Faction giver, Vector3 pos, float radius, bool noneKnownIsFine)
        {
            int around = 0;
            foreach (EnemyInfo e in Game.FindPeople(Factions.PeopleWords))
            {
                if (e.Go == null || Game.HDist(e.Pos, pos) > radius) continue;
                around++;
                if (Game.IsPlayerEnemy(e.Go)) continue;
                Faction f = Factions.OfObject(e.Tag, e.Name, e.Pos);
                if (giver == null || f == null || Factions.Friendly(giver, f)) return true;
            }
            return around == 0 && noneKnownIsFine;
        }

        // Points a delivery quest at a camp: position, place name and the time limit.
        public static void AimDelivery(Quest q, Poi camp, Vector3 giverWorld, float minutesBase, float minutesPerKm)
        {
            Faction owner = Factions.OwnerOfPoi(camp);
            q.Type = QType.Deliver;
            q.HasPos = true;
            q.Pos = Game.Rel(camp.Pos);
            q.Place = (owner != null ? owner.Name + " " : "") + "camp";
            q.ReturnToGiver = false;
            ResetClock(q, giverWorld, minutesBase, minutesPerKm);
        }

        // Full time limit from the giver to the camp (called again when the job is accepted, so the clock starts then).
        public static void ResetClock(Quest q, Vector3 giverWorld, float minutesBase, float minutesPerKm)
        {
            float km = q.HasPos ? Game.HDist(Game.Abs(q.Pos), giverWorld) / 1000f : 1f;
            float minutes = q.Minutes > 0f ? q.Minutes : minutesBase + km * minutesPerKm;
            q.TimeLeft = Math.Max(60f, minutes * 60f);
        }

        // A generated delivery, or null when there is no friendly camp in range.
        public static Quest MakeDelivery(string giverName, System.Random rng, Faction giver, Vector3 giverWorld, float minD, float maxD,
                                         List<Quest> active, HashSet<string> blocked, float minutesBase, float minutesPerKm, float radius)
        {
            Poi camp = PickDeliveryCamp(giver, giverWorld, minD, maxD, active, blocked, rng);
            if (camp == null) return null;
            int total = 0;
            for (int i = 0; i < Goods.Length; i++) total += Goods[i].Weight;
            int r = rng.Next(total);
            Item it = Goods[Goods.Length - 1];
            for (int i = 0; i < Goods.Length; i++) { r -= Goods[i].Weight; if (r < 0) { it = Goods[i]; break; } }
            int n = rng.Next(it.Min, it.Max + 1);

            var q = new Quest();
            q.Id = "gen:" + rng.Next(0x7fffffff).ToString("x");
            q.Once = false;
            q.Giver = "*";
            q.GiverName = giverName;
            q.Target = "=" + it.Prefab;
            q.Label = it.Name;
            q.Count = n;
            q.Radius = radius;
            AimDelivery(q, camp, giverWorld, minutesBase, minutesPerKm);
            float dist = Game.HDist(camp.Pos, giverWorld);
            // the job pays for the trip, plus half the goods' value so delivering beats keeping them
            q.Reward = Round5(25f + dist * 0.06f + it.Value * n * 0.5f);
            q.Title = "Deliver " + n + " " + it.Name;
            Phrases.FillDefaults(q, rng);
            return q;
        }

        /// How many of an item (token as in a quest's target) exist within reach of the giver, or null for "no limit".
        /// Set by the plugin before it generates an offer.
        public static Func<string, int> ItemsAround;

        // A random item that is really out there (and not already offered by a neighbour); null if none is.
        static Item PickAvailableItem(System.Random rng, HashSet<string> blocked, out int available)
        {
            available = 0;
            for (int tries = 0; tries < 12; tries++)
            {
                Item it = PickItem(rng);
                if (!Free(blocked, "fetch:" + Util.Norm("=" + it.Prefab))) continue;
                int n = ItemsAround == null ? int.MaxValue : ItemsAround("=" + it.Prefab);
                if (n >= it.Min) { available = n; return it; }
            }
            return null;
        }

        static int totalWeight = -1;
        static Item PickItem(System.Random rng)
        {
            if (totalWeight < 0) { totalWeight = 0; for (int i = 0; i < Items.Length; i++) totalWeight += Items[i].Weight; }
            int r = rng.Next(totalWeight);
            for (int i = 0; i < Items.Length; i++) { r -= Items[i].Weight; if (r < 0) return Items[i]; }
            return Items[Items.Length - 1];
        }

        // Nuts per kill, roughly the ammo it takes (a 9mm round costs about 5 nuts to buy, a shotgun shell about 11)
        // plus the risk. Checked in order; the first token found in the enemy's name wins.
        static readonly Foe[] Foes =
        {
            new Foe("scorpionking", "Scorpion Kings", 150, 1, 1),          // bosses
            new Foe("alphanightwalker", "Alpha Nightwalkers", 150, 1, 1),
            new Foe("terrorofthenight", "Terrors of the Night", 150, 1, 1),
            new Foe("professor", "Professors", 150, 1, 1),
            new Foe("dukeironjaw", "Duke Ironjaws", 150, 1, 1),
            new Foe("juggernaut", "Juggernauts", 120, 1, 1),
            new Foe("nightwalker", "Nightwalkers", 60, 1, 3),
            new Foe("skinwal", "Skinwals", 50, 1, 3),
            new Foe("teacher", "Teachers", 50, 1, 3),
            new Foe("lanky", "Lankies", 45, 1, 3),
            new Foe("arachnid", "Arachnids", 45, 1, 3),
            new Foe("burrower", "Burrowers", 40, 1, 3),
            new Foe("scorpionsmall", "Scorpions", 15, 2, 5),
            new Foe("scorpion", "Scorpions", 40, 1, 3),
            new Foe("bolter", "Bolters", 35, 2, 4),
            new Foe("grimhound", "Grimhounds", 30, 2, 5),
            new Foe("hound", "Hounds", 20, 2, 5),
            new Foe("pup", "Pups", 12, 2, 5),
            new Foe("zombie", "Zombies", 20, 3, 6),
            new Foe("spider", "Spiders", 8, 4, 8),
            new Foe("bat", "Bats", 8, 3, 6),
            new Foe("rat", "Rats", 6, 3, 6),
        };
        const int DefaultKillValue = 30;   // people of the human factions carry guns

        // a check-up is only a look at somewhere that is already ours: less than half of what a new place pays
        public static int CheckupReward(float dist)
        {
            return Math.Max(5, (int)(Math.Round((20f + dist * 0.05f) * 0.4f / 5.0) * 5.0));
        }

        // Kinds of enemy that no kill job is made for (lizards are everywhere and nobody pays for them).
        public static bool NoKillQuest(string enemyName)
        {
            return Util.Norm(enemyName).Contains("lizard");
        }

        // reputation for finishing a job: grows with the reward (the unscaled worth of the job), a bit more for a camp takeover
        public static int RepFor(Quest q, int baseReward)
        {
            if (q.Type == QType.Goto && q.Checkup) return 1;
            int r = (int)Math.Round(Math.Sqrt(Math.Max(1, baseReward) / 60.0));
            if (q.Type == QType.Takeover) r += 1;
            return Math.Max(1, Math.Min(6, r));
        }

        static int Round5(float v)
        {
            int r = (int)(Math.Round(v / 5.0) * 5.0);
            return Math.Max(10, r);
        }

        // Pretty plural for an enemy name such as "Spider_Small" or "Yard Hound".
        public static string Plural(string name)
        {
            string n = name.Replace('_', ' ').Trim();
            return n.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? n : n + "s";
        }

        // Nuts per kill: known enemies by name, a default for everything else.
        public static int KillValue(string name)
        {
            string n = Util.Norm(name);
            for (int i = 0; i < Foes.Length; i++)
                if (n.Contains(Foes[i].Token)) return Foes[i].Each;
            return DefaultKillValue;
        }

        // allowed = [fetch, kill, goto, talk, clear]. pois / foes are what really exists around the giver
        // (kill quests are only generated when there is something to kill within killRange).
        public static string PosKey(Vector3 rel)
        {
            return "pos:" + Mathf.RoundToInt(rel.x / 10f) * 10 + "," + Mathf.RoundToInt(rel.z / 10f) * 10;
        }

        // what a quest is "about", so neighbouring NPCs do not hand out the same one
        public static string KeyOf(Quest q)
        {
            switch (q.Type)
            {
                case QType.Goto:
                case QType.Takeover: return q.HasPos ? PosKey(q.Pos) : "";
                case QType.Deliver: return q.HasPos ? "deliver:" + PosKey(q.Pos) : "";
                case QType.Kill: return "kill:" + Util.Norm(q.Target);
                case QType.Fetch: return "fetch:" + Util.Norm(q.Target);
                case QType.Clear: return "clear";
                case QType.Talk: return "talk";
            }
            return "";
        }

        static bool Free(HashSet<string> blocked, string key) { return blocked == null || !blocked.Contains(key); }

        public static Quest Generate(string giverName, System.Random rng, Vector3 giverRel, Vector3 playerRel, bool[] allowed,
                                     List<Poi> pois, List<Poi> ownCamps, List<EnemyInfo> foes, float killRange, HashSet<string> blocked)
        {
            // things a neighbour already offers are out; what is left is sorted nearest first
            var freeFoes = new List<EnemyInfo>();
            if (foes != null)
                for (int i = 0; i < foes.Count; i++)
                    if (Free(blocked, "kill:" + Util.Norm(foes[i].Name)) && !NoKillQuest(foes[i].Name)) freeFoes.Add(foes[i]);
            freeFoes.Sort(delegate (EnemyInfo a, EnemyInfo b) { return Game.HDist(Game.Rel(a.Pos), giverRel).CompareTo(Game.HDist(Game.Rel(b.Pos), giverRel)); });
            var freePois = new List<Poi>();
            if (pois != null)
                for (int i = 0; i < pois.Count; i++)
                    if (Free(blocked, PosKey(Game.Rel(pois[i].Pos)))) freePois.Add(pois[i]);
            freePois.Sort(delegate (Poi a, Poi b) { return Game.HDist(a.Pos, Game.Abs(giverRel)).CompareTo(Game.HDist(b.Pos, Game.Abs(giverRel))); });
            int nearHostiles = 0;                       // for a clear job: hostiles really standing within its radius
            for (int i = 0; i < freeFoes.Count; i++)
                if (Game.HDist(Game.Rel(freeFoes[i].Pos), giverRel) <= 250f) nearHostiles++;
            if (foes != null)
            {
                nearHostiles = 0;
                for (int i = 0; i < foes.Count; i++)
                    if (Game.HDist(Game.Rel(foes[i].Pos), giverRel) <= 250f) nearHostiles++;
            }

            var freeOwn = new List<Poi>();
            if (ownCamps != null)
                for (int i = 0; i < ownCamps.Count; i++)
                    if (Free(blocked, PosKey(Game.Rel(ownCamps[i].Pos)))) freeOwn.Add(ownCamps[i]);
            freeOwn.Sort(delegate (Poi a, Poi b) { return Game.HDist(a.Pos, Game.Abs(giverRel)).CompareTo(Game.HDist(b.Pos, Game.Abs(giverRel))); });

            var pool = new List<QType>();
            bool checkupPick = false;
            int fetchAvail = 0;
            Item fetchItem = allowed[0] ? PickAvailableItem(rng, blocked, out fetchAvail) : null;   // only things that exist near the giver
            if (fetchItem != null) { pool.Add(QType.Fetch); pool.Add(QType.Fetch); }
            if (allowed[1] && freeFoes.Count > 0) { pool.Add(QType.Kill); pool.Add(QType.Kill); }
            if (allowed[2] && freePois.Count > 0) pool.Add(QType.Goto);             // only places nobody has looked at yet
            if (allowed[2] && freeOwn.Count > 0) pool.Add(QType.Goto);              // check-ups on the giver's own camps are picked separately (below)
            if (allowed[3] && Free(blocked, "talk")) pool.Add(QType.Talk);
            if (allowed[4] && Free(blocked, "clear") && nearHostiles >= 3) pool.Add(QType.Clear);
            if (pool.Count == 0) return null;

            var q = new Quest();
            q.Id = "gen:" + rng.Next(0x7fffffff).ToString("x");
            q.Once = false;
            q.Giver = "*";
            q.GiverName = giverName;
            q.Type = pool[rng.Next(pool.Count)];
            q.Start = playerRel;

            switch (q.Type)
            {
                case QType.Fetch:
                    {
                        Item it = fetchItem;
                        int n = Math.Min(rng.Next(it.Min, it.Max + 1), Math.Max(it.Min, fetchAvail));
                        q.Target = "=" + it.Prefab;              // exactly this prefab, not everything sharing its ID
                        q.Label = it.Name;
                        q.Count = n;
                        q.Reward = Round5(it.Value * n * FetchBonus);
                        q.Title = "Fetch " + n + " " + it.Name;
                        q.ReturnToGiver = true;
                        break;
                    }
                case QType.Kill:
                    {
                        // pick one of the enemy kinds that is really alive near the giver
                        var counts = new Dictionary<string, int>();
                        for (int i = 0; i < freeFoes.Count; i++)
                        {
                            int have;
                            counts.TryGetValue(freeFoes[i].Name, out have);
                            counts[freeFoes[i].Name] = have + 1;
                        }
                        string pick = freeFoes[0].Name;        // the kind of the closest enemy
                        int c = counts[pick];
                        int n = rng.Next((c + 1) / 2, c + 1);          // at least half of that kind within range, up to all of them
                        q.Target = pick;
                        q.Label = Plural(pick);
                        q.Count = n;
                        q.Reward = Round5(KillValue(pick) * n * KillBonus);
                        q.Title = "Hunt " + q.Label;
                        q.HasPos = true;
                        q.Pos = giverRel;
                        q.Radius = killRange;
                        q.ReturnToGiver = true;
                        break;
                    }
                case QType.Goto:
                    {
                        q.HasPos = true;
                        q.ReturnToGiver = true;                  // you report back what you saw
                        // a check-up on one of the giver's own camps, or a look at a place nobody has seen yet
                        checkupPick = freeOwn.Count > 0 && (freePois.Count == 0 || rng.Next(freePois.Count + freeOwn.Count) < freeOwn.Count);
                        if (checkupPick)
                        {
                            Poi poi = freeOwn[0];
                            q.Pos = Game.Rel(poi.Pos);
                            float d = Game.HDist(q.Pos, giverRel);
                            q.Radius = 80f;
                            q.Checkup = true;
                            q.Reward = CheckupReward(d);
                            q.Label = poi.Kind.ToLowerInvariant();
                            q.Title = "Check up on the " + poi.Name.Replace('_', ' ');
                        }
                        else
                        {
                            Poi poi = freePois[0];             // the closest place nobody has looked at
                            q.Pos = Game.Rel(poi.Pos);
                            float d = Game.HDist(q.Pos, giverRel);
                            q.Radius = 80f;
                            q.Reward = Round5(20f + d * 0.05f);
                            q.Label = poi.Kind.ToLowerInvariant();
                            q.Title = "Check out the " + poi.Kind;
                        }
                        break;
                    }
                case QType.Talk:
                    {
                        float dist = 600f + (float)rng.NextDouble() * 700f;
                        q.Target = "*";
                        q.Label = "someone";
                        q.MinDistance = dist;
                        q.Reward = Round5(20f + dist * 0.045f);
                        q.Title = "Carry word";
                        q.ReturnToGiver = false;
                        break;
                    }
                default:
                    {
                        int n = rng.Next(3, Math.Min(nearHostiles, 8) + 1);     // never more than are really there
                        q.Pos = giverRel;
                        q.HasPos = true;
                        q.Radius = 250f;
                        q.Count = n;
                        q.Target = "any";
                        q.Label = "hostiles";
                        q.Reward = Round5(n * 25f * KillBonus);
                        q.Title = "Clear the area";
                        q.ReturnToGiver = true;
                        break;
                    }
            }
            Phrases.FillDefaults(q, rng);
            return q;
        }
    }
}
