using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>
    /// IFollowers provider over the hired follower (README 6.2/6.13). Hiring itself (price, duration, faction) stays the Quests contract
    /// (Hire.Start); this provider answers who follows, dismisses, and boards / leaves a vehicle through the existing follower code.
    /// Follow only confirms the current follower until the Hire split (B18) moves the follower start into NPCAI.
    /// </summary>
    internal static partial class Hire
    {
        /// <summary>The IFollowers implementation (state lives in <see cref="Hire"/>).</summary>
        internal sealed class FollowersApi : IFollowers
        {
            /// <summary>True when npc already is the hired follower (a new hire goes through the Quests hire contract).</summary>
            public bool Follow(GameObject npc, GameObject leader) { return IsFollower(npc); }
            /// <summary>Ends the hire of this follower (no message).</summary>
            public void Dismiss(GameObject npc) { if (IsFollower(npc)) Hire.Dismiss(null); }
            /// <summary>Boards the follower into the vehicle (the free passenger seat is chosen by the follower code; seat ignored).</summary>
            public bool Mount(GameObject npc, GameObject vehicle, Transform seat) { return IsFollower(npc) && vehicle != null && (mounted || TryMount(vehicle)); }
            /// <summary>Gets the follower out of its vehicle, placed beside it.</summary>
            public void Dismount(GameObject npc) { if (IsFollower(npc) && mounted) Hire.Dismount(true); }
            /// <summary>True for the current follower.</summary>
            public bool IsFollower(GameObject npc) { return npc != null && Active && Hire.npc != null && Hire.npc == npc; }
            /// <summary>Fills <paramref name="into"/> (cleared first) with the current follower.</summary>
            public void Followers(List<GameObject> into) { into.Clear(); if (Active && Hire.npc != null) into.Add(Hire.npc); }
            /// <summary>Number of followers (0 or 1).</summary>
            public int Count { get { return Active && Hire.npc != null ? 1 : 0; } }
        }
    }
}
