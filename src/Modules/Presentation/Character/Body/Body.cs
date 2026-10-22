using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    // Player model/view adapter: a clone of Flexa's "Anim" object (Animator with the humanoid avatar enemy_1_IdleAvatar + the 22 mixamorig bones +
    // the skinned mesh), stripped of props/colliders/FSMs, with our mesh and texture. ModAPI.Character + CharPlan drive the same
    // animation pipeline as NPCs; PlayerInputAdapter supplies native input and action timing. In a car the
    // same graph supplies action arms over the car's seated driver pose (CarSeat).
    // (B05, README 7 / A2-3) the non-Mixamo fallback (the game's raider clips + procedural walk, mirrored body, the 12 [Animation (fallback
    // clips)] entries) is gone: without a complete bundle no body is built and the game's own first-person view stays.
    internal sealed partial class Body
    {
        public GameObject Root;          // player view/model transform; the driver owns animation
        private Transform _anim;         // clone of Flexa/Anim
        private Ragdoll _ragdoll;
        private Equipment _equipment;

        /// <summary>Visible equipment on the body (EquipmentOnBody); off hides the mounts without updating them.</summary>
        public void LateEquipment(bool on)
        {
            // (WW) [PlayerBody] EquipmentOnBody off: the mounts are hidden (and not updated) until it is on again
            if (!on) { if (_equipment != null) _equipment.SetVisible(false, false); return; }
            if (_equipment == null) _equipment = new Equipment(this);
            _equipment.Tick();
            _equipment.SetVisible(_smr.enabled, _shadow.enabled);
        }
        public bool IsRagdoll { get { return _ragdoll != null; } }
        /// <summary>Head and feet points of the live body (occlusion cutaway).</summary>
        internal bool TryOcclusionPoints(out Vector3 head, out Vector3 feet)
        {
            Transform h, left, right;
            if (!Bones.TryGetValue("mixamorig:Head", out h) || !Bones.TryGetValue("mixamorig:LeftFoot", out left) || !Bones.TryGetValue("mixamorig:RightFoot", out right))
            { head = feet = Vector3.zero; return false; }
            head = h.position + Vector3.up * .15f; feet = (left.position + right.position) * .5f;
            return true;
        }
        public Vector3 DeathFocus { get { return _ragdoll != null ? _ragdoll.Focus : Root.transform.position; } }

        /// <summary>Death: the graph stops and a jointed ragdoll takes the bones (first call), then follows physics.</summary>
        public void LateDead(Vector3 velocity, Transform vehicle)
        {
            if (_ragdoll == null)
            {
                EndCrawl(); EndTraversal();
                _driver.Suspended = true;
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
            /// <summary>Collects the Animator's root motion (nothing moves by itself).</summary>
            private void OnAnimatorMove()
            {
                if (_a == null) _a = GetComponent<Animator>();
                if (_a != null) Delta += _a.deltaPosition;
            }
            /// <summary>Returns and clears the collected root motion.</summary>
            public Vector3 Take() { var d = Delta; Delta = Vector3.zero; return d; }
        }
        private Animator _animator;
        private SkinnedMeshRenderer _smr, _shadow, _fpArms, _inner;
        private bool _innerOn;
        private Material _mat;
        public readonly Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Quaternion> _bindLocal = new Dictionary<string, Quaternion>();

        private ModAPI.Character _driver;
        /// <summary>The shared humanoid driver; the player wrapper only owns model/view fitting.</summary>
        internal ModAPI.Character Driver { get { return _driver; } }
        private PlayerInputAdapter _input;
        /// <summary>The common driver owns the graph; traversal/crawl only attach temporary overlays.</summary>
        private PlayableGraph _graph { get { return _driver != null ? _driver.Graph : default(PlayableGraph); } }
        private AnimationLayerMixerPlayable _layers { get { return _driver.Layers; } }
        private bool _mixamo { get { return _driver != null && _driver.Valid; } }
        /// <summary>Animation state of the shared driver used by player view adapters.</summary>
        private CharPlan Plan { get { return _driver.Plan; } }
        private string _upperClip { get { return Plan.UpperClip; } }
        private float _upperW { get { return Plan.UpperW; } }
        private float _actionW { get { return Plan.ActionW; } }
        private string _actionClip { get { return Plan.ActionClip; } }
        private float _throwUntil { get { return Plan.ThrowUntil; } }
        private float _throwFrom { get { return Plan.ThrowFrom; } }
        private string _throwClip { get { return Plan.ThrowClip; } }
        private bool _throwAim { get { return _input.ThrowAim; } }
        private bool _reloading { get { return _driver.Reloading || Plan.Preview != null && Plan.Preview.EndsWith("Reload", StringComparison.Ordinal); } }
        private float _speedSmooth { get { return Plan.SpeedSmooth; } }
        private bool _aimCrouched { get { return Plan.AimCrouched; } }
        private bool _aimingNow { get { return _driver.Aiming; } }
        private bool _shootingNow { get { return _driver.Firing; } }
        private Dictionary<string,float> _poseWn { get { return Plan.PoseW; } }
        private const float ThrowRelease = CharPlan.ThrowRelease;

        private float _crouch, _prone;
        private float _legThigh, _legShin;
        private bool _inCar, _snap;
        private View _lastView = View.FirstPerson;
        private float _propRetry;
        private GameObject _prop; private string _propFor = ""; private Vector3 _propBarrel, _propPos; private Quaternion _propRot; private Transform _propHand; private float _propSince;

        /// <summary>The view the body is drawn for.</summary>
        public enum View { FirstPerson, ThirdPerson }

        // ---------------------------------------------------------------- build
        /// <summary>True when the animation bundle carries the rifle pack's idle and walk (the body's locomotion set).</summary>
        internal static bool MixamoReady { get { return Anims.Loaded && Anims.Get("RifleIdle") != null && (Anims.Get("RifleWalk") != null || Anims.Get("RifleWalkLow") != null); } }
        private static bool _noBundleWarned;

        /// <summary>Builds the player's body from the chosen character's rig (Flexa's or a native one): stripped clone, body/shadow/first-person/inside meshes, Mixamo graph and UpperRig; null without a complete bundle or rig.</summary>
        public static Body Create()
        {
            if (!MixamoReady)
            {
                if (!_noBundleWarned) { _noBundleWarned = true; Core.Log.Warn(PresentationModule.Instance, "Animation bundle missing or without RifleIdle + RifleWalk / RifleWalkLow (" + CharacterSettings.AnimBundleFile.Value + "): no player body, the game's own first-person view"); }
                return null;
            }
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
            b._driver = ModAPI.AttachPlayer(b._animator);
            if (b._driver == null) { UnityEngine.Object.Destroy(b.Root); return null; }
            b._input = new PlayerInputAdapter(b._driver);
            b.MeasureLegs();
            if (Core.Log.L1) Core.Log.Main(PresentationModule.Instance, "Body built from Flexa's skeleton (" + b.Bones.Count + " bones)");
            return b;
        }

        private static GameObject _flexa;
        /// <summary>The Flexa prefab asset (cached).</summary>
        internal static GameObject FlexaPrefab()
        {
            if (_flexa != null) return _flexa;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && go.name == "Flexa" && !go.scene.IsValid() && go.transform.parent == null && go.GetComponent<Rigidbody>() != null)
                { _flexa = go; break; }
            return _flexa;
        }

        /// <summary>Measures thigh and shin length for the crouch drop (defaults when the rig is odd).</summary>
        private void MeasureLegs()
        {
            Transform up, leg, foot;
            if (Bones.TryGetValue("mixamorig:LeftUpLeg", out up) && Bones.TryGetValue("mixamorig:LeftLeg", out leg) && Bones.TryGetValue("mixamorig:LeftFoot", out foot))
            { _legThigh = leg.localPosition.magnitude; _legShin = foot.localPosition.magnitude; }
            if (_legThigh < 0.1f) _legThigh = 0.44f;
            if (_legShin < 0.1f) _legShin = 0.40f;
        }

        /// <summary>Destroys the body: crawl/traversal ended, ragdoll, graphs and objects removed.</summary>
        public void Destroy()
        {
            EndCrawl();
            EndTraversal();
            if (_ragdoll != null) { _ragdoll.Destroy(); _ragdoll = null; }
            if (_driver != null) { _driver.Dispose(); _driver = null; }
            _input = null;
            if (Root != null) UnityEngine.Object.Destroy(Root);
            Root = null;
        }

        public bool Alive { get { return Root != null && _anim != null && _driver != null && _driver.Valid; } }

        // ---------------------------------------------------------------- per frame
        /// <summary>Shows/hides the body mesh and its shadow-only copy (and the equipment and inside meshes with them).</summary>
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
        /// <summary>The black unlit material of the first-person cut caps (shared).</summary>
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

        /// <summary>first person: [Debug] camera offsets (view right/up/forward in the given frame) - her body moves the opposite way under the camera</summary>
        public void ShiftFirstPerson(Quaternion frame, Vector3 viewOffset)
        {
            if (Root == null || viewOffset.sqrMagnitude < 1e-8f) return;
            Root.transform.position -= frame * viewOffset;
        }

        /// <summary>the body's own arms in first person (nothing in hand) - drawn on top of whatever mesh the body has (NoArms / hidden)</summary>
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
        /// <summary>Fills the bone arrays parallel to Bones once (pose fade without dictionary writes).</summary>
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
        /// <summary>Remembers this frame's shown pose (bone rotations, root rotation relative to <paramref name="rel"/>).</summary>
        private void CapturePose(Transform rel)
        {
            BoneArrays();
            for (int i = 0; i < _boneArr.Length; i++) _shownQ[i] = _boneArr[i].localRotation;
            _shownAny = true;
            _shownRoot = rel != null ? Quaternion.Inverse(rel.rotation) * Root.transform.rotation : Root.transform.rotation;
        }
        /// <summary>Starts a pose fade from the last shown pose over <paramref name="len"/> seconds (root too when asked).</summary>
        private void StartPoseFade(float len, bool root)
        {
            if (!_shownAny) return;
            Array.Copy(_shownQ, _fadeQ, _shownQ.Length);
            _fadeRoot = _shownRoot; _fadeRootOn = root;
            _fadeT = _fadeLen = len;
        }
        /// <summary>Blends the remembered pose into this frame's pose while a fade runs.</summary>
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

        /// <summary>On foot: traversal, crawl or the Mixamo frame, then the pose fade.</summary>
        public void LateFoot(View view, float dt)
        {
            if (LateTraversal(view)) return;
            if (LateCrawl(view, dt)) return;
            if (_inCar) StartPoseFade(0.3f, false);   // out of the driver's seat: the seated pose eases into standing
            LateFootInner(view, dt);
            ApplyPoseFade(dt, null); CapturePose(null);
        }

        /// <summary>Places the body under the player (stance, prone) and runs the Mixamo frame.</summary>
        private void LateFootInner(View view, float dt)
        {
            if (_inCar) { _inCar = false; _animator.enabled = true; }
            var player = PlayerState.Player.transform;
            Vector3 fwd = PlayerState.PlayerCamera.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = player.forward;
            var yaw = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            float standH = PlayerState.StandingHeight;
            string mstate = PlayerState.MoveState;
            bool crawl = Crawling.Clip == null && (mstate.StartsWith("Crawl", StringComparison.Ordinal) || standH < 0.75f);
            bool crouch = !crawl && (mstate.StartsWith("Crouch", StringComparison.Ordinal) || standH < 1.4f);
            _crouch = Mathf.MoveTowards(_crouch, crouch ? 1f : 0f, dt * 4f);
            _prone = Mathf.MoveTowards(_prone, crawl ? 1f : 0f, dt * 3f);

            // feet at the bottom of the capsule (the Movement FSM keeps the capsule centred on the Player, height = standingHeight)
            Vector3 feet = player.position - Vector3.up * (standH * 0.5f);
            Vector3 pos = feet;
            float camPitch = Mathf.DeltaAngle(0f, PlayerState.PlayerCamera.eulerAngles.x);   // + = looking down
            var rot = yaw;
            if (_prone > 0f)
            {
                // lying face down, head toward the camera
                rot = yaw * Quaternion.Euler(82f * _prone, 0f, 0f);
                pos = feet - yaw * Vector3.forward * (1.45f * _prone) + Vector3.up * (0.12f * _prone);
            }
            Root.transform.SetPositionAndRotation(pos, rot);
            Root.transform.localScale = Vector3.one;   // Mixamo clips hold guns right-handed (the old mirrored raider-clip body is gone)
            _anim.localPosition = Vector3.zero; _anim.localRotation = Quaternion.identity;

            if (view != _lastView) { _lastView = view; _snap = true; }
            Sway(view, dt); LateCharacter(view, dt, yaw, camPitch, crouch); _snap = false; FitFirstPerson(view, yaw, camPitch);
        }

        /// <summary>first person: after the pose is final, slide her (horizontally) so her head is right under the camera, a little behind it - looking down you see her whole body up to the collar, wherever the animation puts her head (crouch lean, walk bob)</summary>
        private void FitFirstPerson(View view, Quaternion yaw, float camPitch)
        {
            if (view != View.FirstPerson || _prone > 0.01f || PlayerState.PlayerCamera == null) return;
            Transform head;
            if (!Bones.TryGetValue("mixamorig:Head", out head)) return;
            Vector3 want = PlayerState.PlayerCamera.position - yaw * Vector3.forward * (CharacterSettings.BodyBack.Value + CharacterSettings.BodyBackDown.Value * Mathf.Clamp01(camPitch / 70f));
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

        /// <summary>Chooses the body mesh for the view (full, no head, no arms, no right arm) and the inside mesh.</summary>
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

        /// <summary>standing still in third person her whole body follows the clip's root motion (the bundle puts a clip's sideways/forward motion in the root): the Idle's sway moves her hips and body while the feet stay planted. Moving / first person: back to the middle at once. A slow pull to the middle keeps a clip whose loop doesn't close from drifting away.</summary>
        private void Sway(View view, float dt)
        {
            var d = _rootMotion != null ? _rootMotion.Take() : Vector3.zero;
            bool still = view == View.ThirdPerson && _speedSmooth < 0.15f && PlayerState.Velocity.sqrMagnitude < 0.04f;
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

        // the blast lance in a throw: the throwing hand turns it end over end (it pointed backwards). Its forward/backward sense (the barrel axis
        // against her facing) is learned while she just holds it; during the throw, whenever it points the other way, it is turned 180 degrees
        // about the vertical (kept until it clearly points the held way again, so it doesn't flicker when it's sideways)
        private float _lanceSense;   // + = held pointing forward along _propBarrel, - = backward, 0 = unknown
        private bool _lanceFlip;
        /// <summary>Blast lance thrown: flips the prop end over end when it points against its held direction.</summary>
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

        /// <summary>Places the third-person weapon prop from the weapon-pose table and applies the live editor keys.</summary>
        private void PoseProp(Props.Kind kind, Quaternion yaw)
        {
            if (_prop == null || _propHand == null) return;
            var t = _prop.transform;
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
        /// <summary>Weapon pose editor: moves/turns the prop by the held keys and stores the result as the clip's live pose.</summary>
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
        /// <summary>rotate a bone by 'deg' about an axis given in the Anim object's local space (mirror-safe: only local rotations are used)</summary>
        private void Turn(string bone, Vector3 axisAnim, float deg)
        {
            Transform t;
            if (!Bones.TryGetValue(bone, out t)) return;
            CharacterRig.Turn(t, _anim, axisAnim, deg);
        }

        /// <summary>Rotation of a bone in the Anim object's space.</summary>
        private Quaternion AnimSpace(Transform t) { return CharacterRig.SpaceOf(t, _anim); }

        /// <summary>Lowers the hips by the crouch drop (or a jump drop) and bends hips, knees and ankles to keep the feet.</summary>
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
        /// <summary>True while the third-person prop or its shadow shows.</summary>
        internal bool WeaponPropVisible { get { return _propShown || _propShadow; } }
        /// <summary>The third-person weapon/tool model in the body's hand (null when nothing is held). Field name _prop is read by the Weapons Pack bridge.</summary>
        internal Transform HeldProp { get { return _prop != null ? _prop.transform : null; } }
        private Transform _propBolt;
        private Transform _propMuzzle; private Vector3 _propMuzzleLocal;
        private bool _propIsGun;
        private HutongGames.PlayMaker.FsmInt _propBoltAmmo;
        /// <summary>Muzzle of the shown third-person gun prop (scene space).</summary>
        internal bool TryWeaponMuzzle(out Vector3 position)
        {
            position = Vector3.zero;
            if (_prop == null || !_propShown || !_propIsGun || _propMuzzle == null || _propFor != Game.DrawnWeapon) return false;
            position = _propMuzzle.TransformPoint(_propMuzzleLocal);
            return true;
        }
        /// <summary>the weapon model: drawn in third person; in first person (the game draws its own gun) only its shadow, next to her body's shadow</summary>
        private void ShowProp(bool on)
        {
            if (_propBolt != null && _propBoltAmmo != null && _propBolt.gameObject.activeSelf != (_propBoltAmmo.Value > 0)) _propBolt.gameObject.SetActive(_propBoltAmmo.Value > 0);
            bool shadow = !on && CharacterSettings.BodyFirstPerson.Value && (!PlayerState.InCar || CharacterSettings.BodyInCar);
            if (_prop == null || _propShown == on && _propShadow == shadow) return;
            _propShown = on; _propShadow = shadow;
            var mode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            foreach (var r in _prop.GetComponentsInChildren<Renderer>(true)) { r.enabled = on || shadow; r.shadowCastingMode = mode; }
        }

        /// <summary>Rebuilds the third-person prop when the drawn weapon changes (an NPC model or the weapon's world model, retried every 2 s when missing).</summary>
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
                var w = PlayerState.WeaponsParent != null ? PlayerState.WeaponsParent.Find(weapon) : null;
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
