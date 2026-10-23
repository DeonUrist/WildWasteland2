using System;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Isolated bake of one POI prefab (README A6-9, INavApi.BakeIsolated): ex reflection from Towns TownTowerKit into Nav.</summary>
    internal static partial class Nav
    {
        /// <summary>
        /// Bakes a prefab's reusable map away from the world: an inactive-render copy on a flat 64 m test ground at y -128, baked by the
        /// normal sliced baker (without replacing the bake the scheduler is running) and published to the custom map cache, then removed.
        /// </summary>
        internal sealed class IsolatedBake
        {
            private GameObject _stage;
            private Structure _bake;
            private readonly GameObject _prefab;

            /// <summary>The map was published (or navigation is off: nothing to bake).</summary>
            internal bool Ready { get; private set; }

            /// <summary>The map file written by the bake (null until Ready).</summary>
            internal string MapFile { get; private set; }

            /// <summary>A bake for the given prefab (nothing happens until <see cref="Step"/>).</summary>
            internal IsolatedBake(GameObject prefab) { _prefab = prefab; }

            /// <summary>Rough progress 0..1 over load / floors / edges / areas / write.</summary>
            internal float Progress
            {
                get
                {
                    if (Ready) return 1; if (_bake == null) return 0;
                    float next = _bake.Next / (float)Mathf.Max(1, _bake.W * _bake.H);
                    return _bake.Phase < 0 ? .1f : _bake.Phase == 0 ? .1f + .5f * next : _bake.Phase == 1 ? .6f + .25f * next : _bake.Phase == 2 ? .85f : .9f;
                }
            }

            /// <summary>One slice of the bake (first call builds the stage). False when there is nothing (more) to do. Throws when the
            /// prefab has no navigation geometry or the published map is missing.</summary>
            internal bool Step()
            {
                if (Ready || _prefab == null) return false;
                if (!On) { Ready = true; return true; }
                if (_stage == null)
                {
                    _stage = new GameObject("NPCAI isolated navigation bake"); _stage.transform.position = new Vector3(0, -128, 0);
                    var floor = new GameObject("Temporary flat bake ground"); floor.transform.SetParent(_stage.transform, false);
                    var box = floor.AddComponent<BoxCollider>(); box.center = new Vector3(0, -.25f, 0); box.size = new Vector3(64, .5f, 64);
                    var copy = UnityEngine.Object.Instantiate(_prefab, _stage.transform, false); copy.name = _prefab.name; copy.SetActive(true);
                    foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = true;
                    Physics.SyncTransforms();
                    _bake = Make(copy.transform);
                    if (_bake == null) throw new InvalidOperationException(_prefab.name + " has no navigation geometry");
                    _bake.IsolatedMap = true;
                    BeginBake(_bake, true);
                    return true;
                }
                // the normal sliced baker and disk cache, without replacing the scheduler's current structure
                var current = _baking;
                try { BakeStep(_bake); }
                finally { _baking = current; }
                if (_bake.Baked)
                {
                    var file = _bake.LoadedMap && _bake.LoadedVanilla ? _bake.VanillaMapFile : _bake.MapFile;
                    if (file == null || !System.IO.File.Exists(file)) throw new InvalidOperationException("Reusable navigation cache of " + _prefab.name + " was not published");
                    MapFile = file; Ready = true; Release();
                }
                return true;
            }

            /// <summary>Closes the map job, forgets the stage's floor colliders and queued masks and destroys the stage.</summary>
            internal void Release()
            {
                if (_bake != null)
                {
                    CloseMap(_bake);
                    if (_stage != null) foreach (var collider in _stage.GetComponentsInChildren<Collider>()) _floorCols.Remove(collider.GetInstanceID());
                    _maskQueue.Remove(_bake);
                }
                _bake = null;
                if (_stage != null) { _stage.SetActive(false); UnityEngine.Object.Destroy(_stage); _stage = null; }
            }

            // ---------------------------------------------------------- INavApi.BakeIsolated queue (one at a time, driven by Nav.Update)

            private static IsolatedBake _queued;

            /// <summary>Starts an isolated bake of a prefab found by name in Core PrefabCatalog; false when unknown, off or busy.</summary>
            internal static bool Queue(string prefabName)
            {
                if (_queued != null || !On || string.IsNullOrEmpty(prefabName)) return false;
                var prefab = PrefabCatalog.Exact(prefabName) ?? PrefabCatalog.Find(prefabName);
                if (prefab == null) return false;
                _queued = new IsolatedBake(prefab);
                return true;
            }

            /// <summary>Called by Nav.Update: one slice of the queued isolated bake; drops it when done or failed.</summary>
            internal static void TickQueued()
            {
                var job = _queued;
                if (job == null) return;
                try
                {
                    job.Step();
                    if (job.Ready) { _queued = null; if (Log.L1) Log.Main(NavRuntime.Owner, "isolated navigation map ready: " + job.MapFile); }
                }
                catch (Exception e) { _queued = null; job.Release(); Log.Warn(NavRuntime.Owner, "isolated navigation bake failed: " + e.Message); }
            }

            /// <summary>Cancels the queued isolated bake (scene load / module stop).</summary>
            internal static void CancelQueued() { var job = _queued; _queued = null; if (job != null) job.Release(); }
        }
    }
}
