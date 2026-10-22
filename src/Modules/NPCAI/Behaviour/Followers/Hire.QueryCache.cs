using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Follower query memos, NPCAI part (Wave A, from Quests/QueryCache.cs): ranged-NPC check (1 s) and nearest-by-name miss throttle (0.25 s).</summary>
    internal static partial class Hire
    {
        /// <summary>Memo of a ranged check (per NPC and ranged-names config, kept 1 s).</summary>
        sealed class RangedMemo { internal GameObject Go; internal string Names; internal bool Result; internal float Until; }
        static readonly Dictionary<int, RangedMemo> Ranged = new Dictionary<int, RangedMemo>();
        static string _nearestName; static float _nearestAt;
        /// <summary>True when the NPC has a ranged attack (memoised 1 s).</summary>
        static bool IsRangedNpc(GameObject go)
        {
            if (go == null) return false;
            if (madeRaycast != null && madeRaycast.transform.IsChildOf(go.transform)) return true;
            RangedMemo memo; int id = go.GetInstanceID(); string names = rangedNames.Value;
            if (Ranged.TryGetValue(id, out memo) && ReferenceEquals(memo.Go, go) && memo.Names == names && Time.unscaledTime < memo.Until) return memo.Result;
            if (Ranged.Count > 64) Ranged.Clear();
            bool result = RangedUncached(go); Ranged[id] = new RangedMemo { Go = go, Names = names, Result = result, Until = Time.unscaledTime + 1f }; return result;
        }
        /// <summary>Nearest loaded NPC with a name (a miss is not looked for again for 0.25 s).</summary>
        static Transform NearestByName(string name, Vector3 from)
        {
            if (_nearestName == name && Time.unscaledTime < _nearestAt) return null;
            var found = NearestUncached(name, from);
            if (found == null) { _nearestName = name; _nearestAt = Time.unscaledTime + .25f; } else _nearestName = null;
            return found;
        }
    }
}
