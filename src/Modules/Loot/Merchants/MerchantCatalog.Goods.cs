using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Merchant goods lookup (the native shop station's buy/sell list, with fallbacks); Loot part of MerchantCatalog (Wave A split of Patrols Merchants/MerchantModel.cs).</summary>
    internal static partial class MerchantCatalog
    {
        internal static GameObject[] Goods(string type)
        {
            var station=Prefabs.FindAny(Station(type));
            var proxy=station!=null?station.GetComponent<PlayMakerArrayListProxy>():null;
            if(proxy==null){var list=GameObject.Find("ItemBuySellList");if(list!=null)proxy=list.GetComponent<PlayMakerArrayListProxy>();}
            var goods=proxy!=null?proxy.preFillGameObjectList.Where(g=>g!=null).ToArray():new GameObject[0];
            if(goods.Length==0&&proxy?.arrayList!=null)goods=proxy.arrayList.OfType<GameObject>().Where(g=>g!=null).ToArray();
            if(goods.Length==0)return new[]{Prefabs.FindAny("Repairbox_Small"),Prefabs.FindAny("Toolbox"),Prefabs.FindAny("first_aid_1")}.Where(g=>g!=null).ToArray();
            return goods.Where(g=>g.GetComponents<PlayMakerFSM>().Any(f=>f.FsmName=="ForBuy")).ToArray();
        }
    }
}
