using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>NPC ram damage rule (Vehicles part of Ram): every car's CarAttack hit on a creature other than the player is scaled by [Vehicles / Combat] NPC ram damage multiplier.</summary>
    internal static class NativeRamDamage
    {
        /// <summary>
        /// Postfix of SetFsmFloat.OnEnter (Core.Hooks.SetFsmFloatEnter, every SetFsmFloat entered in the game): scales the native creature hit
        /// after CarAttack wrote it into the target's Bodypart Damage, before CarAttack sends Damage; never changes the shared speed/bumper
        /// variable. Early-out: module off, then an FSM-name length gate ("CarAttack") before any string compare.
        /// </summary>
        internal static void ScaleNpcRamDamage(HutongGames.PlayMaker.Actions.SetFsmFloat action)
        {
            if (!VehiclesModule.Running) return;
            var fsm = action.Fsm;
            if (fsm == null || fsm.Name.Length != 9 || fsm.Name != "CarAttack" || fsm.GameObject == null) return;
            if (action.fsmName == null || action.fsmName.Value != "Bodypart" || action.variableName == null || action.variableName.Value != "Damage") return;
            var target = fsm.GetOwnerDefaultTarget(action.gameObject);
            var player = GameRefs.Player;
            if (target == null || (player != null && target.transform.IsChildOf(player.transform))) return;
            if (SameVehicle(fsm, target)) return;   // a car never rams its own crew (Patrols' prefix skipped the write): nothing to scale
            var bodypart = FsmUtil.Find(target, "Bodypart");
            var damage = bodypart != null ? bodypart.FsmVariables.FindFsmFloat("Damage") : null;
            if (damage == null || damage.Value >= 0f) return;
            damage.Value *= VehiclesSettings.NpcRamDamageMultiplier.Value;
            if (Log.L2) Log.Deep(VehiclesModule.Instance, "Ram: car hit -> " + target.name + ", " + (-damage.Value) + " damage (x" + VehiclesSettings.NpcRamDamageMultiplier.Value + ")");
        }

        /// <summary>True when the hit target sits under the same root as the attacking car (its own crew or riders).</summary>
        private static bool SameVehicle(HutongGames.PlayMaker.Fsm fsm, GameObject target)
        {
            return target != null && fsm.GameObject != null && ReferenceEquals(target.transform.root, fsm.GameObject.transform.root);
        }
    }
}
