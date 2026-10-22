using System;
using System.Collections;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Seated-crew AI muting (disables NPC AI/mover FSMs); NPCAI part of Crew, future INpcApi.SetSeated (Wave A split).</summary>
    internal static class NpcSeatState
    {
        private static readonly string[] MutedFsms = { "Movement", "Unstuck", "Rotate", "Detection", "Attack", "RangedAttackWait", "Damage Ranged", "Codex", "Sound", "Sound2", "Sound3" };
        // Switch off the AI / body-mover FSMs; called right after Instantiate (before their Start) and every frame as a guard,
        // because Detection/Damage carry EnableFSM actions that could switch them back on.
        /// <summary>Mutes the AI / mover FSMs of one NPC (allowed FSMs stay on).</summary>
        internal static void MuteAi(GameObject who, string[] allowedFsms = null)
        {
            if (who == null) return;
            MuteAi(who.GetComponents<PlayMakerFSM>(), allowedFsms);
        }

        /// <summary>INpcControl.SetSeated: seated = the AI / mover FSMs muted (MuteAi), unseated = the muted FSMs switched back on.</summary>
        internal static void SetSeated(GameObject who, bool seated)
        {
            if (who == null) return;
            if (seated) { MuteAi(who); return; }
            foreach (var f in who.GetComponents<PlayMakerFSM>())
                if (f != null && !f.enabled && Array.IndexOf(MutedFsms, f.FsmName) >= 0) f.enabled = true;
        }

        /// <summary>Disables every enabled FSM of the muted list except the allowed ones (null = none allowed).</summary>
        internal static void MuteAi(PlayMakerFSM[] fsms, string[] allowedFsms)
        {
            if (fsms == null) return;
            foreach (var f in fsms)
                if (f != null && f.enabled && Array.IndexOf(MutedFsms, f.FsmName) >= 0
                    && (allowedFsms == null || Array.IndexOf(allowedFsms, f.FsmName) < 0)) f.enabled = false;
        }

    }
}
