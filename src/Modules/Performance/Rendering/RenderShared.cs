using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    // Shared plumbing of the Rendering features (plan §2 "settings edits" + "prefab pass"):
    //   RenderScan   - the one sliced scan over prefab assets + scene instances (Resources.FindObjectsOfTypeAll on scene load / world
    //                  ready / when a client starts), then incremental: MapMagic tile objects (PerformanceModule.TileAppliedEvent) and
    //                  objects created by PlayMaker CreateObject (Core.Hooks.CreateObjectEnter postfix).
    //   SaveGuard    - ES3 writes Light / MeshRenderer / ParticleSystem / Camera fields of every saved GameObject and its children
    //                  (Sources code/Assembly-CSharp-firstpass/ES3Types/ES3Type_GameObject.cs:L74-101 "components", ES3Type_Light.cs,
    //                  ES3Type_MeshRenderer.cs, ES3Type_ParticleSystem.cs, ES3Type_Camera.cs). Features never edit the assets of saved
    //                  prefabs; their edits on saved instances are reverted while a save runs and re-applied afterwards.
    //   QualityGuard - QualitySettings values owned by several features; re-applied when the game (graphics menu
    //                  GraphicSettingHelperMethods.ChangeQualitySettings -> QualitySettings.SetQualityLevel, Sources
    //                  code/Assembly-CSharp/AkshayDhotre.GraphicSettingsMenu/GraphicSettingHelperMethods.cs:L17-20) or WW CleanHUD's
    //                  menu (QualitySettings.SetQualityLevel) resets them; restores the level's own value on release.
    //   GameCameras  - PlayerCameraHolder/PlayerCamera and the vehicles' DriveTrigger/3rdCamera/Camera (Sources
    //                  data/hierarchy/scene1_game/PlayerCameraHolder.txt:L7-9, data/hierarchy/prefabs/R/Rustallion.txt:L55-63).

    [Flags]
    public enum ScanTypes { None = 0, Lights = 1, Renderers = 2, Particles = 4, Animators = 8, Roots = 16, Skinned = 32 }

    /// <summary>Where a scanned component lives.</summary>
    public struct ScanCtx
    {
        public bool Asset;          // prefab asset (not in a scene): edits are inherited by every later Instantiate
        public bool Saved;          // self or an ancestor has ES3Prefab: ES3 saves its components -> never edit the asset, revert on save
        public bool Vehicle;        // under a NWH VehicleController
        public bool Player;         // under Player / PlayerCameraHolder (player rig, held weapons, flashlight)
        public Transform Root;      // prefab root: asset root, scene root, or the child of a MapMagic objects pool
    }

    internal interface IScanClient
    {
        ScanTypes Types { get; }
        void OnLight(Light l, ref ScanCtx c);
        void OnRenderer(MeshRenderer r, ref ScanCtx c);
        void OnParticles(ParticleSystem p, ref ScanCtx c);
        void OnAnimator(Animator a, ref ScanCtx c);
        /// <summary>Prefab roots (assets, scene roots, MapMagic pool children) that contain MeshRenderers.</summary>
        void OnRoot(Transform root, ref ScanCtx c);
        /// <summary>A full pass finished, or 64 incremental subtrees were processed: prune records of destroyed objects.</summary>
        void OnPassDone();
    }

    internal interface ISkinnedScanClient { void OnSkinned(SkinnedMeshRenderer r, ref ScanCtx c); }

    internal static class RenderScan
    {
        static readonly List<IScanClient> _clients = new List<IScanClient>();
        static Job _job;
        static bool _fullWanted, _fullRunning;
        static int _requestFrame = -1;
        static ScanTypes _types;

        // full pass state
        static int _stage;                 // 0 lights, 1 renderers, 2 particles, 3 animators, 4 roots, 5 done
        static UnityEngine.Object[] _arr;
        static int _idx;
        static readonly List<Transform> _roots = new List<Transform>();
        static readonly HashSet<int> _rootIds = new HashSet<int>();

        // incremental (subtree) queue
        static readonly Queue<Transform> _subtrees = new Queue<Transform>();
        static readonly HashSet<int> _queued = new HashSet<int>();
        static readonly List<Light> _lBuf = new List<Light>();
        static readonly List<MeshRenderer> _rBuf = new List<MeshRenderer>();
        static readonly List<ParticleSystem> _pBuf = new List<ParticleSystem>();
        static readonly List<Animator> _aBuf = new List<Animator>();
        static readonly List<SkinnedMeshRenderer> _sBuf = new List<SkinnedMeshRenderer>();

        struct Info { public bool Saved, Vehicle, Player; public Transform Root; }
        static readonly Dictionary<int, Info> _info = new Dictionary<int, Info>();

        public static long Passes, Subtrees, Visited;
        public static bool Busy { get { return _fullRunning || _subtrees.Count > 0; } }

        internal static void Register(IScanClient c)
        {
            if (_clients.Contains(c)) return;
            _clients.Add(c);
            _types |= c.Types;
            if (_clients.Count == 1)
            {
                var m = PerformanceModule.Instance;
                _job = Scheduler.Sliced(m, "Performance.RenderScan", 1000, Step);
                PerformanceModule.TileAppliedEvent -= OnTile;
                PerformanceModule.TileAppliedEvent += OnTile;
            }
            RequestFull();
        }

        internal static void Unregister(IScanClient c)
        {
            if (!_clients.Remove(c)) return;
            _types = ScanTypes.None;
            for (int i = 0; i < _clients.Count; i++) _types |= _clients[i].Types;
            if (_clients.Count == 0)
            {
                PerformanceModule.TileAppliedEvent -= OnTile;
                if (_job != null) { _job.Remove(); _job = null; }
                _fullWanted = _fullRunning = false; _arr = null; _subtrees.Clear(); _queued.Clear(); _roots.Clear(); _rootIds.Clear(); _info.Clear();
            }
        }

        /// <summary>Scene load / world ready / a client started: scan assets + all instances again (idempotent per frame).</summary>
        internal static void RequestFull()
        {
            if (_clients.Count == 0 || _requestFrame == Time.frameCount) return;
            _requestFrame = Time.frameCount;
            _fullWanted = true;
        }

        /// <summary>Queue one object (and its children) for the incremental pass.</summary>
        internal static void Enqueue(Transform t)
        {
            if (_clients.Count == 0 || ReferenceEquals(t, null) || t == null) return;
            if (_queued.Add(t.GetInstanceID())) _subtrees.Enqueue(t);
        }

        static void OnTile(MapMagic.Terrains.TerrainTile tile)
        {
            if (tile == null || tile.objectsPool == null) return;
            var pool = tile.objectsPool.transform;
            for (int i = 0; i < pool.childCount; i++) Enqueue(pool.GetChild(i));
        }

        /// <summary>Core.Hooks.CreateObjectEnter postfix (subscribed by the features that need spawns).</summary>
        internal static void AfterCreateObject(HutongGames.PlayMaker.Actions.CreateObject a)
        {
            if (_clients.Count == 0 || a == null || a.storeObject == null) return;
            var go = a.storeObject.Value;
            if (go != null) Enqueue(go.transform);
        }

        static bool Step()
        {
            if (_fullWanted && !_fullRunning) { _fullWanted = false; _fullRunning = true; _stage = -1; _arr = null; _roots.Clear(); _rootIds.Clear(); _info.Clear(); Telemetry.Mark("prefabpass"); }
            if (_fullRunning) { StepFull(); return true; }
            if (_subtrees.Count > 0) { StepSubtree(_subtrees.Dequeue()); if (_subtrees.Count == 0) { _queued.Clear(); _info.Clear(); } return _subtrees.Count > 0; }
            return false;
        }

        static bool Wants(ScanTypes t) { return (_types & t) != 0; }

        static void StepFull()
        {
            if (_stage == 5)
            {
                // roots collected during the renderer stage, 32 per step
                int stop = Math.Min(_idx + 32, _roots.Count);
                for (; _idx < stop; _idx++) DeliverRoot(_roots[_idx]);
                if (_idx >= _roots.Count) Finish();
                return;
            }
            if (_arr == null)
            {
                _stage++; _idx = 0;
                switch (_stage)
                {
                    case 0: if (Wants(ScanTypes.Lights)) _arr = Resources.FindObjectsOfTypeAll<Light>(); break;
                    case 1: if (Wants(ScanTypes.Renderers) || Wants(ScanTypes.Roots)) _arr = Resources.FindObjectsOfTypeAll<MeshRenderer>(); break;
                    case 2: if (Wants(ScanTypes.Particles)) _arr = Resources.FindObjectsOfTypeAll<ParticleSystem>(); break;
                    case 3: if (Wants(ScanTypes.Animators)) _arr = Resources.FindObjectsOfTypeAll<Animator>(); break;
                    case 4: if (Wants(ScanTypes.Skinned)) _arr = Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>(); break;
                    case 5: if (!Wants(ScanTypes.Roots)) Finish(); break;
                    default: Finish(); break;
                }
                return;   // the FindObjectsOfTypeAll call is this step's work
            }
            int end = Math.Min(_idx + 64, _arr.Length);
            for (; _idx < end; _idx++) Deliver(_arr[_idx] as Component);
            if (_idx >= _arr.Length) _arr = null;
        }

        static void Finish()
        {
            _fullRunning = false; _arr = null; _stage = 6; _info.Clear(); _roots.Clear(); _rootIds.Clear(); Passes++;
            for (int i = 0; i < _clients.Count; i++) { try { _clients[i].OnPassDone(); } catch (Exception e) { Log.Error("[Performance] scan pass done", e); } }
        }

        static void DeliverRoot(Transform r)
        {
            if (r == null) return;
            ScanCtx c;
            if (!Ctx(r, out c)) return;
            for (int k = 0; k < _clients.Count; k++)
                if ((_clients[k].Types & ScanTypes.Roots) != 0) { try { _clients[k].OnRoot(r, ref c); } catch (Exception e) { Log.Error("[Performance] scan root", e); } }
        }

        static void StepSubtree(Transform t)
        {
            if (t == null) return;
            if (++Subtrees % 64 == 0)
                for (int i = 0; i < _clients.Count; i++) { try { _clients[i].OnPassDone(); } catch (Exception e) { Log.Error("[Performance] scan prune", e); } }
            var go = t.gameObject;
            if (Wants(ScanTypes.Lights)) { go.GetComponentsInChildren(true, _lBuf); for (int i = 0; i < _lBuf.Count; i++) Deliver(_lBuf[i]); _lBuf.Clear(); }
            if (Wants(ScanTypes.Renderers) || Wants(ScanTypes.Roots)) { go.GetComponentsInChildren(true, _rBuf); for (int i = 0; i < _rBuf.Count; i++) Deliver(_rBuf[i]); _rBuf.Clear(); }
            if (Wants(ScanTypes.Particles)) { go.GetComponentsInChildren(true, _pBuf); for (int i = 0; i < _pBuf.Count; i++) Deliver(_pBuf[i]); _pBuf.Clear(); }
            if (Wants(ScanTypes.Animators)) { go.GetComponentsInChildren(true, _aBuf); for (int i = 0; i < _aBuf.Count; i++) Deliver(_aBuf[i]); _aBuf.Clear(); }
            if (Wants(ScanTypes.Skinned)) { go.GetComponentsInChildren(true, _sBuf); for (int i = 0; i < _sBuf.Count; i++) Deliver(_sBuf[i]); _sBuf.Clear(); }
            if (Wants(ScanTypes.Roots))
            {
                for (int i = 0; i < _roots.Count; i++) DeliverRoot(_roots[i]);
                _roots.Clear(); _rootIds.Clear();
            }
        }

        static void Deliver(Component comp)
        {
            if (ReferenceEquals(comp, null) || comp == null) return;
            ScanCtx c;
            if (!Ctx(comp.transform, out c)) return;
            Visited++;
            var l = comp as Light;
            if (l != null) { for (int k = 0; k < _clients.Count; k++) if ((_clients[k].Types & ScanTypes.Lights) != 0) Safe(_clients[k], l, ref c); return; }
            var r = comp as MeshRenderer;
            if (r != null)
            {
                for (int k = 0; k < _clients.Count; k++) if ((_clients[k].Types & ScanTypes.Renderers) != 0) Safe(_clients[k], r, ref c);
                if (Wants(ScanTypes.Roots) && c.Root != null && _rootIds.Add(c.Root.GetInstanceID())) _roots.Add(c.Root);
                return;
            }
            var p = comp as ParticleSystem;
            if (p != null) { for (int k = 0; k < _clients.Count; k++) if ((_clients[k].Types & ScanTypes.Particles) != 0) Safe(_clients[k], p, ref c); return; }
            var a = comp as Animator;
            if (a != null) { for (int k = 0; k < _clients.Count; k++) if ((_clients[k].Types & ScanTypes.Animators) != 0) Safe(_clients[k], a, ref c); }
            var s = comp as SkinnedMeshRenderer;
            if (s != null)
                for (int k = 0; k < _clients.Count; k++)
                {
                    var client = _clients[k] as ISkinnedScanClient;
                    if (client != null && (_clients[k].Types & ScanTypes.Skinned) != 0)
                        try { client.OnSkinned(s, ref c); } catch (Exception e) { Log.Error("[Performance] scan skin", e); }
                }
        }

        static void Safe(IScanClient cl, Light x, ref ScanCtx c) { try { cl.OnLight(x, ref c); } catch (Exception e) { Log.Error("[Performance] scan light", e); } }
        static void Safe(IScanClient cl, MeshRenderer x, ref ScanCtx c) { try { cl.OnRenderer(x, ref c); } catch (Exception e) { Log.Error("[Performance] scan renderer", e); } }
        static void Safe(IScanClient cl, ParticleSystem x, ref ScanCtx c) { try { cl.OnParticles(x, ref c); } catch (Exception e) { Log.Error("[Performance] scan particles", e); } }
        static void Safe(IScanClient cl, Animator x, ref ScanCtx c) { try { cl.OnAnimator(x, ref c); } catch (Exception e) { Log.Error("[Performance] scan animator", e); } }

        /// <summary>Classifies a transform. False for objects that must never be touched (editor/internal hide flags, WW's own runner).</summary>
        internal static bool Ctx(Transform t, out ScanCtx c)
        {
            c = default(ScanCtx);
            var go = t.gameObject;
            var scene = go.scene;
            c.Asset = !scene.IsValid();
            if (!c.Asset)
            {
                // scene objects only: WW's runner / editor objects (DontSave) and anything in DontDestroyOnLoad (buildIndex -1: other mods)
                if ((go.hideFlags & (HideFlags.DontSave | HideFlags.HideInHierarchy)) != 0) return false;
                if (scene.buildIndex < 0) return false;
            }
            var i = GetInfo(t);
            c.Saved = i.Saved; c.Vehicle = i.Vehicle; c.Player = i.Player; c.Root = i.Root;
            return true;
        }

        static Info GetInfo(Transform t)
        {
            int id = t.GetInstanceID();
            Info info;
            if (_info.TryGetValue(id, out info)) return info;
            var p = t.parent;
            bool top = p == null || p.GetComponent<Den.Tools.ObjectsPool>() != null;
            info = top ? new Info { Root = t } : GetInfo(p);
            if (t.GetComponent<ES3Internal.ES3Prefab>() != null) info.Saved = true;
            if (t.GetComponent<NWH.VehiclePhysics2.VehicleController>() != null) info.Vehicle = true;
            if (top && p == null && t.gameObject.scene.IsValid())
            {
                string n = t.name;
                if (n == "Player" || n == "PlayerCameraHolder") info.Player = true;
            }
            _info[id] = info;
            return info;
        }

        /// <summary>Stable key "prefabRootName|child/path" shared by a prefab asset component and its instances ("(Clone)" stripped).
        /// Allocates: scan paths only.</summary>
        internal static string Key(Transform t, Transform root)
        {
            if (t == null || root == null) return null;
            string rn = root.name;
            int k = rn.IndexOf("(Clone)", StringComparison.Ordinal);
            if (k > 0) rn = rn.Substring(0, k).TrimEnd();
            if (t == root) return rn + "|";
            var sb = new System.Text.StringBuilder();
            for (var x = t; x != null && x != root; x = x.parent) { if (sb.Length > 0) sb.Insert(0, '/'); sb.Insert(0, x.name); }
            return rn + "|" + sb;
        }

        static readonly Dictionary<int, int> _rootHash = new Dictionary<int, int>();
        /// <summary>Hash of the prefab root's name without "(Clone)" (same for an asset and its instances); cached per root.</summary>
        internal static int RootHash(Transform root)
        {
            if (root == null) return 0;
            int id = root.GetInstanceID(), h;
            if (_rootHash.TryGetValue(id, out h)) return h;
            string rn = root.name;
            int k = rn.IndexOf("(Clone)", StringComparison.Ordinal);
            if (k > 0) rn = rn.Substring(0, k).TrimEnd();
            h = rn.GetHashCode();
            if (_rootHash.Count > 20000) _rootHash.Clear();
            _rootHash[id] = h;
            return h;
        }

        /// <summary>World-space half extent (largest axis) of a MeshRenderer from its mesh bounds and lossy scale (valid for assets,
        /// whose Renderer.bounds is not computed outside a scene).</summary>
        internal static float HalfExtent(MeshRenderer r)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return -1f;
            var e = mf.sharedMesh.bounds.extents;
            var s = r.transform.lossyScale;
            return Mathf.Max(Mathf.Abs(e.x * s.x), Mathf.Max(Mathf.Abs(e.y * s.y), Mathf.Abs(e.z * s.z)));
        }

        internal static void Stats(WildWasteland.Core.UI.ToolLayout l)
        {
            l.Value("Prefab pass", (Busy ? "running" : "idle") + ", passes " + Passes + ", subtrees " + Subtrees + ", visited " + Visited);
        }
    }

    /// <summary>Reverts edits on saved objects while the game writes a save. The save starts with the global SaveGame event:
    /// SaveLoadGame goes to "wait" (1 s real time) and Save_NewGO_ArrayList to "arrayResize" (1 s real time) before the GameObject
    /// arrays newGOArray_Cars/_Items/_Sandstorms are written (Sources fsm/scene1_game/SaveLoadGame.md:L88-90, NewGO_ArrayList.md:L38-L68).
    /// Core.SaveBus raises StateChanged on the first frame of "wait", i.e. ~1 s before ES3 serializes the objects.</summary>
    internal static class SaveGuard
    {
        internal interface IClient { void RevertSaved(); void ReapplySaved(); }
        static readonly List<IClient> _clients = new List<IClient>();
        internal static bool Reverted { get; private set; }
        public static long Saves;

        internal static void Register(IClient c)
        {
            if (_clients.Contains(c)) return;
            _clients.Add(c);
            if (_clients.Count == 1) { SaveBus.StateChanged -= Changed; SaveBus.StateChanged += Changed; }
        }

        internal static void Unregister(IClient c)
        {
            _clients.Remove(c);
            if (_clients.Count == 0) { SaveBus.StateChanged -= Changed; Reverted = false; }
        }

        static void Changed(string prev, string now)
        {
            if (now == "wait" || now == "SaveGame")
            {
                if (Reverted) return;
                Reverted = true; Saves++;
                for (int i = 0; i < _clients.Count; i++) { try { _clients[i].RevertSaved(); } catch (Exception e) { Log.Error("[Performance] revert before save", e); } }
            }
            else if (now == "isPlay" && Reverted)
            {
                Reverted = false;
                for (int i = 0; i < _clients.Count; i++) { try { _clients[i].ReapplySaved(); } catch (Exception e) { Log.Error("[Performance] re-apply after save", e); } }
            }
        }
    }

    /// <summary>QualitySettings values shared by several features. Each slot has a wanted value and a direction (cap = never raise
    /// the value above what the player's quality level has; floor = never lower it). A 1 s job and a postfix on the graphics menu
    /// re-apply after the game or another mod resets them; release restores the level's own value if nobody changed it since.</summary>
    internal static class QualityGuard
    {
        internal enum Q { PixelLights, ShadowDistance, ShadowCascades, ShadowResolution, SkinWeights, LodBias, TextureLimit, Aniso, Count }
        struct Slot { public bool Active, HasOrig, Floor; public float Want, Orig, Applied; }
        static readonly Slot[] _s = new Slot[(int)Q.Count];
        static int _level = -1, _users;
        static Job _job;
        public static long Reapplies;

        static float Get(Q q)
        {
            switch (q)
            {
                case Q.PixelLights: return QualitySettings.pixelLightCount;
                case Q.ShadowDistance: return QualitySettings.shadowDistance;
                case Q.ShadowCascades: return QualitySettings.shadowCascades;
                case Q.ShadowResolution: return (int)QualitySettings.shadowResolution;
                case Q.SkinWeights: return (int)QualitySettings.skinWeights;
                case Q.LodBias: return QualitySettings.lodBias;
                case Q.TextureLimit: return QualitySettings.masterTextureLimit;
                case Q.Aniso: return (int)QualitySettings.anisotropicFiltering;
            }
            return 0;
        }

        static void SetRaw(Q q, float v)
        {
            switch (q)
            {
                case Q.PixelLights: QualitySettings.pixelLightCount = Mathf.RoundToInt(v); break;
                case Q.ShadowDistance: QualitySettings.shadowDistance = v; break;
                case Q.ShadowCascades: QualitySettings.shadowCascades = Mathf.RoundToInt(v); break;
                case Q.ShadowResolution: QualitySettings.shadowResolution = (ShadowResolution)Mathf.RoundToInt(v); break;
                case Q.SkinWeights: QualitySettings.skinWeights = (SkinWeights)Mathf.RoundToInt(v); break;
                case Q.LodBias: QualitySettings.lodBias = v; break;
                case Q.TextureLimit: QualitySettings.masterTextureLimit = Mathf.RoundToInt(v); break;
                case Q.Aniso: QualitySettings.anisotropicFiltering = (AnisotropicFiltering)Mathf.RoundToInt(v); break;
            }
        }

        static void Acquire()
        {
            if (_users++ > 0) return;
            var m = PerformanceModule.Instance;
            m.EnsurePatched(typeof(MenuPatch));
            _job = Scheduler.Interval(m, "Performance.QualityGuard", 1f, Tick);
            _level = QualitySettings.GetQualityLevel();
        }

        static void ReleaseUser()
        {
            if (_users > 0 && --_users == 0 && _job != null) { _job.Remove(); _job = null; }
        }

        /// <summary>Request <paramref name="want"/> for <paramref name="q"/>: cap (default) = min(level value, want); floor = max.</summary>
        internal static void Set(Q q, float want, bool floor = false)
        {
            int i = (int)q;
            if (!_s[i].Active) { Acquire(); _s[i].Active = true; _s[i].Orig = Get(q); _s[i].HasOrig = true; }
            _s[i].Want = want; _s[i].Floor = floor;
            Apply(i);
        }

        internal static void Release(Q q)
        {
            int i = (int)q;
            if (!_s[i].Active) return;
            _s[i].Active = false;
            // restore only if nobody changed the value after us (a menu change already wrote the level's value)
            if (_s[i].HasOrig && Mathf.Approximately(Get(q), _s[i].Applied)) SetRaw(q, _s[i].Orig);
            ReleaseUser();
        }

        static void Apply(int i)
        {
            var q = (Q)i;
            float v = _s[i].Floor ? Mathf.Max(_s[i].Orig, _s[i].Want) : Mathf.Min(_s[i].Orig, _s[i].Want);
            _s[i].Applied = Get(q);
            if (!Mathf.Approximately(_s[i].Applied, v)) { SetRaw(q, v); _s[i].Applied = Get(q); }
        }

        /// <summary>Detects a reset (quality level changed, or a value differs from what we wrote) and re-applies.</summary>
        internal static void Tick()
        {
            int level = QualitySettings.GetQualityLevel();
            bool levelChanged = level != _level;
            _level = level;
            for (int i = 0; i < _s.Length; i++)
            {
                if (!_s[i].Active) continue;
                float cur = Get((Q)i);
                if (!levelChanged && Mathf.Approximately(cur, _s[i].Applied)) continue;
                _s[i].Orig = cur; Reapplies++;
                Apply(i);
            }
        }

        internal static string Describe(Q q)
        {
            int i = (int)q;
            if (!_s[i].Active) return "-";
            return Get(q).ToString("0.##") + " (level " + _s[i].Orig.ToString("0.##") + ")";
        }

        [HarmonyPatch]
        static class MenuPatch
        {
            [HarmonyPatch(typeof(AkshayDhotre.GraphicSettingsMenu.GraphicSettingHelperMethods), "ChangeQualitySettings"), HarmonyPostfix]
            static void Post() { if (_users > 0) { try { Tick(); } catch (Exception e) { Log.Error("[Performance] quality re-apply", e); } } }
        }
    }

    /// <summary>The game's view cameras: PlayerCameraHolder/PlayerCamera and every vehicle's DriveTrigger/3rdCamera/Camera
    /// (inactive until used; found when enabled). Refreshed at most once per frame from Camera.GetAllCameras (non-allocating).</summary>
    internal static class GameCameras
    {
        internal static readonly List<Camera> List = new List<Camera>();
        static readonly HashSet<int> _seen = new HashSet<int>();
        static Camera[] _buf = new Camera[16];
        static int _frame = -1;
        internal static int Version;

        internal static void Refresh()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            for (int i = List.Count - 1; i >= 0; i--) if (List[i] == null) { List.RemoveAt(i); Version++; }
            int n = Camera.allCamerasCount;
            if (_buf.Length < n) _buf = new Camera[n + 8];
            n = Camera.GetAllCameras(_buf);
            for (int i = 0; i < n; i++)
            {
                var c = _buf[i]; _buf[i] = null;
                if (c == null || !_seen.Add(c.GetInstanceID())) continue;
                if (IsGameCamera(c)) { List.Add(c); Version++; }
            }
        }

        internal static bool IsGameCamera(Camera c)
        {
            string n = c.name;
            if (n == "PlayerCamera") { var p = c.transform.parent; return p != null && p.parent == null && p.name == "PlayerCameraHolder"; }
            if (n == "Camera") { var p = c.transform.parent; return p != null && p.name == "3rdCamera"; }
            return false;
        }

        /// <summary>True for a vehicle camera (saved with its car: ES3 writes Camera.farClipPlane / layerCullSpherical).</summary>
        internal static bool IsVehicleCamera(Camera c) { return c != null && c.name == "Camera"; }

        internal static void Clear() { List.Clear(); _seen.Clear(); Version++; }
    }
}
