using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    // Native non-town POIs only; shares the interior population's lifecycle and native spawn sequence.
    internal static class NativePoiPopulation
    {
        private sealed class Site
        {
            internal Transform Root; internal PlayMakerFSM[] Spawners;
            internal readonly List<CreateObject> Actions = new List<CreateObject>();
            internal bool Excluded, Rolled, Empty; internal System.Random Random;
        }
        private sealed class Creature
        { internal GameObject Prefab; internal TownFaction Faction; internal float Radius, Height, Bottom; }
        private sealed class Spawn
        {
            internal Site Site; internal FsmVector3 Original, Offset = new FsmVector3();
        }
        private static readonly Dictionary<Transform, Site> Sites = new Dictionary<Transform, Site>();
        private static readonly Dictionary<PlayMakerFSM, Site> Groups = new Dictionary<PlayMakerFSM, Site>();
        private static readonly Dictionary<CreateObject, Spawn> Actions = new Dictionary<CreateObject, Spawn>();
        private static readonly Dictionary<GameObject, Creature> Creatures = new Dictionary<GameObject, Creature>();
        private static readonly Dictionary<TownFaction, List<Creature>> Pools = new Dictionary<TownFaction, List<Creature>>();
        private static readonly List<Transform> Dead = new List<Transform>();
        private const int Solid = (1 << 0) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 14) | (1 << 16);
        private const int Floor = (1 << 0) | (1 << 8) | (1 << 11) | (1 << 14) | (1 << 16);
        internal static void Install(TownsModule module)
        {
            Clear(); Creatures.Clear(); Pools.Clear();
            Hooks.EnterState.Prefix(module, Enter);
            Hooks.CreateObjectEnter.Prefix(module, Creating); Hooks.CreateObjectEnter.Postfix(module, Created);
            Scheduler.Interval(module, "Native POI spawn cache", 30f, Sweep);
        }
        internal static void Catalog(GameObject prefab, TownFaction faction)
        {
            if (Creatures.ContainsKey(prefab) || prefab.name.IndexOf("Merchant", StringComparison.OrdinalIgnoreCase) >= 0
                || prefab.name.IndexOf("Mechanic", StringComparison.OrdinalIgnoreCase) >= 0
                || FsmUtil.Find(prefab, "Health") == null || FsmUtil.Find(prefab, "Detection") == null) return;
            // Cache a conservative upright volume from the native root collider, once per prefab.
            var capsule = prefab.GetComponent<CapsuleCollider>(); var box = prefab.GetComponent<BoxCollider>(); var sphere = prefab.GetComponent<SphereCollider>();
            Vector3 size, center;
            if (capsule != null && !capsule.isTrigger) { size = Vector3.one * capsule.radius * 2; size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2); center = capsule.center; }
            else if (box != null && !box.isTrigger) { size = box.size; center = box.center; }
            else if (sphere != null && !sphere.isTrigger) { size = Vector3.one * sphere.radius * 2; center = sphere.center; }
            else return;
            size = Vector3.Scale(size, prefab.transform.localScale); center = Vector3.Scale(center, prefab.transform.localScale);
            var creature = new Creature { Prefab = prefab, Faction = faction, Radius = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z)) * .5f + new Vector2(center.x, center.z).magnitude,
                Height = Mathf.Abs(size.y), Bottom = center.y - Mathf.Abs(size.y) * .5f };
            creature.Height = Mathf.Max(creature.Height, creature.Radius * 2);
            Creatures.Add(prefab, creature); List<Creature> pool;
            if (!Pools.TryGetValue(faction, out pool)) Pools.Add(faction, pool = new List<Creature>());
            pool.Add(creature);
        }
        private static Site Find(PlayMakerFSM f)
        {
            if (f == null || f.FsmName != "ItemSpawner" || !f.name.StartsWith("EnemySpawn", StringComparison.Ordinal) || !f.gameObject.scene.IsValid()) return null;
            Site site; if (Groups.TryGetValue(f, out site)) return site;
            Transform root = null;
            for (var t = f.transform.parent; t != null; t = t.parent)
            {
                if (TownMarkers.Owns(t) || t.name.IndexOf(" (Towns ", StringComparison.Ordinal) >= 0) return null;
                if (t.name.StartsWith("Building_", StringComparison.Ordinal) || t.name.StartsWith("Camp_", StringComparison.Ordinal)
                    || t.name.StartsWith("Cave_", StringComparison.Ordinal) || t.name.StartsWith("Wreck_", StringComparison.Ordinal)) root = t;
            }
            if (root == null) return null;
            if (Sites.TryGetValue(root, out site)) { Groups[f] = site; return site; }
            // One discovery scan per POI, never per created enemy. Include inactive shop/boss spawners.
            var fsms = root.GetComponentsInChildren<PlayMakerFSM>(true); var spawners = new List<PlayMakerFSM>();
            site = new Site { Root = root };
            foreach (var sibling in fsms)
            {
                if (sibling.FsmName != "ItemSpawner") continue;
                string name = sibling.gameObject.name;
                if (name.IndexOf("Merchant", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Mechanic", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("bodyguard", StringComparison.OrdinalIgnoreCase) < 0) site.Excluded = true;
                if (!name.StartsWith("EnemySpawn", StringComparison.Ordinal)) continue;
                spawners.Add(sibling); Groups[sibling] = site;
                foreach (var state in sibling.FsmStates) foreach (var action in state.Actions)
                {
                    var create = action as CreateObject;
                    if (create != null && create.gameObject != null && create.gameObject.Name == "Item" && create.storeObject != null && create.storeObject.Name == "Item")
                    { Actions[create] = new Spawn { Site = site, Original = create.position }; site.Actions.Add(create); }
                }
            }
            site.Spawners = spawners.ToArray(); Sites.Add(root, site); return site;
        }
        private static int Value(PlayMakerFSM f, string name)
        { var value = f.FsmVariables.FindFsmInt(name); return value == null ? 0 : value.Value; }
        private static void Roll(Site site)
        {
            if (site.Rolled || site.Excluded) return;
            site.Rolled = true;
            var world = Origin.World(site.Root.position); var save = GameRefs.SaveLoadGame;
            var seed = save == null ? null : save.FsmVariables.FindFsmInt("seed");
            int hash = TownPlanner.Hash(site.Root.name + "/native-population");
            unchecked { hash = hash * 397 ^ Mathf.RoundToInt(world.x * 4); hash = hash * 397 ^ Mathf.RoundToInt(world.z * 4); hash = hash * 397 ^ (seed == null ? 0 : seed.Value); }
            site.Random = new System.Random(hash); site.Empty = site.Random.Next(100) < 15;
            // Existing progress belongs to the save: never thin a partially populated location.
            foreach (var f in site.Spawners) if (Value(f, "OnOff") > 0 || Value(f, "number") > 0) { site.Empty = false; break; }
            Log.Always("[POI] native population " + site.Root.name + " at " + world + ": " + (site.Empty ? "empty (15% roll)" : "occupied; same-category variety"));
        }
        private static bool Enter(Fsm fsm, FsmState state)
        {
            if (fsm.Name != "ItemSpawner" || state.Name != "trigger") return true;
            var f = fsm.Owner as PlayMakerFSM; var site = Find(f);
            if (site == null || site.Excluded) return true;
            Roll(site);
            if (!site.Empty || Value(f, "OnOff") > 0) return true;
            // Native completion is saved by the existing POI/saveItemVar FSMs; leave all loot alone.
            var done = f.FsmVariables.FindFsmInt("OnOff"); if (done == null || fsm.GetState("spawned") == null) return true;
            done.Value = 1; fsm.SetState("spawned"); return false;
        }
        private static bool Creating(CreateObject action)
        {
            Spawn spawn;
            if (!Actions.TryGetValue(action, out spawn))
            { Find(action.Fsm == null ? null : action.Fsm.Owner as PlayMakerFSM); if (!Actions.TryGetValue(action, out spawn)) return true; }
            var site = spawn.Site;
            if (site.Excluded || site.Root == null || action.spawnPoint == null || action.spawnPoint.Value == null) return true;
            Roll(site); Creature original;
            if (!Creatures.TryGetValue(action.gameObject.Value, out original)) return true;
            var point = action.spawnPoint.Value.transform.position + (spawn.Original == null || spawn.Original.IsNone ? Vector3.zero : spawn.Original.Value);
            RaycastHit ground;
            if (!Physics.Raycast(point + Vector3.up * .3f, Vector3.down, out ground, original.Height + 1, Floor, QueryTriggerInteraction.Ignore) || ground.normal.y < .7f) return true;
            var pool = Pools[original.Faction]; var chosen = pool[site.Random.Next(pool.Count)];
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float angle = (float)site.Random.NextDouble() * Mathf.PI * 2, distance = .2f + (float)site.Random.NextDouble() * .55f;
                var feet = ground.point + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                RaycastHit hit;
                if (!Physics.Raycast(feet + Vector3.up * .4f, Vector3.down, out hit, .8f, Floor, QueryTriggerInteraction.Ignore) || hit.normal.y < .7f) continue;
                feet.y = hit.point.y;
                if (!Clear(ground.point, feet, chosen)) continue;
                // Different native rigs have different pivot heights: place the selected collider's feet on the floor.
                action.gameObject.Value = chosen.Prefab;
                spawn.Offset.Value = feet + Vector3.up * (.06f - chosen.Bottom) - action.spawnPoint.Value.transform.position;
                action.position = spawn.Offset; return true;
            }
            return true; // No safe nearby point: retain the authored prefab and position together.
        }
        private static bool Clear(Vector3 from, Vector3 feet, Creature creature)
        {
            float radius = creature.Radius + .04f;
            var bottom = feet + Vector3.up * (radius + .06f); var top = feet + Vector3.up * Mathf.Max(radius + .06f, creature.Height - radius + .06f);
            if (Physics.CheckCapsule(bottom, top, radius, Solid, QueryTriggerInteraction.Ignore)) return false;
            var delta = feet - from; var startBottom = bottom - delta; var startTop = top - delta;
            if (Physics.CheckCapsule(startBottom, startTop, radius, Solid, QueryTriggerInteraction.Ignore)
                || Physics.CapsuleCast(startBottom, startTop, radius, delta.normalized, delta.magnitude, Solid, QueryTriggerInteraction.Ignore)) return false;
            if (Physics.Linecast(from + Vector3.up * .35f, feet + Vector3.up * .35f, Solid, QueryTriggerInteraction.Ignore)
                || Physics.Linecast(from + Vector3.up * .85f, feet + Vector3.up * .85f, Solid, QueryTriggerInteraction.Ignore)) return false;
            for (int i = 0; i < 4; i++)
            {
                var edge = feet + new Vector3(i == 0 ? radius : i == 1 ? -radius : 0, .3f, i == 2 ? radius : i == 3 ? -radius : 0);
                RaycastHit hit;
                if (!Physics.Raycast(edge, Vector3.down, out hit, .55f, Floor, QueryTriggerInteraction.Ignore) || hit.normal.y < .7f || Mathf.Abs(hit.point.y - feet.y) > .2f) return false;
            }
            return true;
        }
        private static void Created(CreateObject action)
        { Spawn spawn; if (Actions.TryGetValue(action, out spawn)) action.position = spawn.Original; }
        private static void Sweep()
        {
            Dead.Clear(); foreach (var pair in Sites) if (pair.Key == null) Dead.Add(pair.Key);
            foreach (var root in Dead)
            {
                var site = Sites[root]; foreach (var f in site.Spawners) Groups.Remove(f);
                foreach (var action in site.Actions) Actions.Remove(action);
                Sites.Remove(root);
            }
        }
        internal static void Clear()
        { foreach (var pair in Actions) pair.Key.position = pair.Value.Original; Actions.Clear(); Groups.Clear(); Sites.Clear(); Dead.Clear(); }
    }
}
