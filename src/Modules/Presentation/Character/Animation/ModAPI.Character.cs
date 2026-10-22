using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    public static partial class ModAPI
    {
        // One animated humanoid (ModAPI.Attach). The decisions are CharPlan's (the player's own logic, tested offline); this class applies them
        // like the player's Body does: a PlayableGraph on the character's Animator (layer 0 the 42 locomotion slots, 1 the whole-body actions,
        // 2 throws / strikes on the upper body), an invisible copy of its skeleton (the UpperRig) that plays the hands' clip and is written over its
        // spine and everything above, then the procedural touches (hips turn, chest follow, aim pitch, aim lift) and the weapon in the right hand.
        public sealed class Character
        {
            // ---------------------------------------------------------------- inputs (set them in Update; read in LateUpdate)
            /// <summary>The Animator this character drives.</summary>
            public Animator Animator { get { return _a; } }
            public bool Valid { get { return !_disposed && _a != null; } }
            // the planar velocity: by default the character's Rigidbody's (else its movement since the last frame); ManualVelocity = use Velocity
            public bool ManualVelocity;
            public Vector3 Velocity;
            public bool Crouched, Aiming, Firing, Airborne;
            public float AimPitch;                                 // degrees, + = looking down (the spine bends like the player's aim pitch)
            public bool TurnInPlace { get { return _p.TurnInPlace; } set { _p.TurnInPlace = value; } }
            public bool WalkToStop { get { return _p.WalkToStop; } set { _p.WalkToStop = value; } }
            // ---------------------------------------------------------------- state (read)
            /// <summary>Weapon-pose key of the held weapon ("" unarmed).</summary>
            public string WeaponKey { get { return _wKey ?? ""; } }
            /// <summary>Hold style of the held weapon (None, Rifle, Pistol, Melee, Throw).</summary>
            public string WeaponKind { get { return _kind.ToString(); } }
            /// <summary>A reload is playing.</summary>
            public bool Reloading { get { return _p.Reloading; } }
            /// <summary>A pump / bolt cocking is playing.</summary>
            public bool Pumping { get { return _p.Pumping; } }
            /// <summary>The clip that moves the hands now through the UpperRig ("" when none).</summary>
            public string HandsClip { get { return _p.RigEff > 0.01f ? _p.RigClip : ""; } }       // what moves the hands now (UpperRig)
            /// <summary>The whole-body action clip (kick, jump, turn) while it shows ("" when none).</summary>
            public string ActionClip { get { return _p.ActionW > 0.01f ? _p.ActionClip : ""; } }   // the whole-body action (kick, jump, turn)
            /// <summary>The upper-body one-shot (throw, strike) while it shows ("" when none).</summary>
            public string UpperClip { get { return _p.UpperW > 0.01f ? _p.UpperClip : ""; } }     // a throw / strike
            /// <summary>the fastest the body may move without its feet sliding (twice the clips' native speed: crouched / sprinting)</summary>
            public float SpeedCap { get { return 2f * (Crouched ? CharacterSettings.ClipCrouchSpeed.Value : Anims.Get("RifleSprint") != null ? CharacterSettings.ClipSprintSpeed.Value : CharacterSettings.ClipRunSpeed.Value); } }

            // ---------------------------------------------------------------- commands
            /// <summary>Moves a weapon model into the right hand (key from its name); null puts the previous one back.</summary>
            public void SetWeapon(Transform model) { SetWeapon(model, null); }
            /// <summary>the weapon model it holds (null = bare hands); weaponKey null = from the model's name. The model is moved to the right hand and posed per clip from the player's weapon-pose table; SetWeapon(null) / Suspended / Dispose put it back where it was.</summary>
            public void SetWeapon(Transform model, string weaponKey)
            {
                string key = weaponKey ?? (model != null ? Props.Norm(model.name) : "");
                if (model == _w && key == (_wKey ?? "")) return;
                ReleaseWeapon();
                _w = model; _wKey = key;
                _kind = model != null || key != "" ? Props.KindOf(key) : Props.Kind.None;
                _p.Kind = _kind == Props.Kind.Rifle ? LocoPlan.K_RIFLE : _kind == Props.Kind.Pistol ? LocoPlan.K_PISTOL : _kind == Props.Kind.Melee ? LocoPlan.K_MELEE
                        : _kind == Props.Kind.Throw ? LocoPlan.K_THROW : LocoPlan.K_NONE;
                if (!Suspended) GrabWeapon();
            }
            /// <summary>Plays a 0.25 s shot on the hands.</summary>
            public void Shoot() { _p.SetNow(Time.time); _p.Shoot(0.25f); }
            /// <summary>Plays a shot lasting the given seconds.</summary>
            public void Shoot(float seconds) { _p.SetNow(Time.time); _p.Shoot(seconds); }
            /// <summary>Plays the whole reload clip fitted to the given seconds.</summary>
            public void Reload(float seconds) { _p.SetNow(Time.time); _p.Reload(seconds, 0); }
            /// <summary>rounds &gt; 0: one round at a time (the reload clip's round part, rounds times over the seconds)</summary>
            public void Reload(float seconds, int rounds) { _p.SetNow(Time.time); _p.Reload(seconds, rounds); }
            /// <summary>Cancels a running reload.</summary>
            public void CancelReload() { _p.SetNow(Time.time); _p.CancelReload(); }
            /// <summary>ShotgunPump on the hands (rifle kind; not while reloading).</summary>
            public void Pump() { _p.SetNow(Time.time); _p.Pump(); }                       // ShotgunPump on the hands (rifle kind; not while reloading)
            /// <summary>The jump take-off; the caller keeps Airborne true while in the air.</summary>
            public void Jump() { _p.SetNow(Time.time); _p.Jump(); }                       // the take-off; keep Airborne true while in the air
            /// <summary>Plays a kick.</summary>
            public void Kick() { _p.SetNow(Time.time); _p.Kick(); }
            /// <summary>The quick grenade throw with the left hand.</summary>
            public void Throw() { _p.SetNow(Time.time); _p.Throw(false); }                // the quick grenade, left hand
            /// <summary>A throw with the chosen hand.</summary>
            public void Throw(bool rightHand) { _p.SetNow(Time.time); _p.Throw(rightHand); }
            /// <summary>A 0.5 s punch (bare hands) / melee swing.</summary>
            public void Strike() { _p.SetNow(Time.time); _p.Strike(0.5f); }
            /// <summary>A punch (bare hands) / melee swing taking the given seconds.</summary>
            public void Strike(float seconds) { _p.SetNow(Time.time); _p.Strike(seconds); }   // a punch (bare hands) / melee swing taking that long
            // the Animator's own controller shows (the graph is taken down, the weapon put back); false = the player's clips again
            public bool Suspended
            {
                get { return _suspended; }
                set
                {
                    if (value == _suspended || _disposed) return;
                    _suspended = value;
                    if (value) { ReleaseWeapon(); DestroyGraph(); }
                    else { BuildGraph(); GrabWeapon(); _p.Snap(); }
                }
            }
            /// <summary>The weapon model this character holds (null = unarmed).</summary>
            internal Transform HeldWeapon { get { return _w; } }
            /// <summary>Animation update tier (0 = every frame; higher = cheaper, distance LOD).</summary>
            internal int Tier { get { return _tier; } }
            /// <summary>Ends the character: weapon back, graphs and UpperRig destroyed, Animator settings restored, CharacterDetached published.</summary>
            public void Dispose()
            {
                if (_disposed) return;
                PresentationEvents.Detached(_a, CharacterKind.Human);
                ReleaseWeapon();
                DestroyGraph();
                try { if (_rg.IsValid()) _rg.Destroy(); } catch (Exception) { }
                if (_rigGo != null) UnityEngine.Object.Destroy(_rigGo);
                _disposed = true;
                ModAPI.Forget(this);
            }

            // ---------------------------------------------------------------- internals
            private readonly Animator _a;
            private readonly Transform _root;
            private readonly Rigidbody _rb;
            private readonly CharPlan _p = new CharPlan();
            private bool _disposed, _suspended, _rootMotionWas;
            private AnimatorCullingMode _cullingWas; private AnimatorUpdateMode _updateWas;
            private readonly Dictionary<HumanBodyBones, Transform> _b = new Dictionary<HumanBodyBones, Transform>();
            private AnimationClip _fallback;
            // main graph
            private PlayableGraph _g;
            private AnimationMixerPlayable _base;
            private readonly AnimationClipPlayable[] _bp = new AnimationClipPlayable[LocoPlan.BN];
            private AnimationLayerMixerPlayable _layers;
            private AnimationClipPlayable _act; private string _actName = "";
            private AnimationMixerPlayable _upMix; private AnimationClipPlayable _up0, _up1; private string _up0n = "", _up1n;
            // the UpperRig
            private PlayableGraph _rg; private GameObject _rigGo; private Animator _rigA; private AnimationPlayableOutput _rigOut;
            private AnimationMixerPlayable _rigMix; private AnimationClipPlayable _r0, _r1; private string _r0n = "", _r1n;
            private Transform _spine, _spineC, _rigLFoot, _rigRFoot;
            private readonly List<KeyValuePair<Transform, Transform>> _pairs = new List<KeyValuePair<Transform, Transform>>();
            // weapon
            private Transform _w, _wParent; private string _wKey = ""; private Props.Kind _kind = Props.Kind.None;
            private Vector3 _wPos, _wScale, _defPos; private Quaternion _wRot, _defRot; private bool _wMoved;
            private Vector3 _lastPos; private bool _hasLast;

            /// <summary>Maps the humanoid bones, wires CharPlan to the bundle's clips and builds the UpperRig and the main graph (disposed when the rig is not a usable humanoid).</summary>
            internal Character(Animator a)
            {
                _a = a; _root = a.transform;
                _rb = a.GetComponentInParent<Rigidbody>();
                foreach (HumanBodyBones hb in Enum.GetValues(typeof(HumanBodyBones)))
                {
                    if (hb == HumanBodyBones.LastBone) continue;
                    var t = a.GetBoneTransform(hb);
                    if (t != null) _b[hb] = t;
                }
                if (!_b.ContainsKey(HumanBodyBones.Hips) || !_b.ContainsKey(HumanBodyBones.Spine) || !_b.ContainsKey(HumanBodyBones.RightHand)) { _disposed = true; return; }
                _fallback = Anims.Get("RifleIdle");
                _p.Has = n => n != null && Anims.Get(n) != null;
                _p.Length = n => { var c = Anims.Get(n); return c != null ? c.length : 1f; };
                _p.Looping = n => { var c = Anims.Get(n); return c != null && c.isLooping; };
                _p.PhaseOf = PhaseOf;
                _p.NativeOf = NativeOf;
                ReadSettings();
                _p.Build();
                BuildRig();
                BuildGraph();
            }

            /// <summary>Copies the [Presentation / Animation] tuning into the character's CharPlan.</summary>
            private void ReadSettings()
            {
                _p.RunFrom = CharacterSettings.RunFrom.Value; _p.SprintFrom = CharacterSettings.SprintFrom.Value; _p.DirBlendSpeed = CharacterSettings.DirBlendSpeed.Value; _p.RunLegsTurn = CharacterSettings.RunLegsTurn.Value;
                _p.UpperPhase = CharacterSettings.UpperPhase.Value; _p.PumpSpeed = CharacterSettings.PumpClipSpeed.Value; _p.StrikeWindup = CharacterSettings.StrikeWindup.Value;
                _p.WalkNative = CharacterSettings.ClipWalkSpeed.Value; _p.RunNative = CharacterSettings.ClipRunSpeed.Value; _p.SprintNative = CharacterSettings.ClipSprintSpeed.Value; _p.CrouchNative = CharacterSettings.ClipCrouchSpeed.Value;
                _p.JumpClipStart = CharacterSettings.JumpClipStart.Value; _p.TurnStartRate = CharacterSettings.TurnStartRate.Value;
                _p.TurnInPlace = CharacterSettings.TurnClipAngle.Value > 0f; _p.WalkToStop = CharacterSettings.WalkToStop.Value;
            }

            // ---- the main graph (rebuilt when un-suspended)
            /// <summary>Builds the main PlayableGraph (42 locomotion slots, action layer, masked upper layer) and switches the Animator to always-animate, no root motion.</summary>
            private void BuildGraph()
            {
                if (_g.IsValid() || _a == null) return;
                _g = PlayableGraph.Create("Apocaplayer.ModAPI." + _a.gameObject.name);
                _g.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                var output = AnimationPlayableOutput.Create(_g, "body", _a);
                _base = AnimationMixerPlayable.Create(_g, LocoPlan.BN);
                for (int i = 0; i < LocoPlan.BN; i++)
                {
                    _bp[i] = AnimationClipPlayable.Create(_g, Anims.Get(_p.SlotClip[i]) ?? _fallback);
                    _bp[i].SetApplyFootIK(CharacterSettings.FootIK.Value && (i < LocoPlan.B_RUN || (i >= LocoPlan.B_CIDLE && i != LocoPlan.B_RRUN && i != LocoPlan.B_RSPRINT)));
                    _bp[i].SetSpeed(0);
                    _g.Connect(_bp[i], 0, _base, i);
                    _base.SetInputWeight(i, i == LocoPlan.B_IDLE ? 1f : 0f);
                }
                _layers = AnimationLayerMixerPlayable.Create(_g, 3);
                _g.Connect(_base, 0, _layers, 0); _layers.SetInputWeight(0, 1f);
                _act = Clip(_g, _fallback); _actName = "";
                _g.Connect(_act, 0, _layers, 1); _layers.SetInputWeight(1, 0f);
                _upMix = AnimationMixerPlayable.Create(_g, 2);
                _up0 = Clip(_g, _fallback); _up0n = ""; _up1n = null;
                _g.Connect(_up0, 0, _upMix, 0); _upMix.SetInputWeight(0, 1f); _upMix.SetInputWeight(1, 0f);
                _g.Connect(_upMix, 0, _layers, 2); _layers.SetInputWeight(2, 0f);
                _layers.SetLayerMaskFromAvatarMask(2u, CharacterRig.UpperMask());
                output.SetSourcePlayable(_layers);
                _rootMotionWas = _a.applyRootMotion; _a.applyRootMotion = false;
                // always evaluated while the player's clips drive it: the procedural turns after the Animator are applied to its pose of this
                // frame - a culled Animator would leave last frame's bones and they would add up
                _cullingWas = _a.cullingMode; _a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _updateWas = _a.updateMode; _a.updateMode = AnimatorUpdateMode.Normal;      // every frame, not only on physics steps (same reason)
                _g.Play();
                _p.Snap();
            }
            /// <summary>Destroys the main graph and restores the Animator's root motion, culling and update modes.</summary>
            private void DestroyGraph()
            {
                try { if (_g.IsValid()) { _g.Destroy(); if (_a != null) { _a.applyRootMotion = _rootMotionWas; _a.cullingMode = _cullingWas; _a.updateMode = _updateWas; } } } catch (Exception) { }
            }
            /// <summary>A paused clip playable without foot IK.</summary>
            private static AnimationClipPlayable Clip(PlayableGraph g, AnimationClip c)
            {
                var p = AnimationClipPlayable.Create(g, c);
                p.SetApplyFootIK(false); p.SetSpeed(0);
                return p;
            }
            // ---- the UpperRig: an invisible, stripped copy of the character's Animator object (bones + Animator only), on its own manual graph
            private static GameObject _rigRoot;
            /// <summary>Builds the UpperRig: an invisible skeleton copy with its own manual graph whose upper-body bones are copied onto the character.</summary>
            private void BuildRig()
            {
                try
                {
                    if (_rigRoot == null) { _rigRoot = new GameObject("Apocaplayer.ModAPI.Rigs") { hideFlags = HideFlags.HideAndDontSave }; UnityEngine.Object.DontDestroyOnLoad(_rigRoot); }
                    var holder = new GameObject("Apocaplayer.ModAPI.RigHolder");
                    holder.SetActive(false);                       // nothing in the copy wakes up before it is complete
                    // (B05 perf, README 8 #3) a Transform-only copy of the Animator object's hierarchy (same names, paths and local poses) plus
                    // an Animator with the same avatar, instead of instantiating the whole NPC (FSMs, colliders, renderers) and stripping it
                    var go = CopySkeleton(_a.transform, holder.transform);
                    go.name = "UpperRig." + _a.gameObject.name;
                    var keep = go.AddComponent<Animator>();
                    keep.avatar = _a.avatar;
                    // nothing in the game may take the copy for the character: no tag, no layer, hidden
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    {
                        t.gameObject.layer = 0;
                        try { t.gameObject.tag = "Untagged"; } catch (Exception) { }
                        t.gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                    }
                    go.transform.SetParent(_rigRoot.transform, false);
                    go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = _root.lossyScale;
                    go.SetActive(true);
                    UnityEngine.Object.Destroy(holder);
                    _rigGo = go; _rigA = keep;
                    if (_rigA == null) { UnityEngine.Object.Destroy(go); _rigGo = null; return; }
                    _rigA.runtimeAnimatorController = null;
                    _rigA.applyRootMotion = false; _rigA.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    _rigA.enabled = true;
                    _spine = _b[HumanBodyBones.Spine]; _spineC = _rigA.GetBoneTransform(HumanBodyBones.Spine);
                    _rigLFoot = _rigA.GetBoneTransform(HumanBodyBones.LeftFoot); _rigRFoot = _rigA.GetBoneTransform(HumanBodyBones.RightFoot);
                    if (_spineC == null) { UnityEngine.Object.Destroy(go); _rigGo = null; return; }
                    // the skeleton above the spine: every bone its skinned meshes use (Spine1 / Spine2 too when the avatar maps only some of them) and every
                    // humanoid bone, found in the copy by its path - props under the hands (the gun moved to the right hand ...) are not bones and are left alone
                    var bones = new HashSet<Transform>();
                    foreach (var smr in _a.GetComponentsInChildren<SkinnedMeshRenderer>(true)) foreach (var t in smr.bones) if (t != null) bones.Add(t);
                    foreach (var kv in _b) bones.Add(kv.Value);
                    foreach (var t in bones)
                    {
                        if (t == _spine || !t.IsChildOf(_spine)) continue;
                        var c = go.transform.Find(PathOf(t, _root));
                        if (c != null) _pairs.Add(new KeyValuePair<Transform, Transform>(t, c));
                    }
                    _rg = PlayableGraph.Create("Apocaplayer.ModAPI.UpperRig." + _a.gameObject.name);
                    _rg.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    _rigOut = AnimationPlayableOutput.Create(_rg, "upper", _rigA);
                    _rigMix = AnimationMixerPlayable.Create(_rg, 2);
                    _r0 = Clip(_rg, _fallback); _r0n = ""; _r1n = null;
                    _rg.Connect(_r0, 0, _rigMix, 0); _rigMix.SetInputWeight(0, 1f); _rigMix.SetInputWeight(1, 0f);
                    _rigOut.SetSourcePlayable(_rigMix);
                    _rg.Play();
                }
                catch (Exception e) { Core.Log.Warn(PresentationModule.Instance, "ModAPI: no upper rig for " + _a.gameObject.name + " (" + e.Message + ") - the legs' clips move its hands"); _pairs.Clear(); _rigGo = null; }
            }

            /// <summary>Copies <paramref name="src"/> and its whole child hierarchy as bare GameObjects (name and local pose only) under <paramref name="parent"/>.</summary>
            private static GameObject CopySkeleton(Transform src, Transform parent)
            {
                var go = new GameObject(src.name);
                var t = go.transform;
                t.SetParent(parent, false);
                t.localPosition = src.localPosition; t.localRotation = src.localRotation; t.localScale = src.localScale;
                for (int i = 0; i < src.childCount; i++) CopySkeleton(src.GetChild(i), t);
                return go;
            }

            /// <summary>Slash path of a transform below <paramref name="root"/>.</summary>
            private static string PathOf(Transform t, Transform root)
            {
                var sb = new System.Text.StringBuilder(t.name);
                for (var x = t.parent; x != null && x != root; x = x.parent) sb.Insert(0, x.name + "/");
                return sb.ToString();
            }

            // ---- per clip, measured once on a rig (shared by every character): the stride phase, the native ground speed
            private static readonly Dictionary<string, float> _phases = new Dictionary<string, float>(), _natives = new Dictionary<string, float>();
            /// <summary>Stride phase of a clip, measured once on an UpperRig and shared by every character (-1 = none / not measurable yet).</summary>
            private float PhaseOf(string clip)
            {
                float p;
                if (string.IsNullOrEmpty(clip)) return -1f;
                if (_phases.TryGetValue(clip, out p)) return p;
                if (!_rg.IsValid()) return -1f;                      // no rig to measure on: not cached, another character may measure it
                p = Measure(Anims.Get(clip));
                _phases[clip] = p;
                return p;
            }
            /// <summary>Samples a clip on the UpperRig and derives the stride phase from the feet's height difference.</summary>
            private float Measure(AnimationClip c)
            {
                if (c == null || !_rigOut.IsOutputValid() || _rigLFoot == null || _rigRFoot == null) return -1f;
                var tmp = default(AnimationClipPlayable);
                try
                {
                    tmp = AnimationClipPlayable.Create(_rg, c);
                    tmp.SetApplyFootIK(false); tmp.SetSpeed(0);
                    _rigOut.SetSourcePlayable(tmp);
                    var d = new float[24];
                    var rt = _rigA.transform;
                    for (int k = 0; k < d.Length; k++)
                    {
                        tmp.SetTime(k * c.length / d.Length);
                        _rg.Evaluate(0f);
                        d[k] = rt.InverseTransformPoint(_rigLFoot.position).y - rt.InverseTransformPoint(_rigRFoot.position).y;
                    }
                    return LocoPlan.PhaseFromHeights(d);
                }
                catch (Exception) { return -1f; }
                finally
                {
                    if (_rigOut.IsOutputValid()) _rigOut.SetSourcePlayable(_rigMix);
                    if (tmp.IsValid()) tmp.Destroy();
                }
            }
            /// <summary>Native ground speed of a clip (its average root speed), or the fallback when the clip has none.</summary>
            private static float NativeOf(string clip, float fallback)
            {
                float v;
                if (clip == null) return fallback;
                if (!_natives.TryGetValue(clip, out v))
                {
                    var c = Anims.Get(clip);
                    v = c != null ? new Vector2(c.averageSpeed.x, c.averageSpeed.z).magnitude : 0f;
                    _natives[clip] = v;
                }
                return v > 0.3f ? v : fallback;
            }

            // ---- the weapon in the right hand
            /// <summary>Moves the weapon model into the right hand and remembers where it was (a left-hand gun is mirrored into the right hand).</summary>
            private void GrabWeapon()
            {
                if (_w == null || _wMoved || _kind == Props.Kind.None) return;
                Transform hand; if (!_b.TryGetValue(HumanBodyBones.RightHand, out hand)) return;
                Transform left; _b.TryGetValue(HumanBodyBones.LeftHand, out left);
                _wParent = _w.parent; _wPos = _w.localPosition; _wRot = _w.localRotation; _wScale = _w.localScale;
                // without an entry in the pose table: its own grip (a left-hand gun mirrored into the right hand, as the player's props)
                if (left != null && _wParent == left) Props.MirrorToRight(_wPos, _wRot, out _defPos, out _defRot);
                else if (_wParent == hand) { _defPos = _wPos; _defRot = _wRot; }
                else { _defPos = hand.InverseTransformPoint(_w.position); _defRot = Quaternion.Inverse(hand.rotation) * _w.rotation; }
                _w.SetParent(hand, false);
                _w.localScale = _wScale;
                _wMoved = true;
                PoseWeapon();
            }
            /// <summary>Puts the weapon model back where it was.</summary>
            private void ReleaseWeapon()
            {
                if (!_wMoved) return;
                _wMoved = false;
                if (_w != null) { _w.SetParent(_wParent, false); _w.localPosition = _wPos; _w.localRotation = _wRot; _w.localScale = _wScale; }
            }

            private readonly Dictionary<string, float> _idleOnly = new Dictionary<string, float>(1);
            /// <summary>Places the weapon in the hand from the weapon-pose table, blended by the hand clips' weights.</summary>
            private void PoseWeapon()
            {
                if (!_wMoved || _w == null) return;
                Vector3 p; Quaternion r;
                if (_p.PoseW.Count == 0) { _idleOnly.Clear(); _idleOnly[IdleOf(_kind)] = 1f; GunPose.BlendFor(_wKey, _idleOnly, IdleOf(_kind), _defPos, _defRot, out p, out r); }   // (B05 perf) no Dictionary per frame
                else GunPose.BlendFor(_wKey, _p.PoseW, IdleOf(_kind), _defPos, _defRot, out p, out r);
                _w.localPosition = p; _w.localRotation = r;
            }

            // ---------------------------------------------------------------- every frame (LateUpdate, after the Animator)
            private int _errors;
            /// <summary>LateUpdate tick: animates the character (skipped while suspended, inactive or LOD-skipped); three errors suspend it.</summary>
            internal void Tick()
            {
                if (_disposed) return;
                if (_a == null) { Dispose(); return; }
                if (_suspended) { _hasLast = false; return; }
                if (!_a.isActiveAndEnabled) { _hasLast = false; return; }
                float dt = Time.deltaTime;
                if (SkipByLod(ref dt)) return;
                try { TickInner(dt); }
                catch (Exception e)
                {
                    if (++_errors <= 3) Core.Log.Error(PresentationModule.Instance, "ModAPI " + _a.gameObject.name + ": " + e);
                    if (_errors >= 3) { Core.Log.Warn(PresentationModule.Instance, "ModAPI: " + _a.gameObject.name + " suspended after errors"); Suspended = true; }
                }
            }

            // ---- (B05 perf, README 8 #3) distance / visibility LOD: tier 1 = no renderer of the character visible and more than LodDistance
            // from the player camera: the decision + procedural pass runs every LodStride-th frame with the frames' summed delta time (the
            // Animator keeps evaluating the graph every frame, so no stale bones add up). Visible or near = tier 0 (every frame).
            private const float LodDistance = 40f; private const int LodStride = 3;
            private int _tier, _lodSkipped; private float _lodDt;
            private SkinnedMeshRenderer[] _renderers;
            /// <summary>True when this frame's tick is skipped by the LOD (its delta time is kept); otherwise <paramref name="dt"/> includes the skipped frames.</summary>
            private bool SkipByLod(ref float dt)
            {
                if (!CharacterSettings.AnimationLod.Value || !ModAPI.CameraKnown || AnyVisible() || (ModAPI.CameraPosition - _root.position).sqrMagnitude < LodDistance * LodDistance)
                { _tier = 0; dt += _lodDt; _lodDt = 0f; _lodSkipped = 0; return false; }
                _tier = 1;
                if (++_lodSkipped < LodStride && dt > 0f) { _lodDt += dt; return true; }
                dt += _lodDt; _lodDt = 0f; _lodSkipped = 0;
                return false;
            }
            /// <summary>True when any skinned renderer of the character was visible to a camera last frame.</summary>
            private bool AnyVisible()
            {
                if (_renderers == null) _renderers = _a.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < _renderers.Length; i++) { var r = _renderers[i]; if (r != null && r.isVisible) return true; }
                return _renderers.Length == 0;
            }

            /// <summary>One animation step: CharPlan decides from the velocity and stance, the graph is set for the next frame, then this frame's pose gets the hand rig, spine turns, aim pitch, aim lift, arm poses and the weapon pose.</summary>
            private void TickInner(float dt)
            {
                if (!_g.IsValid()) BuildGraph();
                float now = Time.time;
                if (dt <= 0f) return;                                            // paused: the pose stays (nothing evaluated, nothing added)
                if (_w == null && _wMoved) _wMoved = false;                      // the weapon model was destroyed
                // velocity in its own frame
                Vector3 fwd = _root.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
                var yaw = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                Vector3 v;
                if (ManualVelocity) v = Velocity;
                else if (_rb != null && !_rb.isKinematic) v = _rb.velocity;
                else v = _hasLast && dt > 1e-4f ? (_root.position - _lastPos) / dt : Vector3.zero;
                _lastPos = _root.position; _hasLast = true;
                v.y = 0f;
                var local = Quaternion.Inverse(yaw) * v;
                _p.Lx = local.x; _p.Lz = local.z; _p.Yaw = yaw.eulerAngles.y;
                _p.Crouch = Crouched; _p.Aim = Aiming; _p.Fire = Firing; _p.Airborne = Airborne;
                _p.Logging = Core.Log.L2;
                _p.Step(now, dt);
                if (Core.Log.L2 && _p.Log != "") Core.Log.Deep(PresentationModule.Instance, "ModAPI " + _a.gameObject.name + ": " + _p.Log);   // (B05) ex [PlayerBody / ModAPI] VerboseModApi: Debug level 2

                // ---- next frame's graph (the Animator evaluates it before the next LateUpdate)
                for (int i = 0; i < LocoPlan.BN; i++)
                {
                    _base.SetInputWeight(i, _p.W[i]);
                    if (_p.W[i] > 0.0001f) _bp[i].SetTime(_p.SlotTime[i]);
                }
                if (_p.ActionClip != "" && _p.ActionClip != _actName)
                {
                    _g.Disconnect(_layers, 1); _act.Destroy();
                    _act = Clip(_g, Anims.Get(_p.ActionClip) ?? _fallback); _actName = _p.ActionClip;
                    _g.Connect(_act, 0, _layers, 1);
                }
                _layers.SetInputWeight(1, _p.ActionW);
                _act.SetTime(_p.ActionTime);
                Sync2(_g, _upMix, ref _up0, ref _up0n, ref _up1, ref _up1n, _p.UpperClip, _p.UpperOld);
                _layers.SetInputWeight(2, _p.UpperW);
                _up0.SetTime(_p.UpperTime); if (_up1.IsValid()) _up1.SetTime(_p.UpperOldTime);
                _upMix.SetInputWeight(0, _up1.IsValid() ? _p.UpperX : 1f); _upMix.SetInputWeight(1, _up1.IsValid() ? 1f - _p.UpperX : 0f);

                // ---- this frame's pose (the Animator evaluated it): the hands from the rig, then the procedural touches
                if (Mathf.Abs(_p.HipTurn) > 0.2f && _p.ActionW < 0.5f)
                {
                    Turn(HumanBodyBones.Hips, Vector3.up, _p.HipTurn);
                    if (_p.RigWant < 0.5f) { Turn(HumanBodyBones.Spine, Vector3.up, -_p.HipTurn * 0.5f); Turn(HumanBodyBones.Chest, Vector3.up, -_p.HipTurn * 0.5f); }
                }
                RigApply();
                if (_p.ChestFollow > 0.01f) TwistSpine(_p.ChestFollow);
                // the hips stay over the character (clips with forward motion baked into the pose)
                Transform hips;
                if (_b.TryGetValue(HumanBodyBones.Hips, out hips))
                {
                    var hl = _root.InverseTransformPoint(hips.position);
                    var xz = Vector2.ClampMagnitude(new Vector2(hl.x, hl.z), CharacterSettings.HipsDrift.Value);
                    if (xz.x != hl.x || xz.y != hl.z) hips.position = _root.TransformPoint(new Vector3(xz.x, hl.y, xz.y));
                }
                float pitch = AimPitch * CharacterSettings.AimPitchShare.Value, headPitch = AimPitch;
                if (Mathf.Abs(pitch) > 0.5f)
                {
                    Turn(HumanBodyBones.Spine, Vector3.right, pitch * 0.3f);
                    Turn(HumanBodyBones.Chest, Vector3.right, pitch * 0.3f);
                    Turn(HumanBodyBones.UpperChest, Vector3.right, pitch * 0.3f);
                    Turn(HumanBodyBones.Neck, Vector3.right, pitch * 0.1f);
                    headPitch -= pitch * ((_b.ContainsKey(HumanBodyBones.Spine) ? .3f : 0f) + (_b.ContainsKey(HumanBodyBones.Chest) ? .3f : 0f) + (_b.ContainsKey(HumanBodyBones.UpperChest) ? .3f : 0f) + (_b.ContainsKey(HumanBodyBones.Neck) ? .1f : 0f));
                }
                Turn(HumanBodyBones.Head, Vector3.right, Mathf.Clamp(headPitch, -65f, 65f));
                AimLiftApply();
                ArmPoseApply();
                PoseWeapon();
            }

            /// <summary>the two-input mixer to show 'cur' (input 0) with 'old' fading out under it (input 1)</summary>
            private AnimationClipPlayable Make(PlayableGraph g, string name) { return Clip(g, Anims.Get(name) ?? _fallback); }
            /// <summary>Shows clip 'cur' on mixer input 0 with 'old' fading out on input 1 (creates/destroys clip playables only when the names change).</summary>
            private void Sync2(PlayableGraph g, AnimationMixerPlayable mix, ref AnimationClipPlayable c0, ref string n0, ref AnimationClipPlayable c1, ref string n1, string cur, string old)
            {
                if (string.IsNullOrEmpty(cur)) cur = n0;                   // nothing new: keep the last one (fading out by the layer weight)
                if (cur != n0)
                {
                    if (old != null && old == n0)
                    {   // the shown clip becomes the one fading out
                        if (c1.IsValid()) { g.Disconnect(mix, 1); c1.Destroy(); }
                        g.Disconnect(mix, 0);
                        c1 = c0; n1 = n0; g.Connect(c1, 0, mix, 1);
                    }
                    else { g.Disconnect(mix, 0); if (c0.IsValid()) c0.Destroy(); }
                    c0 = Make(g, cur); n0 = cur; g.Connect(c0, 0, mix, 0);
                }
                if (old != n1)
                {
                    if (c1.IsValid()) { g.Disconnect(mix, 1); c1.Destroy(); }
                    c1 = default(AnimationClipPlayable); n1 = null;
                    if (old != null && old != cur) { c1 = Make(g, old); n1 = old; g.Connect(c1, 0, mix, 1); }
                }
            }

            /// <summary>Evaluates the UpperRig at the hand clip's time and copies its spine and upper-body bones onto the character by the rig weight.</summary>
            private void RigApply()
            {
                float eff = _p.RigEff;
                if (!_rg.IsValid() || _spineC == null || eff < 0.001f || string.IsNullOrEmpty(_p.RigClip)) return;
                Sync2(_rg, _rigMix, ref _r0, ref _r0n, ref _r1, ref _r1n, _p.RigClip, _p.RigOld);
                _r0.SetTime(_p.RigTime); if (_r1.IsValid()) _r1.SetTime(_p.RigOldTime);
                _rigMix.SetInputWeight(0, _r1.IsValid() ? _p.RigX : 1f); _rigMix.SetInputWeight(1, _r1.IsValid() ? 1f - _p.RigX : 0f);
                _rg.Evaluate(0f);
                var target = _root.rotation * (Quaternion.Inverse(_rigA.transform.rotation) * _spineC.rotation);
                _spine.rotation = Quaternion.Slerp(_spine.rotation, target, eff);
                for (int i = 0; i < _pairs.Count; i++)
                {
                    var pr = _pairs[i];
                    pr.Key.localRotation = Quaternion.Slerp(pr.Key.localRotation, pr.Value.localRotation, eff);
                }
            }

            /// <summary>Lifts both hands (and tilts the head) by the weapon's aim-lift entry while aiming.</summary>
            private void AimLiftApply()
            {
                if (_p.AimLiftW < 0.001f || _p.AimLiftClip == null || string.IsNullOrEmpty(_wKey)) return;
                var v = AimLift.Get(_wKey, _p.AimLiftClip, _p.AimCrouched);
                float w = _p.AimLiftW;
                var d = (_root.up * v[0] + _root.forward * v[2]) * (0.01f * w);
                if (d.sqrMagnitude > 1e-8f)
                {
                    LiftArm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, d);
                    LiftArm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, d);
                }
                if (Mathf.Abs(v[1]) > 0.01f) Turn(HumanBodyBones.Head, Vector3.right, v[1] * w);
            }
            private readonly float[] _armPose = new float[10];
            /// <summary>Applies the weapon's per-clip arm offsets (aim-lift table values 3..12) blended by the hand clips' weights.</summary>
            private void ArmPoseApply()
            {
                Array.Clear(_armPose, 0, _armPose.Length);
                if (string.IsNullOrEmpty(_wKey)) return;
                foreach (var pose in _p.PoseW)
                {
                    var v = AimLift.Get(_wKey, pose.Key, _p.CrouchW > .5f);
                    for (int i = 0; i < _armPose.Length; i++) _armPose[i] += v[i + 3] * pose.Value;
                }
                for (int arm = 0; arm < 2; arm++)
                {
                    int i = arm * 5;
                    var upper = arm == 0 ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
                    var lower = arm == 0 ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
                    var hand = arm == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                    var d = (_root.up * _armPose[i] + _root.right * _armPose[i + 1]) * .01f;
                    if (d.sqrMagnitude > 1e-8f) LiftArm(upper, lower, hand, d);
                    if (Mathf.Abs(_armPose[i + 2]) > .01f) Turn(upper, Vector3.right, _armPose[i + 2]);
                    if (Mathf.Abs(_armPose[i + 3]) > .01f) Turn(upper, Vector3.up, _armPose[i + 3]);
                    if (Mathf.Abs(_armPose[i + 4]) > .01f) Turn(upper, Vector3.forward, _armPose[i + 4]);
                }
            }
            /// <summary>Moves a hand by <paramref name="d"/> with the shared two-bone elbow solve, keeping the hand's rotation.</summary>
            private void LiftArm(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand, Vector3 d)
            {
                Transform s, e, h;
                if (!_b.TryGetValue(upper, out s) || !_b.TryGetValue(lower, out e) || !_b.TryGetValue(hand, out h)) return;
                CharacterRig.LiftArm(s, e, h, d);
            }

            /// <summary>Yaw of the line a-b in the character's frame (hips or shoulders direction), degrees.</summary>
            private float YawIn(Transform a, Transform b) { return CharacterRig.YawIn(_root, a, b); }
            /// <summary>Turns the spine so the chest follows the shoulders' facing by <paramref name="follow"/> (LocoPlan.Twist).</summary>
            private void TwistSpine(float follow)
            {
                Transform lh, rh, la, ra;
                if (!_b.TryGetValue(HumanBodyBones.LeftUpperLeg, out lh) || !_b.TryGetValue(HumanBodyBones.RightUpperLeg, out rh)
                    || !_b.TryGetValue(HumanBodyBones.LeftUpperArm, out la) || !_b.TryGetValue(HumanBodyBones.RightUpperArm, out ra)) return;
                float s0, s1, s2, nk;
                LocoPlan.Twist(YawIn(lh, rh), YawIn(la, ra), follow, out s0, out s1, out s2, out nk);
                Turn(HumanBodyBones.Spine, Vector3.up, s0);
                Turn(HumanBodyBones.Chest, Vector3.up, s1);
                Turn(HumanBodyBones.UpperChest, Vector3.up, s2);
                Turn(HumanBodyBones.Neck, Vector3.up, nk);
            }
            /// <summary>a bone turned by deg about an axis given in the character's own space (Body.Turn)</summary>
            private void Turn(HumanBodyBones bone, Vector3 axis, float deg)
            {
                Transform t;
                if (_b.TryGetValue(bone, out t)) CharacterRig.Turn(t, _root, axis, deg);
            }
        }
    }
}
