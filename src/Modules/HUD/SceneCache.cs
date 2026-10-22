using System;
using System.Collections;
using WildWasteland.Core;
using UnityEngine;
namespace WildWasteland.Modules.HUD
{
    /// <summary>HUD-specific controls/map observations over the shared Core world and FSM indexes.</summary>
    internal static class SceneCache
    {
        private static IEnumerator _keys;
        private static WorldWatch _icons;
        internal static bool KeysReady;
        /// <summary>Releases only this HUD consumer's traversal state and watch.</summary>
        internal static void Clear()
        {
            var disposable = _keys as IDisposable; if (disposable != null) disposable.Dispose();
            _keys = null; KeysReady = false;
            if (_icons != null) WorldIndex.Remove(_icons); _icons = null;
        }
        /// <summary>Starts HUD map-icon observation while leaving shared Core discovery caches intact.</summary>
        internal static void Reset()
        {
            Clear(); SceneLookup.Ensure(); KeyCatalog.SceneReset();
            _icons = WorldIndex.WatchName(HudModule.Instance, "mapIcon", ObserveIcon);
        }
        /// <summary>Observes map icons from the common world walker.</summary>
        private static void ObserveIcon(Transform transform, string name, Component component) { LocationMarkers.Observe(transform); }
        /// <summary>Starts action/key inspection over the shared FSM snapshot.</summary>
        internal static void BeginKeys()
        {
            var disposable = _keys as IDisposable; if (disposable != null) disposable.Dispose();
            FsmIndex.Track(HudModule.Instance, null); KeyCatalog.BeginFsms(); KeysReady = false; _keys = DiscoverKeys();
        }
        /// <summary>Visits one control-state inspection at a time under the existing sliced HUD job.</summary>
        internal static bool Step()
        {
            if (_keys == null) return false;
            if (_keys.MoveNext()) return true;
            var disposable = _keys as IDisposable; if (disposable != null) disposable.Dispose(); _keys = null; return false;
        }
        /// <summary>Queues a new world subtree once through the common discovery service.</summary>
        internal static void QueueRoot(Transform root) { WorldIndex.QueueRoot(root); }
        /// <summary>Feeds controls from the common published FSM list without a second scene hierarchy walk.</summary>
        private static IEnumerator DiscoverKeys()
        {
            while (!FsmIndex.Ready) yield return null;
            var snapshot = FsmIndex.All.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var fsm = snapshot[i]; if (fsm == null || fsm.Fsm == null || !fsm.Fsm.Initialized || fsm.FsmStates == null) continue;
                foreach (var state in fsm.FsmStates) { KeyCatalog.ObserveState(fsm, state); yield return null; }
            }
            KeyCatalog.FinishFsms(); KeysReady = true;
        }
        /// <summary>Looks up a native named object's FSM through Core's shared root cache.</summary>
        internal static PlayMakerFSM Fsm(string name, string fsm) { return FsmUtil.Find(FindPath(name) != null ? FindPath(name).gameObject : null, fsm); }
        /// <summary>Resolves a scene-root path through Core's shared world cache.</summary>
        internal static Transform FindPath(string path) { return SceneLookup.FindPath(path); }
    }
}
