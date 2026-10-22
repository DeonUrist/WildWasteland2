using System;
using System.Collections.Generic;
using UnityEngine;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Owns the rider voice asset cache and transient audio sources.</summary>
    internal static class RiderVoice
    {
        // "Witness me!" - Sounds/witness-me.wav (16-bit PCM, normalised to the game's human_hurt voice: -23.8 LUFS, peak -5.6 dBTP) as a
        // 3D voice on the rider
        private static readonly List<GameObject> _voices=new List<GameObject>();
        private static readonly List<AudioClip> _screams = new List<AudioClip>(4); private static bool _screamLooked;
        // plays the scream; returns its length (1.5 s when the wav is missing: the rider still pauses before leaping)
        /// <summary>plays the scream; returns its length (1.5 s when the wav is missing: the rider still pauses before leaping)</summary>
        internal static float Scream(Transform at)
        {
            try
            {
                if (!_screamLooked)
                {
                    _screamLooked = true;
                    for (int i = 1; i <= 4; i++)
                    {
                        string clipName = i == 1 ? "witness-me" : "witness-me" + i;
                        var path = System.IO.Path.Combine(Core.Assets.ModuleDir("Patrols"), "Sounds", clipName + ".wav");
                        var clip = WildWasteland.Core.Audio.TryLoadWav(path, clipName, 1.8f);   // +5 dB over the normalised file (peak about -0.5 dBFS)
                        if (clip != null) _screams.Add(clip);
                        else Core.Log.Warn("[Patrols] " + "Rider: " + path + " not found or not a PCM wav - skipped");
                    }
                }
                if (_screams.Count == 0 || at == null) return 1.5f;
                var scream = _screams[UnityEngine.Random.Range(0, _screams.Count)];
                _voices.RemoveAll(item=>item==null);
                var go = new GameObject("Apocapatrol.WitnessMe");_voices.Add(go);
                go.transform.SetParent(at, false); go.transform.localPosition = Vector3.up * 1.6f;
                var src = go.AddComponent<AudioSource>();
                // loud and far: full volume within 35 m, still clearly heard at 200+ m (mostly 3D, a little 2D so distance doesn't swallow it)
                src.clip = scream; src.spatialBlend = 0.8f; src.rolloffMode = AudioRolloffMode.Logarithmic; src.minDistance = 35f; src.maxDistance = 400f;
                src.volume = 1f; src.priority = 64; src.dopplerLevel = 0f;
                src.Play();
                UnityEngine.Object.Destroy(go, scream.length + 0.2f);
                if (Core.Log.Verbose) Core.Log.Deep(PresentationModule.Instance,"Rider: scream " + scream.name);
                return scream.length;
            }
            catch (Exception e) { Core.Log.Deep(PresentationModule.Instance,"Rider: scream: " + e.Message); return 1.5f; }
        }

        /// <summary>Releases transient voice sources at scene and module boundaries.</summary>
        internal static void Clear() { foreach(var voice in _voices)if(voice!=null)UnityEngine.Object.Destroy(voice);_voices.Clear(); }
    }
}
