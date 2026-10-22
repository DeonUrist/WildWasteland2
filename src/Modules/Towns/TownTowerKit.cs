using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    // Only static tower geometry and its native player ladder; never clone a camp root.
    internal sealed class TownTowerKit : IDisposable
    {
        internal static readonly string[] Parts = { "container_3", "tent_1_support", "tent_1", "decor/fence_small_4",
            "decor/fence_small_4 (1)", "decor/fence_small_3", "decor/ladder_L" };
        internal GameObject Prefab;
        private GameObject _stage;
        private object _bake;
        private MethodInfo _make, _begin, _step, _close;
        private FieldInfo _baked, _current;
        internal bool Ready { get; private set; }
        internal float Progress
        {
            get
            {
                if (Ready) return 1; if (_bake == null) return 0;
                var type = _bake.GetType(); int phase = (int)type.GetField("Phase").GetValue(_bake);
                float next = (int)type.GetField("Next").GetValue(_bake) / (float)Mathf.Max(1, (int)type.GetField("W").GetValue(_bake) * (int)type.GetField("H").GetValue(_bake));
                return phase < 0 ? .1f : phase == 0 ? .1f + .5f * next : phase == 1 ? .6f + .25f * next : phase == 2 ? .85f : .9f;
            }
        }
        internal bool Available { get { return Prefab != null; } }
        internal void Load(List<GameObject> prefabs)
        {
            foreach (var camp in prefabs) if (camp.name == "Camp_9")
            {
                Prefab = new GameObject("TownsTower"); Prefab.SetActive(false); Prefab.tag = "SpawnerStatic";
                foreach (string path in Parts)
                {
                    var source = camp.transform.Find(path);
                    if (source == null) throw new InvalidOperationException("Small tower part missing: " + path);
                    // Each whitelisted subtree contains meshes, solid colliders and the ladder trigger only.
                    var part = UnityEngine.Object.Instantiate(source.gameObject, Prefab.transform, false);
                    part.name = source.name; part.transform.localPosition = camp.transform.InverseTransformPoint(source.position);
                    part.transform.localRotation = Quaternion.Inverse(camp.transform.rotation) * source.rotation;
                    part.SetActive(true);
                }
                if (Prefab.GetComponentsInChildren<PlayMakerFSM>(true).Length != 0 || Prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                    throw new InvalidOperationException("Tower geometry unexpectedly contains camp logic or physics actors");
                Log.Always("[Towns] small tower extracted: container, canopy/supports, three railings and native ladder; no camp spawners, loot, signs or furnishings.");
                return;
            }
        }
        internal bool Step()
        {
            if (Ready || !Available) return false;
            if (!TownBakeBridge.Enabled) { Ready = true; return true; }
            if (_stage == null)
            {
                _stage = new GameObject("Towns isolated tower bake"); _stage.transform.position = new Vector3(0, -128, 0);
                var floor = new GameObject("Temporary flat bake ground"); floor.transform.SetParent(_stage.transform, false);
                var box = floor.AddComponent<BoxCollider>(); box.center = new Vector3(0, -.25f, 0); box.size = new Vector3(64, .5f, 64);
                var tower = UnityEngine.Object.Instantiate(Prefab, _stage.transform, false); tower.name = Prefab.name; tower.SetActive(true);
                foreach (var renderer in tower.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = true;
                Physics.SyncTransforms();
                var nav = typeof(Services).Assembly.GetType("WildWasteland.Modules.NPC.Nav", true);
                _make = Patches.Method(nav, "Make"); _begin = Patches.Method(nav, "BeginBake"); _step = Patches.Method(nav, "BakeStep"); _close = Patches.Method(nav, "CloseMap");
                _current = nav.GetField("_baking", BindingFlags.Static | BindingFlags.NonPublic);
                _bake = _make.Invoke(null, new object[] { tower.transform });
                if (_bake == null) throw new InvalidOperationException("Tower has no navigation geometry");
                _baked = _bake.GetType().GetField("Baked"); _begin.Invoke(null, new object[] { _bake, true });
                return true;
            }
            // Reuse the existing budgeted baker and disk cache without replacing its current town/building job.
            var current = _current.GetValue(null);
            try { _step.Invoke(null, new[] { _bake }); }
            finally { _current.SetValue(null, current); }
            if ((bool)_baked.GetValue(_bake))
            {
                var file = _bake.GetType().GetField("MapFile").GetValue(_bake) as string;
                if (file == null || !System.IO.File.Exists(file)) throw new InvalidOperationException("Reusable tower navigation cache was not published");
                Ready = true; ReleaseBake();
                Log.Always("[Towns] isolated small-tower navigation ready; shared reusable map: " + file);
            }
            return true;
        }
        private void ReleaseBake()
        {
            if (_bake != null && _close != null) _close.Invoke(null, new[] { _bake });
            if (_bake != null && _stage != null)
            {
                var nav = _bake.GetType().DeclaringType;
                var floors = nav.GetField("_floorCols", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as HashSet<int>;
                if (floors != null) foreach (var collider in _stage.GetComponentsInChildren<Collider>()) floors.Remove(collider.GetInstanceID());
                var masks = nav.GetField("_maskQueue", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as System.Collections.IList;
                if (masks != null) masks.Remove(_bake);
            }
            _bake = null;
            if (_stage != null) { _stage.SetActive(false); UnityEngine.Object.Destroy(_stage); _stage = null; }
        }
        public void Dispose() { ReleaseBake(); if (Prefab != null) UnityEngine.Object.Destroy(Prefab); Prefab = null; }
    }
}
