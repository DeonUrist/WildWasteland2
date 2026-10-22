using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Brain part owned by Gunplay: NPC magazines (capacity table, shots fired, reload need).</summary>
    internal static partial class Brain
    {
        // ---------- (1.1.2) magazines ----------
        private static Dictionary<string, int> _mags; private static string _magSrc;
        /// <summary>Magazine capacity of a weapon model from [NpcAim] MagazineTable (0 = never reloads).</summary>
        internal static int MagazineOf(string weaponKey)
        {
            if (weaponKey == null) return 0;
            string src = NpcModule.MagazineTable.Value ?? "";
            if (_mags == null || _magSrc != src)
            {
                _mags = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); _magSrc = src;
                foreach (var part in src.Split(',', ';', '\n'))
                {
                    int eq = part.IndexOf('='); int v;
                    if (eq > 0 && int.TryParse(part.Substring(eq + 1).Trim(), out v)) _mags[part.Substring(0, eq).Trim().ToLowerInvariant()] = v;
                }
            }
            int m;
            return _mags.TryGetValue(weaponKey, out m) ? m : 0;
        }

        // VanillaEvents: one ray of the Damage Ranged burst = one shot; a burst never fires past the magazine (the FSM's own loop counter
        // "multyAttack" is pushed past its limit, so the loop ends after this ray: a single-shot gun fires one ray per burst)
        /// <summary>One ray of the Damage Ranged burst = one shot; the burst stops at the end of the magazine.</summary>
        internal static void Shot(GameObject owner, Fsm damageRanged)
        {
            var n = Get(owner);
            if (n == null || !NpcModule.Magazines.Value) return;
            n.Shots++;
            if (n.Magazine <= 0 && n.Weapon == null)
            {
                n.Weapon = HeldWeapon(n); n.WeaponKey = ApBody.WeaponKey(n.Weapon); n.Magazine = MagazineOf(n.WeaponKey);   // refs-ok: REFACTOR-REQUEST B09-3 (NPC reload timing reads Presentation ApBody; NPCAI decision)
            }
            if (n.Magazine > 0 && n.Shots >= n.Magazine && damageRanged != null)
            {
                if (!ReferenceEquals(n.MultyFsm, damageRanged)) { n.MultyFsm = damageRanged; n.MultyVar = damageRanged.Variables.GetFsmInt("multyAttack"); }
                var cnt = n.MultyVar;
                if (cnt != null) cnt.Value = 1000;
            }
        }

        // Aim, before a burst: true = hold fire (reloading, or empty and not yet standing still enough to reload)
        /// <summary>Before a burst: true = hold fire (reloading, or empty and not yet still enough to reload).</summary>
        internal static bool NeedsReload(GameObject owner)
        {
            var n = Get(owner);
            if (n != null && n.FollowerDriven) return Time.time < n.FollowerReloadUntil;
            if (!NpcModule.Magazines.Value || n == null) return false;
            if (n.Weapon == null)
            {
                // no rig (Apocaplayer absent): the gun is looked up here, once per burst until found
                n.Weapon = HeldWeapon(n); n.WeaponKey = ApBody.WeaponKey(n.Weapon);   // refs-ok: REFACTOR-REQUEST B09-3 (NPC reload timing reads Presentation ApBody; NPCAI decision)
                n.Shots = 0; n.Magazine = MagazineOf(n.WeaponKey);
            }
            if (n.Magazine <= 0) return false;
            float now = Time.time;
            if (n.Reloading)
            {
                if (now < n.ReloadUntil) return true;
                n.Reloading = false; n.Shots = 0;
                return false;
            }
            if (n.Shots < n.Magazine) return false;
            // empty: reload once he stands (the burst itself only happens standing; a runner keeps running with an empty gun)
            bool standing = n.Mode == Mode.Hold || n.Mode == Mode.Rest || (n.Mode == Mode.Cover && n.CoverArrived) || n.Mode == Mode.BackUp || n.Mode == Mode.Off;
            if (!standing) return true;
            // (1.2.0) the reload's length: one round at a time (the revolver, the shotguns, the bolt rifle, the double barrel - the player's copy of the
            // gun reloads that way) = the rounds fired x RoundSeconds; else the player's reload clip (Apocaplayer) or ReloadSeconds; a magazine of
            // 1-2 that isn't loaded round by round: SingleShotReload
            int rounds = 0; float len = 0f;
            bool ap = n.Body != null && ApBody.Available;   // refs-ok: REFACTOR-REQUEST B09-3 (NPC reload timing reads Presentation ApBody; NPCAI decision)
            if (ap && ApBody.OneRoundAtATime(n.WeaponKey))   // refs-ok: REFACTOR-REQUEST B09-3 (NPC reload timing reads Presentation ApBody; NPCAI decision)
            {
                rounds = Mathf.Clamp(n.Shots, 1, n.Magazine);
                len = rounds * NpcModule.RoundSeconds.Value + 0.3f;
            }
            else
            {
                if (ap) len = ApBody.ReloadClipSeconds(n.WeaponKey);   // refs-ok: REFACTOR-REQUEST B09-3 (NPC reload timing reads Presentation ApBody; NPCAI decision)
                if (len <= 0f) len = NpcModule.ReloadSeconds.Value;
                if (n.Magazine <= 2) len = Mathf.Min(len, NpcModule.SingleShotReload.Value);     // a break-action / single shot: a quick one, the clip sped up to it
            }
            n.ReloadRounds = rounds; n.ReloadShown = false;
            n.Reloading = true; n.ReloadUntil = now + len; n.ReloadLen = len; n.ReloadSoundsPlayed = 0;
            if (WildWasteland.Core.Log.L2) GunplayLog.Deep("Brain: " + n.Owner.name + " reloads " + n.WeaponKey + " (" + n.Shots + " shots, " + len.ToString("0.0") + " s" + (rounds > 0 ? ", " + rounds + " rounds one at a time" : "") + ")");
            return true;
        }
    }
}
