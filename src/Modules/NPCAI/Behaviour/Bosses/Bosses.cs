using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.NPC;

namespace WildWasteland.Modules.Guns
{
    // [Gunplay] AdjustHumanBossHP: the human bosses' health in % of the game's own (Duke Ironjaw 1500, Buzzgut 600; asset read 2026-10-02).
    // Bosses carry the FSM "BossUI" (it shows the Health number) and a "Health" FSM. Bosses are registered by NPC detection (Registered) and when the game spawns them
    // (Senses' CreateObject postfix -> Spawned) and by a scene scan shortly after each scene load (bosses restored from a save); only
    // that short list is checked every 3 s. A slow full scan every 60 s stays as a safety net. Health is capped at base x % - a cap,
    // not a multiplication, so it is safe with the game saving and reloading their Health and with any number of checks.
    // Above 100 % only an unhurt boss (Health = base) is raised, once per instance.
    internal static class Bosses
    {
        private sealed class Boss { public GameObject Go; public string Name; public float Base; public FsmFloatRef Hp; }
        private sealed class FsmFloatRef { public PlayMakerFSM Fsm; public HutongGames.PlayMaker.FsmFloat Var; }

        private static readonly Dictionary<string, float> Base = new Dictionary<string, float> { { "Duke_Ironjaw", 1500f }, { "Buzzgut", 600f } };
        private static readonly Dictionary<int, Boss> _bosses = new Dictionary<int, Boss>();
        private static readonly Dictionary<int, GameObject> _detected = new Dictionary<int, GameObject>();
        private static readonly HashSet<int> _raised = new HashSet<int>();
        private static readonly List<int> _dead = new List<int>();
        private static float _next, _nextScan;
        private static int _scanBurst;

        public static void OnSceneLoaded() { _raised.Clear(); _bosses.Clear(); _detected.Clear(); _creates.Clear(); _next = 0f; _nextScan = Time.unscaledTime + 2f; _scanBurst = 4; }

        private static string Clean(string name) { int cut = name.IndexOf('('); return cut > 0 ? name.Substring(0, cut) : name; }

        /// (WW) the boss key for a prefab name, or null (GunsModule caches it per prefab: the CreateObject postfix runs for every spawn)
        internal static string BossName(string prefabName) { return prefabName != null && Base.ContainsKey(prefabName) ? prefabName : null; }
        internal static int Count { get { return _bosses.Count; } }
        /// <summary>True when the object is a tracked human boss (INpcControl.IsBoss).</summary>
        internal static bool IsTracked(GameObject go) { return go != null && _bosses.ContainsKey(go.GetInstanceID()); }
        /// F7: "name: health / cap" per tracked boss
        internal static void Describe(List<string> into)
        {
            into.Clear();
            float pct = Mathf.Max(1f, NpcaiSettings.BossHpPercent.Value) / 100f;
            foreach (var bo in _bosses.Values)
            {
                if (bo.Go == null) continue;
                into.Add(bo.Name + ": " + (bo.Hp != null && bo.Hp.Var != null ? bo.Hp.Var.Value.ToString("0") : "?") + " / " + (bo.Base * pct).ToString("0"));
            }
        }

        // NPC detection registered an NPC (every boss has a Detection FSM): no scene scans needed while detection is on
        internal static void Registered(GameObject owner)
        {
            if (owner == null) return;
            float b; string name = Clean(owner.name);
            if (Base.TryGetValue(name, out b)) Add(owner, name, b);
        }

        internal static bool BeforeGetDetections(Micosmo.SensorToolkit.PlayMaker.SensorGetDetections action)
        {
            if (!NpcaiSettings.BossHealth.Value) return true;
            var fsm = action.Fsm;
            if (fsm == null || fsm.Name != "Detection") return true;
            var owner = fsm.GameObject;
            if (owner == null) return true;
            int id = owner.GetInstanceID();
            GameObject seen;
            if (_detected.TryGetValue(id, out seen) && seen == owner) return true;
            _detected[id] = owner;
            Registered(owner);
            return true;
        }

        // from the CreateObject postfix: prefab name already known (cached), the instance is what was made
        internal static void Spawned(GameObject made, string prefabName)
        {
            float b;
            if (made == null || prefabName == null || !Base.TryGetValue(prefabName, out b)) return;
            Add(made, prefabName, b);
        }

        private static void Add(GameObject go, string name, float b)
        {
            int id = go.GetInstanceID();
            if (!_bosses.ContainsKey(id)) _bosses[id] = new Boss { Go = go, Name = name, Base = b };
        }

