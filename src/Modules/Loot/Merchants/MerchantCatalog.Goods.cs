using System.Linq;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// Merchant goods lookup (Loot, ex Patrols Merchants/MerchantModel.cs): the buy/sell list of the native shop station of a
    /// merchant type, with the scene ItemBuySellList and a small repair/first-aid fallback. Called once per merchant bus shop
    /// setup. Station prefabs resolve through the Patrols prefab matcher (REFACTOR-REQUEST B15-3). Namespace kept for Wave C.
    /// </summary>
    internal static partial class MerchantCatalog
    {
        /// <summary>Item prefabs a merchant type sells: the station's prefill list (else its live array), only items with a "ForBuy"
        /// FSM; Repairbox_Small/Toolbox/first_aid_1 when the station has none.</summary>
        internal static GameObject[] Goods(string type)
        {
            var station=Prefabs.FindAny(Station(type)); // refs-ok: REFACTOR-REQUEST B15-3
            var proxy=station!=null?station.GetComponent<PlayMakerArrayListProxy>():null;
            if(proxy==null){var list=GameObject.Find("ItemBuySellList");if(list!=null)proxy=list.GetComponent<PlayMakerArrayListProxy>();}
            var goods=proxy!=null?proxy.preFillGameObjectList.Where(g=>g!=null).ToArray():new GameObject[0];
            if(goods.Length==0&&proxy?.arrayList!=null)goods=proxy.arrayList.OfType<GameObject>().Where(g=>g!=null).ToArray();
            if(goods.Length==0)return new[]{Prefabs.FindAny("Repairbox_Small"),Prefabs.FindAny("Toolbox"),Prefabs.FindAny("first_aid_1")}.Where(g=>g!=null).ToArray(); // refs-ok: REFACTOR-REQUEST B15-3
            return goods.Where(g=>g.GetComponents<PlayMakerFSM>().Any(f=>f.FsmName=="ForBuy")).ToArray();
        }
    }
}
