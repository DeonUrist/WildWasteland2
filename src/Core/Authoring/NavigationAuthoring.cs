using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Detached view of one live native POI graph. Consumers treat the data and positions as read-only.</summary>
    public sealed class NavigationAuthoringInstance
    {
        public Transform Root;
        public PoiNavData Data;
        public Vector3[] World;
        public Bounds Box;
        public bool Ready;
    }

    /// <summary>Actual navigation data and cache operations needed by Tools; editor input and physics baking stay in Tools.</summary>
    public interface INavigationAuthoring
    {
        /// <summary>Copies native structure roots into a cleared output list.</summary>
        void Roots(List<Transform> into);
        /// <summary>Copies ready graph views without exposing internal route/graph objects.</summary>
        void Instances(List<NavigationAuthoringInstance> into);
        /// <summary>Stable detached snapshot for a POI type, replaced whenever its stored data changes.</summary>
        PoiNavData Get(string poi);
        /// <summary>Validates and atomically publishes authored data, then invalidates runtime graphs.</summary>
        void Save(PoiNavData data);
        /// <summary>Invalidates a type's live runtime graphs.</summary>
        void Forget(string poi);
        /// <summary>Requests native structure and POI rediscovery.</summary>
        void Rescan();
        /// <summary>Canonical POI type normalization.</summary>
        string TypeOf(string rootName);
        /// <summary>Current output file for one authored POI type.</summary>
        string ConfigPath(string poi);
        /// <summary>Current authored POI navigation folder.</summary>
        string ConfigDir { get; }
        /// <summary>Native navigation switch owned by NPCAI.</summary>
        ConfigEntry<bool> Enabled { get; }
        /// <summary>Native ladder-speed tuning owned by NPCAI.</summary>
        ConfigEntry<float> LadderSpeed { get; }
        /// <summary>Current active native traversal count for diagnostics.</summary>
        int Traversals { get; }
        /// <summary>Controls the existing missing-bake marker diagnostic.</summary>
        bool HighlightUnbaked { get; set; }
        /// <summary>Deletes only locally generated map files and resets affected runtime maps.</summary>
        void ResetCustomMaps();
        /// <summary>Exports existing native bake candidates without changing gameplay data.</summary>
        void ExportVanillaMaps();
        /// <summary>Last explicit authoring-operation result.</summary>
        string MapActionStatus { get; }
        /// <summary>Registers Tools' unsaved setup roots for native graph discovery.</summary>
        void RegisterSetupRoots(IModule owner, Action<List<Transform>> append);
        /// <summary>Releases the exact owner's setup-root callback.</summary>
        void RemoveSetupRoots(IModule owner);
    }
}
