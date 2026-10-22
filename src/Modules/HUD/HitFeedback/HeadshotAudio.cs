using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    // WW: the per-frame Tick (2 config reads, Stop() every frame while disabled) became edge-triggered Refresh() calls
    // (Init, scene load, HitFeedback/HeadshotSound changes). The two voices live on a module-owned child of the Core runner
    // ("WW.HUD", destroyed in Shutdown) instead of the old GunplayHUD.Runner; the MP3 decode is a coroutine on the Core
    // runner (Scheduler.StartCoroutine) that waits one frame first, so it runs after every module's Init (the old mod also
    // loaded on its first Update) - in particular after an AudioSettings.Reset at startup.
    internal static class HeadshotAudio
    {
        private static readonly HeadshotSoundGate Gate = new HeadshotSoundGate();
        private static readonly AudioSource[] Voices = new AudioSource[2];
        private static GameObject _host;
        private static AudioClip _clip;
        private static bool _attempted;
        private static Coroutine _load;
        private static UnityWebRequest _request;
        private static string _status = "not loaded";

        internal static string Status { get { return _clip != null ? "loaded (" + _clip.length.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s)" : _status; } }

        private static bool Wanted { get { return HudModule.HudEnabled.Value && HudModule.HeadshotSound.Value; } }

        /// <summary>Old Tick, now called on Init / scene load / setting changes only.</summary>
        internal static void Refresh()
        {
            if (!Wanted) { Stop(); return; }
            if (!_attempted)
            {
                _attempted = true;
                _status = "loading";
                _load = Scheduler.StartCoroutine(Load());
                if (_load == null) { _attempted = false; _status = "runner missing"; }
            }
            else EnsureVoices();
        }

        private static void EnsureVoices()
        {
            if (_host == null)
            {
                var parent = Runner.Host;
                if (parent == null) return;
                _host = new GameObject("WW.HUD") { hideFlags = HideFlags.HideAndDontSave };
                _host.transform.SetParent(parent.transform, false);
            }
            for (int i = 0; i < Voices.Length; i++)
                if (Voices[i] == null)
                {
                    var source = _host.AddComponent<AudioSource>();
                    source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f;
                    source.dopplerLevel = 0f; source.clip = _clip; source.volume = Volume; Voices[i] = source;
                }
        }

        private static float Volume { get { return Mathf.Clamp01(HudModule.HeadshotVolume.Value / 100f); } }

        internal static void ApplyVolume()
        {
            float v = Volume;
            foreach (var voice in Voices) if (voice != null) voice.volume = v;
        }

        private static IEnumerator Load()
        {
            yield return null;     // next frame: after all module Inits (see class comment)
            EnsureVoices();
            string path = HudModule.AssetPath("Sounds", "headshot.mp3");
            if (path == null)
            {
                _status = "missing: " + System.IO.Path.Combine(System.IO.Path.Combine(HudModule.Dir ?? "", "Sounds"), "headshot.mp3");
                Log.Warn("[HUD] Headshot sound " + _status);
                yield break;
            }
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.MPEG))
            {
                _request = request;
                var handler = (DownloadHandlerAudioClip)request.downloadHandler;
                handler.compressed = false; handler.streamAudio = false;
                yield return request.SendWebRequest();
                _request = null;
                if (request.result != UnityWebRequest.Result.Success)
                { _status = "error: " + request.error; Log.Warn("[HUD] Headshot sound: " + request.error); yield break; }
                _clip = DownloadHandlerAudioClip.GetContent(request);
                if (_clip == null) { _status = "could not be decoded"; Log.Warn("[HUD] Headshot sound could not be decoded."); yield break; }
                // AudioSource.volume clamps to 1, so boost decoded samples once instead.
                float gain = HudModule.HeadshotGain.Value;
                var samples = new float[_clip.samples * _clip.channels];
                if (_clip.GetData(samples, 0))
                {
                    for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
                    if (!_clip.SetData(samples, 0)) Log.Warn("[HUD] Headshot sound gain could not be applied.");
                }
                else Log.Warn("[HUD] Headshot sound samples unavailable; using original volume.");
                _clip.hideFlags = HideFlags.HideAndDontSave;
                EnsureVoices();
                foreach (var voice in Voices) if (voice != null) voice.clip = _clip;
                if (Log.Verbose) Log.Info("[HUD] headshot sound loaded from " + path);
            }
            _load = null;
        }

        internal static void Play()
        {
            if (_clip == null || AudioListener.pause || Time.timeScale == 0f) return;
            HeadshotSoundGate.MinimumInterval = HudModule.HeadshotMinInterval.Value;
            int index = Gate.Acquire(Time.unscaledTime, _clip.length);
            if (index < 0) return;
            if (Voices[index] == null) EnsureVoices();      // the runner (and its child) was recreated
            var v = Voices[index];
            if (v == null || v.isPlaying) return;
            if (v.clip != _clip) v.clip = _clip;
            v.Play();
        }

        internal static void Stop()
        { foreach (var voice in Voices) if (voice != null) voice.Stop(); Gate.Reset(); }

        /// <summary>Module off: stop the decode, destroy the voices' host and the clip (HideAndDontSave would leak); Init loads again.</summary>
        internal static void Shutdown()
        {
            if (_load != null) { Scheduler.StopCoroutine(_load); _load = null; }
            if (_request != null) { try { _request.Dispose(); } catch { } _request = null; }
            Stop();
            for (int i = 0; i < Voices.Length; i++) Voices[i] = null;
            if (_host != null) UnityEngine.Object.Destroy(_host);
            _host = null;
            if (_clip != null) UnityEngine.Object.Destroy(_clip);
            _clip = null;
            _attempted = false;
            _status = "not loaded";
        }
    }
}
