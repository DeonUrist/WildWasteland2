using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using WildWasteland.Core;

namespace WildWasteland.Modules.Survival
{
    /// <summary>Camera-only sky hallucinations; no saved objects, weather timeline or gameplay lighting changes.</summary>
    internal sealed class DeliriumEpisode
    {
        private static readonly int[] SkyColors = {
            Shader.PropertyToID("_Azure_RayleighColor"), Shader.PropertyToID("_Azure_MieColor"), Shader.PropertyToID("_Azure_ScatteringColor"),
            Shader.PropertyToID("_Azure_DynamicCloudColor1"), Shader.PropertyToID("_Azure_DynamicCloudColor2"), Shader.PropertyToID("_Azure_StaticCloudColor") };
        private static readonly int SunIntensity = Shader.PropertyToID("_Azure_SunTextureIntensity"), SunDirection = Shader.PropertyToID("_Azure_SunDirection");
        private readonly Insanity _state;
        private readonly IModule _owner;
        private Camera _camera;
        private CommandBuffer _before, _after;
        private Mesh _monster, _disc;
        private Material _monsterMaterial, _sunMaterial;
        private Texture2D _monsterTexture, _sunTexture;
        private Bounds _bounds;
        private bool _attempted;
        internal DeliriumEpisode(Insanity state, IModule owner) { _state = state; _owner = owner; }
        internal void Maintain()
        {
            MentalBreakVisuals visuals;
            if (!_state.TryGetVisuals(Entities.Player, out visuals) || visuals.SunCount == 0 || !GameRefs.InGame || !Application.isFocused)
            { End(); return; }
            var camera = GameRefs.MainCamera;
            if (camera == null) { End(); return; }
            if (!_attempted) Prepare();
            if (_monster == null || _before == null || _after == null) return;
            if (_camera == camera) return;
            End(); _camera = camera;
            camera.AddCommandBuffer(CameraEvent.BeforeSkybox, _before);
            camera.AddCommandBuffer(CameraEvent.AfterSkybox, _after);
            Camera.onPreRender += BeforeRender;
            Log.Always(_owner, "Delirium: shifting sky, two orbiting extra suns and 24 high black cross figures active.");
        }
        private void Prepare()
        {
            _attempted = true;
            try
            {
                var model = Core.Glb.Load(Path.Combine(Assets.ModuleDir("Survival"), "Models", "monster.glb"));
                _monster = model.Mesh; _bounds = model.Bounds;
                if (_monster == null || _bounds.size.y <= .001f) throw new InvalidDataException("Monster has no upright geometry");
                var vertices = _monster.vertices; float height = _bounds.size.y, hip = _bounds.min.y + height * .46f;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var vertex = vertices[i]; float side = vertex.x - _bounds.center.x;
                    if (vertex.y >= hip || Mathf.Abs(side) > height * .11f) continue;
                    float close = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hip, hip - height * .13f, vertex.y));
                    vertex.x -= Mathf.Sign(side) * height * .06f * close; vertices[i] = vertex;
                }
                _monster.vertices = vertices; _monster.RecalculateNormals(); _monster.RecalculateBounds(); _bounds = _monster.bounds;
                if (model.DoubleSided) for (int sub = 0; sub < _monster.subMeshCount; sub++)
                {
                    var front = _monster.GetTriangles(sub); var both = new int[front.Length * 2]; Array.Copy(front, both, front.Length);
                    for (int i = 0; i < front.Length; i += 3)
                    { both[front.Length + i] = front[i]; both[front.Length + i + 1] = front[i + 2]; both[front.Length + i + 2] = front[i + 1]; }
                    _monster.SetTriangles(both, sub);
                }
                var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
                if (shader == null || !shader.isSupported) throw new InvalidDataException("Monster material shader unavailable");
                _monsterMaterial = new Material(shader) { name = "Survival.Delirium.Monster", hideFlags = HideFlags.HideAndDontSave };
                _monsterTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Delirium black silhouette", hideFlags = HideFlags.HideAndDontSave };
                _monsterTexture.SetPixel(0, 0, Color.black); _monsterTexture.Apply(false, true); _monsterMaterial.mainTexture = _monsterTexture;
                if (_monsterMaterial.HasProperty("_EmissionColor"))
                { _monsterMaterial.SetColor("_EmissionColor", Color.white); _monsterMaterial.SetTexture("_EmissionMap", _monsterTexture); _monsterMaterial.EnableKeyword("_EMISSION"); }
                if (_monsterMaterial.HasProperty("_Glossiness")) _monsterMaterial.SetFloat("_Glossiness", 0);
                _monster.hideFlags = HideFlags.HideAndDontSave;
                _disc = new Mesh { name = "Delirium sun disc", hideFlags = HideFlags.HideAndDontSave };
                _disc.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
                _disc.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                _disc.colors = new[] { Color.white, Color.white, Color.white, Color.white }; _disc.triangles = new[] { 0,1,2,0,2,3 };
                _disc.RecalculateBounds();
                _sunTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Delirium sun", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color32[64 * 64];
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    float radius = new Vector2((x + .5f) / 32 - 1, (y + .5f) / 32 - 1).magnitude;
                    float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.46f, .55f, radius));
                    alpha = Mathf.Max(alpha, .3f * Mathf.Pow(Mathf.Clamp01(1 - radius), 2));
                    pixels[y * 64 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
                }
                _sunTexture.SetPixels32(pixels); _sunTexture.Apply(false, true);
                var sunShader = Shader.Find("UI/Default");
                if (sunShader == null || !sunShader.isSupported) throw new InvalidDataException("Sun disc shader unavailable");
                _sunMaterial = new Material(sunShader) { name = "Survival.Delirium.Suns", mainTexture = _sunTexture, hideFlags = HideFlags.HideAndDontSave };
                _sunMaterial.SetVector("_TextureSampleAdd", Vector4.zero);
                _before = new CommandBuffer { name = "Wild Wasteland Delirium sky" };
                _after = new CommandBuffer { name = "Wild Wasteland Delirium sky restore and figures" };
                Log.Always(_owner, "Delirium: monster.glb loaded as a black silhouette with legs together; one shared mesh.");
            }
            catch (Exception error) { Release(); Log.Error(_owner, "Delirium sky assets could not be prepared", error); }
        }
        private void BeforeRender(Camera camera)
        {
            if (camera != _camera) return;
            _before.Clear(); _after.Clear();
            MentalBreakVisuals visuals;
            if (!_state.TryGetVisuals(Entities.Player, out visuals) || visuals.SunCount == 0 || !GameRefs.InGame || !Application.isFocused) return;
            float elapsed = visuals.Elapsed, amount = visuals.Fade;
            if (amount < .001f) return;
            Color color = visuals.SkyColor;
            for (int i = 0; i < SkyColors.Length; i++)
            {
                Vector4 source = Shader.GetGlobalVector(SkyColors[i]);
                float peak = Mathf.Max(.2f, Mathf.Max(source.x, Mathf.Max(source.y, source.z)));
                var tint = new Vector4(color.r * peak, color.g * peak, color.b * peak, source.w);
                _before.SetGlobalVector(SkyColors[i], Vector4.Lerp(source, tint, amount * .95f));
                _after.SetGlobalVector(SkyColors[i], source);
            }
            _before.SetGlobalFloat(SunIntensity, 0);
            _after.SetGlobalFloat(SunIntensity, Shader.GetGlobalFloat(SunIntensity));
            Vector3 eye = camera.transform.position;
            float distance = Mathf.Min(600, camera.farClipPlane * .45f);
            const float elevation = 1.2f;
            float scale = distance * .1125f * (1 + elevation * elevation) / 1.36f * amount / _bounds.size.y;
            Vector3 sun = Shader.GetGlobalVector(SunDirection);
            if (sun.sqrMagnitude < .5f || sun.y < .15f) sun = new Vector3(.35f, .55f, .75f);
            sun.y = Mathf.Clamp(sun.y, .25f, .7f); sun.Normalize();
            _sunMaterial.color = new Color(1, .84f, .48f, amount);
            float sunDistance = camera.farClipPlane * .8f;
            Vector3 sunSide = Vector3.Cross(Mathf.Abs(sun.y) > .95f ? Vector3.forward : Vector3.up, sun).normalized;
            Vector3 sunUp = Vector3.Cross(sun, sunSide);
            for (int i = 0; i < visuals.SunCount; i++)
            {
                float orbit = elapsed * .25f + (i - 1) * Mathf.PI;
                Vector3 direction = i == 0 ? sun : (sun + sunSide * (.32f * Mathf.Sin(orbit)) + sunUp * (.22f * Mathf.Cos(orbit))).normalized;
                var matrix = Matrix4x4.TRS(eye + direction * sunDistance, Quaternion.LookRotation(-direction, Vector3.up), Vector3.one * (sunDistance * .12f));
                _after.DrawMesh(_disc, matrix, _sunMaterial, 0, 0);
            }
            for (int i = 0; i < visuals.SkyFigureCount; i++)
            {
                float angle = i * Mathf.PI / 12 + elapsed * .015f;
                var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                Vector3 at = eye + radial * distance + Vector3.up * (distance * (elevation + .025f * Mathf.Sin(elapsed * .35f + i)));
                var matrix = Matrix4x4.TRS(at, Quaternion.LookRotation(-radial, Vector3.up), Vector3.one * scale) * Matrix4x4.Translate(-_bounds.center);
                for (int sub = 0; sub < _monster.subMeshCount; sub++) _after.DrawMesh(_monster, matrix, _monsterMaterial, sub, 0);
            }
        }
        internal void End()
        {
            if (ReferenceEquals(_camera, null)) return;
            Camera.onPreRender -= BeforeRender;
            if (_camera != null)
            { _camera.RemoveCommandBuffer(CameraEvent.BeforeSkybox, _before); _camera.RemoveCommandBuffer(CameraEvent.AfterSkybox, _after); }
            _camera = null;
            if (_before != null) _before.Clear(); if (_after != null) _after.Clear();
        }
        private void Release()
        {
            End();
            if (_before != null) _before.Release(); if (_after != null) _after.Release(); _before = _after = null;
            if (_monster != null) UnityEngine.Object.Destroy(_monster); _monster = null;
            if (_disc != null) UnityEngine.Object.Destroy(_disc); _disc = null;
            if (_monsterMaterial != null) UnityEngine.Object.Destroy(_monsterMaterial); _monsterMaterial = null;
            if (_sunMaterial != null) UnityEngine.Object.Destroy(_sunMaterial); _sunMaterial = null;
            if (_monsterTexture != null) UnityEngine.Object.Destroy(_monsterTexture); _monsterTexture = null;
            if (_sunTexture != null) UnityEngine.Object.Destroy(_sunTexture); _sunTexture = null;
        }
        internal void Clear() { Release(); _attempted = false; }
    }
}
