using System.Collections.Generic;
using UnityEngine;
using HutongGames.PlayMaker;
namespace WildWasteland.Core
{
    /// <summary>Native item references and stack snapshot returned by Loot; removal is performed by the same provider.</summary>
    public sealed class QuestItemHit
    {
        public GameObject Go;
        public int Stack = 1;
        public FsmInt Qty;       // the item's own "Quantity" variable, if it is a stack
        public bool Held;        // in the hand / a quick slot (under the player camera)
        public bool Reserve;     // not an object: spare rounds in the player's ammo pouch (Qty = that ammo counter)
        public int PerBox = 1;   // rounds that make one "box" for a reserve hit
        public float Dist;
        public string Display = "";
    }

    /// <summary>Loot-owned item discovery and native turn-in operations; quest policy supplies the minimum liquid fraction.</summary>
    public interface IQuestItems
    {
        float ContainerCapacity(string token,out float bestHave);
        List<QuestItemHit> FindItems(string token,Vector3 playerPos,float radius,bool countCarried,float minimumLiquidFraction);
        void TakeItems(List<QuestItemHit> hits,int count,Vector3? from=null);
    }
}
