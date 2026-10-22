using System;
using UnityEngine;
using UnityEngine.Rendering;
using WildWasteland.Core;

namespace WildWasteland.Modules.Weather
{
    // One camera command buffer; no component is added to a saved player/vehicle object.
    internal sealed class WeatherAtmosphere : IDisposable
    {
        private readonly Material _material;
        private readonly Mesh _screen;
        private readonly Vector4[] _centers = new Vector4[4], _sizes = new Vector4[4], _axes = new Vector4[4], _velocity = new Vector4[4];
        private readonly Vector4[] _backLightA = new Vector4[4], _backLightB = new Vector4[4];
        private Camera _camera;
        private CommandBuffer _commands;
        private CommandBuffer _darkCommands;
        private bool _darkAttached;
        private DepthTextureMode _depth;
        private int _count, _insideState;
        private float _inside;
        private readonly WeatherSky _sky = new WeatherSky();
        private Vector3 _wind;
        private float _dustStrength;
        private Vector4 _stormEye;
        private float _eyeHeight, _eyeFade, _eyeDistance;
        internal float InsideStrength { get { return _inside; } }
        internal float DustStrength { get { return _dustStrength*(1-.85f*_eyeFade); } }
        internal Vector3 LocalWind { get { return _wind; } }
        internal void SetFlash(float strength) { _sky.SetFlash(strength); }
        private static readonly int Centers = Shader.PropertyToID("_WWCenters"), Sizes = Shader.PropertyToID("_WWSizes"), Axes = Shader.PropertyToID("_WWAxes"), Velocity = Shader.PropertyToID("_WWVelocity");
        private static readonly int Count = Shader.PropertyToID("_WWCount"), Samples = Shader.PropertyToID("_WWSamples"), Projection = Shader.PropertyToID("_WWInverseProjection"), View = Shader.PropertyToID("_WWViewToWorld"), Eye = Shader.PropertyToID("_WWEye");
        private static readonly int Inside = Shader.PropertyToID("_WWWeatherInside"), Wind = Shader.PropertyToID("_WWWeatherWind");
        private static readonly int BackLightA = Shader.PropertyToID("_WWBackLightA"), BackLightB = Shader.PropertyToID("_WWBackLightB");

