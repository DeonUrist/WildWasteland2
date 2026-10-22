using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Loot
{
    /// <summary>Reads one authoritative Loot table store for virtual and physical cargo rolls.</summary>
    internal sealed class CargoLootApi : ICargoLoot
    {
        private readonly LootTableStore _store;
        /// <summary>Uses the module's actual persisted table store.</summary>
        internal CargoLootApi(LootTableStore store) { _store=store; }
        /// <summary>Copies current authoritative editable cargo metadata.</summary>
        public void Profiles(List<LootProfile> cars,List<LootProfile> trucks) {_store.Profiles(cars,trucks);}
        /// <summary>Publishes current authored profiles through Loot persistence.</summary>
        public void SaveProfiles(IList<LootProfile> cars,IList<LootProfile> trucks) {_store.SaveProfiles(cars,trucks);}
        private sealed class CallerRandom : System.Random { private readonly Func<double> _next;internal CallerRandom(Func<double> next){_next=next;}public override double NextDouble(){return _next();} }
        /// <summary>Selects only the requested table or eligible weighted truck tables; preserves authored allow lists and order.</summary>
        public string Select(bool truck,string requested,IEnumerable<string> allowed,Func<double> random)
        {
            var fixedTable=_store.Find(requested);if(fixedTable!=null&&fixedTable.Kind==(truck?"truck":"car"))return fixedTable.Id;if(!truck)return null;
            var ids=new List<string>();_store.TableIds(ids);var filter=allowed!=null?new HashSet<string>(allowed,StringComparer.OrdinalIgnoreCase):null;var candidates=new List<LootTable>();double total=0;
            foreach(var id in ids){var table=_store.Find(id);if(table==null||table.Kind!="truck"||table.SpawnChance<=0||filter!=null&&!filter.Contains(id))continue;candidates.Add(table);total+=table.SpawnChance;}if(total<=0)return null;double roll=Math.Min(.999999999999,Math.Max(0,random()))*total;foreach(var table in candidates){roll-=table.SpawnChance;if(roll<0)return table.Id;}return candidates[candidates.Count-1].Id;
        }
        /// <summary>Rolls through the same Loot table implementation exposed by ILootTables.</summary>
        public Dictionary<string,int> Roll(string table,Func<double> random) { var keys=new List<string>();var result=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);if(string.IsNullOrEmpty(table)||!_store.Roll(table,new CallerRandom(random),keys))return result;foreach(var key in keys){int count;result.TryGetValue(key,out count);result[key]=count+1;}return result; }
        /// <summary>Reads the chosen table's container texture hint.</summary>
        public string Texture(string table) { var value=_store.Find(table);return value==null||string.IsNullOrWhiteSpace(value.TextureKey)?"none":value.TextureKey; }
        /// <summary>Runs Loot's native cargo volume placement.</summary>
        public IEnumerator Load(GameObject car,Dictionary<string,int> counts,bool truck,List<GameObject> into) { return Cargo.Load(car,counts,truck,into); }
        /// <summary>Completes native lock initialization without respawning items.</summary>
        public void Settle(List<GameObject> items) { Cargo.SettleFsms(items); }
    }
}
