using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    /// <summary>NPCAI part of the Guns entry (Wave A split, B07): human boss health cap config, hook, job and CreateObject registration.</summary>
    public sealed partial class GunsModule
    {
        internal static ConfigEntry<bool> BossHealth;          // new toggle (was always on)
        internal static ConfigEntry<float> BossHpPercent;
        // prefab instance id -> boss name (null = not a boss): GameObject.name allocates, so it is read once per prefab
        static readonly Dictionary<int, string> _creates = new Dictionary<int, string>();

        /// <summary>Binds [Guns] BossHealth at its F6 feature-toggle position.</summary>
        partial void BindNpcaiToggles(ModuleConfig cfg)
        {
            BossHealth = cfg.Player("", "BossHealth", true,
                "Adjust the health of the human bosses (Duke Ironjaw, Buzzgut) to [Guns / Bosses] AdjustHumanBossHP. Off = the game's own boss health.");
        }

        /// <summary>Binds [Guns / Bosses] AdjustHumanBossHP at its F6 value position.</summary>
        partial void BindNpcaiValues(ModuleConfig cfg)
        {
            BossHpPercent = cfg.Player("Bosses", "AdjustHumanBossHP", 50f,
                "Health of the human bosses (Duke Ironjaw 1500, Buzzgut 600) in % of the game's own.", new AcceptableValueRange<float>(5f, 300f));
        }

        /// <summary>Subscribes the boss detection observer (Core.Hooks.SensorDetectionsDo prefix).</summary>
        partial void InitNpcaiHooks() { Hooks.SensorDetectionsDo.Prefix(this, Bosses.BeforeGetDetections); }

        /// <summary>Registers the "Bosses" interval job (0.5 s; also installs the lazy compatibility bridges).</summary>
        partial void InitNpcaiJobs() { Scheduler.Interval(this, "Bosses", 0.5f, BossesTick); }

        /// <summary>Forgets every tracked boss and the per-prefab boss-name cache.</summary>
        static partial void ResetNpcai()
        {
            Bosses.OnSceneLoaded();
            _creates.Clear();
        }

        /// <summary>Job "Guns.Bosses": compatibility bridges first (as before), then the boss health scan.</summary>
        static void BossesTick()
        {
            EnsureCompatibility();
            try { Bosses.Tick(); } catch (Exception e) { Core.Log.Error("[Guns] Bosses", e); }
        }

        /// <summary>CreateObject postfix part: registers spawned human bosses (prefab name read once per prefab).</summary>
        static partial void BossCreated(GameObject prefab, GameObject made)
        {
            if (prefab != null && made != null && BossHealth.Value)
            {
                // ItemSpawner uses the same variable for the prefab and the created instance.
                if (ReferenceEquals(prefab, made)) Bosses.Registered(made);
                else
                {
                    string boss;
                    int id = prefab.GetInstanceID();
                    if (!_creates.TryGetValue(id, out boss)) { boss = Bosses.BossName(prefab.name); _creates[id] = boss; }
                    if (boss != null) Bosses.Spawned(made, boss);
                }
            }
        }
    }
}
