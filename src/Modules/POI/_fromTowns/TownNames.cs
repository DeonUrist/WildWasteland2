using System;
using System.Collections.Generic;
using System.IO;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    internal sealed class TownNames
    {
        private readonly Dictionary<TownFaction, string[]> _pools = new Dictionary<TownFaction, string[]>();
        private readonly HashSet<string> _used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Random _random = new Random(Guid.NewGuid().GetHashCode());
        internal void Load()
        {
            string directory = Path.Combine(Assets.ModuleConfigDir("Towns"), "Names"); Directory.CreateDirectory(directory);
            foreach (TownFaction faction in Enum.GetValues(typeof(TownFaction)))
            {
                string file = Path.Combine(directory, faction + ".txt");
                if (!File.Exists(file))
                    using (var stream = WildWasteland.Core.DiskResources.Open("Towns.Names." + faction + ".txt"))
                    {
                        if (stream == null) throw new InvalidDataException("Missing packaged town names: " + faction);
                        using (var reader = new StreamReader(stream)) File.WriteAllText(file, reader.ReadToEnd());
                    }
                var names = new List<string>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(file))
                { string name = line.Trim(); if (name.Length > 0 && !name.StartsWith("#", StringComparison.Ordinal) && seen.Add(name)) names.Add(name); }
                _pools[faction] = names.ToArray();
            }
            Log.Always("[Towns] editable name lists: " + directory + " (Animals/Mutants/Scrapyard/Coyotes.txt)");
        }
        internal void Begin(Dictionary<string, TownRecord> records)
        {
            _used.Clear();
            var keys = new List<string>(records.Keys); keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            { var p = records[key].Plan; if (p.HasTown && !string.IsNullOrEmpty(p.Name) && !_used.Add(p.Name)) throw new InvalidDataException("Repeated saved town name: " + p.Name); }
            foreach (string key in keys) if (records[key].Plan.HasTown && string.IsNullOrEmpty(records[key].Plan.Name)) Assign(records[key].Plan);
        }
        internal void Assign(TownPlan plan)
        {
            if (!plan.HasTown || !string.IsNullOrEmpty(plan.Name)) return;
            string[] pool = _pools[plan.Faction]; string choice = null; int available = 0;
            foreach (string name in pool) if (!_used.Contains(name) && _random.Next(++available) == 0) choice = name;
            if (choice != null) { _used.Add(choice); plan.Name = choice; return; }
            // Exhausting an editable list must not reuse the name of an unloaded town.
            for (int n = 1; ; n++)
            { string name = plan.Faction + " Settlement " + n; if (_used.Add(name)) { plan.Name = name; return; } }
        }
    }
}
