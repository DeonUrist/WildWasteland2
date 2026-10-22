using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Quests item query memo, Loot part (Wave A, from Quests/QueryCache.cs): caches "does this item match the token" per FSM for 30 s.</summary>
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
}
