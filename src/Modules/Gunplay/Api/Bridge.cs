using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// Calls into the other modules (was Integration.cs: AppDomain scan for NPCAI.Api / GunplayHUD.Api every 5 s + delegates).
    /// Every call null-checks the Core service and falls back exactly like the standalone mod did without the sibling mod.
    /// </summary>
    internal static class Bridge
    {
        /// <summary>NPC aim spread factor at a distance: NPCAI's when it runs, else the built-in +10 % per 5 m beyond 5 m.</summary>
        internal static float SpreadFactor(float distance)
        {
            var npc = Services.Npc;
            if (npc != null)
            {
                try { return npc.SpreadFactor(distance); }
                catch (Exception e) { GunplayLog.Warn("NPC spread: " + e.Message); }
            }
            float beyond = distance - 5f;
            return 1f + (beyond <= 0f ? 0 : (int)(beyond / 5f)) * 0.1f;
        }

        /// <summary>Old service path of a shot: combat activity for autosave and INpcApi.Shot (NPC hearing).</summary>
        internal static void Shot(GameObject shooter, Vector3 position, int kind, bool player)
        {
            Services.ReportCombat();
            var npc = Services.Npc; if (npc == null) return;
            try { npc.Shot(shooter, position, kind, player); } catch (Exception e) { GunplayLog.Warn("NPC shot: " + e.Message); }
        }

        /// <summary>Old service path of a hit on a creature: combat activity and INpcApi.Hurt.</summary>
        internal static void Hurt(GameObject victim, GameObject attacker)
        {
            Services.ReportCombat();
            var npc = Services.Npc; if (npc == null) return;
            try { npc.Hurt(victim, attacker); } catch (Exception e) { GunplayLog.Warn("NPC hurt: " + e.Message); }
        }

        /// <summary>Old service path of the player's hit feedback (IHitFeedback.PlayerHit).</summary>
        internal static void PlayerHit(GameObject target, Vector3 position, float damage, bool head)
        {
            var hud = Services.Hud; if (hud == null) return;
            try { hud.PlayerHit(target, position, damage, head); } catch (Exception e) { GunplayLog.Warn("HUD hit: " + e.Message); }
        }

        /// <summary>Old service path of the player's vehicle-part hit feedback (IHitFeedback.PartHit).</summary>
        internal static void PartHit(GameObject target, Vector3 position, float damage)
        {
            var hud = Services.Hud; if (hud == null) return;
            try { hud.PartHit(target, position, damage); } catch (Exception e) { GunplayLog.Warn("HUD part hit: " + e.Message); }
        }
    }
}
