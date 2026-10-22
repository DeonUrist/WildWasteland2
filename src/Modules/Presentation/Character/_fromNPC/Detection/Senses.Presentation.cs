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
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Senses part owned by Presentation: NPC combat taunt audio (AudioPlay swap, Taunt), the human voice clips (Voice, FlexaClips) and the 2D voice source used for the player's shout.</summary>
    internal static partial class Senses
    {
        // the human Sound FSM's combat shout (Harmony prefix on AudioPlay.OnEnter, FSM "Sound", state "attack")
        /// <summary>AudioPlay.OnEnter prefix: the human Sound FSM's combat shout plays only as a taunt (Taunt decides).</summary>
        public static bool BeforeAudioPlay(AudioPlay __instance)
        {
            try
            {
                if (!On) return true;
                var fsm = __instance.Fsm;
                if (fsm == null) return true;
                if (fsm.Name == "INPUT_Horn") { _hornFsm = fsm; _hornAt = Time.time; return true; }    // the player's car horn starts (Engine() picks it up)
                if (fsm.Name != "Sound" || fsm.ActiveStateName != "attack") return true;
                var a = Get(fsm.GameObject);
                if (a == null) return true;
                // The game's Sound FSM shouts every 0.1-4 s for as long as the NPC has any target, a ghost included. Here a shout only
                // when it sees its target right now, the first time at once, then every TauntMin..TauntMax s; a suppressed shout
                // ends the state at once (the FSM keeps cycling, silent).
                float now = Time.time;
                if (!SeesNow(a) || now < a.NextShout)
                {
                    if (__instance.finishedEvent != null) fsm.Event(__instance.finishedEvent);
                    __instance.Finish();
                    return false;
                }
                a.NextShout = now + UnityEngine.Random.Range(Mathf.Max(1f, NpcModule.TauntMin.Value), Mathf.Max(NpcModule.TauntMin.Value, NpcModule.TauntMax.Value));
                if (a.Human && NpcModule.TauntRange.Value > 0f) Taunt(a);
            }
            catch (Exception e) { NpcModule.Log.LogError("Senses: " + e); }
            return true;
        }

        /// <summary>A human's taunt: passes its knowledge to friends in TauntRange and tells enemies where it stands.</summary>
        private static void Taunt(Agent t)
        {
            float now = Time.time, range = NpcModule.TauntRange.Value;
            // friends: where the shouter's target is (or the ghost the shouter is going to)
            Vector3 where; bool known = false; Ghost tg = null;
            if ((t.State == State.Combat || t.State == State.Hide) && t.Target != null) { where = Fuzz(t.Target.transform.position, t.T.position, 0.2f); known = true; }   // "over there!": roughly
            else if (t.Ghost != null && t.State == State.Investigate && !t.Ghost.Retired) { where = t.Ghost.Pos; tg = t.Ghost; known = true; }   // a searcher found nothing: nothing to pass on
            else where = t.T.position;
            if (known)
            {
                string tag = t.Tag;
                Ghost shared = tg;
                // a shout passes on roughly what the shouter sees (a SHOUT-rank ghost near the target: friends go and look, they do not get a
                // live position) or the very ghost it is going to, at that ghost's rank
                if (shared == null) { shared = GetOrMake(Src.Shout, where, t.Target, Name(t.Owner) + " shouted about " + Name(t.Target), 6f, 15f, now); shared.Subject = t.Target; }   // one ghost per sighting: shouts refresh it instead of minting new ones
                Src relay = tg == null ? Src.Shout : t.GhostPrio;
                int told = 0;
                foreach (var kv in _agents)
                {
                    var f = kv.Value;
                    if (f == t || f.Owner == null || !f.Human || f.Tag != tag || f.T.parent != null) continue;
                    if ((f.T.position - t.T.position).sqrMagnitude > range * range) continue;
                    // a ghost acquired once (from a shout or any other way) is never re-sent by a shout: it would wake a searching NPC
                    // back to investigating the same spot again and again while its friends keep shouting
                    if (f.Ghost == shared || f.Heard.Contains(shared.Id)) continue;
                    if (f.Ghost != null && f.Ghost.Subject != null && f.Ghost.Subject == shared.Subject && (f.Ghost.Pos - shared.Pos).sqrMagnitude < 16f) continue;   // already knows the same thing
                    if (Assign(f, shared, relay, now)) { told++; f.Heard.Add(shared.Id); }
                }
                if (shared != tg && shared.Holders.Count == 0) { int i = _ghosts.IndexOf(shared); if (i >= 0) KillGhost(i); }
                if (NpcModule.VerboseLog.Value && told > 0) NpcModule.Log.LogInfo("Senses: " + Name(t.Owner) + "'s shout sends " + told + " " + tag + " to " + (tg != null ? "ghost #" + tg.Id : "its target"));
            }
            // enemies: the shouter gave itself away
            string ttag = t.Tag;
            int key = -t.Owner.GetInstanceID();
            Noise(t.Owner, t.T.position, range, Src.Shout, Name(t.Owner) + " shouting", a => a.Hostile.Contains(ttag) && !a.Heard.Contains(key), null, t.Owner,
                a => a.Heard.Add(key));
        }

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
            var animation = Core.Services.Animation;
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
                if (_voice == null)
                    foreach (var kv in _agents)
                    {
                        var o = kv.Value.Owner;
                        if (o == null || !(o.name == "Flexa" || o.name.StartsWith("Flexa(", StringComparison.Ordinal))) continue;
                        _voice = FlexaClips(o);
                        if (_voice != null) break;
                    }
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

        private static float _nextFlexaScan;
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
