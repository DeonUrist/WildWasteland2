using System;
using ES3Internal;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Survival
{
    // Borrow the installed game's Sand Player visual, retaining its cloud motion and animated material.
    internal sealed class WeatherDust : IDisposable
    {
        private GameObject _root;
        private ParticleSystem _system;
        private ParticleSystemRenderer _renderer;
        private Material _material;
        private Camera _camera;
        private Vector3 _previous;
        private float _next, _rate, _opacity;
        private bool _hasPosition, _waiting;
        private Color _lastTint;
        private ParticleSystem.Particle[] _particles = new ParticleSystem.Particle[100];
        private float _reportAt;
        private bool _reported;

        /// <summary>Prepares the private native dust pool once its source becomes available.</summary>
        internal bool Prepare() { return _system!=null || Create(); }
        /// <summary>Clones and configures the native dust source for camera-local presentation.</summary>
        private bool Create()
        {
            var manager = ES3ReferenceMgrBase.Current;
            var prefab = manager != null ? manager.GetPrefab(5697600806714206537L,true) : null;
            var source = prefab != null ? prefab.transform.Find("Sand Player") : null;
            if (source == null)
            {
                if (!_waiting) { _waiting = true; Log.Warn(null,"[Weather] Waiting for native SandStorm/Sand Player prefab in the game's reference manager."); }
                return false;
            }
            _root = new GameObject("WW.Weather.GroundDust") { hideFlags = HideFlags.HideAndDontSave, layer = 2 };
            _root.transform.SetParent(Runner.Host.transform,false); _root.SetActive(false);
            var copy = UnityEngine.Object.Instantiate(source.gameObject,_root.transform,false);
            copy.name = "WW.Weather.GroundDust.0"; copy.hideFlags = HideFlags.HideAndDontSave;
            copy.transform.localPosition = Vector3.zero;
            _system = copy.GetComponent<ParticleSystem>(); _renderer = copy.GetComponent<ParticleSystemRenderer>();
            _system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _system.main; main.playOnAwake = false;
            // This camera-gated renderer may be disabled at Unity's simulation/culling step.
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            _rate = _system.emission.rateOverTimeMultiplier;
            _material = new Material(_renderer.sharedMaterial); _renderer.sharedMaterial = _material;
            _opacity = _material.HasProperty("_Opacity") ? _material.GetFloat("_Opacity") : 1;
            _renderer.enabled = false; copy.SetActive(true); _root.SetActive(true);
            Camera.onPreCull += Before;
            Log.Always(null,"[Weather] Vanilla Sand Player clouds ready: native material, UV flow, velocity, lifetime and shape retained; brown tint, 60/100/100 particle cap.");
            return true;
        }
        /// <summary>Advances nearby dust from storm intensity, wind and live palette.</summary>
        internal void Tick(Camera camera, float clock, float strength, Vector3 wind, WeatherVisualDetail detail, Color tint)
        {
            _camera = camera;
            if (_system == null)
            {
                if (Time.unscaledTime < _next) return;
                if (!Create()) { _next = Time.unscaledTime+1; return; }
            }
            Vector3 position = GameRefs.Player != null ? GameRefs.Player.transform.position : camera.transform.position;
            if (_hasPosition && (position-_previous).sqrMagnitude > 10000) _system.Clear();
            _previous = position; _hasPosition = true; _root.transform.position = position;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime+.1f;
            if (tint != _lastTint)
            {
                if (_particles.Length < _system.particleCount) _particles = new ParticleSystem.Particle[_system.particleCount];
                int count = _system.GetParticles(_particles);
                for (int i = 0; i < count; i++) _particles[i].startColor = tint;
                _system.SetParticles(_particles, count); _lastTint = tint;
            }
            if (wind.sqrMagnitude > .01f) _root.transform.rotation = Quaternion.LookRotation(wind);
            bool running = GameRefs.InGame && strength > .005f;
            var emission = _system.emission; emission.enabled = running;
            emission.rateOverTimeMultiplier = _rate*strength*(detail == WeatherVisualDetail.Low ? .6f : 1);
            var main = _system.main; main.maxParticles = detail == WeatherVisualDetail.Low ? 60 : 100;
            main.startColor = new ParticleSystem.MinMaxGradient(tint);
            if (_material.HasProperty("_Opacity")) _material.SetFloat("_Opacity",_opacity*strength);
            if (!GameRefs.InGame) { if (_system.isPlaying) _system.Pause(); }
            else if (!_system.isPlaying) _system.Play();
            if (!_reported && running)
            {
                if (_reportAt == 0) _reportAt = Time.unscaledTime+2;
                if (_system.particleCount > 0 || Time.unscaledTime >= _reportAt)
                {
                    _reported = true;
                    Log.Always(null,"[Weather] Local sand live: particles="+_system.particleCount+"; rate="+emission.rateOverTimeMultiplier+"; time="+_system.time+"; culling="+main.cullingMode+"; strength="+strength+"; playing="+_system.isPlaying);
                }
            }
        }
        /// <summary>Shows local dust only to the selected gameplay camera.</summary>
        private void Before(Camera camera) { if (_renderer != null) _renderer.enabled = camera == _camera; }
        /// <summary>Clears particles and forgets the previous camera position.</summary>
        internal void Clear() { _hasPosition = false; if (_system != null) _system.Clear(); }
        /// <summary>Hides and clears local dust while retaining its prepared pool.</summary>
        internal void Suspend()
        {
            Clear(); _camera = null; _next = 0;
            if (_system != null) _system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_renderer != null) _renderer.enabled = false;
        }
        /// <summary>Releases camera observation and destroys private particle resources.</summary>
        public void Dispose()
        {
            Camera.onPreCull -= Before; Suspend();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_material != null) UnityEngine.Object.Destroy(_material);
        }
    }
}
