using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    // Change only the HUD component; weapon/ADS FSMs retain control of active state.
    // WW: was a per-frame Tick writing both localScales every frame. Now an Interval job (0.25 s, RunNow on setting changes)
    // that writes localScale / sprite only when the wanted value differs from the current one, keeps the found UI objects
    // cached (Unity-null re-check), looks for them at most once per [HUD / Crosshair] SearchSeconds while missing (reused
    // root list, no allocation) and forgets them on scene load. Shutdown restores every original and destroys the loaded assets.
    internal static class CrosshairController
    {
        private static Transform _dot, _cross;
        private static Vector3 _dotScale, _crossScale;
        private static Image _image;
        private static Color _originalColor;
        private static Transform _weapons;
        private static PlayMakerFSM[] _weaponFsms;
        private static float _nextWeapons;

        private static bool NeedsCocking()
        {
            if (_weapons == null)
            {
                if (Time.unscaledTime < _nextWeapons) return false;
                _nextWeapons = Time.unscaledTime + 2f;
                var holder = GameRefs.PlayerCameraHolder;
                _weapons = holder != null ? holder.transform.Find("PlayerCamera/WeaponsArm/Parent") : null;
                _weaponFsms = _weapons != null ? _weapons.GetComponentsInChildren<PlayMakerFSM>(true) : null;
            }
            if (_weapons == null || _weaponFsms == null) return false;
            for (int i = 0; i < _weaponFsms.Length; i++)
            {
                var fsm = _weaponFsms[i];
                if (fsm == null || fsm.FsmName != "Attack" || !fsm.gameObject.activeInHierarchy) continue;
                if (fsm.Fsm.GetState("pump") == null && fsm.Fsm.GetState("bolt action") == null && fsm.Fsm.GetState("hammer") == null) continue;
                // Native firing tests hammer before pumping / working the bolt; false means the next click cocks it.
                var hammer = fsm.FsmVariables.FindFsmBool("hammer");
                if (hammer != null && !hammer.Value) return true;
            }
            return false;
        }
        private static RawImage _raw;
        private static Sprite _originalSprite, _originalOverride, _linesSprite, _dotSprite;
        private static Texture _originalTexture;
        private static Texture2D _linesTexture, _dotTexture;
        private static Rect _originalUv;
        private static bool _originalAspect, _assetsAttempted, _unsupportedWarned;
        private static float _nextLook;
        private static int _appliedMode = -1;
        private static readonly List<GameObject> _roots = new List<GameObject>(64);
        private static Sprite _previewSprite;
        private static Texture _previewTexture;
        private static Texture2D _previewFallbackTexture;
        private static Sprite _previewFallbackSprite;
        private static Rect _previewUv = new Rect(0, 0, 1, 1);

        internal static void PreparePreview()
        {
            LoadAssets();
            _previewSprite = _originalOverride != null ? _originalOverride : _originalSprite;
            _previewTexture = _originalTexture;
            _previewUv = _originalTexture != null ? _originalUv : new Rect(0, 0, 1, 1);
            if (_previewSprite == null && _previewTexture == null)
            {
                foreach (var image in Resources.FindObjectsOfTypeAll<Image>())
                    if (image != null && image.gameObject.scene.IsValid() && image.name == "MouseCrosshair")
                    { _previewSprite = image.overrideSprite != null ? image.overrideSprite : image.sprite; break; }
            }
            if (_previewSprite == null && _previewTexture == null)
                foreach (var image in Resources.FindObjectsOfTypeAll<RawImage>())
                    if (image != null && image.gameObject.scene.IsValid() && image.name == "MouseCrosshair") { _previewTexture = image.texture; _previewUv = image.uvRect; break; }
            if (_previewSprite == null && _previewTexture == null)
            {
                if (_previewFallbackTexture == null) Load("crosshair_vanilla.png", out _previewFallbackTexture, out _previewFallbackSprite);
                _previewTexture = _previewFallbackTexture;
            }
            LoadLines();
        }

        internal static void DrawPreview(Rect rect, int mode)
        {
            if (mode == 0) return;
            Sprite sprite = mode == 1 ? _previewSprite : mode == 2 ? _linesSprite : _dotSprite;
            Texture texture = mode == 1 ? _previewTexture : mode == 2 ? _linesTexture : _dotTexture;
            if (mode == 3)
            {
                float size = rect.width * HudModule.DotScale.Value;
                rect = new Rect(rect.center.x - size / 2, rect.center.y - size / 2, size, size);
            }
            if (sprite != null)
            {
                var uv = sprite.textureRect;
                GUI.DrawTextureWithTexCoords(rect, sprite.texture, new Rect(uv.x / sprite.texture.width, uv.y / sprite.texture.height, uv.width / sprite.texture.width, uv.height / sprite.texture.height));
            }
            else if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
        }

        internal static string Status
        {
            get
            {
                string ui = _cross != null ? "MouseCrosshair" + (_image != null ? " (Image)" : _raw != null ? " (RawImage)" : " (no graphic)") : "MouseCrosshair not found";
                ui += _dot != null ? ", MousePoint" : ", MousePoint not found";
                if (_assetsAttempted) ui += ", textures " + (_linesSprite != null || _linesTexture != null ? "lines " : "") + (_dotSprite != null || _dotTexture != null ? "dot" : "");
                return ui;
            }
        }

        internal static void Tick()
        {
            if ((_dot == null || _cross == null) && Time.unscaledTime >= _nextLook)
            {
                _nextLook = Time.unscaledTime + Mathf.Max(1f, HudModule.SearchSeconds.Value);
                bool found = false;
                for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount && !found; sceneIndex++)
                {
                    var scene = SceneManager.GetSceneAt(sceneIndex);
                    if (!scene.isLoaded) continue;
                    scene.GetRootGameObjects(_roots);
                    for (int r = 0; r < _roots.Count; r++)
                    {
                        var root = _roots[r];
                        if (root == null || root.name != "Canvas") continue;
                        var dot = root.transform.Find("MousePoint");
                        var cross = root.transform.Find("MouseCrosshair");
                        if (dot == null && cross == null) continue;
                        Attach(dot, cross);
                        found = true;
                        break;
                    }
                    _roots.Clear();
                }
                if (_dot == null && _cross == null)
                {
                    var dot = GameObject.Find("MousePoint");
                    if (dot != null && dot.transform.parent != null) Attach(dot.transform, dot.transform.parent.Find("MouseCrosshair"));
                }
            }
            if (_dot != null)
            {
                Vector3 want = HudModule.SelectionDot.Value ? _dotScale : Vector3.zero;
                if (_dot.localScale != want) _dot.localScale = want;
            }
            if (_cross == null) return;
            int mode = HudModule.Crosshair.Value;
            Vector3 wantCross = mode == 0 ? Vector3.zero : mode == 3 ? _crossScale * HudModule.DotScale.Value : _crossScale;
            if (_cross.localScale != wantCross) _cross.localScale = wantCross;
            Color color = NeedsCocking() ? new Color(224f / 255f, 95f / 255f, 81f / 255f, _originalColor.a) : _originalColor;
            if (_image != null && _image.color != color) _image.color = color;
            if (_raw != null && _raw.color != color) _raw.color = color;
            if (_appliedMode == mode) return;
            if (mode >= 2) { LoadAssets(); LoadLines(); }
            if (_image != null)
            {
                _image.sprite = mode == 2 && _linesSprite != null ? _linesSprite : mode == 3 && _dotSprite != null ? _dotSprite : _originalSprite;
                _image.overrideSprite = mode >= 2 ? null : _originalOverride;
                _image.preserveAspect = mode >= 2 || _originalAspect;
            }
            else if (_raw != null)
            {
                _raw.texture = mode == 2 && _linesTexture != null ? _linesTexture : mode == 3 && _dotTexture != null ? _dotTexture : _originalTexture;
                _raw.uvRect = mode >= 2 ? new Rect(0, 0, 1, 1) : _originalUv;
            }
            else if (mode >= 2 && !_unsupportedWarned)
            { Log.Warn("[HUD] Weapon crosshair has no Image/RawImage component; keeping its vanilla graphic."); _unsupportedWarned = true; }
            _appliedMode = mode;
        }

        private static void Attach(Transform dot, Transform cross)
        {
            if (_dot != dot)
            {
                if (_dot != null) _dot.localScale = _dotScale;
                _dot = dot;
                if (dot != null) _dotScale = dot.localScale == Vector3.zero ? Vector3.one : dot.localScale;
            }
            if (_cross == cross) return;
            RestoreCross(); _cross = cross; _appliedMode = -1; _unsupportedWarned = false;
            _image = cross != null ? cross.GetComponent<Image>() : null;
            _raw = cross != null ? cross.GetComponent<RawImage>() : null;
            _originalColor = _image != null ? _image.color : _raw != null ? _raw.color : Color.white;
            if (cross != null) _crossScale = cross.localScale == Vector3.zero ? Vector3.one : cross.localScale;
            if (_image != null) { _originalSprite = _image.sprite; _originalOverride = _image.overrideSprite; _originalAspect = _image.preserveAspect; }
            if (_raw != null) { _originalTexture = _raw.texture; _originalUv = _raw.uvRect; }
            if (Log.Verbose) Log.Info("[HUD] crosshair UI attached: " + (cross != null ? cross.name : "-") + " / " + (dot != null ? dot.name : "-"));
        }
        private static void RestoreCross()
        {
            if (_image != null) _image.color = _originalColor;
            if (_raw != null) _raw.color = _originalColor;
            if (_cross != null) _cross.localScale = _crossScale;
            if (_image != null) { _image.sprite = _originalSprite; _image.overrideSprite = _originalOverride; _image.preserveAspect = _originalAspect; }
            if (_raw != null) { _raw.texture = _originalTexture; _raw.uvRect = _originalUv; }
        }
        /// <summary>Restore the vanilla dot/crosshair and forget the cached UI objects (they belong to the old scene).</summary>
        internal static void OnSceneLoaded()
        {
            _weapons = null; _weaponFsms = null; _nextWeapons = 0f;
            if (_dot != null) _dot.localScale = _dotScale;
            RestoreCross(); _dot = null; _cross = null; _image = null; _raw = null; _nextLook = 0f; _appliedMode = -1;
            _originalSprite = null; _originalOverride = null; _originalTexture = null;
            _previewSprite = null; _previewTexture = null;
        }
        /// <summary>Module off: vanilla UI back, loaded textures/sprites destroyed, next Init loads them again.</summary>
        internal static void Shutdown()
        {
            OnSceneLoaded();
            DestroyAsset(ref _linesSprite, ref _linesTexture);
            DestroyAsset(ref _dotSprite, ref _dotTexture);
            DestroyAsset(ref _previewFallbackSprite, ref _previewFallbackTexture);
            _assetsAttempted = false; _unsupportedWarned = false;
        }
        private static void DestroyAsset(ref Sprite sprite, ref Texture2D texture)
        {
            if (sprite != null) UnityEngine.Object.Destroy(sprite);
            if (texture != null) UnityEngine.Object.Destroy(texture);
            sprite = null; texture = null;
        }
        private static void LoadAssets()
        {
            if (_assetsAttempted) return;
            _assetsAttempted = true;
            Load("crosshair_dot.png", out _dotTexture, out _dotSprite);
        }
        private static void LoadLines()
        {
            if (_linesTexture != null) return;
            var sprite = _originalOverride != null ? _originalOverride : _originalSprite != null ? _originalSprite : _previewSprite;
            var source = sprite != null ? sprite.texture : _originalTexture != null ? _originalTexture : _previewTexture;
            if (source == null) return;
            var uv = _originalTexture != null ? _originalUv : _previewUv;
            var crop = sprite != null ? sprite.textureRect : new Rect(uv.x * source.width, uv.y * source.height, uv.width * source.width, uv.height * source.height);
            var previous = RenderTexture.active;
            var buffer = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            try
            {
                Graphics.Blit(source, buffer); RenderTexture.active = buffer;
                int width = Mathf.RoundToInt(crop.width), height = Mathf.RoundToInt(crop.height);
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Wild Wasteland vanilla horizontal crosshair", hideFlags = HideFlags.HideAndDontSave, filterMode = source.filterMode, wrapMode = TextureWrapMode.Clamp };
                texture.ReadPixels(crop, 0, 0, false);
                var pixels = texture.GetPixels32();
                // The vanilla horizontal bars occupy the outer quarters; clear the central vertical bars.
                for (int y = 0; y < height; y++)
                    for (int x = width / 4; x < width * 3 / 4; x++) pixels[y * width + x] = new Color32(0, 0, 0, 0);
                texture.SetPixels32(pixels); texture.Apply(false, true);
                _linesSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
                _linesSprite.hideFlags = HideFlags.HideAndDontSave; _linesTexture = texture;
            }
            catch (Exception e)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                Log.Warn("[HUD] Vanilla horizontal crosshair: " + e.Message);
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(buffer); }
        }
        private static void Load(string name, out Texture2D texture, out Sprite sprite)
        {
            texture = null; sprite = null;
            try
            {
                var bytes = HudModule.ReadAsset("Textures", name);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = name, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
                if (!ImageConversion.LoadImage(texture, bytes)) throw new InvalidDataException("Unreadable PNG");
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                sprite.hideFlags = HideFlags.HideAndDontSave;
            }
            catch (Exception e)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                texture = null; Log.Warn("[HUD] Crosshair asset " + name + ": " + e.Message);
            }
        }
    }
}
