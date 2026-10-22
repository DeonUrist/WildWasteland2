using System.Collections.Generic;
using SkrilStudio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.EngineSimCompat
{
    // Includes inactive engine sounds: the native sound object is off with the ignition.
    internal static class EngineDiscovery
    {
        private static readonly Stack<Transform> Walk = new Stack<Transform>();
        private static readonly List<GameObject> Roots = new List<GameObject>(256);
        private static readonly Dictionary<int, RealisticEngineSound> Sounds = new Dictionary<int, RealisticEngineSound>();
        private static readonly List<RealisticEngineSound> NodeSounds = new List<RealisticEngineSound>();
        private static readonly HashSet<AudioListener> Listeners = new HashSet<AudioListener>();
        private static readonly List<AudioListener> NodeListeners = new List<AudioListener>();
        private static readonly HashSet<Transform> Queued = new HashSet<Transform>();
        private static readonly List<int> Removed = new List<int>();
        private static RealisticEngineSound[] _snapshot = new RealisticEngineSound[0];
        private static Camera _camera;
        private static AudioListener _cameraEar;
        private static float _next;
        private static bool _dirty;

        internal static void Init()
        {
            Clear();
            // Scene-boundary seeds include inactive objects before their first OnEnable.
            foreach (var sound in Resources.FindObjectsOfTypeAll<RealisticEngineSound>()) Observe(sound);
            foreach (var ear in Object.FindObjectsOfType<AudioListener>()) Listeners.Add(ear);
            for (int i = 0; i < SceneManager.sceneCount; i++) AddScene(SceneManager.GetSceneAt(i));
            if (Core.Runner.Host != null) AddScene(Core.Runner.Host.scene);
        }
        internal static RealisticEngineSound[] Snapshot
        {
            get
            {
                if (_dirty)
                {
                    if (_snapshot.Length != Sounds.Count) _snapshot = new RealisticEngineSound[Sounds.Count];
                    Sounds.Values.CopyTo(_snapshot, 0); _dirty = false;
                }
                return _snapshot;
            }
        }
        internal static AudioListener Listener()
        {
            var camera = Core.GameRefs.MainCamera;
            if (_camera != camera) { _camera = camera; _cameraEar = camera != null ? camera.GetComponent<AudioListener>() : null; }
            if (_cameraEar != null && _cameraEar.isActiveAndEnabled) return _cameraEar;
            foreach (var ear in Listeners) if (ear != null && ear.isActiveAndEnabled) return ear;
            return null;
        }
        internal static void Observe(RealisticEngineSound sound)
        {
            if (sound == null || !sound.gameObject.scene.IsValid()) return;
            int id = sound.GetInstanceID();
            if (!Sounds.ContainsKey(id)) { Sounds.Add(id, sound); _dirty = true; }
        }
        internal static void Queue(Transform root) { if (root != null && Queued.Add(root)) Walk.Push(root); }
        private static void AddScene(Scene scene)
        {
            if (!scene.isLoaded) return;
            Roots.Clear(); scene.GetRootGameObjects(Roots);
            for (int i = 0; i < Roots.Count; i++) Queue(Roots[i].transform);
        }
        internal static bool Step()
        {
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + .5f; Removed.Clear();
                foreach (var sound in Sounds) if (sound.Value == null) Removed.Add(sound.Key);
                for (int i = 0; i < Removed.Count; i++) { Sounds.Remove(Removed[i]); _dirty = true; }
                Listeners.RemoveWhere(DeadListener);
            }
            if (Walk.Count > 0)
            {
                var t = Walk.Pop(); if (t == null) return true;
                for (int i = 0; i < t.childCount; i++) Walk.Push(t.GetChild(i));
                NodeSounds.Clear(); t.GetComponents<RealisticEngineSound>(NodeSounds);
                for (int i = 0; i < NodeSounds.Count; i++) Observe(NodeSounds[i]);
                NodeListeners.Clear(); t.GetComponents<AudioListener>(NodeListeners);
                for (int i = 0; i < NodeListeners.Count; i++) Listeners.Add(NodeListeners[i]);
                return true;
            }
            Queued.Clear(); return false;
        }
        private static bool DeadListener(AudioListener listener) { return listener == null; }
        internal static void Clear()
        {
            Walk.Clear(); Roots.Clear(); Sounds.Clear(); NodeSounds.Clear(); Listeners.Clear(); NodeListeners.Clear(); Queued.Clear(); Removed.Clear();
            _snapshot = new RealisticEngineSound[0]; _camera = null; _cameraEar = null; _dirty = false; _next = 0;
        }
    }
}
