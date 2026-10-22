using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Camp takeover, POI part (Wave A, from Quests/Takeover.cs): camp ownership after a takeover - suppressing the old owner's spawners, topping up the new owner's garrison, neutralizing camps (B13; Factions.Overrides is the ownership store).</summary>
    internal static partial class Takeover
    {
        static readonly Dictionary<GameObject, bool> Suppressed = new Dictionary<GameObject, bool>();

        // Switches off the old faction's spawners at camps that were taken over, so its people do not respawn.
        /// <summary>Retains and disables a replaced faction’s native spawner.</summary>
        static void Suppress(GameObject go) { if (!Suppressed.ContainsKey(go)) Suppressed[go] = go.activeSelf; go.SetActive(false); }

        static float nextNeutralize;

        // The game's own spawner objects cannot be cloned reliably, so the mod tops the camp up itself: when you come
        // near a taken camp and fewer than GarrisonSize of its owner's people are there, the missing ones are created
        // (and saved with the game like the walkers). At most once per GarrisonCooldownHours of game time.
        static readonly Dictionary<string, float> campFirstNear = new Dictionary<string, float>();

        static readonly Dictionary<string, float> campLastTry = new Dictionary<string, float>();

        // The flag on a taken camp: when the camp has loaded in around you (a few seconds within range) and none of its
        // owner's people are there, a fresh garrison is created. If any of them is ever killed (destroyed while you are
        // near) the camp is not refilled for GarrisonCooldownHours of game time. Unloading is not a death, so leaving
        // and coming back refills an empty camp straight away.
        /// <summary>Reinforces a settled empty camp under its existing cooldown rules.</summary>
        static void TryGarrison(Poi p, string ownerName)
        {
            if (GarrisonSize <= 0 || Stamps == null) return;
            if (Time.unscaledTime < settleUntil) return;                        // a save was just loaded: the game first restores its own NPCs
            double now = WildWasteland.Core.Clock.GameHours;
            if (now < 0.0) return;
            string key = "camp:" + Factions.KeyOf(p);
            float rt = Time.unscaledTime;
            float first;
            if (!campFirstNear.TryGetValue(key, out first)) { campFirstNear[key] = rt; return; }
            if (rt - first < 2f) return;                                        // let the camp finish loading
            long st;
            if (Stamps.TryGetValue(key, out st) && now >= st / 1000.0 && now - st / 1000.0 < GarrisonCooldownHours) return;
            float last;
            if (campLastTry.TryGetValue(key, out last) && rt - last < 60f) return;
            Vector3 campRel = Game.Rel(p.Pos);
            if (pending.Count > 0) return;
            for (int i = 0; i < walkers.Count; i++)
                if (!walkers[i].Patrol && Game.HDist(walkers[i].TargetRel, campRel) < 30f) return;     // the group is still on its way
            Faction f = Factions.Get(ownerName);
            if (f == null || CountDefenders(p.Pos, f.Name, 120f) > 0) return;  // somebody is already home
            int made = SpawnAround(f, p.Pos, campRel, GarrisonSize, key);
            if (made > 0)
            {
                campLastTry[key] = rt;
                Log("Takeover: " + p.Name + " was empty; garrisoned it with " + made + " " + f.Name + " people.");
            }
            else campLastTry[key] = rt - 45f;     // nobody could be made (no one loaded to copy, no ground yet): try again in about 15 s
        }

        /// <summary>Suppresses previous owners and advances takeover garrison maintenance.</summary>
        public static void NeutralizeTick(Vector3 playerWorld)
        {
            if (Factions.Overrides.Count == 0 || Time.unscaledTime < nextNeutralize) return;
            nextNeutralize = Time.unscaledTime + 2f;
            foreach (Poi p in Game.FindPois(playerWorld, 0f, 3000f, "camp"))
            {
                string ownerName;
                if (!Factions.Overrides.TryGetValue(Factions.KeyOf(p), out ownerName) || p.Obj == null) continue;
                string keep = Util.Norm(ownerName);
                for (int c = 0; c < p.Obj.childCount; c++)
                {
                    Transform ch = p.Obj.GetChild(c);
                    if (!ch.gameObject.activeSelf) continue;
                    string n = Util.Norm(ch.name);
                    for (int h = 0; h < Factions.HumanFactions.Length; h++)
                    {
                        string hf = Factions.HumanFactions[h];
                        if (hf.Length > 0 && hf != keep && n.Contains(hf)) { Suppress(ch.gameObject); break; }
                    }
                }
                float pd = Game.HDist(playerWorld, p.Pos);
                if (pd <= GarrisonTrigger) TryGarrison(p, ownerName);
                else if (pd > GarrisonTrigger * 1.5f) campFirstNear.Remove("camp:" + Factions.KeyOf(p));
            }
        }
    }
}
