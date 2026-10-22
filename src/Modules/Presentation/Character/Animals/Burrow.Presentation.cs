using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Burrow part owned by Presentation: the visual sink of the rig (LateTick) and the sand puff / sound FX pool (Burrower_Effect).</summary>
    internal static partial class Burrow
    {
        private static GameObject _effect; private static AudioClip _clip; private static float _nextFind;

        // (WW perf) the sand puffs: finished ones are deactivated and reused (was Instantiate + Destroy per puff)
        private static readonly List<GameObject> _fxFree = new List<GameObject>();
        private static readonly List<GameObject> _fxLive = new List<GameObject>();
        private static readonly List<float> _fxUntil = new List<float>();
        private static readonly List<ParticleSystem> _psBuf = new List<ParticleSystem>();
        private static readonly List<PlayMakerFSM> _fsmBuf = new List<PlayMakerFSM>();

        // runs in LateUpdate, after the Animator: the body (the Rig child) is sunk along the NPC's own up axis.
        // Amount 0..1 = the burrow (the eyes stay up), 1..2 = a hunter's dive (the tail goes under too)
        /// <summary>LateUpdate: sinks the rig along the NPC's up axis (0..1 burrow, 1..2 a hunter's dive).</summary>
        internal static void LateTick()
        {
            for (int i = 0; i < _list.Count; i++)
            {
                var b = _list[i];
                if (b.Rig == null || b.T == null) continue;
                if (b.Amount <= 0.0005f && !b.Applied) continue;
                float a1 = Mathf.Clamp01(b.Amount), a2 = Mathf.Clamp01(b.Amount - 1f);
                float sink = b.Depth * a1 * a1 * (3f - 2f * a1) + (b.Deep - b.Depth) * a2 * a2 * (3f - 2f * a2);
                b.Rig.localPosition = b.RigBase + b.T.InverseTransformVector(-b.T.up * sink);
                b.Applied = b.Amount > 0.0005f;
                if (!b.Applied) b.Rig.localPosition = b.RigBase;
            }
        }

        // ---------- the sand worm's puff and sound, small and quiet ----------
        /// <summary>Finds the sand worm's Burrower_Effect prefab and its clip (at most once a minute while missing).</summary>
        private static void FindEffect()
        {
            if (_effect != null || Time.time < _nextFind) return;
            _nextFind = Time.time + 60f;       // (1.4.0) a scan of every FSM in memory: at most once a minute while the worm's effect is not loaded (was 10 s)
            try
            {
                foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                {
                    if (f == null || f.gameObject == null || f.FsmName != "DestroySelf") continue;
                    var go = f.gameObject;
                    if (go.scene.IsValid() || go.transform.parent != null || go.name != "Burrower_Effect") continue;
                    _effect = go;
                    if (f.Fsm != null && f.Fsm.States != null)
                        foreach (var st in f.Fsm.States)
                        {
                            if (st == null || st.Actions == null) continue;
                            foreach (var act in st.Actions) { var sc = act as SetAudioClip; if (sc != null && sc.audioClip != null && sc.audioClip.Value as AudioClip != null) _clip = sc.audioClip.Value as AudioClip; }
                        }
                    if (WildWasteland.Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Burrow: sand effect found (" + (_clip != null ? _clip.name : "no clip") + ")");
                    break;
                }
            }
            catch (Exception e) { Core.Log.Warn(PresentationModule.Instance, "Burrow: effect lookup: " + e.Message); }
        }

        /// <summary>Plays a sand puff and sound at a point (pooled, small scale, low volume).</summary>
        private static void Fx(B b, Vector3 at)
        {
            var cam = Core.GameRefs.MainCamera;
            if (cam == null || (cam.transform.position - at).sqrMagnitude > NpcaiSettings.BurrowFxRange.Value * NpcaiSettings.BurrowFxRange.Value) return;
            FindEffect();
            if (_effect == null) return;
            try
            {
                float s = Mathf.Clamp(b.Hunter ? NpcaiSettings.HuntFxScale.Value : NpcaiSettings.BurrowFxScale.Value, 0.05f, 1f);
                GameObject fx = null;
                while (_fxFree.Count > 0 && fx == null) { fx = _fxFree[_fxFree.Count - 1]; _fxFree.RemoveAt(_fxFree.Count - 1); }
                if (fx == null)
                {
                    fx = UnityEngine.Object.Instantiate(_effect, at, Quaternion.identity);
                    fx.GetComponentsInChildren(true, _fsmBuf);
                    foreach (var f in _fsmBuf) f.enabled = false;     // played by hand, at our scale and volume
                    _fsmBuf.Clear();
                }
                else fx.transform.SetPositionAndRotation(at, Quaternion.identity);
                fx.SetActive(true);
                fx.transform.localScale = Vector3.one * s;
                fx.GetComponentsInChildren(true, _psBuf);
                foreach (var ps in _psBuf)
                {
                    var m = ps.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    ps.Play(true);
                }
                _psBuf.Clear();
                var src = fx.GetComponent<AudioSource>() ?? fx.GetComponentInChildren<AudioSource>(true);
                float life = 3f;
                if (src != null)
                {
                    if (_clip != null) src.clip = _clip;
                    src.volume = Mathf.Clamp01(b.Hunter ? NpcaiSettings.HuntFxVolume.Value : NpcaiSettings.BurrowFxVolume.Value);
                    src.pitch = Mathf.Clamp(b.Hunter ? NpcaiSettings.HuntFxPitch.Value : NpcaiSettings.BurrowFxPitch.Value, 0.5f, 3f);
                    src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 1f; src.maxDistance = Mathf.Max(5f, NpcaiSettings.BurrowFxRange.Value * (b.Hunter ? 1f : 0.5f));
                    src.loop = false; src.dopplerLevel = 0f;
                    if (src.clip != null) { src.Play(); life = Mathf.Max(life, src.clip.length / src.pitch + 0.5f); }
                }
                _fxLive.Add(fx); _fxUntil.Add(Time.time + life);
            }
            catch (Exception e) { Core.Log.Warn(PresentationModule.Instance, "Burrow: effect: " + e.Message); }
        }

        // puffs whose time is up: off, and back to the free list (at most 6 kept; more are destroyed as before)
        /// <summary>Puffs whose time is up go back to the free list (at most 6 kept).</summary>
        private static void FxRecycle(float now)
        {
            for (int i = _fxLive.Count - 1; i >= 0; i--)
            {
                if (now < _fxUntil[i] && _fxLive[i] != null) continue;
                var fx = _fxLive[i];
                _fxLive.RemoveAt(i); _fxUntil.RemoveAt(i);
                if (fx == null) continue;
                if (_fxFree.Count < 6) { fx.SetActive(false); _fxFree.Add(fx); }
                else UnityEngine.Object.Destroy(fx);
            }
        }
    }
}
