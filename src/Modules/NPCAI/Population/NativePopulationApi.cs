using System;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Publishes native population registration without exposing town or NPC private state.</summary>
    internal sealed class NativePopulationApi:INativePopulation
    {
        public void Own(PlayMakerFSM f,PopulationArea a,bool suspended,Func<bool> ready)=>InteriorPopulation.Own(f,a,suspended,ready);
        public bool Ready(PopulationArea a)=>InteriorPopulation.TownReady(a);
        public void Forget(PopulationArea a)=>InteriorPopulation.Forget(a);
        public void Discover()=>InteriorPopulation.DiscoverExisting();
        public string[] CaptureNames()=>InteriorPopulation.CaptureNames();
        public void RestoreNames(string[] names)=>InteriorPopulation.RestoreNames(names);
        public void Catalog(GameObject prefab,string faction)=>NativePoiPopulation.Catalog(prefab,faction);
    }
}
