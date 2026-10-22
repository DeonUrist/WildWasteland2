using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Owns access to real stored navigation data and runtime graph views; no editor state lives here.</summary>
    internal sealed class NavigationAuthoringApi : INavigationAuthoring
    {
        private readonly Dictionary<PoiNavData, PoiNavData> _snapshots = new Dictionary<PoiNavData, PoiNavData>();
        /// <summary>Retains detached view buffers while a ready native instance and its transform stay unchanged.</summary>
        private sealed class View
        { internal NavigationAuthoringInstance Value; internal PoiNavData Source; internal Vector3[] Positions; internal Vector3 RootPosition; }
        private readonly Dictionary<PoiNav.Inst, View> _views = new Dictionary<PoiNav.Inst, View>();
        private static IModule _setupOwner;
        private static Action<List<Transform>> _setupRoots;
        private static bool _highlight;

        /// <summary>Existing optional missing-bake tint switch, independent of legacy NPC configuration.</summary>
        internal static bool HighlightUnbaked { get { return _highlight; } }
        /// <summary>Appends only roots from a currently running editor owner.</summary>
        internal static void AppendSetupRoots(List<Transform> into)
        { if (_setupOwner != null && ModuleHost.IsRunning(_setupOwner) && _setupRoots != null) _setupRoots(into); }
        /// <summary>Releases all callbacks and diagnostics when the native navigation owner stops.</summary>
        internal static void Reset() { _setupOwner = null; _setupRoots = null; _highlight = false; }
        /// <summary>Copies native roots from the actual grid-navigation registry.</summary>
        public void Roots(List<Transform> into) { into.Clear(); Nav.Roots(into); }
        /// <summary>Returns a cached detached data snapshot, never the live runtime data instance.</summary>
        private PoiNavData Snapshot(PoiNavData data)
        {
            if (data == null) return null;
            PoiNavData snapshot;
            if (!_snapshots.TryGetValue(data, out snapshot))
            { if (_snapshots.Count > 512) _snapshots.Clear(); _snapshots.Add(data, snapshot = data.Copy()); }
            return snapshot;
        }
        /// <summary>Copies graph metadata and world positions only when their source is ready.</summary>
        public void Instances(List<NavigationAuthoringInstance> into)
        {
            into.Clear();
            foreach (var row in PoiNav.Instances)
            {
                if (!row.Ready || row.Root == null) continue;
                View view;
                if (!_views.TryGetValue(row, out view))
                { if (_views.Count > 512) _views.Clear(); _views[row] = view = new View { Value = new NavigationAuthoringInstance() }; }
                if (view.Source != row.Data || view.Positions != row.World || view.RootPosition != row.Root.position)
                {
                    view.Source = row.Data; view.Positions = row.World; view.RootPosition = row.Root.position;
                    view.Value.Root = row.Root; view.Value.Data = Snapshot(row.Data);
                    view.Value.World = row.World != null ? (Vector3[])row.World.Clone() : null;
                    view.Value.Box = row.Box; view.Value.Ready = true;
                }
                into.Add(view.Value);
            }
        }
        /// <summary>Loads authored data synchronously only for explicit editor use.</summary>
        public PoiNavData Get(string poi) { return Snapshot(PoiNavStore.Get(poi)); }
        /// <summary>Publishes a deep copy so later draft edits cannot mutate the runtime store.</summary>
        public void Save(PoiNavData data) { if (data == null) throw new ArgumentNullException(nameof(data)); PoiNavStore.Save(data.Copy()); PoiNav.Forget(data.Poi); }
        /// <summary>Invalidates the runtime graph of one authored type.</summary>
        public void Forget(string poi) { PoiNav.Forget(poi); }
        /// <summary>Asks the native POI registry to reconcile current instances.</summary>
        public void Rescan() { PoiNav.Rescan(); }
        /// <summary>Initializes normal NPC behavior for an unsaved editor test actor.</summary>
        public void NotifyTestSpawn(GameObject actor) { if (actor != null) Brain.Spawned(actor); }
        /// <summary>Uses the canonical native type normalizer.</summary>
        public string TypeOf(string name) { return PoiNavStore.TypeOf(name); }
        /// <summary>Uses the canonical authored output path.</summary>
        public string ConfigPath(string poi) { return PoiNavStore.ConfigPath(poi); }
        /// <summary>Current authored navigation output directory.</summary>
        public string ConfigDir { get { return PoiNavStore.ConfigDir; } }
        /// <summary>Native runtime navigation switch.</summary>
        public ConfigEntry<bool> Enabled { get { return PoiNav.Enabled; } }
        /// <summary>Native ladder-speed tuning.</summary>
        public ConfigEntry<float> LadderSpeed { get { return PoiNav.LadderSpeed; } }
        /// <summary>Current live traversal count.</summary>
        public int Traversals { get { return PoiNavTraverse.Count; } }
        /// <summary>Sets the real native missing-bake tint policy.</summary>
        bool INavigationAuthoring.HighlightUnbaked { get { return _highlight; } set { _highlight = value; } }
        /// <summary>Resets generated native maps through their actual owner.</summary>
        public void ResetCustomMaps() { Nav.ResetCustomMaps(); }
        /// <summary>Exports native bake candidates through their actual owner.</summary>
        public void ExportVanillaMaps() { Nav.ExportVanillaMaps(); }
        /// <summary>Last native export or reset status.</summary>
        public string MapActionStatus { get { return Nav.MapActionStatus; } }
        /// <summary>Retains one owner-scoped setup-root producer.</summary>
        public void RegisterSetupRoots(IModule owner, Action<List<Transform>> append)
        { if (owner == null || append == null) throw new ArgumentNullException(); if (_setupOwner != null && !ReferenceEquals(_setupOwner, owner) && ModuleHost.IsRunning(_setupOwner)) throw new InvalidOperationException("Navigation setup already has an owner"); _setupOwner = owner; _setupRoots = append; }
        /// <summary>Releases only the registering owner's root producer.</summary>
        public void RemoveSetupRoots(IModule owner) { if (ReferenceEquals(_setupOwner, owner)) { _setupOwner = null; _setupRoots = null; } }
    }
}
