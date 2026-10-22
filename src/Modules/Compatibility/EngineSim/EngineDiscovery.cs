using System.Collections.Generic;
using SkrilStudio;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    // Includes inactive engine sounds: the native sound object is off with the ignition.
    internal static class EngineDiscovery
    {
        private static readonly Dictionary<int, RealisticEngineSound> Sounds = new Dictionary<int, RealisticEngineSound>();
        private static readonly HashSet<AudioListener> Listeners = new HashSet<AudioListener>();
        private static readonly List<RealisticEngineSound> NodeSounds = new List<RealisticEngineSound>();
        private static readonly List<AudioListener> NodeListeners = new List<AudioListener>();
        private static readonly List<int> Removed = new List<int>();
        private static RealisticEngineSound[] _snapshot = new RealisticEngineSound[0];
        private static Camera _camera;
        private static AudioListener _cameraEar;
        private static bool _dirty;

        /// <summary>Seeds native inactive sounds once, then observes the shared Core walk instead of running another scene walker.</summary>
        internal static void Init(IModule owner)
        {
            WorldIndex.RemoveAll(owner); Clear();
            foreach (var sound in Resources.FindObjectsOfTypeAll<RealisticEngineSound>()) Observe(sound);
            foreach (var ear in Object.FindObjectsOfType<AudioListener>()) Listeners.Add(ear);
            WorldIndex.WatchComponent<RealisticEngineSound>(owner, SoundFound);
            WorldIndex.WatchComponent<AudioListener>(owner, ListenerFound);
        }
        /// <summary>Adds an inactive or active sound found by Core's shared component watch.</summary>
        private static void SoundFound(Transform node, string name, Component component) { NodeSounds.Clear(); node.GetComponents<RealisticEngineSound>(NodeSounds); foreach (var sound in NodeSounds) Observe(sound); }
        /// <summary>Retains a listener found by Core without an independent scene traversal.</summary>
        private static void ListenerFound(Transform node, string name, Component component) { NodeListeners.Clear(); node.GetComponents<AudioListener>(NodeListeners); foreach (var ear in NodeListeners) if (ear != null) Listeners.Add(ear); }
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
        /// <summary>Prefers the current camera listener, then any retained active native listener.</summary>
        internal static AudioListener Listener()
        {
            var camera = Core.GameRefs.MainCamera;
            if (_camera != camera) { _camera = camera; _cameraEar = camera != null ? camera.GetComponent<AudioListener>() : null; }
            if (_cameraEar != null && _cameraEar.isActiveAndEnabled) return _cameraEar;
            foreach (var ear in Listeners) if (ear != null && ear.isActiveAndEnabled) return ear;
            return null;
        }
        /// <summary>Retains a live scene sound and invalidates the snapshot only when newly observed.</summary>
        internal static void Observe(RealisticEngineSound sound)
        {
            if (sound == null || !sound.gameObject.scene.IsValid()) return;
            int id = sound.GetInstanceID();
            if (!Sounds.ContainsKey(id)) { Sounds.Add(id, sound); _dirty = true; }
        }
        /// <summary>Queues newly enabled vehicles into the shared Core world walker.</summary>
        internal static void Queue(Transform root) { WorldIndex.QueueRoot(root); }
        /// <summary>Prunes destroyed entries without querying the scene again.</summary>
        internal static void Prune()
        {
            Removed.Clear();
            foreach (var sound in Sounds) if (sound.Value == null) Removed.Add(sound.Key);
            foreach (int id in Removed) { Sounds.Remove(id); _dirty = true; }
            Listeners.RemoveWhere(DeadListener);
        }
        /// <summary>Identifies Unity-destroyed listener entries during bounded periodic maintenance.</summary>
        private static bool DeadListener(AudioListener listener) { return listener == null; }
        /// <summary>Releases all retained scene objects and invalidates camera and sound caches.</summary>
        internal static void Clear()
        {
            Sounds.Clear(); Listeners.Clear(); Removed.Clear(); NodeSounds.Clear(); NodeListeners.Clear();
            _snapshot = new RealisticEngineSound[0]; _camera = null; _cameraEar = null; _dirty = false;
        }
    }
}
