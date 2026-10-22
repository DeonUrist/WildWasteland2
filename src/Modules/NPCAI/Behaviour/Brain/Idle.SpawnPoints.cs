using System;
using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Idle part: camp spawn points (homes) found from the structures' spawners.</summary>
    internal static partial class Idle
    {
        // ---------- spawn points ----------
        /// <summary>postfix on CreateObject.OnEnter (its own; Senses' and Gungirl's postfixes are separate): a camp spawner made an NPC at a spawn point</summary>
        public static void AfterCreateObject(CreateObject __instance)
        {
            if (!NpcaiRuntime.Running) return;
            try
            {
                if (__instance.spawnPoint == null || __instance.storeObject == null) return;
                var made = __instance.storeObject.Value; var at = __instance.spawnPoint.Value;
                var owner = __instance.Fsm != null ? __instance.Fsm.GameObject : null;
                if (made == null || at == null || owner == null) return;
                string n = owner.name;
                bool scrap = n.StartsWith("EnemySpawn_Scrapyard", StringComparison.Ordinal), coy = n.StartsWith("EnemySpawn_Coyotes", StringComparison.Ordinal);
                if ((!scrap && !coy) || n.IndexOf("Attack", StringComparison.Ordinal) >= 0) return;
                _spawnOf[made.GetInstanceID()] = new Spot { T = at.transform, Coyotes = coy };
            }
            catch (Exception e) { NpcaiRuntime.Error("Idle: " + e); }
        }

        /// <summary>the spawn points of a structure: children of each spawn_locations_enemy* that follows an EnemySpawn_Scrapyard / _Coyotes spawner</summary>
        private static List<Spot> SpotsOf(Transform root)
        {
            List<Spot> list;
            int id = root.GetInstanceID();
            if (_spots.TryGetValue(id, out list)) return list;
            list = new List<Spot>();
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                string n = c.name;
                if (!n.StartsWith("spawn_locations_enemy", StringComparison.Ordinal)) continue;
                // the spawner is the sibling just before it (EnemySpawn_X then spawn_locations_enemy_X in every camp prefab read)
                var sp = i > 0 ? root.GetChild(i - 1).name : "";
                bool scrap = sp.StartsWith("EnemySpawn_Scrapyard", StringComparison.Ordinal), coy = sp.StartsWith("EnemySpawn_Coyotes", StringComparison.Ordinal);
                if ((!scrap && !coy) || sp.IndexOf("Attack", StringComparison.Ordinal) >= 0) continue;
                for (int k = 0; k < c.childCount; k++) list.Add(new Spot { T = c.GetChild(k), Coyotes = coy });
            }
            _spots[id] = list;
            return list;
        }

        /// <summary>True when the spawn point may be someone's home ([NPCAI] CoyotesIdle for Coyote towns).</summary>
        private static bool Wanted(Spot s) { return s != null && s.T != null && (!s.Coyotes || NpcaiSettings.IdleCoyotes.Value); }

        /// <summary>Floor point under a position (Default / Ground layers), else the position.</summary>
        private static Vector3 FloorUnder(Vector3 p)
        {
            RaycastHit h;
            return Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out h, 6f, (1 << 0) | (1 << 14), QueryTriggerInteraction.Ignore) ? h.point : p;
        }

        /// <summary>First think of a controller: only Scrapyard (and Coyote) NPCs get a home, the nearest unclaimed spawn point of their structure.</summary>
        private static void Resolve(Ctl c, float now)
        {
            var a = c.A;
            if (a.Tag != "Scrapyard" && (a.Tag != "Coyotes" || !NpcaiSettings.IdleCoyotes.Value)) { c.Excluded = true; c.Resolved = true; return; }
            Spot spot;
            if (_spawnOf.TryGetValue(a.Owner.GetInstanceID(), out spot))
            {
                c.Resolved = true;
                if (!Wanted(spot)) { c.Excluded = true; return; }
                SetHome(c, spot);
                return;
            }
            // restored from a save: the nearest free spawn point of its faction in the structure it stands in
            var root = Nav.StructureRootAt(a.T.position);
            if (root == null) { c.NextResolve = now + 10f; c.NoHome = true; return; }      // not in a known camp (yet): try again later
            Spot best = null; float bd = 40f * 40f;
            foreach (var s in SpotsOf(root))
            {
                if (s.T == null || _claimed.Contains(s.T.GetInstanceID()) || s.Coyotes != (a.Tag == "Coyotes")) continue;
                float d = (s.T.position - a.T.position).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
            c.Resolved = true;
            if (best == null) { c.NoHome = true; return; }          // still searches like the rest, just has no home to go back to
            if (!Wanted(best)) { c.Excluded = true; return; }
            SetHome(c, best);
        }

        /// <summary>Claims a spawn point as the controller's home (floor point under it).</summary>
        private static void SetHome(Ctl c, Spot s)
        {
            c.Home = FloorUnder(s.T.position); c.HasHome = true; c.SpotId = s.T.GetInstanceID(); _claimed.Add(c.SpotId);
            if (WildWasteland.Core.Log.L2) Log(c, "home at " + s.T.parent.name + "/" + s.T.name);
        }

    }
}
