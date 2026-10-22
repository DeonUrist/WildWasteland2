using System;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// Calls into the other modules (was Integration.cs: AppDomain scan for NPCAI.Api / GunplayHUD.Api every 5 s + delegates).
    /// Every call null-checks the Core service and falls back exactly like the standalone mod did without the sibling mod.
    /// </summary>
    internal static class Bridge
    {
        internal static float SpreadFactor(float distance)
        {
            var npc = Services.Npc;
            if (npc != null)
            {
                try { return npc.SpreadFactor(distance); }
                catch (Exception e) { Core.Log.Warn("[Guns] NPC spread: " + e.Message); }
            }
            float beyond = distance - 5f;
            return 1f + (beyond <= 0f ? 0 : (int)(beyond / 5f)) * 0.1f;
        }

        internal static void Shot(GameObject shooter, Vector3 position, int kind, bool player)
        {
            Services.ReportCombat();
            var npc = Services.Npc; if (npc == null) return;
            try { npc.Shot(shooter, position, kind, player); } catch (Exception e) { Core.Log.Warn("[Guns] NPC shot: " + e.Message); }
        }

        internal static void Hurt(GameObject victim, GameObject attacker)
        {
            Services.ReportCombat();
            var npc = Services.Npc; if (npc == null) return;
            try { npc.Hurt(victim, attacker); } catch (Exception e) { Core.Log.Warn("[Guns] NPC hurt: " + e.Message); }
        }

        internal static void PlayerHit(GameObject target, Vector3 position, float damage, bool head)
        {
            var hud = Services.Hud; if (hud == null) return;
            try { hud.PlayerHit(target, position, damage, head); } catch (Exception e) { Core.Log.Warn("[Guns] HUD hit: " + e.Message); }
        }

        internal static void PartHit(GameObject target, Vector3 position, float damage)
        {
            var hud = Services.Hud; if (hud == null) return;
            try { hud.PartHit(target, position, damage); } catch (Exception e) { Core.Log.Warn("[Guns] HUD part hit: " + e.Message); }
        }
    }
}
