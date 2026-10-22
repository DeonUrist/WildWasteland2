using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Loot
{
    /// <summary>Loot-owned token-match memo, invalidated with the item identity, name, display text and world lifecycle.</summary>
    internal sealed partial class QuestItems : IQuestItems
    {
        /// <summary>Releases item identities and native ammo references at world boundaries.</summary>
        internal void Clear() { MatchesByToken.Clear();_matches=0;ammoFsm=null; }
        /// <summary>One memoized token match.</summary>
        sealed class MatchMemo
        {
            /// <summary>Matched object (identity check).</summary>
            internal GameObject Go;
            /// <summary>Object name and display name at match time.</summary>
            internal string Name, Display;
            /// <summary>Match result.</summary>
            internal bool Result;
            /// <summary>Unscaled time the entry expires.</summary>
            internal float Until;
        }
        readonly Dictionary<string, Dictionary<int, MatchMemo>> MatchesByToken = new Dictionary<string, Dictionary<int, MatchMemo>>(StringComparer.Ordinal);
        int _matches;

        /// <summary>Memoized <see cref="MatchUncached"/>; <paramref name="display"/> is always the item's current display name.</summary>
        bool Matches(GameObject go, PlayMakerFSM itemNameFsm, string token, out string display)
        {
            display = ItemText(itemNameFsm, "ItemName");
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
