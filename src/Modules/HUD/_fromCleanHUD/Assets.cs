using WildWasteland.Core.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace WildWasteland.Modules.CleanHUD
{
    internal static partial class Assets
    {
        /// <summary>Shared theme texture or HUD-owned artwork loaded through the common cache.</summary>
        internal static Texture2D Tex(string name)
        {
            if (name.StartsWith("panel_", StringComparison.Ordinal) || name.EndsWith("_atlas", StringComparison.Ordinal)) return ThemeAssets.Tex(name);
            return Core.Textures.LoadResource(CleanHudModule.Instance, "CleanHUD." + name + ".png", new Core.TextureSettings { Mipmaps = true, Readable = true, Wrap = TextureWrapMode.Clamp, Filter = FilterMode.Trilinear, Aniso = 4, Flags = HideFlags.DontUnloadUnusedAsset }, "HUD." + name);
        }
        /// <summary>Shared white fill texture.</summary>
        internal static Texture2D White { get { return ThemeAssets.White; } }
        /// <summary>Shared vertical fill gradient.</summary>
        internal static Texture2D VGradient(float bottom) { return ThemeAssets.VGradient(bottom); }
        /// <summary>Shared compass strip texture.</summary>
        internal static Texture2D CompassLine() { return ThemeAssets.CompassLine(); }
        /// <summary>Shared compass shadow texture.</summary>
        internal static Texture2D CompassShadow() { return ThemeAssets.CompassShadow(); }
        /// <summary>Shared caret texture.</summary>
        internal static Texture2D Caret() { return ThemeAssets.Caret(); }
        /// <summary>Shared horizontal fade curve.</summary>
        internal static float Fade(float t) { return ThemeAssets.Fade(t); }
        /// <summary>Preloads HUD-owned map/icon resources.</summary>
        internal static void Preload() { MapIcons.Preload(); IconRenderer.Preload(); }
        /// <summary>Destroys HUD-only rendered artwork and scene references.</summary>
        internal static void Dispose()
        {
            MapIcons.Dispose(); SceneLoaded();
            foreach (var texture in Rendered.Values) if (texture != null) UnityEngine.Object.Destroy(texture);
            Rendered.Clear(); RenderFailed.Clear(); IconRenderer.Clear();
        }
        private static readonly Dictionary<string, Texture2D> Rendered = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> RenderFailed = new HashSet<string>();
        private static readonly Queue<GameObject> IconQueue = new Queue<GameObject>();
        private static readonly HashSet<string> Queued = new HashSet<string>();
        private static IEnumerator _render;
        private static string _renderKey;
        internal static int IconRevision;

        private static readonly Regex CloneSuffix = new Regex(@"\(Clone\)\d*$|\s*\(\d+\)$", RegexOptions.Compiled);
        internal static string KeyOf(GameObject go)
        {
            if (go == null) return null;
            string packKey; WeaponsPackIcons.Model(go, out packKey);
            if (packKey != null) return packKey;
            var n = go.name;
            for (int i = 0; i < 3; i++) n = CloneSuffix.Replace(n, "");
            return n.Trim();
        }

        // Weapon icon: embedded flat render for the vanilla weapons, otherwise a silhouette rendered at runtime from the item's own meshes.
        internal static Texture2D WeaponIcon(GameObject item)
        {
            var key = KeyOf(item);
            if (string.IsNullOrEmpty(key)) return null;
            var t = Tex(key);
            if (t != null) return t;
            if (Rendered.TryGetValue(key, out t) && t != null) return t;
            if (RenderFailed.Contains(key)) return null;
            if (Queued.Add(key)) IconQueue.Enqueue(item);
            return null;
        }

        internal static bool RenderStep()
        {
            if (_render == null)
            {
                if (IconQueue.Count == 0) return false;
                var item = IconQueue.Dequeue();
                if (item == null) return IconQueue.Count > 0;
                BeginRender(item, KeyOf(item));
            }
            try { if (_render.MoveNext()) return true; }
            catch (Exception e) { RenderFailed.Add(_renderKey); Queued.Remove(_renderKey); CleanHudModule.Warn("Icon render failed: " + e.Message); }
            var disposable = _render as IDisposable; if (disposable != null) disposable.Dispose();
            _render = null; _renderKey = null;
            return IconQueue.Count > 0;
        }

        private static void BeginRender(GameObject item, string key)
        {
            _renderKey = key;
            _render = IconRenderer.Render(item, key, texture =>
            {
                if (texture != null) Rendered[key] = texture; else RenderFailed.Add(key);
                Queued.Remove(key); IconRevision++;
            });
        }

        internal static void SceneLoaded()
        {
            var disposable = _render as IDisposable; if (disposable != null) disposable.Dispose();
            _render = null; _renderKey = null; IconQueue.Clear(); Queued.Clear();
        }
    }

    // Orthographic side view (muzzle to the left), white unlit on transparent, cropped. Same framing as the offline renders.
    internal static class IconRenderer
    {
        private const int Layer = 31, W = 1024, H = 384;
        private static Dictionary<string, GameObject> _prefabs;
        internal static void Clear() { _prefabs = null; }
        internal static void Preload() { Prefab(""); }

        private static GameObject Prefab(string key)
        {
            if (_prefabs == null)
            {
                _prefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (go != null && !go.scene.IsValid() && go.transform.parent == null && !_prefabs.ContainsKey(go.name)) _prefabs[go.name] = go;
            }
            GameObject p; return _prefabs.TryGetValue(key, out p) ? p : null;
        }

        internal static IEnumerator Render(GameObject item, string key, Action<Texture2D> complete)
        {
            string packKey;
            var packModel = WeaponsPackIcons.Model(item, out packKey);
            var src = packModel ?? Prefab(key) ?? item;
            bool fromPrefab = packModel == null && src != item;
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) { complete(null); yield break; }
            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetColor("_Color", Color.white);
            mat.SetInt("_Cull", (int)CullMode.Off);
            mat.SetInt("_ZWrite", 1);
            var rig = new GameObject("CleanHUD.IconRig") { hideFlags = HideFlags.HideAndDontSave };
            rig.transform.position = new Vector3(0f, -4000f, 0f);
            RenderTexture rt = null; Camera cam = null; Texture2D raw = null, outT = null;
            try
            {
                var b = new Bounds(); bool any = false;
                foreach (var mf in src.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mf.sharedMesh == null || mr == null) continue;
                    // On the prefab, respect its default visibility; a held instance has its renderers switched off by the game.
                    if (fromPrefab && (!mr.enabled || !mf.gameObject.activeInHierarchy)) continue;
                    var m = src.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    var go = new GameObject("part") { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
                    go.transform.SetParent(rig.transform, false);
                    go.transform.localPosition = m.GetColumn(3);
                    go.transform.localRotation = m.rotation;
                    go.transform.localScale = m.lossyScale;
                    go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var r = go.AddComponent<MeshRenderer>();
                    var mats = new Material[Mathf.Max(1, mf.sharedMesh.subMeshCount)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                    var mb = mf.sharedMesh.bounds;
                    for (int cx = -1; cx <= 1; cx += 2) for (int cy = -1; cy <= 1; cy += 2) for (int cz = -1; cz <= 1; cz += 2)
                    {
                        var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3(cx, cy, cz)));
                        if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                    }
                    yield return null;
                }
                if (!any) { complete(null); yield break; }
                // Long axis decides the view: weapons are modelled along local Z (blast lances along Y).
                var e = b.size;
                Vector3 fwd, up;
                if (e.y > e.z && e.y > e.x) { fwd = Vector3.forward; up = Vector3.right; }
                else { fwd = Vector3.right; up = Vector3.up; }
                var camGo = new GameObject("CleanHUD.IconCam") { hideFlags = HideFlags.HideAndDontSave };
                cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.cullingMask = 1 << Layer;
                cam.allowHDR = false; cam.allowMSAA = true;
                float aspect = W / (float)H;
                float hgt = Mathf.Abs(Vector3.Dot(e, up)), len = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
                cam.orthographicSize = Mathf.Max(hgt * .5f, len * .5f / aspect) * 1.06f;
                float depth = Mathf.Max(e.x, Mathf.Max(e.y, e.z)) + 1f;
                cam.nearClipPlane = 0.01f; cam.farClipPlane = depth * 2f + 2f;
                var centre = rig.transform.TransformPoint(b.center);
                var wf = rig.transform.TransformDirection(fwd); var wu = rig.transform.TransformDirection(up);
                camGo.transform.position = centre - wf * depth;
                camGo.transform.rotation = Quaternion.LookRotation(wf, wu);
                rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                try
                {
                    RenderTexture.active = rt;
                    raw = new Texture2D(W, H, TextureFormat.RGBA32, false);
                    raw.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    raw.Apply(false);
                }
                finally { RenderTexture.active = prev; }
                var px = raw.GetPixels32();
                UnityEngine.Object.Destroy(raw); raw = null;
                int x0 = W, y0 = H, x1 = -1, y1 = -1;
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var a = px[y * W + x].r;
                        if (a > 8) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
                    }
                    yield return null;
                }
                if (x1 < 0) { complete(null); yield break; }
                const int pad = 12;
                int cw = x1 - x0 + 1 + pad * 2, ch = y1 - y0 + 1 + pad * 2;
                outT = new Texture2D(cw, ch, TextureFormat.RGBA32, true) { name = "CleanHUD.icon." + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
                var opx = new Color32[cw * ch];
                for (int y = 0; y < ch; y++)
                {
                    for (int x = 0; x < cw; x++)
                    {
                        int sx = x0 + x - pad, sy = y0 + y - pad;
                        byte a = (sx >= 0 && sy >= 0 && sx < W && sy < H) ? px[sy * W + sx].r : (byte)0;
                        opx[y * cw + x] = new Color32(255, 255, 255, a);
                    }
                    yield return null;
                }
                outT.SetPixels32(opx); outT.Apply(true, false);
                CleanHudModule.Log("Rendered a flat icon for " + key + " (" + cw + "x" + ch + ")");
                complete(outT); outT = null;
            }
            finally
            {
                if (raw != null) UnityEngine.Object.Destroy(raw);
                if (outT != null) UnityEngine.Object.Destroy(outT);
                if (cam != null) { cam.targetTexture = null; UnityEngine.Object.Destroy(cam.gameObject); }
                if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
                UnityEngine.Object.Destroy(rig);
                UnityEngine.Object.Destroy(mat);
            }
        }
    }
}
