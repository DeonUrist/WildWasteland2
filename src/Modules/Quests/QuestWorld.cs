using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Quests
{
    /// <summary>Quest-owned projections of Core's single world/FSM/prefab discovery indexes.</summary>
    internal static class QuestWorld
    {
        internal static List<PlayMakerFSM> All { get { return FsmIndex.All; } }
        internal static List<PlayMakerFSM> Items { get { return FsmIndex.ByName("ItemName"); } }
        internal static readonly List<PlayMakerFSM> People = new List<PlayMakerFSM>();
        internal static readonly List<GameObject> Objects = new List<GameObject>();
        internal static readonly List<Poi> Pois = new List<Poi>();
        internal static readonly List<TextMeshPro> Icons = new List<TextMeshPro>();
        private static readonly HashSet<int> Known = new HashSet<int>();
        private static readonly Dictionary<int, PlayMakerFSM[]> Sets = new Dictionary<int, PlayMakerFSM[]>();
        private static readonly Dictionary<int, GameObject> SetOwners = new Dictionary<int, GameObject>();
        private static WorldWatch _world;
        private static IModule _owner;
        internal static int Revision;
        internal static bool Ready;
        private static RaycastHit[] _rays = new RaycastHit[64];
        private static readonly Dictionary<int, RaycastHit[]> ExactRays = new Dictionary<int, RaycastHit[]>();
        /// <summary>Registers quest projections; Core owns all scene traversal and global FSM-list work.</summary>
        internal static void Begin(IModule owner)
        {
            Clear(); _owner = owner;
            FsmIndex.Track(owner, null); FsmIndex.Track(owner, "ItemName"); FsmIndex.Track(owner, "Health"); FsmIndex.Track(owner, "ID");
            _world = WorldIndex.Watch(owner, ObserveEveryObject, Observe);
        }
        /// <summary>Matches loaded scene objects for quest identity lookups.</summary>
        private static bool ObserveEveryObject(Transform transform, string name) { return true; }
        /// <summary>Projects quest POI names/factions and visited markers from one shared world observation.</summary>
        private static void Observe(Transform transform, string objectName, Component component)
        {
            Add(transform.gameObject);
            string name = Names.Base(objectName), kind = null;
            if (name.StartsWith("Camp_", StringComparison.Ordinal)) kind = "Camp";
            else if (name.StartsWith("Building_", StringComparison.Ordinal)) kind = "Building";
            else if (name.StartsWith("Cave_", StringComparison.Ordinal)) kind = "Cave";
            else if (name.StartsWith("Wreck_", StringComparison.Ordinal)) kind = "Wreck";
            if (kind != null && transform.GetComponent<Rigidbody>() == null && !objectName.Contains("mapIcon"))
                Pois.Add(new Poi { Obj = transform, Name = name, Kind = kind, Pos = transform.position, Faction = Game.SpawnFactionOf(transform) });
            if (objectName == "mapIcon") { var icon = transform.GetComponent<TextMeshPro>(); if (icon != null) Icons.Add(icon); }
        }
        /// <summary>Keeps a single reference to each scene or prefab object.</summary>
        private static void Add(GameObject gameObject) { if (gameObject != null && Known.Add(gameObject.GetInstanceID())) Objects.Add(gameObject); }
        /// <summary>Releases quest projections while leaving Core indexes available to other consumers.</summary>
        internal static void Clear()
        {
            if (_world != null) WorldIndex.Remove(_world); _world = null;
            if (_owner != null) FsmIndex.RemoveAll(_owner);
            Objects.Clear(); People.Clear(); Pois.Clear(); Icons.Clear(); Known.Clear(); Sets.Clear(); SetOwners.Clear(); Ready = false; Revision = 0; _owner = null;
        }
        /// <summary>Publishes the shared FSM revision into quest-facing lists and observes crew identity without re-scanning the world.</summary>
        internal static bool Step()
        {
            if (!GameRefs.InPlay || !FsmIndex.Ready) return false;
            if (Revision != FsmIndex.Revision)
            {
                People.Clear();
                foreach (var fsm in FsmIndex.ByName("Health")) if (fsm != null) { People.Add(fsm); CrewHistory.Observe(fsm.gameObject); }
                foreach (var fsm in FsmIndex.ByName("ID")) if (fsm != null) People.Add(fsm);
                for (int i = Objects.Count - 1; i >= 0; i--) if (Objects[i] == null) Objects.RemoveAt(i);
                var roots = new List<GameObject>(); PrefabCatalog.AllRoots(roots); foreach (var root in roots) Add(root);
                Revision = FsmIndex.Revision; Ready = true;
            }
            return false;
        }
        /// <summary>Native FSMs on a known object, cached until scene reset.</summary>
        internal static PlayMakerFSM[] Fsms(GameObject gameObject)
        {
            if (gameObject == null) return new PlayMakerFSM[0];
            int id = gameObject.GetInstanceID(); PlayMakerFSM[] result; GameObject owner;
            if (SetOwners.TryGetValue(id, out owner) && owner == gameObject && Sets.TryGetValue(id, out result)) return result;
            result = gameObject.GetComponents<PlayMakerFSM>(); Sets[id] = result; SetOwners[id] = gameObject; return result;
        }
        internal static RaycastHit[] RaycastAll(Vector3 start, Vector3 direction, float length)
        { return RaycastAll(start, direction, length, ~0, QueryTriggerInteraction.UseGlobal); }
        internal static RaycastHit[] RaycastAll(Vector3 start, Vector3 direction, float length, int mask, QueryTriggerInteraction triggers)
        {
            int count;
            while ((count = Physics.RaycastNonAlloc(start, direction, _rays, length, mask, triggers)) >= _rays.Length && _rays.Length < 4096) Array.Resize(ref _rays, _rays.Length * 2);
            RaycastHit[] exact;
            if (!ExactRays.TryGetValue(count, out exact)) ExactRays[count] = exact = new RaycastHit[count];
            Array.Copy(_rays, exact, count); return exact;
        }
    }
}
