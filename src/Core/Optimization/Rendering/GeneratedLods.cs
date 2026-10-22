using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    // Only sharedMesh is exchanged. Native renderer state, transforms, materials, colliders and rig components remain owned by the game.
    public sealed class GeneratedLods : Feature, IScanClient, ISkinnedScanClient, SaveGuard.IClient
    {
        public override string PlanId { get { return "RC9"; } }
        public override string Key { get { return "Generated mesh LODs"; } }
        public override string Title { get { return "Offline-generated object, vehicle and NPC mesh LODs"; } }
        public override string Category { get { return Categories.Rendering; } }
        public override int Order { get { return 9; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "Uses the optional Performance/LOD pack made by tools/lod-generator/Generate.cmd. Zoom restores original meshes. Missing/unsupported meshes stay original. Restores originals for saves and when disabled."; } }
        public ScanTypes Types { get { return ScanTypes.Renderers | ScanTypes.Skinned; } }

        Knob _near, _far, _zoomFov, _memory, _distance;
        Job _job, _discovery;
        GeneratedLodPack _pack;
        readonly List<Binding> _objects = new List<Binding>();
        readonly Dictionary<int, Binding> _bindings = new Dictionary<int, Binding>();
        readonly Dictionary<int, Entry> _sources = new Dictionary<int, Entry>();
        readonly Dictionary<string, Entry> _cache = new Dictionary<string, Entry>(StringComparer.Ordinal);
        readonly Queue<Completion> _completed = new Queue<Completion>();
        readonly Queue<Entry> _uploads = new Queue<Entry>();
        readonly Dictionary<LODGroup, bool> _wallCulls = new Dictionary<LODGroup, bool>();
        int _cursor, _pending;
        volatile int _epoch;
        long _reserved;
        bool _saving, _guard, _zoom;
        string _status;
        int _missing, _errors;

        sealed class Entry
        {
            internal Mesh Source;
            internal string Payload;
            internal int State, Uploaded, Vertices, Submeshes;
            internal Bounds Bounds;
            internal readonly Mesh[] Variants = new Mesh[2];
            internal GeneratedLodPack.Level[] Data;
        }
        sealed class Binding
        {
            internal Renderer Renderer;
            internal MeshFilter Filter;
            internal SkinnedMeshRenderer Skin;
            internal Mesh Original;
            internal Entry Entry;
            internal int Level;
            internal Mesh Current { get { return Skin != null ? Skin.sharedMesh : Filter != null ? Filter.sharedMesh : null; } }
            internal void Set(Mesh mesh) { if (Skin != null) Skin.sharedMesh = mesh; else if (Filter != null) Filter.sharedMesh = mesh; }
        }
        sealed class Completion { internal int Epoch; internal Entry Entry; internal GeneratedLodPack.Level[] Data; internal string Error; }

        public override void Bind(ModuleConfig cfg)
        {
            _near = NewKnob(cfg, "Full detail screen height", "Projected size above which the original mesh is used.", .08f, .12f, .18f, .18f);
            _far = NewKnob(cfg, "Far detail screen height", "Projected size below which the second generated mesh is used.", .02f, .035f, .05f, .05f);
            _distance = NewKnob(cfg, "Distance multiplier", "Multiplier of mesh detail transition distances. 1.5 retains each detail level 50% farther from the camera.", 1.5f, 1.5f, 1.5f, 1.5f);
            _zoomFov = NewKnob(cfg, "Full detail zoom FOV", "Actual rendered zoom relative to normal FOV, or FOV at/below this value, restores originals (RMB/scroll/binoculars/scopes). 0 disables this override.", 30, 30, 30, 30);
            _memory = NewKnob(cfg, "Cache MB", "Maximum generated payload bytes reserved for the current scene; two worker reads at most. GPU overhead is additional.", 128, 256, 384, 256);
        }

        public override void SubscribeHooks() { Hooks.CreateObjectEnter.Postfix(Module, RenderScan.AfterCreateObject); }
        protected override void OnEnable(FeatureMode mode)
        {
            string root = Path.Combine(Assets.Root, "Performance", "LOD");
            if (!File.Exists(Path.Combine(root, "catalog.tsv")))
            {
                _status = "No pack installed; run tools/lod-generator/Generate.cmd -Install";
                Log.Info("[Performance] RC9 " + _status);
                return;
            }
            _pack = new GeneratedLodPack(root);
            Camera.onPreRender += RenderView;
            _status = _pack.Entries.Count + " generated meshes indexed";
            _epoch++;
            SaveGuard.Register(this);
            RenderScan.Register(this);
            _job = Scheduler.Sliced(Module, "Performance.GeneratedLods", 750, Step, Phase.LateUpdateLast);
            _discovery = Scheduler.Interval(Module, "Performance.GeneratedLodDiscovery", 15, Discover);
            Log.Always("[Performance] RC9 " + _status);
        }

        protected override void OnDisable()
        {
            GeneratedLodExport.Stop();
            Camera.onPreRender -= RenderView;
            RenderScan.Unregister(this);
            SaveGuard.Unregister(this);
            if (_job != null) { _job.Remove(); _job = null; }
            if (_discovery != null) { _discovery.Remove(); _discovery = null; }
            Clear();
            _pack = null;
            RenderScan.RequestFull();
        }

        void Discover() { if (_pack != null && !RenderScan.Busy) RenderScan.RequestFull(); }
        public override void OnWorldReady() { Discover(); }
        public override void OnSceneLoaded() { Clear(); Discover(); }
        public override void OnSaveBegin() { _saving = true; Restore(); }
        public override void OnSaveEnd() { _saving = false; }
        public void RevertSaved() { _guard = true; Restore(); }
        public void ReapplySaved() { _guard = false; }

        void Clear()
        {
            _epoch++;
            Restore();
            foreach (var entry in _cache.Values)
                for (int i = 0; i < entry.Variants.Length; i++) if (entry.Variants[i] != null && (i == 0 || entry.Variants[i] != entry.Variants[0])) UnityEngine.Object.Destroy(entry.Variants[i]);
            foreach (var group in _wallCulls) if (group.Key != null) group.Key.enabled = group.Value;
            _wallCulls.Clear();
            _objects.Clear(); _bindings.Clear(); _sources.Clear(); _cache.Clear(); _uploads.Clear();
            lock (_completed) _completed.Clear();
            _cursor = 0; _reserved = 0; _pending = 0; _saving = _guard = _zoom = false;
            _cameraFrame = -1; _cameras.Clear(); _missing = _errors = 0;
            _rendered.Clear(); _normalFov = null;
        }

        void Restore()
        {
            foreach (var b in _objects)
            {
                if (b.Renderer == null) continue;
                var mesh = b.Current;
                if (mesh == b.Entry.Variants[0] || mesh == b.Entry.Variants[1]) b.Set(b.Original);
                b.Level = 0;
            }
        }

        public void OnRenderer(MeshRenderer renderer, ref ScanCtx c)
        {
            if (c.Asset || c.Player || (c.Root != null && c.Root.name == "ApocaplayerBody")) return;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null)
            {
                if (renderer.name.StartsWith("Towns metal wall ", StringComparison.Ordinal) && c.Root != null && c.Root.name.StartsWith("Towns perimeter ", StringComparison.Ordinal)) Wall(renderer, filter, c.Root);
                else Attach(renderer, filter, null, filter.sharedMesh);
            }
        }

        void Wall(MeshRenderer renderer, MeshFilter filter, Transform root)
        {
            if (_pack == null || filter.sharedMesh == null || _bindings.ContainsKey(renderer.GetInstanceID())) return;
            // RC2 may already have injected a cull-only group before RC9 was enabled. Keep its ownership and restore on disable.
            var group = root.GetComponent<LODGroup>();
            if (group != null && !_wallCulls.ContainsKey(group) && CullLods.Owns(group)) { _wallCulls.Add(group, group.enabled); group.enabled = false; }
            Mesh original = filter.sharedMesh;
            string key = "wall:" + GeneratedLodPack.Key(original);
            Entry entry;
            if (!_cache.TryGetValue(key, out entry))
            {
                Bounds bounds = original.bounds; Vector3 a = bounds.min, b = bounds.max; float x = bounds.center.x;
                var proxy = new Mesh { name = "WWLOD_town_wall_panel" };
                // Two opposite faces, each two triangles: visible from inside and outside, with correct lighting normals.
                proxy.vertices = new[] { new Vector3(x,a.y,a.z), new Vector3(x,b.y,a.z), new Vector3(x,b.y,b.z), new Vector3(x,a.y,b.z),
                    new Vector3(x,a.y,a.z), new Vector3(x,b.y,a.z), new Vector3(x,b.y,b.z), new Vector3(x,a.y,b.z) };
                proxy.normals = new[] { Vector3.right, Vector3.right, Vector3.right, Vector3.right, Vector3.left, Vector3.left, Vector3.left, Vector3.left };
                proxy.tangents = new[] { new Vector4(0,0,1,-1), new Vector4(0,0,1,-1), new Vector4(0,0,1,-1), new Vector4(0,0,1,-1), new Vector4(0,0,1,1), new Vector4(0,0,1,1), new Vector4(0,0,1,1), new Vector4(0,0,1,1) };
                proxy.uv = new[] { new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0), new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0) };
                proxy.subMeshCount = Mathf.Max(1, renderer.sharedMaterials.Length);
                proxy.SetTriangles(new[] { 0,1,2,0,2,3,4,6,5,4,7,6 }, 0, false); proxy.bounds = bounds; proxy.UploadMeshData(true);
                entry = new Entry { Source = original, State = 3, Bounds = bounds };
                entry.Variants[0] = proxy; entry.Variants[1] = proxy; _cache.Add(key, entry);
            }
            var binding = new Binding { Renderer = renderer, Filter = filter, Original = original, Entry = entry };
            _objects.Add(binding); _bindings.Add(renderer.GetInstanceID(), binding);
        }
        public void OnSkinned(SkinnedMeshRenderer renderer, ref ScanCtx c)
        {
            if (!c.Asset && !c.Player && (c.Root == null || c.Root.name != "ApocaplayerBody")) Attach(renderer, null, renderer, renderer.sharedMesh);
        }

        void Attach(Renderer renderer, MeshFilter filter, SkinnedMeshRenderer skin, Mesh source)
        {
            if (_pack == null || source == null || source.blendShapeCount != 0 || source.name.StartsWith("WWLOD_", StringComparison.Ordinal)) return;
            int id = renderer.GetInstanceID();
            if (_bindings.ContainsKey(id)) return;
            Entry entry;
            int sourceId = source.GetInstanceID();
            if (!_sources.TryGetValue(sourceId, out entry))
            {
                string key = GeneratedLodPack.Key(source), payload;
                if (_pack.Entries.TryGetValue(key, out payload))
                {
                    if (!_cache.TryGetValue(key, out entry))
                    {
                        entry = new Entry { Source = source, Payload = payload, Vertices = source.vertexCount, Submeshes = source.subMeshCount, Bounds = source.bounds };
                        _cache.Add(key, entry);
                    }
                }
                else _missing++;
                _sources[sourceId] = entry;
            }
            if (entry == null) return;
            // Bindposes refer to this renderer's existing bone array; never reparent or replace a rig.
            var b = new Binding { Renderer = renderer, Filter = filter, Skin = skin, Original = source, Entry = entry };
            _objects.Add(b); _bindings.Add(id, b);
        }

        void Request(Entry entry)
        {
            if (entry.State != 0 || _pending >= 2) return;
            long size;
            try { size = new FileInfo(Path.Combine(_pack.Root, entry.Payload + ".wwlod")).Length; }
            catch (Exception e) { Fail(entry, e.Message); return; }
            if (size > 128 * 1024 * 1024 || size > Math.Max(1, _memory.Value) * 1024 * 1024 - _reserved) { entry.State = 4; return; }
            _reserved += size; _pending++; entry.State = 1;
            var pack = _pack;
            int epoch = _epoch;
            ThreadPool.QueueUserWorkItem(delegate
            {
                var completed = new Completion { Entry = entry, Epoch = epoch };
                try { completed.Data = pack.Read(entry.Payload, entry.Vertices, entry.Submeshes); }
                catch (Exception e) { completed.Error = e.Message; }
                lock (_completed) { if (epoch == _epoch) _completed.Enqueue(completed); }
            });
        }

        void Fail(Entry entry, string error)
        {
            entry.State = 4; entry.Data = null; _errors++;
            foreach (var mesh in entry.Variants) if (mesh != null) UnityEngine.Object.Destroy(mesh);
            entry.Variants[0] = entry.Variants[1] = null;
            if (_errors <= 5) Log.Warn("[Performance] RC9 keeps original mesh: " + error);
        }

        bool Upload()
        {
            Completion completed = null;
            lock (_completed) if (_completed.Count > 0) completed = _completed.Dequeue();
            if (completed != null && completed.Epoch == _epoch)
            {
                _pending--;
                if (completed.Error != null) Fail(completed.Entry, completed.Error);
                else { completed.Entry.Data = completed.Data; completed.Entry.State = 2; _uploads.Enqueue(completed.Entry); }
            }
            if (_uploads.Count == 0) return completed != null;
            var entry = _uploads.Peek();
            try
            {
                int level = entry.Uploaded;
                entry.Variants[level] = entry.Data[level].Create(entry.Bounds);
                entry.Data[level] = null;
                entry.Uploaded++;
                if (entry.Uploaded == 2) { entry.Data = null; entry.State = 3; _uploads.Dequeue(); }
            }
            catch (Exception e) { _uploads.Dequeue(); Fail(entry, e.Message); }
            return true;
        }

        int _cameraFrame = -1;
        readonly List<Camera> _cameras = new List<Camera>();
        struct View { internal float Tan; internal int Frame; }
        readonly Dictionary<Camera, View> _rendered = new Dictionary<Camera, View>();
        HutongGames.PlayMaker.FsmFloat _normalFov;
        static float ProjectionTan(Camera camera)
        { float m11 = Mathf.Abs(camera.projectionMatrix.m11); return m11 > .0001f ? 1 / m11 : Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f); }
        float ViewTan(Camera camera)
        {
            View view; float tan = ProjectionTan(camera);
            // Third-person projection is restored after rendering. Retain its last actual view for the next sliced pass.
            return _rendered.TryGetValue(camera, out view) && view.Frame >= Time.frameCount - 1 ? Mathf.Min(tan, view.Tan) : tan;
        }
        bool Zoom(Camera camera, float tan)
        {
            if (_zoomFov.Value <= 0 || camera.orthographic) return false;
            if (_normalFov == null) _normalFov = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmFloat("FOV");
            float normal = _normalFov != null && _normalFov.Value > 0 ? _normalFov.Value : camera.fieldOfView;
            return tan <= Mathf.Tan(_zoomFov.Value * Mathf.Deg2Rad * .5f) || tan < Mathf.Tan(normal * Mathf.Deg2Rad * .5f) * .98f;
        }
        void RenderView(Camera camera)
        {
            if (camera == null || !camera.isActiveAndEnabled || !GameCameras.IsGameCamera(camera) || camera.orthographic) return;
            float tan = ProjectionTan(camera); _rendered[camera] = new View { Tan = tan, Frame = Time.frameCount };
            // Runs after camera projection overrides, even when the shared slice budget is exhausted.
            if (!_zoom && Zoom(camera, tan)) { Restore(); _zoom = true; }
        }
        void Cameras()
        {
            if (_cameraFrame == Time.frameCount) return;
            _cameraFrame = Time.frameCount;
            _cameras.Clear();
            GameCameras.Refresh();
            bool zoom = false;
            foreach (var camera in GameCameras.List)
            {
                if (camera == null || !camera.isActiveAndEnabled || camera.cameraType != CameraType.Game) continue;
                _cameras.Add(camera);
                if (Zoom(camera, ViewTan(camera))) zoom = true;
            }
            if (zoom && !_zoom) Restore();
            _zoom = zoom;
        }

        bool Step()
        {
            if (_pack == null) return false;
            Cameras();
            if (_saving || _guard || SaveGuard.Reverted || _zoom || _cameras.Count == 0) return false;
            if (Upload()) return true;
            int remaining = Math.Min(32, _objects.Count);
            for (int i = 0; i < remaining; i++)
            {
                if (_cursor >= _objects.Count) _cursor = 0;
                if (_objects.Count == 0) break;
                var b = _objects[_cursor];
                if (b.Renderer == null) { _objects.RemoveAt(_cursor); continue; }
                _cursor++;
                if (!b.Renderer.enabled || !b.Renderer.gameObject.activeInHierarchy) continue;
                Mesh current = b.Current;
                if (current != b.Original && current != b.Entry.Variants[0] && current != b.Entry.Variants[1]) continue;
                Bounds bounds = b.Renderer.bounds;
                float radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
                float height = 0;
                foreach (var camera in _cameras)
                {
                    if ((camera.cullingMask & (1 << b.Renderer.gameObject.layer)) == 0) continue;
                    float distance = Mathf.Max(.01f, Vector3.Distance(camera.transform.position, bounds.center) / Mathf.Max(.01f, _distance.Value) - radius);
                    float value = camera.orthographic ? radius / Mathf.Max(.01f, camera.orthographicSize)
                        : radius / (distance * ViewTan(camera));
                    height = Mathf.Max(height, value * QualitySettings.lodBias);
                }
                float near = Mathf.Max(.001f, _near.Value), far = Mathf.Clamp(_far.Value, .0001f, near * .9f);
                // Hysteresis prevents repeated mesh swaps at a transition boundary.
                float nearLimit = near * (b.Level == 0 ? .85f : 1.15f);
                float farLimit = far * (b.Level == 2 ? 1.15f : .85f);
                int wanted = height >= nearLimit ? 0 : height >= farLimit ? 1 : 2;
                if (wanted != 0 && b.Entry.State == 0) Request(b.Entry);
                if (b.Entry.State != 3) wanted = 0;
                if (b.Level != wanted) { b.Set(wanted == 0 ? b.Original : b.Entry.Variants[wanted - 1]); b.Level = wanted; }
            }
            // One visit per binding per frame at most; scheduler still enforces its 750 us budget.
            if (_cursor >= _objects.Count) { _cursor = 0; return false; }
            return true;
        }

        public void OnPassDone()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i].Renderer == null) _objects.RemoveAt(i);
            _bindings.Clear();
            foreach (var b in _objects) _bindings[b.Renderer.GetInstanceID()] = b;
        }
        public void OnLight(Light l, ref ScanCtx c) { }
        public void OnParticles(ParticleSystem p, ref ScanCtx c) { }
        public void OnAnimator(Animator a, ref ScanCtx c) { }
        public void OnRoot(Transform root, ref ScanCtx c) { }
        public override void DrawStats(ToolLayout l)
        {
            l.Value("LOD pack", _status ?? "not loaded");
            l.Value("LOD renderers", _objects.Count + "; unique cache " + _cache.Count + "; unmatched meshes " + _missing);
            l.Value("LOD cache", (_reserved / 1048576) + " MB reserved; reads " + _pending + "; errors " + _errors);
            l.Value("LOD view", _zoom ? "zoom: original meshes" : _saving || _guard ? "saving: original meshes" : "automatic screen-size LOD");
            l.Value("LOD export", GeneratedLodExport.Status);
            if (l.Buttons("Export loaded models for LOD generator") == 0) GeneratedLodExport.Start(Module);
        }
    }
}
