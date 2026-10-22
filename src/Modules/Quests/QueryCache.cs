using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Quests
{
    internal static partial class Game
    {
        sealed class MatchMemo { internal GameObject Go; internal string Name, Display; internal bool Result; internal float Until; }
        static readonly Dictionary<string, Dictionary<int, MatchMemo>> MatchesByToken = new Dictionary<string, Dictionary<int, MatchMemo>>(StringComparer.Ordinal);
        static int _matches;
        static bool Matches(GameObject go, PlayMakerFSM itemNameFsm, string token, out string display)
        {
            display = FindString(itemNameFsm, "ItemName");
            if (go == null || itemNameFsm == null || token == null) return false;
            if (_matches > 20000) { MatchesByToken.Clear(); _matches = 0; }
            Dictionary<int, MatchMemo> entries;
            if (!MatchesByToken.TryGetValue(token, out entries)) MatchesByToken[token] = entries = new Dictionary<int, MatchMemo>();
            int id = itemNameFsm.GetInstanceID(); MatchMemo memo;
            if (entries.TryGetValue(id, out memo) && ReferenceEquals(memo.Go, go) && memo.Name == go.name && memo.Display == display && Time.unscaledTime < memo.Until) return memo.Result;
            bool result = MatchUncached(go, itemNameFsm, token, out display);
            entries[id] = new MatchMemo { Go = go, Name = go.name, Display = display, Result = result, Until = Time.unscaledTime + 30f }; _matches++;
            return result;
        }
    }
    internal static partial class Hire
    {
        sealed class RangedMemo { internal GameObject Go; internal string Names; internal bool Result; internal float Until; }
        static readonly Dictionary<int, RangedMemo> Ranged = new Dictionary<int, RangedMemo>();
        static string _nearestName; static float _nearestAt;
        static bool IsRangedNpc(GameObject go)
        {
            if (go == null) return false;
            if (madeRaycast != null && madeRaycast.transform.IsChildOf(go.transform)) return true;
            RangedMemo memo; int id = go.GetInstanceID(); string names = rangedNames.Value;
            if (Ranged.TryGetValue(id, out memo) && ReferenceEquals(memo.Go, go) && memo.Names == names && Time.unscaledTime < memo.Until) return memo.Result;
            if (Ranged.Count > 64) Ranged.Clear();
            bool result = RangedUncached(go); Ranged[id] = new RangedMemo { Go = go, Names = names, Result = result, Until = Time.unscaledTime + 1f }; return result;
        }
        static Transform NearestByName(string name, Vector3 from)
        {
            if (_nearestName == name && Time.unscaledTime < _nearestAt) return null;
            var found = NearestUncached(name, from);
            if (found == null) { _nearestName = name; _nearestAt = Time.unscaledTime + .25f; } else _nearestName = null;
            return found;
        }
    }
}
