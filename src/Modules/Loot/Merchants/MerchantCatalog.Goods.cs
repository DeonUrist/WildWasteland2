using System.Linq;
using UnityEngine;

namespace WildWasteland.Modules.Loot
{
    /// <summary>Loot-owned native merchant stock query for the exact station prefab supplied by the traffic merchant policy.</summary>
    internal sealed class MerchantGoods : Core.IMerchantGoods
    {
        /// <summary>Item prefabs a merchant type sells: the station's prefill list (else its live array), only items with a "ForBuy"
        /// FSM; Repairbox_Small/Toolbox/first_aid_1 when the station has none.</summary>
        public GameObject[] Goods(string stationName)
        {
            var station=Core.PrefabCatalog.Find(stationName);
            var proxy=station!=null?station.GetComponent<PlayMakerArrayListProxy>():null;
            if(proxy==null){var list=GameObject.Find("ItemBuySellList");if(list!=null)proxy=list.GetComponent<PlayMakerArrayListProxy>();}
            var goods=proxy!=null?proxy.preFillGameObjectList.Where(g=>g!=null).ToArray():new GameObject[0];
            if(goods.Length==0&&proxy?.arrayList!=null)goods=proxy.arrayList.OfType<GameObject>().Where(g=>g!=null).ToArray();
            if(goods.Length==0)return new[]{Core.PrefabCatalog.Find("Repairbox_Small"),Core.PrefabCatalog.Find("Toolbox"),Core.PrefabCatalog.Find("first_aid_1")}.Where(g=>g!=null).ToArray();
            return goods.Where(g=>g.GetComponents<PlayMakerFSM>().Any(f=>f.FsmName=="ForBuy")).ToArray();
        }
    }
}
