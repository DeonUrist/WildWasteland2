using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using UnityEngine.SceneManagement;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>Senses part owned by Presentation: NPC combat taunt audio (AudioPlay swap, Taunt), the human voice clips (Voice, FlexaClips) and the 2D voice source used for the player's shout.</summary>
    internal sealed class NpcVoice : INpcVoice
    {
        /// <summary>Plays the current character shout on the camera-local source.</summary>
        public bool Shout(float volume) { var clip=Voice();if(clip==null)return false;var source=VoiceSource();if(source==null)return false;source.PlayOneShot(clip,Mathf.Clamp01(volume));return true; }
        /// <summary>Releases the owned source when Presentation stops or a scene changes.</summary>
        internal static void Clear() { if(_voiceSrc!=null)UnityEngine.Object.Destroy(_voiceSrc.gameObject);_voiceSrc=null; }
        private static AudioClip[] _voice;

        private static AudioSource _voiceSrc;
        /// <summary>A 2D AudioSource on the camera for the player's shout.</summary>
        private static AudioSource VoiceSource()
        {
            var cam = Core.GameRefs.MainCamera;
            if (cam == null) return null;
            if (_voiceSrc != null && _voiceSrc.transform.parent == cam.transform) return _voiceSrc;
            var go = new GameObject("NPCAI.Voice");
            go.transform.SetParent(cam.transform, false);
            _voiceSrc = go.AddComponent<AudioSource>();
            _voiceSrc.spatialBlend = 0f; _voiceSrc.playOnAwake = false; _voiceSrc.loop = false;
            return _voiceSrc;
        }

        /// <summary>A random raider shout clip for the player's shout (Animation PlayerShout or the Flexa clips).</summary>
        private static AudioClip Voice()
        {
            var animation = ModApi.Get<IAnimationApi>();
            if (animation != null && animation.PlayerShout != null) return animation.PlayerShout;
            if (_voice == null || _voice.Length == 0 || _voice[0] == null)
            {
                // Flexa's own shouts, read from the Flexa prefab asset (Gungirl's voice swap only ever edits spawned instances, so the
                // prefab always carries the game's original clips); else from a live vanilla Flexa. Gungirls are skipped, and nothing is
                // collected by clip name: the mod's Gungirl voice files carry the same names (enemy_human_single_N).
                _voice = null;
                GameObject prefab = null;
                if (Time.unscaledTime >= _nextFlexaScan)
                {
                    _nextFlexaScan = Time.unscaledTime + 30f;       // Gungirl keeps the Flexa prefab once found; looked up at most every 30 s until then
                    prefab = FlexaPrefab();
                }
                if (prefab != null) _voice = FlexaClips(prefab);
                if (_voice == null && _scannedAt != _nextFlexaScan)
                    foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (o == null || !(o.name == "Flexa" || o.name.StartsWith("Flexa(", StringComparison.Ordinal))) continue;
                        _voice = FlexaClips(o);
                        if (_voice != null) break;
                    }
                _scannedAt = _nextFlexaScan;
                if (_voice == null) return null;      // no Flexa anywhere yet: a silent shout, tried again next time
            }
            return _voice[UnityEngine.Random.Range(0, _voice.Length)];
        }

        private static GameObject _flexaPrefab;
        /// <summary>The Flexa prefab asset (looked up once).</summary>
        private static GameObject FlexaPrefab()
        {
            if (_flexaPrefab != null) return _flexaPrefab;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && go.name == "Flexa" && !go.scene.IsValid() && go.transform.parent == null && go.GetComponent<Rigidbody>() != null)
                { _flexaPrefab = go; break; }
            return _flexaPrefab;
        }

        private static float _nextFlexaScan, _scannedAt;
        /// <summary>The enemy_human_single clips of the Flexa Sound FSM.</summary>
        private static AudioClip[] FlexaClips(GameObject flexa)
        {
            foreach (var f in flexa.GetComponents<PlayMakerFSM>())
            {
                if (f == null || f.FsmName != "Sound") continue;
                var arr = f.FsmVariables.FindFsmArray("soundArray");
                if (arr == null || arr.Values == null) continue;
                var list = new List<AudioClip>();
                foreach (var v in arr.Values) { var c = v as AudioClip; if (c != null) list.Add(c); }
                if (list.Count > 0) return list.ToArray();
            }
            return null;
        }
    }
}
