using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>NPC ram damage rule: every car's CarAttack hit on a creature other than the player is scaled by NPC ram damage multiplier; Vehicles part of Ram (Wave A split; hooked by PatrolsModule on SetFsmFloat.OnEnter).</summary>
    internal static partial class Ram
    {
        // Scale the native creature hit after its value is written, before CarAttack sends Damage. Never change the shared speed/bumper variable.

        internal static void AfterSetFsmFloat(HutongGames.PlayMaker.Actions.SetFsmFloat action)
        {
            if (!PatrolsModule.Running) return;
            var fsm = action.Fsm;
            if (fsm == null || fsm.Name.Length != 9 || fsm.Name != "CarAttack" || fsm.GameObject == null) return;
            if (action.fsmName == null || action.fsmName.Value != "Bodypart" || action.variableName == null || action.variableName.Value != "Damage") return;
            var target = fsm.GetOwnerDefaultTarget(action.gameObject);
            var player = Core.GameRefs.Player;
            if (target == null || (player != null && target.transform.IsChildOf(player.transform))) return;
            if (OwnCrew(fsm, target)) return;   // BeforeSetFsmFloat skipped the write: nothing to scale
            var bodypart = Core.FsmUtil.Find(target, "Bodypart");
            var damage = bodypart != null ? bodypart.FsmVariables.FindFsmFloat("Damage") : null;
            if (damage == null || damage.Value >= 0f) return;
            damage.Value *= PatrolsModule.NpcRamDamageMultiplier.Value;
            if (Core.Log.Verbose) PatrolsModule.Verbose("Ram: car hit -> " + target.name + ", " + (-damage.Value) + " damage (x" + PatrolsModule.NpcRamDamageMultiplier.Value + ")");
        }
    }
}
