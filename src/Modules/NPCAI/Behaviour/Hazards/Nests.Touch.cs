using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Nests part: touching a web raises the nest's alarm.</summary>
    internal static partial class Nests
    {
        // ---------- touching a web ----------
        /// <summary>The player touched a web: the nest's spiders are alerted and run faster, sitters drop.</summary>
        private static void Touch(GameObject player, Vector3 pp, float now)
        {
            if (Senses.PlayerInCar) return;
            Vector3 feet = pp + Vector3.up * 0.2f, mid = pp + Vector3.up * 0.9f, head = pp + Vector3.up * 1.6f;
            foreach (var n in _nests)
            {
                if (n.Phase < 3 || now < n.NextAlarm || Flat(n.C - pp) > n.Radius + 6f) continue;
                Webs.Web hit = null;
                foreach (var w in n.Hanging) if (w.Go != null && (InOrb(w, feet) || InOrb(w, mid) || InOrb(w, head))) { hit = w; break; }
                if (hit == null) foreach (var w in n.Ground) if (w.Go != null && Flat(feet - w.Center) < w.R * 0.9f && Mathf.Abs(pp.y - w.Center.y) < 0.6f) { hit = w; break; }
                if (hit == null) continue;
                n.NextAlarm = now + Mathf.Max(1f, NpcaiSettings.NestAlarmCooldown.Value);
                int c = 0;
                foreach (var a in n.Spiders)
                {
                    if (a == null || a.Owner == null) continue;
                    if (Senses.Alert(a.Owner, player)) { Brain.Boost(a.Owner, NpcaiSettings.NestAlarmSpeed.Value, NpcaiSettings.NestAlarmSeconds.Value); c++; }
                }
                for (int i = n.Sitters.Count - 1; i >= 0; i--) { Drop(n.Sitters[i], player); n.Sitters.RemoveAt(i); }
                PublishTouched(n, hit.Center, player);
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Nests: you touched a " + (hit.Orb ? "hanging" : "ground") + " web at " + n.Name + " - " + c + " spider(s) come at you");
            }
        }

        /// <summary>True when a point lies within a hanging web's disc.</summary>
        private static bool InOrb(Webs.Web w, Vector3 q)
        {
            Vector3 d = q - w.Center; float off = Vector3.Dot(d, w.N);
            if (Mathf.Abs(off) > 0.35f) return false;
            return (d - w.N * off).sqrMagnitude < w.R * w.R * 0.8f;
        }
    }
}
