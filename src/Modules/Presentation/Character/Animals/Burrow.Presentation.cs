using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>Burrow part owned by Presentation: the visual sink of the rig (LateTick) and the sand puff / sound FX pool (Burrower_Effect).</summary>
    internal sealed class AnimalVisuals : Core.IAnimalVisuals
    {
        /// <summary>Applies the requested burrow displacement in the actor's local plane.</summary>
        public void Sink(Transform rig,Transform actor,Vector3 origin,float depth) { if(rig!=null && actor!=null)rig.localPosition=origin+actor.InverseTransformVector(-actor.up*depth); }
        /// <summary>Applies the requested leap pitch to the authored rig rotation.</summary>
        public void Pitch(Transform rig,Quaternion origin,float pitch) { if(rig!=null)rig.localRotation=Quaternion.AngleAxis(-pitch,Vector3.right)*origin; }
        /// <summary>Applies the procedural neck/head bite and body displacement.</summary>
        public void Bite(Transform actor,Transform rig,Vector3 origin,Transform[] bones,float angle,float push) {
            if(actor==null)return; var axis=actor.right;
            if(bones!=null)for(int b=0;b<bones.Length;b++)if(bones[b]!=null)bones[b].rotation=Quaternion.AngleAxis(angle*(b==bones.Length-1?.4f:.3f),axis)*bones[b].rotation;
            if(rig!=null)rig.localPosition=origin+actor.InverseTransformVector(actor.forward*push);
        }
        /// <summary>Returns finished particles to the bounded Presentation pool.</summary>
        internal static void Tick() { if(_fxLive.Count>0)FxRecycle(Time.time); }
        /// <summary>Destroys all owned effects on scene reset or shutdown.</summary>
        internal static void Clear() { foreach(var fx in _fxLive)if(fx!=null)UnityEngine.Object.Destroy(fx); foreach(var fx in _fxFree)if(fx!=null)UnityEngine.Object.Destroy(fx); _fxLive.Clear();_fxFree.Clear();_fxUntil.Clear();_nextFind=0f;if(_effect==null)_clip=null; }
        private static GameObject _effect; private static AudioClip _clip; private static float _nextFind;

        // (WW perf) the sand puffs: finished ones are deactivated and reused (was Instantiate + Destroy per puff)
        private static readonly List<GameObject> _fxFree = new List<GameObject>();
        private static readonly List<GameObject> _fxLive = new List<GameObject>();
        private static readonly List<float> _fxUntil = new List<float>();
        private static readonly List<ParticleSystem> _psBuf = new List<ParticleSystem>();
        private static readonly List<PlayMakerFSM> _fsmBuf = new List<PlayMakerFSM>();

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
        public void Sand(Vector3 at,float scale,float volume,float pitch,float range,bool hunter)
        {
            var cam = Core.GameRefs.MainCamera;
            if (cam == null || (cam.transform.position - at).sqrMagnitude > range * range) return;
            FindEffect();
            if (_effect == null) return;
            try
            {
                float s = Mathf.Clamp(scale, 0.05f, 1f);
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
                    src.volume = Mathf.Clamp01(volume);
                    src.pitch = Mathf.Clamp(pitch, 0.5f, 3f);
                    src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 1f; src.maxDistance = Mathf.Max(5f, range * (hunter ? 1f : 0.5f));
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