        /// <summary>
        /// Registers every live BossUI FSM owner with a known base health (bosses restored from a save) and forgets destroyed detections.
        /// Reads the shared Core FsmIndex "BossUI" list (sliced FsmList walk) instead of a FindObjectsOfType scene scan (README 8 #5).
        /// </summary>
        private static void Scan()
        {
            _dead.Clear();
            foreach (var kv in _detected) if (kv.Value == null) _dead.Add(kv.Key);
            foreach (var id in _dead) _detected.Remove(id);
            var list = FsmIndex.ByName("BossUI");
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (f == null) continue;
                var go = f.gameObject;
                string name = Clean(go.name);
                float b;
                if (Base.TryGetValue(name, out b)) Add(go, name, b);
            }
        }

        // prefab instance id -> boss name (null = not a boss): GameObject.name allocates, so it is read once per prefab
        static readonly Dictionary<int, string> _creates = new Dictionary<int, string>();

        /// <summary>Starts tracking the BossUI FSMs in the Core FsmIndex (module start).</summary>
        internal static void Install(IModule owner) { FsmIndex.Track(owner, "BossUI"); }

        /// <summary>CreateObject.OnEnter postfix: registers spawned human bosses (prefab name read once per prefab).</summary>
        internal static void AfterCreateObject(HutongGames.PlayMaker.Actions.CreateObject __instance)
        {
            if (!NpcaiRuntime.Running || __instance.storeObject == null || __instance.gameObject == null || !NpcaiSettings.BossHealth.Value) return;
            GameObject prefab = __instance.gameObject.Value, made = __instance.storeObject.Value;
            if (prefab == null || made == null) return;
            // ItemSpawner uses the same variable for the prefab and the created instance.
            if (ReferenceEquals(prefab, made)) { Registered(made); return; }
            string boss;
            int id = prefab.GetInstanceID();
            if (!_creates.TryGetValue(id, out boss)) { boss = BossName(prefab.name); _creates[id] = boss; }
            if (boss != null) Spawned(made, boss);
        }

        /// <summary>Job "NPCAI.Bosses" (0.5 s): the boss health cap; exceptions are logged and the next tick runs.</summary>
        internal static void Job()
        {
            try { Tick(); } catch (Exception e) { NpcaiRuntime.Error("Bosses", e); }
        }

        // (WW) scheduler job "Guns.Bosses" (Interval 0.5 s; the 3 s / 5 s / 60 s gates below are unchanged)
        public static void Tick()
        {
            if (!NpcaiSettings.BossHealth.Value) return;
            float now = Time.unscaledTime;
            if (now >= _nextScan)
            {
                // Restored bosses are discovered independently of NPCAI.
                if (GameRefs.Player != null) { Scan(); if (_scanBurst > 0) _scanBurst--; }
                _nextScan = now + (_scanBurst > 0 ? 5f : 60f);
            }
            if (now < _next || _bosses.Count == 0) return;
            _next = now + 3f;
            float pct = Mathf.Max(1f, NpcaiSettings.BossHpPercent.Value) / 100f;
            _dead.Clear();
            foreach (var kv in _bosses)
            {
                var bo = kv.Value;
                if (bo.Go == null) { _dead.Add(kv.Key); continue; }
                if (bo.Hp == null || bo.Hp.Fsm == null)
                {
                    bo.Hp = null;
                    foreach (var h in bo.Go.GetComponents<PlayMakerFSM>())
                        if (h != null && h.FsmName == "Health" && h.Fsm != null && h.Fsm.Initialized)
                        {
                            var hv = h.FsmVariables.FindFsmFloat("Health");
                            if (hv != null) bo.Hp = new FsmFloatRef { Fsm = h, Var = hv };
                            break;
                        }
                    if (bo.Hp == null) continue;        // not initialised yet: next tick
                }
                var v = bo.Hp.Var;
                if (v.Value < 1f) continue;
                float b = bo.Base, target = b * pct;
                int id = kv.Key;
                if (v.Value > target + 0.5f)
                {
                    if (Core.Log.L1) NpcaiRuntime.Main("Bosses: " + bo.Go.name + " health " + v.Value.ToString("0") + " -> " + target.ToString("0") + " (" + (pct * 100f).ToString("0") + " % of " + b.ToString("0") + ")");
                    v.Value = target;
                }
                else if (pct > 1f && !_raised.Contains(id) && Mathf.Abs(v.Value - b) <= 0.5f)
                {
                    v.Value = target;
                    if (Core.Log.L1) NpcaiRuntime.Main("Bosses: " + bo.Go.name + " health raised to " + target.ToString("0"));
                }
                _raised.Add(id);
            }
            foreach (var k in _dead) { _bosses.Remove(k); _raised.Remove(k); }
        }
    }
}
