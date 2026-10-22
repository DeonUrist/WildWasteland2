using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// RC6: NPC Animators set to AlwaysAnimate in their prefab (roots with a "Detection" FSM, e.g. Sources
    ///    data/hierarchy/prefabs/L/Lugnut.txt:L1-44) become CullUpdateTransforms: the state machine keeps running (parameters,
    ///    transitions, events), only bone writes stop while no renderer of the NPC is visible. Prefab assets only: Animator has no
    ///    ES3Type (not saved), and live instances are never touched, because WW modules switch the animators they drive to
    ///    AlwaysAnimate at runtime (NPC DogAnimations, Patrols RiderAnchorGuard, PlayerBody) and their write must win. Disable
    ///    restores the assets and every live animator of the same controller that still has our CullUpdateTransforms.
    /// Risk (research C §5): bone-mounted sensors / Bodypart colliders of an off-screen NPC keep the last pose.
    /// </summary>
    public sealed class AnimatorCulling : Feature, IScanClient
    {
        /// <summary>Stable performance-plan identifier used in diagnostics and capture comparisons.</summary>
        public override string PlanId { get { return "RC6"; } }
        /// <summary>Configuration key for this optimization; independent of its display title.</summary>
        public override string Key { get { return "Animator culling"; } }
        /// <summary>Player-facing description of this optimization in the performance controls.</summary>
        public override string Title { get { return "Off-screen NPC animation"; } }
        /// <summary>Performance settings group used for stable feature ordering.</summary>
        public override string Category { get { return Categories.World; } }
        /// <summary>Ordering within the feature category; retains the established hook/settings order.</summary>
        public override int Order { get { return 20; } }
        /// <summary>Whether Vanilla disables this feature because it changes presentation or simulation.</summary>
        public override bool UsesPreset { get { return true; } }
        /// <summary>Explains the behavior and limits of this optimization in the settings UI.</summary>
        public override string Help { get { return "NPC skeletons stop updating while off-screen. Native IK runs unchanged at every distance."; } }

        Knob _cullAnim;
        /// <summary>Binds the feature-specific tuning entries and their preset defaults.</summary>
        public override void Bind(ModuleConfig cfg)
        {
            _cullAnim = NewKnob(cfg, "Cull NPC animators", "1 = AlwaysAnimate NPC animators become CullUpdateTransforms (prefabs).", 1, 1, 1, 0);
        }

        sealed class ARec { public Animator A; public AnimatorCullingMode Orig; }
        readonly List<ARec> _assets = new List<ARec>();
        readonly Dictionary<RuntimeAnimatorController, AnimatorCullingMode> _byController = new Dictionary<RuntimeAnimatorController, AnimatorCullingMode>();
        readonly List<PlayMakerFSM> _fbuf = new List<PlayMakerFSM>();
        readonly HashSet<int> _npcRoots = new HashSet<int>(), _otherRoots = new HashSet<int>();
        public ScanTypes Types { get { return ScanTypes.Animators; } }

        /// <summary>Activates this feature-owned replacement paths and resources for the selected mode.</summary>
        protected override void OnEnable(FeatureMode mode)
        {
            if (_cullAnim.Value > 0.5f) RenderScan.Register(this);
        }

        /// <summary>Stops this feature and restores the native values and resources it changed.</summary>
        protected override void OnDisable()
        {
            RenderScan.Unregister(this);
            RestoreAnimators();
        }

        /// <summary>Reapplies active feature settings after its preset or override values change.</summary>
        public override void OnSettingsChanged()
        {
            if (_cullAnim.Value > 0.5f) RenderScan.Register(this); else { RenderScan.Unregister(this); RestoreAnimators(); }
        }

        /// <summary>Drops scene-bound references and refreshes this feature for the newly loaded scene.</summary>
        public override void OnSceneLoaded() { _npcRoots.Clear(); _otherRoots.Clear(); RenderScan.RequestFull(); }

        void RestoreAnimators()
        {
            for (int i = 0; i < _assets.Count; i++) if (_assets[i].A != null) _assets[i].A.cullingMode = _assets[i].Orig;
            if (_byController.Count > 0)
            {
                var live = Resources.FindObjectsOfTypeAll<Animator>();
                AnimatorCullingMode o;
                for (int i = 0; i < live.Length; i++)
                {
                    var a = live[i];
                    if (a == null || !a.gameObject.scene.IsValid() || a.cullingMode != AnimatorCullingMode.CullUpdateTransforms) continue;
                    var rc = a.runtimeAnimatorController;
                    if (rc != null && _byController.TryGetValue(rc, out o)) a.cullingMode = o;
                }
            }
            _assets.Clear(); _byController.Clear(); _npcRoots.Clear(); _otherRoots.Clear();
        }

        public void OnAnimator(Animator a, ref ScanCtx c)
        {
            if (!c.Asset || c.Player || c.Vehicle || a.cullingMode != AnimatorCullingMode.AlwaysAnimate) return;
            if (!IsNpcRoot(c.Root)) return;
            _assets.Add(new ARec { A = a, Orig = a.cullingMode });
            if (a.runtimeAnimatorController != null) _byController[a.runtimeAnimatorController] = a.cullingMode;
            a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        bool IsNpcRoot(Transform root)
        {
            if (root == null) return false;
            int id = root.GetInstanceID();
            if (_npcRoots.Contains(id)) return true;
            if (_otherRoots.Contains(id)) return false;
            root.GetComponents(_fbuf);
            bool npc = false;
            for (int i = 0; i < _fbuf.Count; i++) if (_fbuf[i] != null && _fbuf[i].FsmName == "Detection") { npc = true; break; }
            _fbuf.Clear();
            (npc ? _npcRoots : _otherRoots).Add(id);
            return npc;
        }

        /// <summary>This scan client does not modify lights.</summary>
        public void OnLight(Light l, ref ScanCtx c) { }
        /// <summary>This scan client does not modify mesh renderers.</summary>
        public void OnRenderer(MeshRenderer r, ref ScanCtx c) { }
        /// <summary>This scan client does not modify particle systems.</summary>
        public void OnParticles(ParticleSystem p, ref ScanCtx c) { }
        /// <summary>This scan client does not consume root geometry.</summary>
        public void OnRoot(Transform root, ref ScanCtx c) { }
        public void OnPassDone() { }

        /// <summary>Draws this feature-owned current counters and state without performing optimization work.</summary>
        public override void DrawStats(ToolLayout l)
        {
            l.Value("NPC animators culled (prefabs)", _assets.Count.ToString());
        }
    }
}
