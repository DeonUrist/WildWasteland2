using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Quests
{
    /// <summary>Quest acceptance policy consuming the actual Loot item-query provider.</summary>
    internal static partial class Game
    {
        internal static float MinLiquidFraction=.5f;
        /// <summary>Queries native container capacity through Loot, returning no capacity when that module is disabled.</summary>
        internal static float ContainerCapacity(string token,out float bestHave) { var items=ModApi.Get<IQuestItems>();if(items!=null)return items.ContainerCapacity(token,out bestHave);bestHave=0f;return 0f; }
        /// <summary>Finds matching item stacks under the current quest liquid policy.</summary>
        internal static List<QuestItemHit> FindItems(string token,Vector3 playerPos,float radius,bool countCarried) { return ModApi.Get<IQuestItems>()?.FindItems(token,playerPos,radius,countCarried,MinLiquidFraction)??new List<QuestItemHit>(); }
        /// <summary>Sums the actual discovered stack snapshot without changing native inventory.</summary>
        internal static int CountStacks(List<QuestItemHit> hits) { int count=0;foreach(var hit in hits)count+=hit.Stack;return count; }
        /// <summary>Consumes native items through Loot; an unavailable provider cannot silently award a turn-in.</summary>
        internal static void TakeItems(List<QuestItemHit> hits,int count,Vector3? from=null) { var items=ModApi.Get<IQuestItems>();if(items==null)throw new InvalidOperationException("Loot is disabled; quest items cannot be handed in.");items.TakeItems(hits,count,from); }
    }
}
