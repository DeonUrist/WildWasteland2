using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Gunplay
{
    /// <summary>Native player damage path shared by lance explosions.</summary>
    internal static class NativePlayerDamage
    {
        // Explosion damage to the player (the "Witness me!" rider, 2.1.4), the same vanilla path as a ram: Bodypart.Damage = -amount +
        // "Damage", the effect FSM's "Damage" for the hurt sound and splash. God mode: nothing. Returns whether it landed.
        /// <summary>Explosion damage to the player (the "Witness me!" rider, 2.1.4), the same vanilla path as a ram: Bodypart.Damage = -amount + "Damage", the effect FSM's "Damage" for the hurt sound and splash. God mode: nothing. Returns whether it landed.</summary>
        internal static bool Apply(float amount, string source)
        {
            var player = PlayerState.Player;
            if (player == null || amount < 1f) return false;
            PlayMakerFSM bodypart = null, health = null;
            var effects = new List<PlayMakerFSM>();
            foreach (var f in player.GetComponents<PlayMakerFSM>())
            {
                if (f.FsmName == "Bodypart") bodypart = f;
                else if (f.FsmName == "Health") health = f;
                else if (f.FsmName == "DamageEffectSound" || f.FsmName == "DamageEffectSound_InCar") effects.Add(f);
                else if ((f.FsmName == "GODMODE" || f.FsmName == "GODMODEMovement") && f.enabled && f.Fsm.Initialized && f.ActiveStateName == "active")
                { GunplayLog.Deep(source + ": god mode, no damage"); return false; }
            }
            amount = Mathf.Round(amount);
            bool applied = false;
            if (bodypart != null && bodypart.Fsm.Initialized)
            {
                var d = bodypart.FsmVariables.GetFsmFloat("Damage");
                if (d != null) { d.Value = -amount; bodypart.SendEvent("Damage"); applied = true; }
            }
            if (!applied && health != null && health.Fsm.Initialized)
            {
                var h = health.FsmVariables.GetFsmFloat("Health");
                if (h != null) { h.Value -= amount; applied = true; }
            }
            if (applied) foreach (var f in effects) if (f.enabled && f.Fsm.Initialized) f.SendEvent("Damage");
            GunplayLog.Deep(source + ": " + amount + " damage to the player" + (applied ? "" : " (no Player FSM found!)"));
            return applied;
        }

    }
}
