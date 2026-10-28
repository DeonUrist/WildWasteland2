using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ES3Internal;
using UnityEngine;
using WildWasteland.Core;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Vehicles
{
    internal static class MotorcycleRack
    {
        [Serializable] private sealed class Pose
        {
            public string name = null;
            public float[] position = null, rotation = null, size = null;
            internal Vector3 Position { get { return new Vector3(position[0], position[1], position[2]); } }
            internal Quaternion Rotation { get { return new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]); } }
        }
        [Serializable] private sealed class Layout { public Pose[] tools = null, locks = null; }
        private static GlbModel _model;
        private static Material[] _materials;
        private static Layout _layout;

        private static Pose[] ReadPoses(Dictionary<string, object> root, string key)
        {
            return ((List<object>)root[key]).Select(value =>
            {
                var row = (Dictionary<string, object>)value;
                return new Pose {
                    name = row.ContainsKey("name") ? (string)row["name"] : null,
                    position = ((List<object>)row["position"]).Select(n => (float)(double)n).ToArray(),
                    rotation = ((List<object>)row["rotation"]).Select(n => (float)(double)n).ToArray(),
                    size = row.ContainsKey("size") ? ((List<object>)row["size"]).Select(n => (float)(double)n).ToArray() : null
                };
            }).ToArray();
        }

        /// <summary>Builds the authored rack into the inactive chassis, retaining deterministic local save references.</summary>
        internal static void Prepare(GameObject bike)
        {
            if (bike.transform.Find("MotoRack") != null) return;
            if (_model == null)
            {
                var path = Path.Combine(Assets.ModuleDir("Vehicles"), "Motorcycles", "MotoRack.glb");
                var layout = Json.ParseObject(File.ReadAllText(Path.ChangeExtension(path, ".layout.json")));
                _layout = new Layout { tools = ReadPoses(layout, "tools"), locks = ReadPoses(layout, "locks") };
                _model = Glb.Load(path);
                Object.DontDestroyOnLoad(_model.Mesh);
                foreach (var mesh in _model.Colliders) Object.DontDestroyOnLoad(mesh);
                var native = Resources.FindObjectsOfTypeAll<Material>();
                _materials = new Material[_model.MaterialNames.Count];
                for (int i = 0; i < _materials.Length; i++)
                {
                    var source = native.FirstOrDefault(m => m != null && m.name == _model.MaterialNames[i] && m.shader != null && m.shader.name == "Standard");
                    var material = source != null ? new Material(source) : new Material(Shader.Find("Standard"));
                    material.name = "MotoRack_" + _model.MaterialNames[i];
                    if (source == null && _model.ImageBytes[i] != null)
                    {
                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                        ImageConversion.LoadImage(texture, _model.ImageBytes[i], true);
                        Object.DontDestroyOnLoad(texture); material.mainTexture = texture;
                    }
                    Object.DontDestroyOnLoad(material); _materials[i] = material;
                }
            }
            var rack = new GameObject("MotoRack") { layer = 8 };
            rack.transform.SetParent(bike.transform, false);
            rack.AddComponent<MeshFilter>().sharedMesh = _model.Mesh;
            rack.AddComponent<MeshRenderer>().sharedMaterials = _materials;
            foreach (var mesh in _model.Colliders)
            { var collider = rack.AddComponent<MeshCollider>(); collider.convex = true; collider.sharedMesh = mesh; }
            for (int i = 0; i < _layout.locks.Length; i++)
            {
                var pose = _layout.locks[i];
                var storage = new GameObject("PhysicsLock") { layer = 18 };
                storage.transform.SetParent(rack.transform, false);
                storage.transform.localPosition = pose.Position; storage.transform.localRotation = pose.Rotation;
                var box = storage.AddComponent<BoxCollider>(); box.isTrigger = true;
                box.size = new Vector3(pose.size[0], pose.size[1], pose.size[2]);
            }
            WarRig.Factory.RefTree(bike.GetComponent<ES3Prefab>(), rack.transform, "Motorcycle/MotoRack");
            Refresh(bike);
            Log.Main(VehiclesModule.Instance, "Motorcycle rack ready: authored Rack/Bags, " + _model.Colliders.Count + " hollow collision hulls, 3 cargo lock areas; both toolsets placed from MotoRack.blend.");
        }

        /// <summary>Easy Save may restore inherited collider flags and old tool poses after cloning the corrected chassis.</summary>
        internal static void Refresh(GameObject bike)
        {
            var legacy = bike.transform.Find("parts/PhysicsLock");
            if (legacy != null) foreach (var box in legacy.GetComponents<BoxCollider>()) box.enabled = false;
            if (_layout == null) return;
            var basket = bike.transform.Find("MotoRack/PhysicsLock");
            if (basket != null)
            {
                var box = basket.GetComponent<BoxCollider>(); var size = _layout.locks[0].size;
                box.center = new Vector3(0, .2f, 0); box.size = new Vector3(size[0], size[1] + .4f, size[2]);
            }
            foreach (var pose in _layout.tools)
            {
                var tool = bike.transform.Find("parts/" + pose.name);
                if (tool == null) continue;
                tool.localPosition = pose.Position; tool.localRotation = pose.Rotation;
                tool.GetComponent<Renderer>().enabled = true; tool.GetComponent<Collider>().enabled = true;
                foreach (var fsm in tool.GetComponents<PlayMakerFSM>()) fsm.enabled = true;
                tool.gameObject.SetActive(true);
            }
        }
    }
}
