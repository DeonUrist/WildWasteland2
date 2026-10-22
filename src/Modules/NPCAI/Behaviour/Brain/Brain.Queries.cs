using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Brain part behind the NPCAI ModAPI (INpcControl): follower / cover queries for the behaviour mode and the generic fright order.</summary>
    internal static partial class Brain
    {
        /// <summary>True while a follower driver (Quests hire) moves this NPC instead of the brain.</summary>
        internal static bool IsFollowerDriven(GameObject owner) { var n = Get(owner); return n != null && n.FollowerDriven; }

        /// <summary>True when the brain registered this NPC.</summary>
        internal static bool Knows(GameObject owner) { return Get(owner) != null; }

        /// <summary>True while the brain keeps this NPC in cover (low health, the game's hide, a fright).</summary>
        internal static bool InCover(GameObject owner) { var n = Get(owner); return n != null && n.Mode == Mode.Cover; }

        /// <summary>
        /// INpcControl.Frighten: the NPC retreats to cover away from <paramref name="threat"/>. Infinite seconds = until told otherwise
        /// (Weapons Pack burning: FireRetreat on), 0 = the fright ends (burning over: FireRetreat off), otherwise for that many seconds.
        /// </summary>
        internal static void FrightenFor(GameObject owner, Vector3 threat, float seconds, string cause)
        {
            if (float.IsPositiveInfinity(seconds)) { FireRetreat(owner, threat, true); return; }
            if (seconds <= 0f) { FireRetreat(owner, threat, false); return; }
            if (!On || owner == null) return;
            var n = Of(owner);
            if (n == null || n.FollowerDriven || n.Travel || n.T.parent != null || n.HpVar == null || n.HpVar.Value < 1f) return;
            Frighten(n, threat, cause ?? "frightened");
            if (!n.BurningFear) n.FrightUntil = Time.time + seconds;
        }
    
        /// <summary>
        /// NpcFrightened subscriber (B17-2, Weapons Pack): an event naming an NPC frightens it (Seconds as in <see cref="FrightenFor"/>); an
        /// event without NPC whose Threat is a corpse is a witnessed kill: hostile humans within 25 m with a line of sight to it flee 6 s.
        /// </summary>
        internal static void OnFrightened(ref NpcFrightened e)
        {
            if (!On) return;
            Vector3 threat = Origin.Scene(e.ThreatWorld);
            if (e.Npc.IsValid) { var npc = Entities.Find(e.Npc); if (npc != null) FrightenFor(npc, threat, e.Seconds, e.Cause); return; }
            var corpse = e.Threat.IsValid ? Entities.Find(e.Threat) : null;
            if (corpse != null) ChainsawWitnesses(corpse);
        }
    }
}
