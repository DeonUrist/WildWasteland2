using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WildWasteland.Modules.Patrols
{
    internal static partial class EditorStore
    {
        internal static EditorData Data;
        internal static string Root, StatePath;

        internal static EditorData Defaults()
        {
            using (var stream = WildWasteland.Core.DiskResources.Open("Patrols.EditorDefaults.json"))
            {
                if (stream == null) throw new InvalidOperationException("Missing shipped editor defaults");
                using (var reader = new StreamReader(stream)) return EditorData.FromJson(reader.ReadToEnd());
            }
        }

        internal static void Load(string root)
        {
            Root = Path.GetFullPath(root); StatePath = Path.Combine(Root, "PatrolsAndLoot.json");
            Directory.CreateDirectory(Root);
            Data = Defaults();
            if (!File.Exists(StatePath)) { Save(); return; }
            try { Data = EditorData.FromJson(File.ReadAllText(StatePath)); }
            catch (Exception e)
            {
                File.Copy(StatePath, UniquePath(Root, "PatrolsAndLoot.invalid-" + DateTime.Now.ToString("yyyyMMddHHmmss"), ".json"));
                Core.Log.Warn("[Patrols] " + "Editor settings invalid; preserved a copy and loaded defaults: " + e.Message);
            }
        }

        /// <summary>Saves patrol group authoring and delegates cargo profile publication to its actual Loot owner.</summary>
        internal static void Save() { var cargo=Core.ModApi.Get<Core.ICargoLoot>();if(cargo!=null)cargo.SaveProfiles(Data.CarLoot,Data.TruckLoot);var root=Core.Json.ParseObject(Data.ToJson());root.Remove("carLoot");root.Remove("truckLoot");AtomicWrite(StatePath,Core.Json.Write(root)); }
        /// <summary>Refreshes editor views from the authoritative provider when Loot starts, restarts or changes its tables.</summary>
        internal static void CargoApiChanged(Type changed) { if(changed!=null&&changed!=typeof(Core.ICargoLoot)||Data==null)return;var api=Core.ModApi.Get<Core.ICargoLoot>();if(api!=null)api.Profiles(Data.CarLoot,Data.TruckLoot); }


        // avoid = the group id of the previous spawn: left out of the roll when at least one other group of that list is eligible
        internal static SpawnGroup Roll(float km, int bosses, float patrolPercent, Func<double> random, out bool convoy, string avoid = null)
        {
            bool p = Data.Patrols.Any(g => g.Eligible(km, bosses)), c = Data.Convoys.Any(g => g.Eligible(km, bosses));
            double percentage = Math.Max(0, Math.Min(100, patrolPercent));
            convoy = c && (!p || percentage <= 0 || (percentage < 100 && random() >= percentage / 100d));
            return SpawnGroup.Roll(convoy ? Data.Convoys : Data.Patrols, km, bosses, random, avoid);
        }

        internal static List<CarTemplate> Compose(SpawnGroup group, bool convoy, Func<double> random)
        {return ComposeRoster(group,convoy,random,true);}
        internal static List<CarTemplate> ComposeFull(SpawnGroup group,bool convoy,Func<double> random)
        {return ComposeRoster(group,convoy,random,false);}
        private static List<CarTemplate> ComposeRoster(SpawnGroup group,bool convoy,Func<double> random,bool cap)
        {
            var list = new List<CarTemplate>();
            foreach (string name in group.Templates)
            {
                var template = CarTemplate.Park.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (template != null && template.Available && CrewFactions.Compatible(template,group.Faction)) list.Add(template.CloneForSpawn());
            }
            if (!convoy && list.Count == 0 && TrafficFactions.Normalize(group.Faction)=="Scrapyard") list.Add(CarTemplate.FallbackCar.CloneForSpawn());
            if (convoy && !list.Any(t => t.IsTruck)) { var fallback = CarTemplate.FallbackTruck.CloneForSpawn();if(TrafficFactions.Normalize(group.Faction)=="Coyotes"){fallback.Faction="Coyotes";fallback.Driver="Bodyguard";fallback.Passenger="";fallback.Turrets=new string[4];} fallback.SpawnLoot = new Dictionary<string, int>(); fallback.SpawnCargoKey = ""; list.Insert(0, fallback); }
            if (convoy) list = list.OrderByDescending(t => t.IsTruck).ToList();
            Shrink(list, convoy, random);
            if(cap&&PatrolsModule.CarSpawnLimit!=null&&PatrolsModule.CarSpawnLimit.Value&&list.Count>PatrolsModule.MaxCarsInEvent.Value)
                list.RemoveRange(PatrolsModule.MaxCarsInEvent.Value,list.Count-PatrolsModule.MaxCarsInEvent.Value);
            if (convoy) foreach (var truck in list.Where(t => t.IsTruck)) truck.SpawnCargoOptions = (group.AllowedCargo ?? new List<string>()).ToArray();
            if (convoy && group.UniformCargo)
            {
                var api=Core.ModApi.Get<Core.ICargoLoot>();
                var trucks=list.Where(t=>t.IsTruck&&t.SpawnLoot==null&&(api==null||api.Select(true,t.LootPreset,new string[0],random)==null)).ToList();
                string shared=api!=null&&trucks.Count>0?api.Select(true,null,group.AllowedCargo,random):null;
                var roll=api!=null?api.Roll(shared,random):new Dictionary<string,int>();
                foreach(var truck in trucks){truck.SpawnLoot=new Dictionary<string,int>(roll,StringComparer.OrdinalIgnoreCase);truck.SpawnCargoKey=api!=null?api.Texture(shared):"none";}
            }
            foreach(var car in list)car.Faction=TrafficFactions.Normalize(group.Faction);
            return list;
        }

        // [Scaling] PatrolSizePercent (2.6.0): below 100 the group is cut to that share of its cars - floor(n x %), never below 1 - by removing
        // cars at random; a convoy's trucks are never removed (the escort shrinks, the trucks count toward the total)
        internal static void Shrink(List<CarTemplate> list, bool convoy, Func<double> random)
        {
            float percent = PatrolsModule.PatrolSizePercent != null ? PatrolsModule.PatrolSizePercent.Value : 100f;
            if (percent >= 100f || list.Count <= 1) return;
            int keep = Math.Max(1, (int)Math.Floor(list.Count * Math.Max(0f, percent) / 100f + 1e-4));
            while (list.Count > keep)
            {
                var removable = new List<int>();
                for (int i = 0; i < list.Count; i++) if (!(convoy && list[i].IsTruck)) removable.Add(i);
                if (removable.Count == 0) break;
                list.RemoveAt(removable[Math.Min(removable.Count - 1, (int)(random() * removable.Count))]);
            }
        }


        internal static void Reset(bool recycleCustom)
        {
            CarTemplates.Refresh(true);
            var custom = CarTemplate.Park.Where(t => !t.IsDefault).ToList();
            string old = Path.Combine(Root, "OldTemplatesFolder"); Directory.CreateDirectory(old);
            var moved = new List<KeyValuePair<string, string>>();
            try
            {
                foreach (string file in Directory.GetFiles(CarTemplates.Folder, "*.json"))
                {
                    string source = SafePath(CarTemplates.Folder, Path.GetFileName(file));
                    string target = UniquePath(old, Path.GetFileNameWithoutExtension(file), ".json");
                    File.Move(source, target); moved.Add(new KeyValuePair<string, string>(source, target));
                }
                CarTemplates.RestoreDefaults();
                if (!recycleCustom)
                    foreach (var template in custom.Where(t => t.Favorite))
                    {
                        var match = moved.FirstOrDefault(pair => string.Equals(pair.Key, template.SourcePath, StringComparison.OrdinalIgnoreCase));
                        if (match.Value != null) File.Copy(match.Value, template.SourcePath);
                    }
                else
                    foreach (var template in custom)
                    {
                        string file = template.SourcePath;
                        var match = moved.FirstOrDefault(pair => string.Equals(pair.Key, file, StringComparison.OrdinalIgnoreCase));
                        if (match.Value != null) file = match.Value;
                        CarTemplates.Recycle(file);
                    }
                var defaults = Defaults();
                defaults.CarLoot.AddRange(Data.CarLoot.Where(p => !defaults.CarLoot.Any(d => d.Id.Equals(p.Id, StringComparison.OrdinalIgnoreCase))).ToList());
                defaults.TruckLoot.AddRange(Data.TruckLoot.Where(p => !defaults.TruckLoot.Any(d => d.Id.Equals(p.Id, StringComparison.OrdinalIgnoreCase))).ToList());
                Data = defaults; Save(); CarTemplates.Refresh(true);
            }
            catch
            {
                foreach (var pair in moved)
                    if (File.Exists(pair.Value) && !File.Exists(pair.Key)) File.Move(pair.Value, pair.Key);
                CarTemplates.Refresh(true);
                throw;
            }
        }
    }
}
