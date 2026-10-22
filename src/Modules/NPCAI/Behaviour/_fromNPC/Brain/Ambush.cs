using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    // (1.7.3) Ambush predators. Scorpions and spiders (small and big) do not go for you - or for another creature they hate - from the edge of
    // sight. Calm, they let a target come within 3-5 m (chosen at random each time they settle) and only then attack; sounds do not draw
    // them either. Hit by anything, they go for whoever hit them from any distance (Senses.Hurt), and a nest's alarm sends them at you
    // (Senses.Alert). When the fight is over and they are calm again, they lie in wait again.
    // A burrowed scorpion keeps Burrow's own distance (small: 3-5 m, a big one hunting underground: 15 m); this only covers the creatures
    // above ground. Cost: one pass over the registered ambushers every half second.
    internal static class Ambush
    {
        private static readonly List<Senses.Agent> _list = new List<Senses.Agent>();
        private static string[] _prefabs; private static string _src;

        internal static void OnSceneLoaded() { _list.Clear(); }

        private static bool Wanted(string prefab)
        {
            string cfg = NpcModule.AmbushPrefabs.Value ?? "";
            if (_prefabs == null || _src != cfg) { _src = cfg; _prefabs = cfg.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries); for (int i = 0; i < _prefabs.Length; i++) _prefabs[i] = _prefabs[i].Trim(); }
            foreach (var p in _prefabs) if (p.Length > 0 && string.Equals(prefab, p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Senses.Make: every registered NPC passes here once
        internal static void Register(Senses.Agent a)
        {
            if (a == null || a.Owner == null || !Wanted(Senses.PrefabOf(a.Owner))) return;
            _list.Add(a);
            if (NpcModule.AmbushEnabled.Value && a.AmbushDist <= 0f && a.State == Senses.State.Idle) a.AmbushDist = Roll();
        }

        private static float Roll()
        {
            float lo = Mathf.Max(0.5f, NpcModule.AmbushMin.Value), hi = Mathf.Max(lo, NpcModule.AmbushMax.Value);
            return UnityEngine.Random.Range(lo, hi);
        }

        internal static void Tick()      // (WW) a 0.5 s Interval job (game time), was gated here
        {
            if (_list.Count == 0) return;
            bool on = NpcModule.AmbushEnabled.Value;
            for (int i = _list.Count - 1; i >= 0; i--)
            {
                var a = _list[i];
                if (a == null || a.Owner == null) { _list.RemoveAt(i); continue; }
                if (a.Burrowed || a.Travel) continue;                  // burrowed / underground: Burrow decides
                if (!on) { if (a.AmbushDist > 0f) a.AmbushDist = 0f; continue; }     // switched off: Senses' own rules
                if (a.State == Senses.State.Idle && a.AmbushDist <= 0f) a.AmbushDist = Roll();     // calm again: lies in wait
            }
        }
    }
}
