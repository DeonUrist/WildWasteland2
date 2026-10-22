using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    internal sealed class TownWallKit
    {
        internal readonly Mesh[] Meshes = new Mesh[4];
        internal readonly Material[][] Materials = new Material[4][];
        internal GameObject Banner;
        internal readonly TownTowerKit Tower = new TownTowerKit();
        internal readonly List<GameObject> Trees = new List<GameObject>();
        internal readonly TownDressingKit Dressing = new TownDressingKit();
        private readonly Dictionary<Material, Material> _shared = new Dictionary<Material, Material>();
        private readonly Material[] _gateMaterials = new Material[2];
        private readonly Texture2D[] _gateTextures = new Texture2D[2];
        internal bool Available { get { return Banner != null && Meshes[0] != null; } }
        internal void Load(List<GameObject> prefabs)
        {
            Tower.Load(prefabs);
            foreach (var prefab in prefabs) if (prefab.name == "Camp_9")
                foreach (string name in new[] { "Dry3", "Dry4" }) { var tree = prefab.transform.Find(name); if (tree != null) Trees.Add(tree.gameObject); }
            // Numeric kit for worker-side dressing and overlay plans: the same colliders the runtime props carry.
            Bounds box = new Bounds();
            Dressing.TowerAvailable = Tower.Available && TownAssets.ColliderBox(Tower.Prefab.transform, out box);
            if (Dressing.TowerAvailable) Dressing.TowerLocal = box;
            for (int i = Trees.Count - 1; i >= 0; i--)
            {
                if (TownAssets.ColliderBox(Trees[i].transform, out box)) Dressing.TreeLocal.Insert(0, box);
                else { Log.Warn("[Towns] kit tree without colliders skipped: " + Trees[i].name); Trees.RemoveAt(i); }
            }
            foreach (var prefab in prefabs) if (prefab.name == "Camp_14")
            {
                for (int i = 0; i < 4; i++)
                {
                    var part = prefab.transform.Find("decor_visible/fence_" + (i + 1));
                    var mesh = part == null ? null : part.GetComponent<MeshFilter>();
                    var renderer = part == null ? null : part.GetComponent<Renderer>();
                    if (mesh != null && renderer != null)
                    {
                        Meshes[i] = mesh.sharedMesh; Materials[i] = renderer.sharedMaterials;
                        for (int m = 0; m < Materials[i].Length; m++)
                        {
                            var source = Materials[i][m]; Material shared;
                            if (source == null) continue;
                            if (!_shared.TryGetValue(source, out shared)) { shared = new Material(source) { name = "Towns shared " + source.name, enableInstancing = true }; _shared.Add(source, shared); }
                            Materials[i][m] = shared;
                        }
                    }
                }
                var banner = prefab.transform.Find("decor/flag_pole"); Banner = banner == null ? null : banner.gameObject;
                break;
            }
            if (Available) { LoadGateTextures(); Log.Always("[Towns] perimeter kit: Camp_14 heavy metal fences and faction entrance banners; four open vehicle gates."); }
            else Log.Warn("[Towns] native Camp_14 perimeter kit missing; wall prototype unavailable (towns retained).");
        }
        private void LoadGateTextures()
        {
            var cloth = Banner.transform.Find("flag_2_2_sided"); var renderer = cloth == null ? null : cloth.GetComponent<Renderer>();
            var source = renderer == null ? null : renderer.sharedMaterial; if (source == null) return;
            for (int i = 0; i < _gateMaterials.Length; i++)
            {
                string file = i == 0 ? "gate_scrapyard.png" : "gate_coyotes.png";
                string path = Path.Combine(Assets.ModuleDir("Towns"), "Perimeter", file);
                Texture2D texture = null;
                try
                {
                    if (!File.Exists(path))
                    {
                        using (var embedded = WildWasteland.Core.DiskResources.Open("Towns.Perimeter." + file))
                        {
                            if (embedded == null) throw new InvalidDataException("Missing packaged gate texture: " + file);
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) embedded.CopyTo(output);
                        }
                    }
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "Towns gate " + i };
                    if (!texture.LoadImage(File.ReadAllBytes(path), true)) throw new InvalidDataException("PNG could not be decoded");
                    if (source.mainTexture != null) { texture.wrapMode = source.mainTexture.wrapMode; texture.filterMode = source.mainTexture.filterMode; texture.anisoLevel = source.mainTexture.anisoLevel; }
                    _gateMaterials[i] = new Material(source) { name = "Towns gate " + i, mainTexture = texture, enableInstancing = true };
                    _gateTextures[i] = texture; texture = null;
                    Log.Always("[Towns] gate texture loaded: " + path);
                }
                catch (Exception e) { Log.Warn("[Towns] gate texture retained native banner: " + path + "; " + e.Message); }
                finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
            }
        }
        internal void ApplyGateTexture(GameObject gate, TownFaction faction)
        {
            var material = _gateMaterials[faction == TownFaction.Coyotes ? 1 : 0]; if (material == null) return;
            var cloth = gate.transform.Find("flag_2_2_sided"); var renderer = cloth == null ? null : cloth.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }
        internal void Dispose()
        {
            Tower.Dispose(); Trees.Clear(); foreach (var material in _shared.Values) UnityEngine.Object.Destroy(material); _shared.Clear();
            for (int i = 0; i < _gateMaterials.Length; i++) { if (_gateMaterials[i] != null) UnityEngine.Object.Destroy(_gateMaterials[i]); if (_gateTextures[i] != null) UnityEngine.Object.Destroy(_gateTextures[i]); _gateMaterials[i] = null; _gateTextures[i] = null; }
        }
    }
    internal sealed class TownWalls
    {
        private struct Piece { internal Vector3 Position, Direction; internal float Length; internal int Kind; }
        private readonly TownSite _site;
        private readonly TownWallKit _kit;
        private readonly List<Piece> _pieces = new List<Piece>();
        private readonly GameObject _root;
        private int _next;
        private readonly TownBorderProps _border;
        internal readonly TownVehicleGates Vehicles;
        internal bool Ready { get { return _next >= _pieces.Count && _border.Ready; } }
        internal bool Prepared { get { return Ready && Vehicles.Ready && Walkers.Ready; } }
        internal readonly TownVehicleGates Walkers;
        internal float Progress { get { return .65f * _next / Mathf.Max(1, _pieces.Count) + .25f * _border.Progress + .05f * (Vehicles.Progress + Walkers.Progress); } }
        internal string Stage { get { return _next < _pieces.Count ? "Building town walls " + _next + "/" + _pieces.Count : !_border.Ready ? _border.Stage : "Connecting town gates"; } }
        internal TownWalls(TownSite site, TownAssets assets, Transform parent)
        {
            _site = site; _kit = assets.WallKit;
            _root = new GameObject("Towns perimeter " + site.Plan.Key); _root.transform.SetParent(parent, false);
            _border = new TownBorderProps(site, assets, _root.transform);
            Vehicles = new TownVehicleGates(site.Plan, false, _border.Blockers);
            Walkers = new TownVehicleGates(site.Plan, true, _border.Blockers);
            var frame = TownPerimeter.Frame(site.Plan);
            for (int side = 0; side < 4; side++)
            {
                var normal = TownPerimeter.Normal(side); var gate = TownPerimeter.Gate(frame, side);
                _pieces.Add(new Piece { Position = gate, Direction = normal, Kind = -1 });
            }
            foreach (var run in TownPerimeter.Outline(site.Plan).Runs)
            {
                var delta = run.B - run.A; float length = delta.magnitude;
                int count = Mathf.Max(1, Mathf.CeilToInt(length / 9.3996f)); float step = length / count;
                for (int i = 0; i < count; i++) _pieces.Add(new Piece { Position = run.A + delta * ((i + .5f) / count),
                    Direction = delta.normalized, Length = step + .04f, Kind = i % 4 });
            }
        }
        // spawned = a native prefab clone was activated this call (the only indivisible cost; the caller paces those per frame).
        internal bool Step(out bool spawned)
        {
            spawned = false;
            if (Ready) return Vehicles.Ready ? Walkers.Step() : Vehicles.Step();
            if (_next >= _pieces.Count) return _border.Step(out spawned);
            var p = _pieces[_next]; var rotation = _site.Anchor.rotation * Quaternion.LookRotation(p.Direction);
            var position = _site.Ground(_site.Anchor.TransformPoint(_site.Plan.WorldCenter + p.Position));
            if (p.Kind < 0)
            {
                // The native warehouse entrance is open. Preserve its banner/support, no invented door FSM.
                var go = UnityEngine.Object.Instantiate(_kit.Banner, _root.transform, false); go.name = "Towns cardinal gate " + _next;
                _kit.ApplyGateTexture(go, _site.Plan.Faction);
                var scale = new Vector3(14f / 1.8193f, 4.1542f, 4.1542f);
                go.transform.localScale = scale; go.transform.rotation = rotation;
                go.transform.position = position - rotation * new Vector3(-.33315f * scale.x, -2.2889f * scale.y, -.3127f * scale.z);
                go.SetActive(true); spawned = true;
            }
            else
            {
                int kind = _kit.Meshes[p.Kind] != null ? p.Kind : 0;
                var go = new GameObject("Towns metal wall " + _next); go.transform.SetParent(_root.transform, false);
                go.transform.SetPositionAndRotation(position + _site.Anchor.up * 2.1052f, rotation);
                go.transform.localScale = new Vector3(1, 1, p.Length / 9.3996f);
                go.AddComponent<MeshFilter>().sharedMesh = _kit.Meshes[kind]; go.AddComponent<MeshRenderer>().sharedMaterials = _kit.Materials[kind];
                var collider = go.AddComponent<BoxCollider>(); collider.size = new Vector3(.2794f, 4.416f, 9.3996f); collider.center = new Vector3(.0173f, .1028f, .0007858f);
            }
            _next++;
            if (_next == _pieces.Count) { Physics.SyncTransforms(); Log.Always("[Towns] bent perimeter built " + _site.Plan.Key + ": " + (_pieces.Count - 4) + " shared-mesh sections, four cardinal open gates; parking outside east gate."); }
            return true;
        }
        internal void Dispose() { if (_root != null) UnityEngine.Object.Destroy(_root); }
    }
}
