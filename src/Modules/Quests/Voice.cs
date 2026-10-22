using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    // Voice acting for hand-written quests. Layout (next to the DLL, next to quests.txt):
    //   Voice Actors\<actor>\<quest id>\offer.wav, accept.wav, remind.wav, done.wav     (the quest id is the [section] name in quests.txt)
    // When a line is spoken, the actors that have a folder for that quest (and that file) are tried in a random order, seeded by
    // the quest and the NPC so the same person keeps the same voice through the whole quest; the first one that has the file is played.
    internal static partial class Voice
    {
        static readonly bool PlaybackEnabled = false; // Temporary until the quest recordings are complete.
        static ConfigEntry<bool> enabled;
        static ConfigEntry<float> volume;
        static ConfigEntry<string> folderCfg;
        static ConfigEntry<float> spatial;
        static string root;
        static bool rootLogged;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> bad = new HashSet<string>();
        static AudioSource src;
        static readonly HashSet<string> logged = new HashSet<string>();

        /// <summary>Binds retained voice settings while preserving the current playback-disabled policy.</summary>
        public static void Init(VoiceSettings cfg, string dllDir)
        {
            enabled = cfg.enabled;
            Core.Log.Always("[Quests] Quest voicelines temporarily disabled (recording set incomplete).");
            volume = cfg.volume;

            spatial = cfg.spatial;
            folderCfg = cfg.folderCfg;
            root = dllDir;
            volume.SettingChanged += Changed; folderCfg.SettingChanged += Changed;
        }

        /// <summary>Resolves the authored voice-content directory.</summary>
        static string Root()
        {
            string c = folderCfg != null ? (folderCfg.Value ?? "").Trim() : "";
            string[] tries = c.Length > 0 ? new[] { c, Path.Combine(c, "Voice Actors") } : new[] { Path.Combine(root, "Voice Actors"), Path.Combine(Path.GetDirectoryName(root) ?? root, "Voice Actors") };
            foreach (string t in tries) if (Directory.Exists(t)) { if (!rootLogged) { rootLogged = true; Plugin.Info("Voice: using " + t); } return t; }
            return null;
        }

        // Plays the line (offer / accept / remind / done) of this quest if some voice actor recorded it. Returns true when something plays.
        /// <summary>Selects and optionally plays a recorded quest line for its speaker.</summary>
        public static bool Say(Quest q, string line, Transform npc, string npcName)
        {
            try
            {
                if (!PlaybackEnabled || enabled == null || !enabled.Value || q == null || string.IsNullOrEmpty(q.Id) || q.Id.StartsWith("gen:")) return false;
                string r = Root();
                if (r == null) return false;
                string[] actors = Directory.GetDirectories(r);
                if (actors.Length == 0) return false;
                // random order, but the same for the same quest and NPC, so one person keeps one voice
                var rng = new System.Random(Hash(q.Id + "|" + (npcName ?? "")));
                for (int i = actors.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); string t = actors[i]; actors[i] = actors[j]; actors[j] = t; }
                string safe = SafeName(q.Id);
                foreach (string a in actors)
                {
                    string path = Path.Combine(Path.Combine(Path.Combine(r, Path.GetFileName(a)), safe), line + ".wav");
                    if (!File.Exists(path)) continue;
                    AudioClip clip = Load(path);
                    if (clip == null) continue;
                    Play(clip, npc);
                    if (logged.Add(q.Id + "|" + line + "|" + a)) Plugin.Info("Voice: " + Path.GetFileName(a) + " says " + q.Id + "/" + line + ".");
                    return true;
                }
            }
            catch (Exception e) { Plugin.LogOnce("Voice", e); }
            return false;
        }

        /// <summary>Stops the current quest voice clip.</summary>
        public static void Stop() { try { if (src != null) src.Stop(); } catch (Exception) { } }

        /// <summary>Sanitizes an authored voice path component.</summary>
        static string SafeName(string id)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) id = id.Replace(c, '_');
            return id;
        }

        /// <summary>Derives a deterministic speaker and quest seed.</summary>
        static int Hash(string s)
        {
            unchecked { int h = 17; foreach (char c in s) h = h * 31 + c; return h & 0x7fffffff; }
        }

        /// <summary>Plays a decoded clip on the retained quest audio source.</summary>
        static void Play(AudioClip clip, Transform npc)
        {
            if (src == null)
            {
                var go = new GameObject("ApocalypterQuests_Voice");
                UnityEngine.Object.DontDestroyOnLoad(go);
                src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
            }
            src.Stop();
            if (npc != null && spatial.Value > 0f) src.transform.position = npc.position + Vector3.up * 1.6f;
            src.spatialBlend = Mathf.Clamp01(spatial.Value);
            src.minDistance = 4f; src.maxDistance = 40f;
            src.volume = Mathf.Clamp01(volume.Value / 5f);
            src.clip = clip;
            src.Play();
        }

        /// <summary>Loads and caches a supported recorded voice clip.</summary>
        static AudioClip Load(string path)
        {
            AudioClip c;
            if (clips.TryGetValue(path, out c) && c != null) return c;
            if (bad.Contains(path)) return null;
            try
            {
                c = ReadWav(path);
                if (c != null) { clips[path] = c; return c; }
            }
            catch (Exception e) { Plugin.Warn("Voice: cannot read " + path + ": " + e.Message); }
            bad.Add(path);
            return null;
        }

        /// <summary>
        /// Decodes a WAV with the Core decoder (<see cref="Audio.ReadWav"/>: PCM 8/16/24/32-bit, float, any channel count and rate),
        /// levels every recording the same (peak to 0.9) and boosts for Volume above 5 with a soft clip (<see cref="Audio.NormalizePeak"/>).
        /// Replaces the Quests copy of the decoder (Wave A).
        /// </summary>
        static AudioClip ReadWav(string path)
        {
            int ch, rate;
            float[] data = Audio.ReadWav(path, out ch, out rate);
            Audio.NormalizePeak(data, 0.9f, Mathf.Max(1f, volume.Value / 5f));
            return Audio.Clip(Path.GetFileNameWithoutExtension(path), data, ch, rate, false);
        }
        /// <summary>Unsubscribes configuration callbacks and destroys owned voice resources.</summary>
        internal static void Shutdown()
        {
            if (volume != null) volume.SettingChanged -= Changed; if (folderCfg != null) folderCfg.SettingChanged -= Changed;
            Stop(); if (src != null) UnityEngine.Object.Destroy(src.gameObject); src = null;
            foreach (var clip in clips.Values) if (clip != null) UnityEngine.Object.Destroy(clip);
            clips.Clear(); bad.Clear(); logged.Clear(); root = null; rootLogged = false;
        }
        /// <summary>Invalidates decoded voices after their path or volume settings change.</summary>
        static void Changed(object sender, EventArgs args)
        {
            Stop(); if (src != null) src.clip = null;
            foreach (var clip in clips.Values) if (clip != null) UnityEngine.Object.Destroy(clip);
            clips.Clear(); bad.Clear(); rootLogged = false;
        }
    }
}
