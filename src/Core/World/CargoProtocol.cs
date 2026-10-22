using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Loot-owned cargo table metadata and physical placement.</summary>
    public interface ICargoLoot
    {
        /// <summary>Copies editable file-backed cargo metadata; runtime registrations remain owned by their contributors.</summary>
        void Profiles(List<LootProfile> cars,List<LootProfile> trucks);
        /// <summary>Atomically saves edited cargo profiles into the authoritative Loot table file.</summary>
        void SaveProfiles(IList<LootProfile> cars,IList<LootProfile> trucks);
        /// <summary>Returns an authoritative cargo profile, optionally selecting a weighted truck profile.</summary>
        string Select(bool truck,string requested,IEnumerable<string> allowed,Func<double> random);
        /// <summary>Rolls the authoritative Loot table using the caller's existing random stream.</summary>
        Dictionary<string,int> Roll(string table,Func<double> random);
        /// <summary>Container artwork assigned to the table.</summary>
        string Texture(string table);
        /// <summary>Loads actual prefab counts into the native cargo lock volume, one item per iterator yield.</summary>
        IEnumerator Load(GameObject vehicle,Dictionary<string,int> counts,bool truck,List<GameObject> into);
        /// <summary>Parks existing cargo lock FSMs after the native build initializes.</summary>
        void Settle(List<GameObject> items);
    }
}
