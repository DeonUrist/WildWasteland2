using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>
    /// The one navigation service of NPCAI (README 6.2, P6), provided through <c>ModApi.Provide&lt;INavApi&gt;</c> by the NPCAI entry: grid
    /// structures (baseline), the PoiNav graph (inside Nav.Next for human NPCs), instance overlays and waypoint providers (towns), the
    /// developer world-generation suspension and a change counter. A thin facade over <see cref="Nav"/>; every call runs on the main thread.
    /// </summary>
    internal sealed class NavApi : INavApi
    {
        private readonly IModule _owner;

        /// <summary>Facade owned by the NPCAI module.</summary>
        internal NavApi(IModule owner) { _owner = owner; }

        /// <summary>Fills into (cleared first) with the current nav structures.</summary>
        public void Structures(List<NavStructure> into) { Nav.Structures(into); }

        /// <summary>Structure containing a scene point, or null.</summary>
        public NavStructure StructureAt(Vector3 scene) { return Nav.StructureAt(scene); }

        /// <summary>Discovers/bakes the structure under root now (needs the NPC brain running, like the sweep).</summary>
        public void Discover(Transform root) { Nav.Discover(root); }

        /// <summary>Drops the structure of root; the sweep or Discover finds it again.</summary>
        public void ForgetStructure(Transform root) { Nav.Forget(root); }

        /// <summary>Starts an isolated bake of a POI prefab (resolved through Core PrefabCatalog); false when the prefab is unknown,
        /// navigation is off or another isolated bake is running. Progress is driven by the NPCAI "Nav" job.</summary>
        public bool BakeIsolated(string prefabName) { return Nav.IsolatedBake.Queue(prefabName); }

        /// <summary>Template info for a POI prefab.</summary>
        public bool TryGetTemplate(string prefabName, out NavTemplateInfo info) { return Nav.TryGetTemplate(prefabName, out info); }

        /// <summary>Path of scene points from one point to another (cleared first); false when unreachable or not mapped yet.</summary>
        public bool TryPath(Vector3 fromScene, Vector3 toScene, List<Vector3> into) { return Nav.TryPath(fromScene, toScene, into); }

        /// <summary>Cover spot near scene hidden from threatScene within radius.</summary>
        public bool TryFindCover(Vector3 scene, Vector3 threatScene, float radius, out Vector3 coverScene) { return Nav.CoverSpot(scene, threatScene, radius, out coverScene); }

        /// <summary>Registers an overlay for one structure root; returns its id.</summary>
        public int RegisterOverlay(IModule owner, Transform root, INavOverlay overlay) { return Nav.AddOverlay(owner, root, overlay); }

        /// <summary>Removes an overlay.</summary>
        public void RemoveOverlay(int id) { Nav.RemoveOverlay(id); }

        /// <summary>Registers a waypoint provider (lower order = consulted first); returns its id.</summary>
        public int RegisterWaypointProvider(IModule owner, IWaypointProvider provider, int order) { return Nav.AddWaypointProvider(owner, provider, order); }

        /// <summary>Removes a waypoint provider.</summary>
        public void RemoveWaypointProvider(int id) { Nav.RemoveWaypointProvider(id); }

        /// <summary>"Setup POI pathfinding" developer mode: POI/Towns plan no new towns while true (shared Core flag, set by Tools).</summary>
        public bool WorldGenerationSuspended
        {
            get;
            set;
        }

        /// <summary>Increments whenever structures, overlays or providers change.</summary>
        public int Revision { get { return Nav.Revision; } }

        /// <summary>The module that provides this facade.</summary>
        internal IModule Owner { get { return _owner; } }
    }
}
