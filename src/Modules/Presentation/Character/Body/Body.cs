using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    // Her body: a clone of Flexa's "Anim" object (Animator with the humanoid avatar enemy_1_IdleAvatar + the 22 mixamorig bones +
    // the skinned mesh), stripped of props/colliders/FSMs, with our mesh and texture. Animated by a PlayableGraph with the game's own
    // humanoid clips (idle, run, rifle / pistol aim, melee swing), plus procedural layers in LateUpdate: walk cycle, strafe twist,
    // aim pitch, crouch, prone. In a car the Animator is off and the pose is copied from the car's seated driver (CarSeat).
    internal sealed partial class Body
    {
        public GameObject Root;          // mirror lives here (scale x -1)
        private Transform _anim;         // clone of Flexa/Anim
        private Ragdoll _ragdoll;
        private Equipment _equipment;

        public void LateEquipment(bool on)
        {
            // (WW) [PlayerBody] EquipmentOnBody off: the mounts are hidden (and not updated) until it is on again
            if (!on) { if (_equipment != null) _equipment.SetVisible(false, false); return; }
            if (_equipment == null) _equipment = new Equipment(this);
            _equipment.Tick();
            _equipment.SetVisible(_smr.enabled, _shadow.enabled);
        }
        public bool IsRagdoll { get { return _ragdoll != null; } }
        internal bool TryOcclusionPoints(out Vector3 head, out Vector3 feet)
        {
            Transform h, left, right;
            if (!Bones.TryGetValue("mixamorig:Head", out h) || !Bones.TryGetValue("mixamorig:LeftFoot", out left) || !Bones.TryGetValue("mixamorig:RightFoot", out right))
            { head = feet = Vector3.zero; return false; }
            head = h.position + Vector3.up * .15f; feet = (left.position + right.position) * .5f;
            return true;
        }
        public Vector3 DeathFocus { get { return _ragdoll != null ? _ragdoll.Focus : Root.transform.position; } }

        public void LateDead(Vector3 velocity, Transform vehicle)
        {
            if (_ragdoll == null)
            {
                if (_graph.IsValid()) _graph.Stop();
                _animator.enabled = false;
                ShowProp(false);
                SetMesh(false, false);
                SetVisible(true, false);
                SetFirstPersonArms(false);
                _ragdoll = new Ragdoll(Bones, velocity, vehicle);
                Core.Log.Deep(PresentationModule.Instance, "Death: animation stopped, jointed ragdoll activated");
            }
            _ragdoll.Apply();
        }
        private RootMotionTap _rootMotion;
        private Vector3 _sway;           // standing still in third person: the clips' own sideways/forward root motion (Idle sways 16 cm), Anim-parent space

        // the Animator's root motion each evaluation (OnAnimatorMove = handled by script: nothing moves by itself)
        internal sealed class RootMotionTap : MonoBehaviour
        {
            private Animator _a;
            public Vector3 Delta;
            private void OnAnimatorMove()
            {
                if (_a == null) _a = GetComponent<Animator>();
                if (_a != null) Delta += _a.deltaPosition;
            }
            public Vector3 Take() { var d = Delta; Delta = Vector3.zero; return d; }
        }
        private Animator _animator;
        private SkinnedMeshRenderer _smr, _shadow, _fpArms, _inner;
        private bool _innerOn;
        private Material _mat;
        public readonly Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Quaternion> _bindLocal = new Dictionary<string, Quaternion>();

        private PlayableGraph _graph;
        private AnimationMixerPlayable _loco;
        private AnimationLayerMixerPlayable _layers;
        private AnimationClipPlayable _upper;
        // upper layer = a 2-input mixer: _upper (the current clip, input 0) and _upperOld (the clip it replaced, input 1) cross-faded.
        // The new clip always starts at once at its own time and speed - only the old pose fades out under it - so nothing gets longer.
        private AnimationMixerPlayable _upperMix;
        private AnimationClipPlayable _upperOld;
        private float _upperX = 1f, _upperW, _upperWTarget;
        private const float UpperIn = 0.08f, UpperOut = 0.15f, UpperCross = 0.12f;
        // the layer mixer's inputs: 0 the base, then the upper-body layer (strikes / throws, masked) and the whole-body actions (kick, jumps,
        // turns). (2.1.9) In Mixamo mode the upper layer is ABOVE the actions (index 2): a grenade thrown or a punch mid-jump shows on top
        private int _lUpper = 1, _lAction = 2;

        private void MakeUpper(AnimationClip c)
        {
            _upperMix = AnimationMixerPlayable.Create(_graph, 2);
            _upper = AnimationClipPlayable.Create(_graph, c);
            _graph.Connect(_upper, 0, _upperMix, 0);
            _upperMix.SetInputWeight(0, 1f); _upperMix.SetInputWeight(1, 0f);
            _graph.Connect(_upperMix, 0, _layers, _lUpper);
            _layers.SetInputWeight(_lUpper, 0f);
            _upperW = _upperWTarget = 0f; _upperX = 1f;
        }

        // every frame: the upper layer's weight eases toward what SetUpper asked (fast in, a little slower out), the cross-fade advances
        private void TickUpper(float dt)
        {
            if (!_layers.IsValid() || !_upperMix.IsValid()) return;
            if (_snap) _upperW = _upperWTarget;
            else _upperW = Mathf.MoveTowards(_upperW, _upperWTarget, dt / (_upperWTarget > _upperW ? UpperIn : UpperOut));
            _layers.SetInputWeight(_lUpper, _upperW);
            if (_upperOld.IsValid())
            {
                _upperX = _snap ? 1f : Mathf.MoveTowards(_upperX, 1f, dt / UpperCross);
                _upperMix.SetInputWeight(0, _upperX); _upperMix.SetInputWeight(1, 1f - _upperX);
                if (_upperX >= 1f) { _graph.Disconnect(_upperMix, 1); _upperOld.Destroy(); }
            }
            else { _upperMix.SetInputWeight(0, 1f); _upperMix.SetInputWeight(1, 0f); }
        }
        private string _upperClip = "";
        private AnimationClip _fallback;
        private bool _mixamo;                       // locomotion from the animation bundle (Mixamo clips) instead of the game's clips + procedural walk
        private float _reloadUntil, _throwUntil, _reloadStart;
        private bool _reloading;
        private string _kickState = "", _jumpState = "";
        private AnimationClipPlayable _action;
        private string _actionClip = "";
        private float _actionUntil, _actionW;

        private float _jumpAt = -10f;
        private void StartAction(string name, float startFraction = 0f)
        {
            var c = Anims.Get(name);
            if (c == null || !_action.IsValid()) return;
            if (_actionClip != name)
            {
                _actionClip = name;
                _graph.Disconnect(_layers, _lAction);
                _action.Destroy();
                _action = AnimationClipPlayable.Create(_graph, c);
                _action.SetApplyFootIK(false);   // jumps / turns / kick: no foot IK (2.0.2)
                _graph.Connect(_action, 0, _layers, _lAction);
            }
            float t0 = Mathf.Clamp01(startFraction) * c.length;   // jumps: skip the clip's crouch before take-off, the game pushes her up at once
            _action.SetTime(t0); _action.SetSpeed(1);
            _actionUntil = Time.time + c.length - t0;
        }

        private void UpdateAction(float dt)
        {
            if (!_action.IsValid()) return;
            bool on = Time.time < _actionUntil - 0.15f;
            _actionW = Mathf.MoveTowards(_actionW, on ? 1f : 0f, dt * (on ? 10f : _actionFade));
            if (!on && _actionW <= 0f) _actionFade = 6f;
            _layers.SetInputWeight(_lAction, _actionW);
        }
        private string _grenadeState = "";

        // a throw: the quick grenade with the LEFT hand ("Throw", mirrored in the bundle - the right one holds the gun), a drawn throwing weapon
        // (blast lance) with the RIGHT hand ("ThrowRight" = the same clip unmirrored, else "Throw"). The blast lance's [Attack] FSM goes
        // on -> fire (0.4 s) -> throw (the lance leaves the hand), so its clip starts ThrowLead s in: her release (0.85 s into the clip) on the game's
        private string _throwClip = "Throw";
        private float _throwFrom;
        private bool _throwFresh;
        private const float ThrowRelease = 0.85f, ThrowLead = 0.4f;
        private float _throwAt = -10f;
        private bool _throwAim;          // the quick grenade: aimed at where the game's grenade flies (ThrowAim)
        private void StartThrow(bool rightHand = false, bool timed = false)
        {
            if (Time.time - _throwAt < 1.2f) return;   // one throw at a time (the lance's FSM can pass "on" again around its throw)
            _throwAt = Time.time;
            _throwAim = !rightHand; _aimMeasured = false;
            string n = rightHand && Anims.Get("ThrowRight") != null ? "ThrowRight" : "Throw";
            var c = Anims.Get(n);
            if (c == null) return;
            _throwClip = n;
            _throwFrom = timed ? Mathf.Clamp(ThrowRelease - ThrowLead, 0f, c.length * 0.5f) : 0f;
            _throwUntil = Time.time + Mathf.Clamp(c.length - _throwFrom, 0.5f, 2f);
            _upperClip = ""; _throwFresh = true;   // restart the clip
            if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Throw: " + n + " from " + _throwFrom.ToString("0.00") + " s");
        }
        // the throw runs on its own clock (started at _throwAt from _throwFrom): a weapon change during it (the lance leaving her hand, the
        // grenade holstering the drawn weapon) resets the upper clip, which must not start the throw over
        private void PlayThrow()
        {
            SetUpper(_throwClip, 1f, 1f);
            if (!_upper.IsValid()) return;
            double want = _throwFrom + (Time.time - _throwAt);
            if (_throwFresh || System.Math.Abs(_upper.GetTime() - want) > 0.05) _upper.SetTime(want);
            _throwFresh = false;
        }
        private readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>();

        private float _phase, _speedSmooth, _strafeSmooth, _crouch, _prone, _runW;
        private float _legThigh, _legShin;
        private bool _inCar, _snap;
        private View _lastView = View.FirstPerson;
        private float _propRetry;
        private GameObject _prop; private string _propFor = ""; private Vector3 _propBarrel, _propPos; private Quaternion _propRot; private Transform _propHand; private float _propSince;

        public enum View { FirstPerson, ThirdPerson }

        // ---------------------------------------------------------------- build
        public static Body Create()
        {
            var flexa = FlexaPrefab();
            if (flexa == null) return null;
            var animSrc = flexa.transform.Find("Anim");
            var selected = CharacterModels.SelectedSource(); if (selected != null) animSrc = selected.transform;
            if (CharacterModels.NativeSelection && selected == null) return null;
            if (animSrc == null) { Core.Log.Error(PresentationModule.Instance, "Flexa prefab has no Anim child"); return null; }

            var b = new Body();
            b.Root = new GameObject("ApocaplayerBody");
            b.Root.SetActive(false);   // nothing in the clone wakes up until it is stripped
            var clone = UnityEngine.Object.Instantiate(animSrc.gameObject, b.Root.transform, false);
            clone.name = "Anim";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            b._anim = clone.transform;

            // props (beard, headband, bags, guns ...) out, then every component that isn't the skeleton or its renderer
            var props = new List<GameObject>();
            foreach (var mf in clone.GetComponentsInChildren<MeshFilter>(true)) props.Add(mf.gameObject);
            foreach (var p in props) if (p != null) UnityEngine.Object.DestroyImmediate(p);
            for (int pass = 0; pass < 2; pass++)
                foreach (var c in clone.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform || c is Animator || c is SkinnedMeshRenderer) continue;
                    try { UnityEngine.Object.DestroyImmediate(c); } catch (Exception) { }
                }
            var smrs = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var s in smrs) if (s.bones.Length >= 20) { b._smr = s; break; }
            foreach (var s in smrs) if (s != b._smr) UnityEngine.Object.DestroyImmediate(s.gameObject);
            if (b._smr == null) { Core.Log.Error(PresentationModule.Instance, "Flexa's body renderer not found"); UnityEngine.Object.Destroy(b.Root); return null; }
            foreach (var t in clone.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = 0; if (t.name.StartsWith("mixamorig:")) b.Bones[t.name] = t; }

            var characterMaterial = (int)CharacterSettings.Character.Value >= 8 ? Model.CharacterSkin(b._smr, (int)CharacterSettings.Character.Value) : null;
            if (!Model.Build(b._smr)) { if (characterMaterial != null) UnityEngine.Object.Destroy(characterMaterial); UnityEngine.Object.Destroy(b.Root); return null; }
            b._mat = characterMaterial ?? new Material(b._smr.sharedMaterial) { name = "Apocaplayer body" };
            var tex = Model.Body();
            if (tex != null) b._mat.mainTexture = tex;
            b._smr.sharedMesh = Model.NoArms;
            b._smr.sharedMaterial = b._mat;
            b._smr.updateWhenOffscreen = true;
            b._smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 2.5f);
            b._smr.gameObject.SetActive(true);

            var sh = new GameObject("ApocaplayerShadow");
            sh.transform.SetParent(b._smr.transform.parent, false);
            b._shadow = sh.AddComponent<SkinnedMeshRenderer>();
            b._shadow.bones = b._smr.bones;
            b._shadow.rootBone = b._smr.rootBone;
            b._shadow.sharedMesh = Model.Full;
            b._shadow.sharedMaterial = b._mat;
            b._shadow.updateWhenOffscreen = true;
            b._shadow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

            // first person with nothing in hand: her own arms (same bones, same animation), no shadow (the shadow body has them)
            var fa = new GameObject("ApocaplayerFirstPersonArms");
            fa.transform.SetParent(b._smr.transform.parent, false);
            b._fpArms = fa.AddComponent<SkinnedMeshRenderer>();
            b._fpArms.bones = b._smr.bones;
            b._fpArms.rootBone = b._smr.rootBone;
            b._fpArms.sharedMesh = Model.ArmsOnly;
            b._fpArms.sharedMaterial = b._mat;
            b._fpArms.updateWhenOffscreen = true;
            b._fpArms.localBounds = new Bounds(Vector3.zero, Vector3.one * 2.5f);
            b._fpArms.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b._fpArms.enabled = false;

            // first person: the camera sits inside her (neck opening, near-plane cuts through the chest when looking down). The inside of
            // the body mesh is drawn too (same mesh, reversed faces, darker), so a cut shows her inside instead of the ground through her.
            var inr = new GameObject("ApocaplayerInside");
            inr.transform.SetParent(b._smr.transform.parent, false);
            b._inner = inr.AddComponent<SkinnedMeshRenderer>();
            b._inner.bones = b._smr.bones;
            b._inner.rootBone = b._smr.rootBone;
            var im = new Material(b._mat) { name = "Apocaplayer inside" };
            if (im.HasProperty("_Color")) im.color = im.color * 0.45f;
            if (im.HasProperty("_Glossiness")) im.SetFloat("_Glossiness", 0f);
            b._inner.sharedMaterials = new[] { im, CapMaterial(b._mat) };
            b._inner.updateWhenOffscreen = true;
            b._inner.localBounds = new Bounds(Vector3.zero, Vector3.one * 2.5f);
            b._inner.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b._inner.receiveShadows = false;
            b._inner.enabled = false;

            // bind-pose local rotations (for the car pose's unmapped bones)
            var bindWorld = new Dictionary<string, Matrix4x4>();
            foreach (var kv in Bindposes.Human)
            {
                var m = Matrix4x4.identity;
                for (int r = 0; r < 3; r++) for (int c = 0; c < 4; c++) m[r, c] = kv.Value[r * 4 + c];
                bindWorld[kv.Key] = m.inverse;
            }
            foreach (var kv in b.Bones)
            {
                var par = kv.Value.parent;
                if (par == null || !bindWorld.ContainsKey(kv.Key) || !bindWorld.ContainsKey(par.name)) continue;
                var local = bindWorld[par.name].inverse * bindWorld[kv.Key];
                b._bindLocal[kv.Key] = local.rotation;
            }

            b._animator = clone.GetComponent<Animator>();
            b._animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            b._animator.applyRootMotion = false;
            b._rootMotion = clone.AddComponent<RootMotionTap>();
            b.Root.SetActive(true);
            b.BuildGraph(flexa);
            if (b._mixamo) b.BuildUpperRig(clone);
            b.MeasureLegs();
            if (Core.Log.L1) Core.Log.Main(PresentationModule.Instance, "Body built from Flexa's skeleton (" + b.Bones.Count + " bones)");
            return b;
        }

        private static GameObject _flexa;
        internal static GameObject FlexaPrefab()
        {
            if (_flexa != null) return _flexa;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && go.name == "Flexa" && !go.scene.IsValid() && go.transform.parent == null && go.GetComponent<Rigidbody>() != null)
                { _flexa = go; break; }
            return _flexa;
        }

        private AnimationClip Clip(string name)
        {
            AnimationClip c;
            if (string.IsNullOrEmpty(name)) return null;
            if (_clips.TryGetValue(name, out c)) return c;
            c = Anims.Get(name);
            if (c != null) { _clips[name] = c; return c; }
            foreach (var a in Resources.FindObjectsOfTypeAll<AnimationClip>())
                if (a != null && a.name == name && a.humanMotion) { c = a; break; }
            if (c == null)
                foreach (var a in Resources.FindObjectsOfTypeAll<AnimationClip>())
                    if (a != null && a.name == name) { c = a; break; }
            if (c == null) { if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Animation clip " + name + " not found"); }
            _clips[name] = c;
            return c;
        }

        private void BuildGraph(GameObject flexa)
        {
            _graph = PlayableGraph.Create("Apocaplayer");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(_graph, "body", _animator);
            _mixamo = Anims.Loaded && Anims.Get("RifleIdle") != null && (Anims.Get("RifleWalk") != null || Anims.Get("RifleWalkLow") != null);   // 2.0: the rifle pack's legs carry every weapon
            if (Anims.Loaded && !_mixamo) Core.Log.Warn(PresentationModule.Instance, "Animation bundle without RifleIdle + RifleWalk / RifleWalkLow: the bundle's locomotion is OFF (the game's clips + procedural legs)");
            if (_mixamo) { BuildMixamo(output); return; }
            _loco = AnimationMixerPlayable.Create(_graph, 2);
            var idle = Clip(CharacterSettings.IdleClip.Value);
            var run = Clip(CharacterSettings.RunClip.Value);
            if (idle == null) idle = Clip("enemy_1_idle");
            if (run == null) run = idle;
            _fallback = idle;
            var pIdle = AnimationClipPlayable.Create(_graph, idle);
            var pRun = AnimationClipPlayable.Create(_graph, run);
            pIdle.SetApplyFootIK(false); pRun.SetApplyFootIK(false);
            _graph.Connect(pIdle, 0, _loco, 0);
            _graph.Connect(pRun, 0, _loco, 1);
            _loco.SetInputWeight(0, 1f); _loco.SetInputWeight(1, 0f);

            _layers = AnimationLayerMixerPlayable.Create(_graph, 2);
            _graph.Connect(_loco, 0, _layers, 0);
            _layers.SetInputWeight(0, 1f);
            MakeUpper(idle);
            var mask = new AvatarMask();
            foreach (AvatarMaskBodyPart part in Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                if (part == AvatarMaskBodyPart.LastBodyPart) continue;
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                          || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            _layers.SetLayerMaskFromAvatarMask((uint)_lUpper, mask);
            output.SetSourcePlayable(_layers);
            _graph.Play();
            if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Animation graph: idle " + (idle != null ? idle.name : "-") + ", run " + (run != null ? run.name : "-"));
        }


        private void PlayStrike(float dt)
        {
            if (_meleeStart > 0f)
            {
                string mc = Anims.Get("Melee") != null ? "Melee" : CharacterSettings.MeleeClip.Value;
                if (UpdateMelee(mc)) { SetUpper(mc, 1f, _meleeSpeed); return; }
                _strikeEnd = Time.time;
            }
            if (_strike == null) { SetUpper("", 0f, 0f); return; }
            float el = Time.time - _strikeAt, t;
            if (_striking && el >= _strikeCycle) { _striking = false; _strikeEnd = Time.time; }
            if (_striking)
            {
                float w = Mathf.Min(CharacterSettings.StrikeWindup.Value, _strikeCycle * 0.4f);
                t = el < w ? Mathf.Lerp(_strike.From, _strike.Hit, el / Mathf.Max(0.001f, w))
                           : Mathf.Lerp(_strike.Hit, _strike.To, (el - w) / Mathf.Max(0.01f, _strikeCycle - w));
            }
            else
            {
                t = _strike.To;   // back on guard: fade out
                _strikeW = Mathf.MoveTowards(_strikeW, 0f, dt * 8f);
            }
            if (_strikeW <= 0f) { SetUpper("", 0f, 0f); return; }
            SetUpper(_strike.Clip, _strikeW, 0f);
            _upper.SetTime(t);
        }

        private void SetUpper(string clipName, float weight, float speed)
        {
            if (string.IsNullOrEmpty(clipName)) { _upperWTarget = 0f; return; }   // keep the last clip connected, fading out
            if (clipName != _upperClip)
            {
                _upperClip = clipName;
                // the clip being shown fades out under the new one (only when it is visible at all)
                if (_upperOld.IsValid()) { _graph.Disconnect(_upperMix, 1); _upperOld.Destroy(); }
                _graph.Disconnect(_upperMix, 0);
                if (_upper.IsValid() && _upperW > 0.01f && !_snap)
                {
                    _upperOld = _upper; _upperOld.SetSpeed(0);   // frozen where it was
                    _graph.Connect(_upperOld, 0, _upperMix, 1);
                    _upperX = 0f;
                }
                else { if (_upper.IsValid()) _upper.Destroy(); _upperX = 1f; }
                var c = Clip(clipName) ?? _fallback;
                _upper = AnimationClipPlayable.Create(_graph, c);
                _upper.SetApplyFootIK(false);
                _graph.Connect(_upper, 0, _upperMix, 0);
                _upper.SetTime(0);
                _upperMix.SetInputWeight(0, _upperX); _upperMix.SetInputWeight(1, 1f - _upperX);
            }
            _upperWTarget = weight;
            if (_upper.IsValid()) _upper.SetSpeed(speed);
        }

        private void MeasureLegs()
        {
            Transform up, leg, foot;
            if (Bones.TryGetValue("mixamorig:LeftUpLeg", out up) && Bones.TryGetValue("mixamorig:LeftLeg", out leg) && Bones.TryGetValue("mixamorig:LeftFoot", out foot))
            { _legThigh = leg.localPosition.magnitude; _legShin = foot.localPosition.magnitude; }
            if (_legThigh < 0.1f) _legThigh = 0.44f;
            if (_legShin < 0.1f) _legShin = 0.40f;
        }

        public void Destroy()
        {
            EndCrawl();
            EndTraversal();
            if (_ragdoll != null) { _ragdoll.Destroy(); _ragdoll = null; }
            try { if (_graph.IsValid()) _graph.Destroy(); } catch (Exception) { }
            try { if (_rigGraph.IsValid()) _rigGraph.Destroy(); } catch (Exception) { }
            if (Root != null) UnityEngine.Object.Destroy(Root);
            Root = null;
        }

        public bool Alive { get { return Root != null && _anim != null; } }

        // ---------------------------------------------------------------- per frame
        public void SetVisible(bool body, bool shadow)
        {
            if (_equipment != null) { if (CharacterSettings.EquipmentOnBody.Value) _equipment.SetVisible(body, shadow); else _equipment.SetVisible(false, false); }
            if (_smr.enabled != body) _smr.enabled = body;
            if (_shadow.enabled != shadow) _shadow.enabled = shadow;
            bool inner = body && _innerOn && _inner != null && _inner.sharedMesh != null;
            if (_inner != null && _inner.enabled != inner) _inner.enabled = inner;
        }

        // the caps closing the first-person cut openings: pure black, no lighting (Unlit/Color if the game ships it, else a black
        // Standard copy without texture, specular or reflections)
        private static Material _capMat;
        private static Material CapMaterial(Material body)
        {
            if (_capMat != null) return _capMat;
            var sh = Shader.Find("Unlit/Color");
            Material m;
            if (sh != null) m = new Material(sh);
            else
            {
                m = new Material(body) { mainTexture = null };
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                if (m.HasProperty("_SpecularHighlights")) { m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
                if (m.HasProperty("_GlossyReflections")) { m.SetFloat("_GlossyReflections", 0f); m.EnableKeyword("_GLOSSYREFLECTIONS_OFF"); }
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            }
            m.name = "Apocaplayer cut cap";
            m.color = Color.black;
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _capMat = m;
            return m;
        }

        // first person: [Debug] camera offsets (view right/up/forward in the given frame) - her body moves the opposite way under the camera
        public void ShiftFirstPerson(Quaternion frame, Vector3 viewOffset)
        {
            if (Root == null || viewOffset.sqrMagnitude < 1e-8f) return;
            Root.transform.position -= frame * viewOffset;
        }

        // the body's own arms in first person (nothing in hand) - drawn on top of whatever mesh the body has (NoArms / hidden)
        public void SetFirstPersonArms(bool on)
        {
            var want = Game.ToolInHand ? Model.LeftArmOnly : Model.ArmsOnly;
            if (_fpArms != null && _fpArms.sharedMesh != want) _fpArms.sharedMesh = want;
            if (_fpArms != null && _fpArms.enabled != on) _fpArms.enabled = on;
        }

        // on foot: place, animate (graph is already evaluated by the Animator this frame), then the procedural layers
        // pose fades: the last shown pose (captured after every frame) blends into the new one over a short time when she gets into / out of
        // the driver's seat or turns off the seat in the car (bone local rotations; hips stay where the new pose puts them)
        // (WW perf, F9) the shown pose per bone in arrays parallel to Bones (filled once at creation): was ~50 string-keyed dictionary writes a frame
        private Transform[] _boneArr; private int _hipsIdx = -1;
        private Quaternion[] _shownQ, _fadeQ; private bool _shownAny;
        private void BoneArrays()
        {
            if (_boneArr != null && _boneArr.Length == Bones.Count) return;
            int n = Bones.Count, i = 0;
            _boneArr = new Transform[n]; _shownQ = new Quaternion[n]; _fadeQ = new Quaternion[n]; _shownAny = false; _hipsIdx = -1; _fadeT = 0f;
            foreach (var kv in Bones) { if (kv.Key == "mixamorig:Hips") _hipsIdx = i; _boneArr[i++] = kv.Value; }
        }
        private float _fadeT, _fadeLen = 0.3f;
        private Quaternion _fadeRoot = Quaternion.identity, _shownRoot = Quaternion.identity;
        private bool _fadeRootOn;
        private void CapturePose(Transform rel)
        {
            BoneArrays();
            for (int i = 0; i < _boneArr.Length; i++) _shownQ[i] = _boneArr[i].localRotation;
            _shownAny = true;
            _shownRoot = rel != null ? Quaternion.Inverse(rel.rotation) * Root.transform.rotation : Root.transform.rotation;
        }
        private void StartPoseFade(float len, bool root)
        {
            if (!_shownAny) return;
            Array.Copy(_shownQ, _fadeQ, _shownQ.Length);
            _fadeRoot = _shownRoot; _fadeRootOn = root;
            _fadeT = _fadeLen = len;
        }
        private void ApplyPoseFade(float dt, Transform rel)
        {
            if (_fadeT <= 0f) return;
            float k = 1f - _fadeT / _fadeLen; k = k * k * (3f - 2f * k);
            if (_fadeRootOn && rel != null)
            {
                var hipsT = Bones.ContainsKey("mixamorig:Hips") ? Bones["mixamorig:Hips"] : null;
                Vector3 hp = hipsT != null ? hipsT.position : Root.transform.position;
                var cur = Quaternion.Inverse(rel.rotation) * Root.transform.rotation;
                Root.transform.rotation = rel.rotation * Quaternion.Slerp(_fadeRoot, cur, k);
                if (hipsT != null) Root.transform.position += hp - hipsT.position;   // turn about her hips
            }
            for (int i = 0; i < _boneArr.Length; i++)
            {
                if (i == _hipsIdx) continue;
                var bt = _boneArr[i];
                bt.localRotation = Quaternion.Slerp(_fadeQ[i], bt.localRotation, k);
            }
            _fadeT -= dt;
        }

        public void LateFoot(View view, float dt)
        {
            if (LateTraversal(view)) return;
            if (LateCrawl(view, dt)) return;
            TickUpper(dt);
            if (_inCar) StartPoseFade(0.3f, false);   // out of the driver's seat: the seated pose eases into standing
            LateFootInner(view, dt);
            if (_mixamo) { ApplyPoseFade(dt, null); CapturePose(null); }
        }

        private void LateFootInner(View view, float dt)
        {
            if (_inCar) { _inCar = false; _animator.enabled = true; }
            var player = Game.Player.transform;
            Vector3 fwd = Game.PlayerCamera.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = player.forward;
            var yaw = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            float standH = Game.StandingHeight;
            string mstate = Game.MoveState;
            bool crawl = Crawling.Clip == null && (mstate.StartsWith("Crawl", StringComparison.Ordinal) || standH < 0.75f);
            bool crouch = !crawl && (mstate.StartsWith("Crouch", StringComparison.Ordinal) || standH < 1.4f);
            _crouch = Mathf.MoveTowards(_crouch, crouch ? 1f : 0f, dt * 4f);
            _prone = Mathf.MoveTowards(_prone, crawl ? 1f : 0f, dt * 3f);

            // feet at the bottom of the capsule (the Movement FSM keeps the capsule centred on the Player, height = standingHeight)
            Vector3 feet = player.position - Vector3.up * (standH * 0.5f);
            Vector3 pos = feet;
            float camPitch = Mathf.DeltaAngle(0f, Game.PlayerCamera.eulerAngles.x);   // + = looking down
            var rot = yaw;
            if (_prone > 0f)
            {
                // lying face down, head toward the camera
                rot = yaw * Quaternion.Euler(82f * _prone, 0f, 0f);
                pos = feet - yaw * Vector3.forward * (1.45f * _prone) + Vector3.up * (0.12f * _prone);
            }
            Root.transform.SetPositionAndRotation(pos, rot);
            bool mirror = CharacterSettings.MirrorBody.Value && !_mixamo;   // Mixamo clips hold guns right-handed already
            Root.transform.localScale = new Vector3(mirror ? -1f : 1f, 1f, 1f);
            _anim.localPosition = Vector3.zero; _anim.localRotation = Quaternion.identity;

            if (view != _lastView) { _lastView = view; _snap = true; }
            if (_mixamo) { Sway(view, dt); LateMixamo(view, dt, yaw, camPitch); _snap = false; FitFirstPerson(view, yaw, camPitch); return; }

            // locomotion
            Vector3 v = Game.Velocity; v.y = 0f;
            var local = Quaternion.Inverse(yaw) * v;
            float speed = v.magnitude;
            _speedSmooth = Mathf.Lerp(_speedSmooth, speed, 1f - Mathf.Exp(-dt * 10f));
            float runW = Mathf.Clamp01((_speedSmooth - CharacterSettings.RunFrom.Value) / 0.8f);
            if (_crouch > 0.5f || _prone > 0.5f) runW = 0f;
            _runW = Mathf.MoveTowards(_runW, runW, dt * 4f);
            _loco.SetInputWeight(0, 1f - _runW);
            _loco.SetInputWeight(1, _runW);
            var run = (AnimationClipPlayable)_loco.GetInput(1);
            if (run.IsValid()) run.SetSpeed(Mathf.Max(0.6f, _speedSmooth / Mathf.Max(0.5f, CharacterSettings.RunClipSpeed.Value)));

            // weapon -> upper-body clip
            string weapon = Game.DrawnWeapon;
            var kind = Props.KindOf(weapon);
            bool fire = !Game.Paused && Input.GetMouseButton(0);
            if (kind == Props.Kind.Melee || kind == Props.Kind.None || kind == Props.Kind.Throw)
            {
                if (kind == Props.Kind.Melee && !Game.Paused && (Input.GetMouseButtonDown(0) || Input.GetMouseButton(0) && _meleeStart <= 0f)) StartMelee(weapon);
                if (UpdateMelee(CharacterSettings.MeleeClip.Value)) SetUpper(CharacterSettings.MeleeClip.Value, 1f, _meleeSpeed);
                else SetUpper("", 0f, 0f);
            }
            else
            {
                string clip = kind == Props.Kind.Pistol ? CharacterSettings.PistolClip.Value : CharacterSettings.RifleClip.Value;
                SetUpper(clip, 1f, fire ? 1f : 0f);
                if (!fire && _upper.IsValid()) _upper.SetTime(0);
            }
            UpdateProp(weapon); ShowProp(view == View.ThirdPerson);

            // ---- procedural, in the (unmirrored) space of the Anim object ----
            float walkW = (1f - _runW) * Mathf.Clamp01(_speedSmooth / 0.6f) * (1f - _prone);
            if (walkW > 0.001f)
            {
                float dir = local.z < -0.2f ? -1f : 1f;
                _phase += dt * (_speedSmooth / Mathf.Max(0.3f, CharacterSettings.WalkStride.Value)) * Mathf.PI * 2f * dir;
                if (_phase > Mathf.PI * 2f) _phase -= Mathf.PI * 2f; else if (_phase < 0f) _phase += Mathf.PI * 2f;
            }
            // strafe: legs turn toward the movement direction, the chest keeps facing the camera
            float strafe = 0f;
            if (speed > 0.3f) { strafe = Mathf.Atan2(local.x, Mathf.Abs(local.z)) * Mathf.Rad2Deg; strafe = Mathf.Clamp(strafe, -60f, 60f); }
            _strafeSmooth = Mathf.Lerp(_strafeSmooth, strafe * (1f - _prone), 1f - Mathf.Exp(-dt * 8f));
            float ysign = mirror ? -1f : 1f;   // a turn about the up axis flips in the mirrored body
            if (Mathf.Abs(_strafeSmooth) > 0.5f)
            {
                Turn("mixamorig:Hips", Vector3.up, _strafeSmooth * ysign);
                Turn("mixamorig:Spine", Vector3.up, -_strafeSmooth * 0.5f * ysign);
                Turn("mixamorig:Spine1", Vector3.up, -_strafeSmooth * 0.5f * ysign);
            }
            if (walkW > 0.001f) Walk(walkW, kind == Props.Kind.None);
            if (_crouch > 0.001f) Crouch(_crouch);

            // aim pitch: the spine follows the camera (looking down = positive)
            // (not in first person: bending the torso toward the camera put her chest in front of the lens)
            float pitch = view == View.FirstPerson ? 0f : camPitch * CharacterSettings.AimPitchShare.Value * (1f - _prone);
            if (Mathf.Abs(pitch) > 0.5f)
            {
                Turn("mixamorig:Spine", Vector3.right, pitch * 0.3f);
                Turn("mixamorig:Spine1", Vector3.right, pitch * 0.3f);
                Turn("mixamorig:Spine2", Vector3.right, pitch * 0.3f);
                Turn("mixamorig:Neck", Vector3.right, pitch * 0.1f);
            }
            Turn("mixamorig:Head", Vector3.right, camPitch * (1f - _prone) - pitch);
            PoseProp(kind, yaw);
            FitFirstPerson(view, yaw, camPitch);
        }

        // first person: after the pose is final, slide her (horizontally) so her head is right under the camera, a little behind it -
        // looking down you see her whole body up to the collar, wherever the animation puts her head (crouch lean, walk bob)
        private void FitFirstPerson(View view, Quaternion yaw, float camPitch)
        {
            if (view != View.FirstPerson || _prone > 0.01f || Game.PlayerCamera == null) return;
            Transform head;
            if (!Bones.TryGetValue("mixamorig:Head", out head)) return;
            Vector3 want = Game.PlayerCamera.position - yaw * Vector3.forward * (CharacterSettings.BodyBack.Value + CharacterSettings.BodyBackDown.Value * Mathf.Clamp01(camPitch / 70f));
            // a weapon drawn: the game's first-person arms hang right in front of the camera, where her chest is - lean her back and bend the
            // chest back (more the further you look down) so the arms don't sink into it. Moving HER back = the camera forward, without
            // touching the camera (which would also push the game's arms closer and distort them).
            _fpWeaponW = Mathf.MoveTowards(_fpWeaponW, Game.DrawnWeapon != "" ? 1f : 0f, Time.deltaTime * 4f);
            want -= yaw * Vector3.forward * (CharacterSettings.FirstPersonWeaponBack.Value * _fpWeaponW);
            Vector3 d = want - head.position; d.y = 0f;
            Root.transform.position += d;
            float lean = CharacterSettings.FirstPersonChestLean.Value * _fpWeaponW * (0.6f + 0.4f * Mathf.Clamp01(camPitch / 60f));
            if (lean > 0.1f)
            {
                Turn("mixamorig:Spine1", Vector3.right, -lean * 0.4f);
                Turn("mixamorig:Spine2", Vector3.right, -lean * 0.6f);
            }
        }
        private float _fpWeaponW;


        // first person: no head; her own arms only in a car with nothing drawn (hands on the wheel) - with a gun the game draws its arms

        public void SetMesh(bool firstPerson, bool inCar, bool gameArms = false)
        {
            var want = !firstPerson ? Model.Full : inCar && !gameArms && CharacterSettings.ArmsInCar ? Model.NoHead : Model.NoArms;   // arms on the wheel only with FirstPersonArms
            if (firstPerson && want == Model.NoHead && Game.ToolInHand) want = Model.NoHeadRightArm;
            if (_smr.sharedMesh != want) _smr.sharedMesh = want;
            _innerOn = firstPerson;
            if (_inner != null) { var inside = firstPerson ? Model.Inside(want) : null; if (_inner.sharedMesh != inside) _inner.sharedMesh = inside; }
            var mode = firstPerson ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            if (_smr.shadowCastingMode != mode) _smr.shadowCastingMode = mode;
        }

        private static AvatarMask UpperMask()
        {
            var mask = new AvatarMask();
            foreach (AvatarMaskBodyPart part in Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                if (part == AvatarMaskBodyPart.LastBodyPart) continue;
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                          || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            return mask;
        }

        // standing still in third person her whole body follows the clip's root motion (the bundle puts a clip's sideways/forward motion in the
        // root): the Idle's sway moves her hips and body while the feet stay planted. Moving / first person: back to the middle at once.
        // A slow pull to the middle keeps a clip whose loop doesn't close from drifting away.
        private void Sway(View view, float dt)
        {
            var d = _rootMotion != null ? _rootMotion.Take() : Vector3.zero;
            bool still = view == View.ThirdPerson && _speedSmooth < 0.15f && Game.Velocity.sqrMagnitude < 0.04f;
            if (still && _anim.parent != null)
            {
                var l = _anim.parent.InverseTransformVector(d);
                l.y = 0f;
                if (l.sqrMagnitude < 0.04f) _sway += l;   // a loop wrap / teleport is not a sway
                _sway *= 1f - Mathf.Clamp01(dt * 0.15f);
            }
            else _sway = Vector3.MoveTowards(_sway, Vector3.zero, dt * 0.6f);
            _sway = Vector3.ClampMagnitude(_sway, 0.25f);
            _anim.localPosition = _sway;
        }

        // Rifles: the raider prop's pose was made for Flexa's left-handed raider clip; in Mixamo clips the hands hold it differently.
        // Point the barrel from her right hand toward her left (support) hand, turning about the right hand, so it lies along both hands.
        // every frame: the prop's hand pose, then (rifles, bundle clips) the barrel along both hands, then the player's own offset for this weapon
        // from the [Gun position] settings (her right/up/forward in cm, pitch/yaw/roll in degrees, about the hand holding it)
        // every frame: the prop's grip in her right hand (raider pose, optionally turned toward the left hand) + the player's grip offset for
        // this weapon in the HAND's axes - so it stays glued to the hand in every animation. WeaponAdjustment edits that offset with the numpad
        // (moves/turns along her body axes, converted to the hand's axes).
        // how much she is in each grip animation (GunPose.Poses): reload, else the locomotion slots split into firing / not firing
        // every frame: the weapon model's pose in her right hand = the absolute poses of the animations she is in (GunPose, weapon-poses.txt),
        // blended by the animations' weights. Nothing changes them but the numpad (WeaponAdjustment). Grips from 0.5/0.6 (offsets on the raider
        // grip, for rifles on the AutoGrip one) are converted once per weapon.
        // reload, robustly: a reload is shown only
        //  - while the game's first-person arms of this weapon play their reload (Animator state "reload" / a clip named *reload*), once this
        //    weapon was seen doing that together with its Reload FSM (then that is all that counts), else
        //  - while its Reload / ReloadAnimation FSM reloads - but only if it started reloading AFTER the weapon was drawn (a state left over
        //    from before never counts) and for at most 8 s
        //  never in the first 0.3 s after drawing.
        private string _lastWeapon = "";
        private float _weaponSince, _fsmReloadSince;
        private bool _reloadFresh;                                     // the FSM was seen not reloading since the weapon was drawn
        private readonly HashSet<string> _armsShowReload = new HashSet<string>();   // weapons whose FP arms are known to play a recognisable reload
        private bool ReloadNow(string weapon)
        {
            if (Time.time - _weaponSince < 0.3f) return false;
            bool known;
            bool arms = Game.ArmsReloading(weapon, out known);
            bool fsm = Game.IsReloading(weapon);
            if (fsm && arms && _armsShowReload.Add(weapon)) { if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Reload: " + weapon + " - following its first-person arms' reload animation"); }
            if (known && _armsShowReload.Contains(weapon)) return arms || (fsm && Game.ReloadsPerRound(weapon));   // (2.1.8) one round at a time: the arms rest between rounds
            if (!fsm) { _reloadFresh = true; _fsmReloadSince = 0f; return false; }
            if (!_reloadFresh) return false;                           // already "reloading" when drawn: stale
            if (_fsmReloadSince <= 0f) _fsmReloadSince = Time.time;
            return Time.time - _fsmReloadSince < 8f;
        }

        // the blast lance in a throw: the throwing hand turns it end over end (it pointed backwards). Its forward/backward sense (the barrel axis
        // against her facing) is learned while she just holds it; during the throw, whenever it points the other way, it is turned 180 degrees
        // about the vertical (kept until it clearly points the held way again, so it doesn't flicker when it's sideways)
        private float _lanceSense;   // + = held pointing forward along _propBarrel, - = backward, 0 = unknown
        private bool _lanceFlip;
        private void LanceAlign(Props.Kind kind)
        {
            if (kind != Props.Kind.Throw || _prop == null || _propBarrel == Vector3.zero) { _lanceSense = 0f; _lanceFlip = false; return; }
            var t = _prop.transform;
            float d = Vector3.Dot(t.TransformDirection(_propBarrel), _anim.forward);
            bool throwing = Time.time < _throwUntil && _throwClip == "ThrowRight" || Time.time < _throwUntil && _throwClip == "Throw" && !_throwAim;
            if (!throwing)
            {
                if (Mathf.Abs(d) > 0.2f) _lanceSense = Mathf.Lerp(_lanceSense, Mathf.Sign(d), 0.1f);
                _lanceFlip = false;
                return;
            }
            if (Mathf.Abs(_lanceSense) < 0.3f) _lanceSense = 1f;   // never seen held: the barrel axis is the tip
            if (Mathf.Abs(d) > 0.3f) _lanceFlip = Mathf.Sign(d) != Mathf.Sign(_lanceSense);
            if (_lanceFlip) t.rotation = Quaternion.AngleAxis(180f, _anim.up) * t.rotation;
        }

        private void PoseProp(Props.Kind kind, Quaternion yaw)
        {
            if (_prop == null || _propHand == null) return;
            var t = _prop.transform;
            PoseWeights();
            Vector3 p; Quaternion r;
            GunPose.Blend(_propFor, _poseWn, _propPos, _propRot, out p, out r);
            t.localPosition = p; t.localRotation = r;

            string pose = DominantPose();
            // (WW perf) the raider-grip default only when the clip has no pose; a new array only while editing (the editor keeps 'shown')
            var shown = GunPose.Effective(_propFor, pose, null) ?? (GunPose.Editing ? GunPose.Abs(_propPos, _propRot) : GunPose.Abs(_propPos, _propRot, _shownDef));
            Vector3 move, rot;
            GunPose.Status(kind, _reloading, _shootingNow, _aimingNow);
            if (_propShown && GunPose.Keys(_propFor, pose, shown, out move, out rot)) Adjust(t, yaw, move, rot, pose);
        }

        private readonly float[] _shownDef = new float[6];
        private void Adjust(Transform t, Quaternion yaw, Vector3 move, Vector3 rot, string pose)
        {
            float dt = Time.unscaledDeltaTime;
            Vector3 right = yaw * Vector3.right, fwd = yaw * Vector3.forward;
            Vector3 barrel = _propBarrel != Vector3.zero ? t.TransformDirection(_propBarrel) : fwd;
            var q = Quaternion.AngleAxis(rot.z * dt, barrel) * Quaternion.AngleAxis(rot.y * dt, Vector3.up) * Quaternion.AngleAxis(-rot.x * dt, right);
            Vector3 wp = t.position + (right * move.x + Vector3.up * move.y + fwd * move.z) * (0.01f * dt);
            Quaternion wr = q * t.rotation;
            // back into the hand's axes: that IS the pose saved for this animation
            Vector3 lp = _propHand.InverseTransformPoint(wp);
            Quaternion lr = Quaternion.Inverse(_propHand.rotation) * wr;
            t.localPosition = lp; t.localRotation = lr;
            GunPose.SetLive(_propFor, pose, GunPose.Abs(lp, lr));
        }

        // ---------------------------------------------------------------- procedural helpers
        // rotate a bone by 'deg' about an axis given in the Anim object's local space (mirror-safe: only local rotations are used)
        private void Turn(string bone, Vector3 axisAnim, float deg)
        {
            Transform t;
            if (!Bones.TryGetValue(bone, out t)) return;
            var q = AnimSpace(t);
            t.localRotation = t.localRotation * Quaternion.Inverse(q) * Quaternion.AngleAxis(deg, axisAnim) * q;
        }

        private Quaternion AnimSpace(Transform t)
        {
            var q = Quaternion.identity;
            for (var x = t; x != null && x != _anim; x = x.parent) q = x.localRotation * q;
            return q;
        }

        private void Walk(float w, bool swingArms)
        {
            float s = Mathf.Sin(_phase), c = Mathf.Cos(_phase);
            float A = CharacterSettings.ThighSwing.Value * w, K = CharacterSettings.KneeBend.Value * w;
            // thighs: forward swing = negative about the right axis
            Turn("mixamorig:LeftUpLeg", Vector3.right, -A * s);
            Turn("mixamorig:RightUpLeg", Vector3.right, A * s);
            // knees bend while the leg swings forward
            float kl = K * Mathf.Max(0f, c) + 6f * w, kr = K * Mathf.Max(0f, -c) + 6f * w;
            Turn("mixamorig:LeftLeg", Vector3.right, kl);
            Turn("mixamorig:RightLeg", Vector3.right, kr);
            Turn("mixamorig:LeftFoot", Vector3.right, -0.5f * kl + 0.3f * A * s);
            Turn("mixamorig:RightFoot", Vector3.right, -0.5f * kr - 0.3f * A * s);
            Transform hips;
            if (Bones.TryGetValue("mixamorig:Hips", out hips))
                hips.localPosition += Vector3.up * (-CharacterSettings.HipBob.Value * w * (1f - Mathf.Abs(c)));
            Turn("mixamorig:Spine1", Vector3.up, 4f * w * s);
            if (swingArms)
            {
                Turn("mixamorig:LeftArm", Vector3.right, CharacterSettings.ArmSwing.Value * w * s);
                Turn("mixamorig:RightArm", Vector3.right, -CharacterSettings.ArmSwing.Value * w * s);
            }
        }

        private void Crouch(float k, float jumpDrop = -1f)
        {
            float drop = jumpDrop >= 0f ? jumpDrop : CharacterSettings.CrouchDrop.Value * k;
            float L = _legThigh + _legShin;
            float a = Mathf.Acos(Mathf.Clamp(1f - drop / L, -1f, 1f)) * Mathf.Rad2Deg;
            Transform hips;
            if (Bones.TryGetValue("mixamorig:Hips", out hips)) hips.localPosition += Vector3.down * drop;
            if (jumpDrop < 0f)
            {
                Turn("mixamorig:LeftUpLeg", Vector3.right, -a);
                Turn("mixamorig:RightUpLeg", Vector3.right, -a);
                Turn("mixamorig:LeftLeg", Vector3.right, 2f * a);
                Turn("mixamorig:RightLeg", Vector3.right, 2f * a);
                Turn("mixamorig:LeftFoot", Vector3.right, -a);
                Turn("mixamorig:RightFoot", Vector3.right, -a);
            }
            Turn("mixamorig:Spine", Vector3.right, 18f * k);
        }

        // ---------------------------------------------------------------- weapon in hand (third person)
        private bool _propShown, _propShadow;
        internal bool WeaponPropVisible { get { return _propShown || _propShadow; } }
        /// <summary>The third-person weapon/tool model in the body's hand (null when nothing is held). Field name _prop is read by the Weapons Pack bridge.</summary>
        internal Transform HeldProp { get { return _prop != null ? _prop.transform : null; } }
        private Transform _propBolt;
        private Transform _propMuzzle; private Vector3 _propMuzzleLocal;
        private bool _propIsGun;
        private HutongGames.PlayMaker.FsmInt _propBoltAmmo;
        internal bool TryWeaponMuzzle(out Vector3 position)
        {
            position = Vector3.zero;
            if (_prop == null || !_propShown || !_propIsGun || _propMuzzle == null || _propFor != Game.DrawnWeapon) return false;
            position = _propMuzzle.TransformPoint(_propMuzzleLocal);
            return true;
        }
        // the weapon model: drawn in third person; in first person (the game draws its own gun) only its shadow, next to her body's shadow
        private void ShowProp(bool on)
        {
            if (_propBolt != null && _propBoltAmmo != null && _propBolt.gameObject.activeSelf != (_propBoltAmmo.Value > 0)) _propBolt.gameObject.SetActive(_propBoltAmmo.Value > 0);
            bool shadow = !on && CharacterSettings.BodyFirstPerson.Value && (!Game.InCar || CharacterSettings.BodyInCar);
            if (_prop == null || _propShown == on && _propShadow == shadow) return;
            _propShown = on; _propShadow = shadow;
            var mode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            foreach (var r in _prop.GetComponentsInChildren<Renderer>(true)) { r.enabled = on || shadow; r.shadowCastingMode = mode; }
        }

        private void UpdateProp(string weapon)
        {
            if (weapon == _propFor && (_prop != null || weapon == "" || Time.unscaledTime < _propRetry)) return;   // (2.2.8) no model: tried again every 2 s, not every frame
            _propFor = weapon; _propRetry = Time.unscaledTime + 2f;
            if (_prop != null) { UnityEngine.Object.Destroy(_prop); _prop = null; }
            _propBolt = null; _propBoltAmmo = null; _propMuzzle = null;
            var kind = Props.KindOf(weapon); _propIsGun = kind == Props.Kind.Rifle || kind == Props.Kind.Pistol;
            if (weapon == "" || kind == Props.Kind.None) return;   // bare hands ("hands"): no weapon model
            var p = Props.Find(weapon);
            if (p == null) { if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "No third-person model for " + weapon); return; }
            Transform hand;
            if (!Bones.TryGetValue(p.Hand, out hand)) return;
            bool otherHand = _mixamo && p.Hand == "mixamorig:LeftHand";   // raider guns sit in the left hand; Mixamo clips hold them right-handed
            Transform right;
            if (otherHand && Bones.TryGetValue("mixamorig:RightHand", out right)) hand = right; else otherHand = false;
            if (p.Item)
            {
                // the weapon's own world model: real size, put where the stand-in NPC gun would sit, its barrel along the stand-in's barrel
                var stand = Props.Instantiate(p.Ref, hand, otherHand);
                _prop = Props.Instantiate(p, hand, false);
                var ls = hand.lossyScale; var ss = p.Source.transform.localScale;
                _prop.transform.localScale = new Vector3(ss.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)), ss.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)), ss.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
                _prop.transform.SetPositionAndRotation(stand.transform.position, stand.transform.rotation);
                Vector3 bi = Props.Barrel(_prop), bs = Props.Barrel(stand);
                if (bi != Vector3.zero && bs != Vector3.zero)
                    _prop.transform.rotation = Quaternion.FromToRotation(_prop.transform.TransformDirection(bi), stand.transform.TransformDirection(bs)) * _prop.transform.rotation;
                UnityEngine.Object.DestroyImmediate(stand);
            }
            else _prop = Props.Instantiate(p, hand, otherHand);
            if (weapon.StartsWith("crossbow", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var t in _prop.GetComponentsInChildren<Transform>(true)) if (t.name == "arrow") { _propBolt = t; break; }
                var w = Game.WeaponsParent != null ? Game.WeaponsParent.Find(weapon) : null;
                var reload = w != null ? Core.FsmUtil.Find(w.gameObject, "Reload") : null;
                if (reload != null) _propBoltAmmo = reload.FsmVariables.GetFsmInt("ammo_in_gun");
            }
            _propBarrel = Props.Barrel(_prop);
            Props.TryMuzzle(_prop, out _propMuzzle, out _propMuzzleLocal);
            _propPos = _prop.transform.localPosition; _propRot = _prop.transform.localRotation; _propHand = hand;
            _propSince = Time.time; GunPose.Note("");
            _propShown = true; ShowProp(false);
            if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Third person: barrel axis of " + p.Key + " = " + _propBarrel);
            if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Third person: " + weapon + " -> " + p.Owner + "'s " + p.Source.name + " on " + p.Hand);
        }
    }
}