        internal WeatherAtmosphere(Material source)
        {
            _material = new Material(source);
            _screen = new Mesh { name = "WW.Weather.AtmosphereQuad" };
            _screen.vertices = new[] { new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0) };
            _screen.uv = new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up };
            _screen.triangles = new[] { 0,1,2,0,2,3 };
        }
        internal bool Supported { get { return _material.shader.isSupported; } }
        internal void Begin(Camera camera)
        {
            _count = 0;
            _stormEye = Vector4.zero; _eyeFade = 0; _eyeDistance = float.MaxValue;
            if (_camera != camera)
            {
                Suspend(); _camera = camera; _depth = camera.depthTextureMode;
                camera.depthTextureMode |= DepthTextureMode.Depth;
                _commands = new CommandBuffer { name = "Wild Wasteland local dust atmosphere" };
                _commands.DrawMesh(_screen,Matrix4x4.identity,_material,0,0);
                _darkCommands = new CommandBuffer { name = "Wild Wasteland storm darkening" };
                _darkCommands.DrawMesh(_screen,Matrix4x4.identity,_material,0,1);
                camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, _commands);
                Camera.onPreCull += BeforeCamera;
                _sky.Begin(camera,0);
                Log.Always("[Weather] Depth-only atmosphere attached to " + camera.name + "; alpha blend before transparents, no camera-color read/copy; native weather retained.");
            }
        }
        internal void Add(WeatherRegion region, Vector3 center, float height, float life, float critical, Vector4 backA, Vector4 backB)
        {
            if (_count == 4) return;
            float angle = region.Heading * Mathf.Deg2Rad, c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            _centers[_count] = new Vector4(center.x, center.y, center.z, life);
            _sizes[_count] = new Vector4(region.Radii.x, height, region.Radii.y, 0);
            _axes[_count] = new Vector4(c, -s, s, c);
            _backLightA[_count] = backA; _backLightB[_count] = backB;
            _velocity[_count++] = new Vector4(region.Velocity.x, region.Velocity.y, region.Velocity.z, Mathf.Lerp(.004f, .005f, critical));
        }
        internal void End(Camera camera, WeatherVisualDetail detail, Mesh nearMesh, Material nearMaterial)
        {
            if (_count == 0) { Suspend(); return; }
            Shader.SetGlobalInt(Count, _count); Shader.SetGlobalInt(Samples, detail == WeatherVisualDetail.Low ? 4 : detail == WeatherVisualDetail.Max ? 8 : 6);
            Shader.SetGlobalVectorArray(Centers, _centers); Shader.SetGlobalVectorArray(Sizes, _sizes);
            Shader.SetGlobalVectorArray(Axes, _axes); Shader.SetGlobalVectorArray(Velocity, _velocity);
            Shader.SetGlobalVectorArray(BackLightA, _backLightA); Shader.SetGlobalVectorArray(BackLightB, _backLightB);
            Shader.SetGlobalVector("_WWStormEye",_stormEye);
            Shader.SetGlobalVector("_WWStormEyeShape",new Vector4(_eyeHeight,_eyeFade,0,0));
        }
        internal void AddEye(Vector3 center, float radius, float height, float life, Vector3 eye)
        {
            float distance = new Vector2(center.x-eye.x,center.z-eye.z).magnitude;
            if (distance >= radius || distance >= _eyeDistance || eye.y < center.y-10 || eye.y > center.y+height) return;
            _eyeDistance = distance; _stormEye = new Vector4(center.x,center.y,center.z,radius); _eyeHeight = height;
            _eyeFade = life*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((distance/radius-.65f)/.35f)));
        }
        private void BeforeCamera(Camera camera)
        {
            if (camera != _camera) return;
            if (WeatherDiagnostics.PendingCamera) WeatherDiagnostics.CameraRecord(camera,Shader.GetGlobalFloat("_WWWeatherClock"));
            Matrix4x4 view = camera.worldToCameraMatrix.inverse;
            Vector3 eye = view.MultiplyPoint3x4(Vector3.zero);
            Shader.SetGlobalMatrix(Projection, camera.projectionMatrix.inverse); Shader.SetGlobalMatrix(View, view); Shader.SetGlobalVector(Eye, eye);
            float target = 0, approach = 0, dust = 0; Vector4 wind = _velocity[0];
            for (int i = 0; i < _count; i++)
            {
                Vector3 delta = eye - new Vector3(_centers[i].x, _centers[i].y, _centers[i].z);
                float x = (delta.x * _axes[i].x + delta.z * _axes[i].y) / _sizes[i].x;
                float z = (delta.x * _axes[i].z + delta.z * _axes[i].w) / _sizes[i].z;
                float edge = WeatherRegion.EdgeDistance(x,z);
                float outside = Mathf.Max(0,-edge)*Mathf.Min(_sizes[i].x,_sizes[i].z);
                approach = Mathf.Max(approach,(1-Mathf.SmoothStep(0,1,outside/4500))*_centers[i].w);
                dust = Mathf.Max(dust,(1-Mathf.SmoothStep(0,1,outside/450))*_centers[i].w);
                float rear = Mathf.Clamp01((z+1)*.5f);
                float ceiling = _sizes[i].y * Mathf.Lerp(1,.25f,rear);
                float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(edge/.18f));
                weight *= 1 - Mathf.SmoothStep(0, 1, (delta.y / ceiling - .48f) / .52f);
                if (delta.y < -80) weight = 0;
                weight *= _centers[i].w;
                if (weight <= target) continue;
                target = weight; wind = _velocity[i];
            }
            _inside = Mathf.Lerp(_inside, target, 1 - Mathf.Exp(-Time.deltaTime * 1.5f));
            Shader.SetGlobalFloat(Inside, _inside); Shader.SetGlobalVector(Wind, wind);
            float darkness = WeatherDiagnostics.Atmosphere ? WeatherPalette.Darkening*(_inside > .995f ? 1 : _inside) : 0;
            Shader.SetGlobalFloat("_WWStormDarkening",darkness);
            bool dark = darkness > .0001f;
            if (dark != _darkAttached)
            {
                if (dark) camera.AddCommandBuffer(CameraEvent.AfterImageEffects,_darkCommands);
                else camera.RemoveCommandBuffer(CameraEvent.AfterImageEffects,_darkCommands);
                _darkAttached = dark;
            }
            _wind = wind; _sky.SetAmount(WeatherDiagnostics.SkyScope ? Mathf.Max(_inside,approach*.85f) : 0);
            _dustStrength = Mathf.Lerp(_dustStrength,Mathf.Max(_inside,dust*.5f),1-Mathf.Exp(-Time.deltaTime*1.5f));
            Shader.SetGlobalFloat("_WWWeatherApproach",approach);
            int state = _inside < .02f ? 0 : _inside > .9f ? 2 : 1;
            if (state == _insideState) return;
            _insideState = state;
            if (Log.Verbose) Log.Info("[Weather] Local atmosphere: " + (state == 0 ? "outside" : state == 2 ? "interior" : "boundary transition"));
        }
        internal void Suspend()
        {
            if (_commands == null && ReferenceEquals(_camera, null)) return;
            Camera.onPreCull -= BeforeCamera;
            _sky.Suspend();
            if (_camera != null)
            {
                if (_commands != null) _camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, _commands);
                if (_darkAttached && _darkCommands != null) _camera.RemoveCommandBuffer(CameraEvent.AfterImageEffects,_darkCommands);
                if (_camera.depthTextureMode == (_depth | DepthTextureMode.Depth)) _camera.depthTextureMode = _depth;
            }
            _camera = null;
            if (_commands != null) _commands.Release(); _commands = null;
            if (_darkCommands != null) _darkCommands.Release(); _darkCommands = null; _darkAttached = false;
            Shader.SetGlobalFloat("_WWStormDarkening",0);
            _inside = 0; _dustStrength = 0; _insideState = 0; Shader.SetGlobalFloat(Inside, 0); Shader.SetGlobalFloat("_WWWeatherApproach",0);
            _eyeFade = 0; Shader.SetGlobalVector("_WWStormEyeShape",Vector4.zero);
        }
        public void Dispose() { Suspend(); UnityEngine.Object.Destroy(_material); UnityEngine.Object.Destroy(_screen); }
    }
}
