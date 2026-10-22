using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.NPC
{
    // Clips contain Generic transform curves for the original creature skeletons.
    // Native FSMs retain sensing, damage amounts and their post-hit cooldown.
    internal static partial class DogAnimations
    {
        /// <summary>Which clip family a dog plays (locomotion, lying down/resting/getting up, bites, jump attack, sitting).</summary>
        private enum Pose { Locomotion, LieDown, Rest, GetUp, Bite, RunningBite, JumpAttack, SitDown, Sitting }
        private sealed class Dog
        {
            public Senses.Agent Agent; public Animator Animator; public Rigidbody Body;
            public DogCompanionCompat.Driver Companion;   // refs-ok: REFACTOR-REQUEST B05-2 (B17-13: quadruped companion API missing in the catalog)
            public Dictionary<string, AnimationClip> Clips;
            public Transform[] Bones; public Vector3[] NativePosition, FadePosition;
            public Quaternion[] NativeRotation, FadeRotation;
            public Vector3[] DisplayPosition; public Quaternion[] DisplayRotation; public bool HasDisplay;
            public Transform[] SupportPaws; public Transform SupportMarker; public Vector3 AnimatorPosition; public bool Aligned;
            public float SoleOffset, FadeSoleOffset;
            public Behaviour[] Ik; public bool[] IkEnabled;
            public AnimatorCullingMode Culling;
            public SkinnedMeshRenderer[] Meshes; public bool[] MeshOffscreen; public Bounds[] MeshBounds;
            public Pose Pose; public string Playing; public float Time, Fade, NextBite;
            public float Reach = 1f, Drag, JumpSince; public bool Applied, Held, Prepared, Jumping, Committing, HitSent;
            public bool Boss, WasQuiet, Dashing;
            public float NextIdleChange, AttackRate = 1f, RecoveryUntil, DashSpeed;
            public Vector3 DashDirection;
            public PlayMakerFSM DamageComponent; public float NextDamageLook;
            // (WW perf, D10) the target's collider (looked up once per target, again when it is destroyed or after 2 s) and this frame's
            // closest point / line of reach to it (TargetPoint and InReach ran the lookup, ClosestPoint and the Linecast up to 4 times a frame)
            public GameObject ColOf; public Collider TargetCol; public float ColAt;
            public int ReachFrame = -1; public GameObject ReachOf; public Vector3 ReachOrigin, ReachPoint; public sbyte ReachLine;
            public readonly Dictionary<Wait, float> OriginalWaits = new Dictionary<Wait, float>();
            public GameObject BiteTarget; public Fsm DamageFsm;
            public Transform[] LegTop, LegPaw;      // (1.9.2) the two front legs: shoulder bone and paw (deepest skinned bone)
            public Transform[] HindTop; public Transform FrontRoot; public bool LevelLogged; public float ShoulderOverHip;   // (1.9.3) hind leg tops; the bone that carries the front half (chest, front legs, neck)
        }
        private static readonly Dictionary<int, Dog> Dogs = new Dictionary<int, Dog>();
        private static readonly Dictionary<string, Dictionary<string, AnimationClip>> Clips = new Dictionary<string, Dictionary<string, AnimationClip>>(StringComparer.OrdinalIgnoreCase);
        // (WW) the six [Behaviour] entries are bound in NpcModule.BindConfig (F6: DogAnimations, DogsLieDown, DogJumpAttack; F7: the rest)
        private static ConfigEntry<bool> enabled { get { return CharacterSettings.DogAnimationsEnabled; } }
        private static ConfigEntry<bool> resting { get { return CharacterSettings.DogsLieDown; } }
        private static ConfigEntry<bool> jumping { get { return NpcModule.DogJumpAttack; } }
        private static ConfigEntry<float> idleMin { get { return NpcModule.DogIdlePoseMin; } }
        private static ConfigEntry<float> idleMax { get { return NpcModule.DogIdlePoseMax; } }
        private static ConfigEntry<float> frontLift { get { return CharacterSettings.DogLieFrontLift; } }
        private static AssetBundle bundle; private static bool _loadTried;
        /// <summary>True when the dog bundle loaded clips (F7 diagnostics).</summary>
        internal static bool BundleLoaded { get { return Clips.Count > 0; } }
        /// <summary>Number of dogs with a mapped rig.</summary>
        internal static int DogCount { get { return Dogs.Count; } }
        internal static int ClipCount { get { int n = 0; foreach (var c in Clips.Values) n += c.Count; return n; } }
        private static readonly List<int> _dead = new List<int>();      // (WW) reused (was a new List per frame)
        private static readonly List<PlayMakerFSM> _fsms = new List<PlayMakerFSM>();
        private static AssetBundle wildHoundBundle;
        private static Dictionary<string, AnimationClip> wildHoundClips;
        private const int Solid = (1 << 0) | (1 << 8) | (1 << 9) | (1 << 11) | (1 << 14) | (1 << 16);
        private const int Floor = (1 << 0) | (1 << 8) | (1 << 11) | (1 << 14);
        private static bool On { get { return PresentationModule.Running && enabled != null && enabled.Value && NpcModule.DogBiteEnabled != null && NpcModule.DogBiteEnabled.Value && Senses.On; } }

        /// <summary>(WW) the bundles load once per session (an AssetBundle cannot be loaded twice; a module restart reuses the clips); the hooks / patch are installed on every Init, only when clips are there (no bundle = the procedural DogBite only, as before)</summary>
        internal static void Initialize(PresentationModule module)
        {
            if (!_loadTried) { _loadTried = true; LoadBundles(); }
            if (Clips.Count == 0) return;
            Core.SaveBus.StateChanged -= OnSaveState;
            Core.SaveBus.StateChanged += OnSaveState;
            Core.Hooks.ProcessEvent.Prefix(module, BeforeDamageEvent);
            Core.Hooks.SetVelocityDo.Prefix(module, BeforeSetVelocity);
            module.PatchAction(typeof(Wait), "OnEnter", typeof(DogAnimations), "BeforeDamageWait", true);
        }

        /// <summary>Loads Presentation/Models/npcai_dogs.bundle (clips per creature prefab) and, when present, the native Wild Hound offsets bundle; failures keep the procedural bite.</summary>
        private static void LoadBundles()
        {
            string path = PresentationModule.AssetPath("Models/npcai_dogs.bundle");
            if (!File.Exists(path)) { if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Dog animations: bundle absent (" + path + "); using procedural bite."); return; }
            string models = Path.GetDirectoryName(path);
            try
            {
                bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null) throw new InvalidDataException("Unity could not load the canine animation bundle");
                foreach (var clip in bundle.LoadAllAssets<AnimationClip>())
                {
                    int split = clip.name.LastIndexOf('_'); if (split <= 0) continue;
                    string prefab = clip.name.Substring(0, split), action = clip.name.Substring(split + 1);
                    Dictionary<string, AnimationClip> list;
                    if (!Clips.TryGetValue(prefab, out list)) { list = new Dictionary<string, AnimationClip>(); Clips[prefab] = list; }
                    list[action] = clip;
                }
                // The companion positions its pivot by paw depth; native hounds use the game's original pivot.
                // Keep those animation offsets independent, without touching the companion's tuned clips.
                string wildPath = Path.Combine(models, "npcai_wildhound.bundle");
                if (File.Exists(wildPath))
                {
                    try
                    {
                        wildHoundBundle = AssetBundle.LoadFromFile(wildPath);
                        if (wildHoundBundle == null) throw new InvalidDataException("Native Wild Hound bundle could not be loaded");
                        var nativeClips = wildHoundBundle.LoadAllAssets<AnimationClip>().Where(c => c.name.StartsWith("Wild_Hound_", StringComparison.Ordinal)).ToDictionary(c => c.name.Substring("Wild_Hound_".Length));
                        if (new[] { "Idle", "Walk", "Run", "LieDown", "LieIdle", "GetUp", "Bite", "MovingBite", "JumpAttack" }.Any(action => !nativeClips.ContainsKey(action)))
                            throw new InvalidDataException("Native Wild Hound bundle is incomplete");
                        wildHoundClips = nativeClips;
                        Core.Log.Deep(PresentationModule.Instance, "Wild Hound: loaded independent native offsets; personal dog keeps companion offsets.");
                    }
                    catch (Exception e) { wildHoundClips = null; Core.Log.Warn(PresentationModule.Instance, "Native Wild Hound offsets unavailable; using shared clips: " + e.Message); }
                }
                if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Dog animations: loaded " + Clips.Values.Sum(c => c.Count) + " clips for " + Clips.Count + " creature rigs.");
            }
            catch (Exception e) { Clips.Clear(); Core.Log.Error(PresentationModule.Instance, "Dog animations unavailable; procedural fallback: " + e); }
        }

        /// <summary>(WW) module shutdown: every dog (native and companion) released - bones back to the Animator's pose, culling / IK / Damage waits / drag restored, the brain's hold lifted</summary>
        internal static void Shutdown()
        {
            Core.SaveBus.StateChanged -= OnSaveState;
            foreach (var dog in Dogs.Values) { try { Release(dog); } catch (Exception e) { Core.Log.Warn(PresentationModule.Instance, "Dog animations shutdown: " + e.Message); } }
            Dogs.Clear();
            DogCompanionCompat.Shutdown();   // refs-ok: REFACTOR-REQUEST B05-2 (B17-13: quadruped companion API missing in the catalog)
        }

        /// <summary>Maps the rig of a native quadruped NPC (called by NPCAI's dog registry).</summary>
        internal static void Register(Senses.Agent agent)
        { if (agent != null && agent.Owner != null) RegisterRig(agent, Senses.PrefabOf(agent.Owner).Replace(' ', '_'), null); }

        /// <summary>Maps the Dog Companion's rig with the Wild Hound clips; true when the companion is now animated.</summary>
        internal static bool RegisterCompanion(Senses.Agent agent, DogCompanionCompat.Driver driver)   // refs-ok: REFACTOR-REQUEST B05-2 (B17-13: quadruped companion API missing in the catalog)
        { RegisterRig(agent, "Wild_Hound", driver); return agent != null && agent.Owner != null && Dogs.ContainsKey(agent.Owner.GetInstanceID()); }

        /// <summary>Builds the per-dog record (bones, clips, meshes, IK, waits) when the prefab has the required clips.</summary>
        private static void RegisterRig(Senses.Agent agent, string prefab, DogCompanionCompat.Driver companion)   // refs-ok: REFACTOR-REQUEST B05-2 (B17-13: quadruped companion API missing in the catalog)
        {
            if (agent == null || agent.Owner == null) return;
            int id = agent.Owner.GetInstanceID(); if (Dogs.ContainsKey(id)) return;
            Dictionary<string, AnimationClip> clips;
            if (!Clips.TryGetValue(prefab, out clips) || new[] { "Idle", "Run", "LieDown", "LieIdle", "GetUp", "Bite" }.Any(n => !clips.ContainsKey(n))) return;
            if (companion != null && new[] { "Walk", "MovingBite", "SitDown", "SitIdle" }.Any(n => !clips.ContainsKey(n))) return;
            if (companion == null && prefab == "Wild_Hound" && wildHoundClips != null)
            {
                clips = new Dictionary<string, AnimationClip>(clips);
                foreach (var nativeClip in wildHoundClips) clips[nativeClip.Key] = nativeClip.Value;
            }
            var animator = agent.Owner.GetComponentInChildren<Animator>(true);
            var body = agent.Owner.GetComponent<Rigidbody>(); if (animator == null || (body == null && companion == null)) return;
            var meshes = agent.Owner.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var bones = meshes.SelectMany(r => r.bones).Where(t => t != null && t != animator.transform).Distinct().ToArray();
            if (bones.Length < 10) return;
            var dog = new Dog { Agent = agent, Companion = companion, Animator = animator, Body = body, Clips = clips, Bones = bones,
                NativePosition = new Vector3[bones.Length], NativeRotation = new Quaternion[bones.Length], FadePosition = new Vector3[bones.Length], FadeRotation = new Quaternion[bones.Length], Culling = animator.cullingMode,
                DisplayPosition = companion != null ? new Vector3[bones.Length] : null, DisplayRotation = companion != null ? new Quaternion[bones.Length] : null,
                Boss = prefab == "Alpha_Nightwalker" || agent.Owner.GetComponentsInChildren<PlayMakerFSM>(true).Any(f => f != null && f.FsmName == "BossUI") };
            dog.Ik = agent.Owner.GetComponentsInChildren<Behaviour>(true).Where(b => b != null && b.GetType().Name == "FastIKFabric").ToArray();
            dog.IkEnabled = dog.Ik.Select(b => b.enabled).ToArray();
            var rayObject = agent.T.Find("AttackRaycast");
            var ray = rayObject != null ? rayObject.GetComponent<Micosmo.SensorToolkit.RaySensor>() : null;
            if (ray != null && ray.Length > .1f) dog.Reach = ray.Length;
            FrontLegs(dog);
            if (companion != null)
            {
                dog.Meshes = meshes; dog.MeshOffscreen = new bool[meshes.Length]; dog.MeshBounds = new Bounds[meshes.Length];
                dog.SupportPaws = bones.Where(t => t.name.EndsWith("_toe", StringComparison.OrdinalIgnoreCase)).ToArray();
                var marker = animator.transform.Find("__NPCAI_PawSupport");
                if (marker == null) { marker = new GameObject("__NPCAI_PawSupport").transform; marker.SetParent(animator.transform, false); }
                dog.SupportMarker = marker;
            }
            if (companion != null) dog.NextBite = companion.NativeNextBite;
            Dogs[id] = dog;
            if (WildWasteland.Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Dog animations: " + agent.Owner.name + " mapped " + bones.Length + " bones; " + clips.Count + " clips.");
        }

        /// <summary>True while the dog may be animated: module/feature on, active, free-standing (not blown up, not travelling unless jumping/dashing) and within NPC range; companions by their driver state.</summary>
        private static bool Eligible(Dog d)
        {
            var a = d.Agent;
            if (d.Companion != null) return On && d.Companion.Active && a != null && a.Owner != null && a.Owner.activeInHierarchy && d.Animator != null && d.Animator.enabled;
            return On && a != null && a.Owner != null && a.Owner.activeInHierarchy && a.T != null && a.T.parent == null && !a.Blown && (!a.Travel || d.Jumping || d.Dashing)
                && d.Animator != null && d.Animator.enabled && d.Body != null && !d.Body.isKinematic && WithinRange(a);
        }
        /// <summary>True when the dog is within the NPC module's MaxDistance of the player.</summary>
        private static bool WithinRange(Senses.Agent agent)
        {
            var player = Senses.PlayerObj;
            float distance = NpcModule.MaxDistance != null ? NpcModule.MaxDistance.Value : 120f;
            return player == null || (agent.T.position - player.transform.position).sqrMagnitude <= distance * distance;
        }
        /// <summary>True when the dog is currently animated by this driver.</summary>
        internal static bool Owns(GameObject owner)
        { Dog d; return owner != null && Dogs.TryGetValue(owner.GetInstanceID(), out d) && Eligible(d); }

        /// <summary>Puts the Animator's own bone pose (and the companion's alignment offset) back.</summary>
        private static void Restore(Dog d)
        {
            if (!d.Applied) return;
            for (int i = 0; i < d.Bones.Length; i++) if (d.Bones[i] != null) { d.Bones[i].localPosition = d.NativePosition[i]; d.Bones[i].localRotation = d.NativeRotation[i]; }
            if (d.Aligned && d.Animator != null) d.Animator.transform.localPosition = d.AnimatorPosition;
            d.Aligned = false;
            d.Applied = false;
        }
        /// <summary>First use of a dog: always-animate culling, companion mesh bounds, native IK off.</summary>
        private static void Prepare(Dog d)
        {
            if (d.Prepared) return;
            d.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (d.Meshes != null)
            {
                for (int i = 0; i < d.Meshes.Length; i++) if (d.Meshes[i] != null)
                { d.MeshOffscreen[i] = d.Meshes[i].updateWhenOffscreen; d.MeshBounds[i] = d.Meshes[i].localBounds; }
                MeshCulling(d, !Core.SaveBus.Busy);
                if (WildWasteland.Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Dog animations: " + d.Agent.Owner.name + " companion mesh bounds updating (" + d.Meshes.Length + " renderers).");
            }
            for (int i = 0; i < d.Ik.Length; i++) if (d.Ik[i] != null) { d.IkEnabled[i] = d.Ik[i].enabled; d.Ik[i].enabled = false; }
            d.Prepared = true;
        }
        /// <summary>Ends a jump attack: drag and the brain's travel flag restored.</summary>
        private static void ReleaseJump(Dog d)
        {
            if (!d.Jumping) return;
            if (d.Body != null) d.Body.drag = d.Drag;
            if (d.Agent != null && d.Agent.Owner != null) { Brain.SetTravel(d.Agent.Owner, false); d.Agent.Travel = false; }
            d.Jumping = false;
        }
        /// <summary>Ends a dash: the companion's halt released, or the native dog's drag, planar velocity and travel flag restored.</summary>
        private static void ReleaseDash(Dog d)
        {
            if (!d.Dashing) return;
            if (d.Companion != null) d.Companion.Halt(false);
            else
            {
                if (d.Body != null) { d.Body.drag = d.Drag; Vector3 v = d.Body.velocity; v.x = v.z = 0f; d.Body.velocity = v; }
                if (d.Agent != null && d.Agent.Owner != null) { Brain.SetTravel(d.Agent.Owner, false); d.Agent.Travel = false; }
            }
            d.Dashing = false;
        }
        /// <summary>Hands the dog back to the game: pose, jump, dash, hold, Damage waits, culling, mesh bounds and IK restored.</summary>
        private static void Release(Dog d)
        {
            Restore(d); ReleaseJump(d); ReleaseDash(d);
            if (d.Held) Hold(d, false);
            d.Held = false; d.DamageFsm = null; d.BiteTarget = null; d.Pose = Pose.Locomotion; d.Playing = null;
            d.WasQuiet = false; d.NextIdleChange = 0f; d.RecoveryUntil = 0f;
            d.HasDisplay = false;
            d.SoleOffset = d.FadeSoleOffset = 0f;
            foreach (var wait in d.OriginalWaits) if (wait.Key.time != null) wait.Key.time.Value = wait.Value;
            d.OriginalWaits.Clear();
            if (!d.Prepared) return;
            MeshCulling(d, false);
            if (d.Animator != null) d.Animator.cullingMode = d.Culling;
            for (int i = 0; i < d.Ik.Length; i++) if (d.Ik[i] != null) d.Ik[i].enabled = d.IkEnabled[i];
            d.Prepared = false;
        }
        /// <summary>Retargeted clips and paw alignment can move the companion outside its imported animation bounds.</summary>
        private static void MeshCulling(Dog d, bool posed)
        {
            if (d.Meshes == null) return;
            for (int i = 0; i < d.Meshes.Length; i++) if (d.Meshes[i] != null)
            {
                d.Meshes[i].updateWhenOffscreen = posed || d.MeshOffscreen[i];
                if (!posed) d.Meshes[i].localBounds = d.MeshBounds[i];
            }
        }
        /// <summary>SaveBus state change: companion mesh bounds follow whether the world is playing.</summary>
        private static void OnSaveState(string previous, string current)
        { foreach (var d in Dogs.Values) if (d.Prepared) MeshCulling(d, current == "isPlay"); }
        /// <summary>Holds or releases the dog's movement (companion driver or NPC brain).</summary>
        private static void Hold(Dog d, bool held)
        { if (d.Companion != null) d.Companion.Halt(held); else if (d.Agent != null && d.Agent.Owner != null) Brain.Halt(d.Agent.Owner, held); }
        /// <summary>Planar speed of the dog, m/s.</summary>
        private static float Speed(Dog d)
        { return d.Companion != null ? d.Companion.Speed : new Vector2(d.Body.velocity.x, d.Body.velocity.z).magnitude; }
        /// <summary>Run speed of the dog (companion driver or NPC brain), m/s.</summary>
        private static float RunSpeed(Dog d)
        { return d.Companion != null ? d.Companion.RunSpeed : Brain.RunSpeed(d.Agent.Owner); }
        /// <summary>Reference point 25 cm above the dog's feet (reach and ground probes).</summary>
        private static Vector3 Origin(Dog d)
        { return (d.Companion != null ? d.Companion.Feet : d.Agent.T.position) + Vector3.up * .25f; }
        /// <summary>Restores the companion's native pose before the companion mod moves it.</summary>
        internal static void RestoreCompanion(GameObject owner)
        { Dog d; if (owner != null && Dogs.TryGetValue(owner.GetInstanceID(), out d)) Restore(d); }
        /// <summary>True while the companion is dashing.</summary>
        internal static bool CompanionDashing(GameObject owner)
        { Dog d; return owner != null && Dogs.TryGetValue(owner.GetInstanceID(), out d) && d.Dashing; }
        /// <summary>Releases and forgets the companion.</summary>
        internal static void RemoveCompanion(GameObject owner)
        { Dog d; if (owner != null && Dogs.TryGetValue(owner.GetInstanceID(), out d)) { Release(d); Dogs.Remove(owner.GetInstanceID()); } }
        /// <summary>Scene load: every dog released and forgotten, the companion bridge told.</summary>
        internal static void OnSceneLoaded() { foreach (var dog in Dogs.Values) Release(dog); Dogs.Clear(); DogCompanionCompat.OnSceneLoaded(); }   // refs-ok: REFACTOR-REQUEST B05-2 (B17-13: quadruped companion API missing in the catalog)

        /// <summary>Starts a new clip/pose and snapshots the current bones for the cross-fade.</summary>
        private static void Switch(Dog d, Pose pose, string clip)
        {
            for (int i = 0; i < d.Bones.Length; i++) if (d.Bones[i] != null)
            {
                d.FadePosition[i] = d.Companion != null && d.HasDisplay ? d.DisplayPosition[i] : d.Bones[i].localPosition;
                d.FadeRotation[i] = d.Companion != null && d.HasDisplay ? d.DisplayRotation[i] : d.Bones[i].localRotation;
            }
            d.Pose = pose; d.Playing = clip; d.Time = 0f; d.Fade = 0f;
            d.FadeSoleOffset = d.SoleOffset;
            d.AttackRate = 1f;
        }
        /// <summary>True when the dog's feet are on the floor (short ray down).</summary>
        private static bool Grounded(Dog d)
        {
            if (d.Companion != null) return Physics.Raycast(d.Companion.Feet + Vector3.up * .2f, Vector3.down, .4f, Floor, QueryTriggerInteraction.Ignore);
            var c = d.Agent.Col; if (c == null) return false;
            return Physics.Raycast(c.bounds.center, Vector3.down, c.bounds.extents.y + .18f, Floor, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Update (from NPCAI DogBite.Tick): advances every native dog; dogs whose owner is gone are released.</summary>
        internal static void Tick()
        {
            if (Dogs.Count == 0) return;
            var dead = _dead; dead.Clear();
            foreach (var pair in Dogs)
            {
                var d = pair.Value;
                if (d.Agent == null || d.Agent.Owner == null) { Release(d); dead.Add(pair.Key); continue; }
                if (d.Companion == null) TickDog(d, Time.deltaTime);
            }
            foreach (int id in dead) Dogs.Remove(id);
        }
        /// <summary>Advances the companion (called by the companion bridge).</summary>
        internal static void TickCompanion(GameObject owner)
        { Dog d; if (owner != null && Dogs.TryGetValue(owner.GetInstanceID(), out d)) TickDog(d, Time.deltaTime); }
        /// <summary>One dog's frame: pose choice (rest, bite, jump, dash, locomotion) and clip time.</summary>
        private static void TickDog(Dog d, float dt)
        {
            // Transition blends start from the prior displayed pose before restoring the native baseline.
            if (!Eligible(d)) { Release(d); return; }
            Prepare(d);
            var a = d.Agent; float speed = Speed(d);
            if (d.Companion != null && a.State == Senses.State.Combat && d.Playing != null && d.Playing.StartsWith("Sneak", StringComparison.Ordinal))
            {
                Restore(d); d.HasDisplay = false;
                Switch(d, Pose.Locomotion, "Idle");
            }
            bool riding = d.Companion != null && d.Companion.Riding;
            if (riding)
            {
                if (d.Pose != Pose.SitDown && d.Pose != Pose.Sitting) { ReleaseDash(d); d.BiteTarget = null; d.DamageFsm = null; Switch(d, Pose.SitDown, "SitDown"); }
            }
            else if (d.Pose == Pose.SitDown || d.Pose == Pose.Sitting) Switch(d, Pose.Locomotion, "Idle");
            bool quiet = d.Companion == null && resting.Value && !d.Boss && a.State == Senses.State.Idle && a.Target == null && a.Ghost == null && speed < .15f && Grounded(d);
            if (quiet && !d.WasQuiet) d.NextIdleChange = Time.time + IdleSpan();
            if (!quiet) d.NextIdleChange = 0f;
            d.WasQuiet = quiet;
            if (d.Companion != null && d.BiteTarget != null && (a.Target != d.BiteTarget || !d.Companion.TargetAlive(d.BiteTarget)))
            { ReleaseDash(d); d.BiteTarget = null; Switch(d, Pose.Locomotion, "Idle"); }
            if (d.Pose == Pose.Locomotion)
            {
                if (quiet && Time.time >= d.NextIdleChange) Switch(d, Pose.LieDown, "LieDown");
                else
                {
                    string clip = speed > 3f ? "Run" : speed > .15f && d.Clips.ContainsKey("Walk") ? "Walk" : "Idle";
                    if (d.Companion != null && d.Companion.Sneaking && d.Clips.ContainsKey("SneakIdle") && d.Clips.ContainsKey("SneakWalk")) clip = speed > .15f ? "SneakWalk" : "SneakIdle";
                    if (clip != d.Playing) Switch(d, Pose.Locomotion, clip);
                }
                if (a.State == Senses.State.Combat && a.Target != null)
                {
                    Vector3 to = TargetPoint(d, a.Target) - a.T.position; to.y = 0f;
                    if (to.sqrMagnitude > .0025f && to.magnitude <= d.Reach + 2f)
                        a.T.rotation = Quaternion.RotateTowards(a.T.rotation, Quaternion.LookRotation(to), 1080f * dt);
                    TryAttack(d);
                }
            }
            else if ((d.Pose == Pose.Rest || d.Pose == Pose.LieDown) && !quiet) Switch(d, Pose.GetUp, "GetUp");
            else if (d.Pose == Pose.Rest && quiet && Time.time >= d.NextIdleChange) Switch(d, Pose.GetUp, "GetUp");
            d.Fade += dt;
            float rate = d.Pose == Pose.Bite || d.Pose == Pose.RunningBite ? d.AttackRate : d.Playing == "Run" ? Mathf.Clamp(speed / Math.Max(2f, RunSpeed(d)), .35f, 2f) : 1f;
            if (d.Playing == "SneakWalk") rate = Mathf.Clamp(speed / 1.3f, .35f, 1.6f);
            d.Time += dt * rate;
            if (d.Dashing) DriveDash(d, dt);
            if (d.Jumping && ((Time.time - d.JumpSince > .12f && d.Body.velocity.y <= 0f && Grounded(d)) || Time.time - d.JumpSince > 2f)) ReleaseJump(d);
            if ((d.Pose == Pose.Bite || d.Pose == Pose.RunningBite || d.Pose == Pose.JumpAttack) && !d.HitSent && d.Time >= d.Clips[d.Playing].length * (d.Pose == Pose.Bite ? .38f : .45f))
            {
                d.HitSent = true;
                if (InReach(d, d.BiteTarget) && d.Companion != null) d.Companion.Contact(d.BiteTarget);
                else if (InReach(d, d.BiteTarget) && d.DamageFsm != null && d.DamageFsm.ActiveStateName == "trigger")
                {
                    d.Committing = true;
                    try
                    {
                        var victim = d.DamageFsm.Variables.FindFsmGameObject("detectedObj");
                        if (victim != null) victim.Value = d.BiteTarget;
                        d.DamageFsm.Event("attack");
                    }
                    finally { d.Committing = false; }
                }
            }
            if (d.Time >= d.Clips[d.Playing].length)
            {
                if (d.Pose == Pose.SitDown) Switch(d, Pose.Sitting, "SitIdle");
                else if (d.Pose == Pose.LieDown) { Switch(d, Pose.Rest, "LieIdle"); d.NextIdleChange = Time.time + IdleSpan(); }
                else if (d.Pose == Pose.GetUp || d.Pose == Pose.Bite || d.Pose == Pose.RunningBite || (d.Pose == Pose.JumpAttack && !d.Jumping))
                {
                    ReleaseDash(d); d.DamageFsm = null; d.BiteTarget = null; Switch(d, Pose.Locomotion, "Idle");
                    if (quiet) d.NextIdleChange = Time.time + IdleSpan();
                }
            }
            // Only hold melee position where a bite can actually reach. Outside it, continue chasing or commit to a running bite.
            bool close = a.State == Senses.State.Combat && a.Target != null && InReach(d, a.Target);
            bool held = !d.Dashing && (close || d.Pose != Pose.Locomotion || Time.time < d.RecoveryUntil);
            if (held != d.Held) { d.Held = held; Hold(d, held); }
            else if (held) Hold(d, true); // Brain may have registered after the rest/attack began.
            if (d.Companion == null && held && !d.Jumping && !d.Dashing) d.Body.velocity = new Vector3(0f, d.Body.velocity.y, 0f);
            Restore(d);
        }

        /// <summary>(1.9.2) front legs by name: a bone with "front" and the side in its name (leg_front_left_top0 / legFront_0Left), the shoulder being the one nearest the root, the paw its deepest descendant the skin uses</summary>
        private static void FrontLegs(Dog d)
        {
            var skinned = new HashSet<Transform>(d.Bones);
            var tops = new List<Transform>(); var paws = new List<Transform>();
            foreach (string side in new[] { "left", "right" })
            {
                Transform top = null; int topDepth = int.MaxValue;
                foreach (var b in d.Bones)
                {
                    string n = b.name.ToLowerInvariant();
                    if (!n.Contains("front") || !n.Contains(side)) continue;
                    int depth = 0; for (var t = b; t != null; t = t.parent) depth++;
                    if (depth < topDepth) { topDepth = depth; top = b; }
                }
                if (top == null) continue;
                Transform paw = null; int pawDepth = -1;
                foreach (var t in top.GetComponentsInChildren<Transform>(true))
                {
                    if (!skinned.Contains(t) || t == top) continue;
                    int depth = 0; for (var u = t; u != null && u != top; u = u.parent) depth++;
                    if (depth > pawDepth) { pawDepth = depth; paw = t; }
                }
                if (paw != null) { tops.Add(top); paws.Add(paw); }
            }
            d.LegTop = tops.ToArray(); d.LegPaw = paws.ToArray();
            // (1.9.3) hind leg tops (leg_hind_left_top0 / legRear_0Left), and the front half's root: the child, on the way up from a front
            // shoulder, of the first bone that also carries a hind leg (hounds/nightwalkers body_top0, Grimhound Spine2)
            var hind = new List<Transform>();
            foreach (string side in new[] { "left", "right" })
            {
                Transform top = null; int topDepth = int.MaxValue;
                foreach (var b in d.Bones)
                {
                    string n = b.name.ToLowerInvariant();
                    if (!(n.Contains("hind") || n.Contains("rear")) || !n.Contains(side)) continue;
                    int depth = 0; for (var t = b; t != null; t = t.parent) depth++;
                    if (depth < topDepth) { topDepth = depth; top = b; }
                }
                if (top != null) hind.Add(top);
            }
            d.HindTop = hind.ToArray(); d.FrontRoot = null;
            if (d.LegTop.Length == 0 || d.HindTop.Length == 0) return;
            for (Transform child = d.LegTop[0], up = child.parent; up != null; child = up, up = up.parent)
            {
                bool carries = false;
                foreach (var h in d.HindTop) if (h.IsChildOf(up)) { carries = true; break; }
                if (!carries) continue;
                if (child != d.LegTop[0] && !d.HindTop[0].IsChildOf(child)) d.FrontRoot = child;
                break;
            }
            // the rig's own shoulder height over the hips, standing (the pose it has when it registers), in its own frame
            if (d.FrontRoot != null) d.ShoulderOverHip = d.Agent.T.InverseTransformPoint(Mid(d.LegTop)).y - d.Agent.T.InverseTransformPoint(Mid(d.HindTop)).y;
        }

        /// <summary>Average position of the transforms.</summary>
        private static Vector3 Mid(Transform[] ts)
        {
            Vector3 m = Vector3.zero; int n = 0;
            foreach (var t in ts) if (t != null) { m += t.position; n++; }
            return n > 0 ? m / n : Vector3.zero;
        }

        /// <summary>(1.9.3) lying, getting down or up: the clips (made for another body) leave the hips above the ground and push the chest into it. The whole front half is turned up at its root until the shoulders stand over the hips as they do standing (+ DogLieFrontLift); never down. Faded in over the first 60 % of LieDown, out over the first 70 % of GetUp.</summary>
        private static void LevelFront(Dog d)
        {
            if (d.FrontRoot == null) return;
            float len0 = d.Clips[d.Playing].length;
            float w = d.Pose == Pose.Rest ? 1f : d.Pose == Pose.LieDown ? Mathf.Clamp01(d.Time / Mathf.Max(.05f, len0 * .6f)) : 1f - Mathf.Clamp01(d.Time / Mathf.Max(.05f, len0 * .7f));
            if (w <= 0f) return;
            Vector3 s = Vector3.zero, h = Vector3.zero; int ns = 0, nh = 0;
            foreach (var t in d.LegTop) if (t != null) { s += t.position; ns++; }
            foreach (var t in d.HindTop) if (t != null) { h += t.position; nh++; }
            if (ns == 0 || nh == 0) return;
            s /= ns; h /= nh;
            RaycastHit gs, gh;
            if (!Physics.Raycast(s + Vector3.up * .8f, Vector3.down, out gs, 2.5f, Floor, QueryTriggerInteraction.Ignore)) return;
            if (!Physics.Raycast(h + Vector3.up * .8f, Vector3.down, out gh, 2.5f, Floor, QueryTriggerInteraction.Ignore)) return;
            float extra = frontLift != null ? frontLift.Value : 0f;
            float lift = ((h.y - gh.point.y) + d.ShoulderOverHip - (s.y - gs.point.y) + extra) * w;
            if (WildWasteland.Core.Log.L2 && d.Pose == Pose.Rest && !d.LevelLogged)
            {
                d.LevelLogged = true;
                if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Dog animations: " + d.Agent.Owner.name + " lying: shoulders " + (s.y - gs.point.y).ToString("F3") + " m, hips " + (h.y - gh.point.y).ToString("F3") + " m above the ground (standing: shoulders " + d.ShoulderOverHip.ToString("F3") + " m over the hips); front raised " + lift.ToString("F3") + " m at " + d.FrontRoot.name);
            }
            if (d.Pose != Pose.Rest) d.LevelLogged = false;
            if (lift <= .003f) return;
            Vector3 pivot = d.FrontRoot.position, arm = s - pivot;
            Vector3 flat = arm; flat.y = 0f; float len = flat.magnitude;
            if (len < .05f) return;
            float ang = Mathf.Min(30f, Mathf.Asin(Mathf.Clamp01(lift / len)) * Mathf.Rad2Deg);
            Vector3 axis = d.Agent.T.right;
            float up1 = (Quaternion.AngleAxis(ang, axis) * arm).y, up2 = (Quaternion.AngleAxis(-ang, axis) * arm).y;
            d.FrontRoot.rotation = Quaternion.AngleAxis(up1 >= up2 ? ang : -ang, axis) * d.FrontRoot.rotation;
        }

        /// <summary>(1.9.2) lying, getting down or up: a front paw that the clip puts into the ground (the clips were made on flat ground for another body) is lifted by turning its whole leg at the shoulder, just enough to bring the paw onto the ground</summary>
        private static void PawsOnGround(Dog d)
        {
            if (d.LegTop == null || d.LegTop.Length == 0) return;
            var cam = Core.GameRefs.MainCamera;
            if (cam != null && (cam.transform.position - d.Agent.T.position).sqrMagnitude > 60f * 60f) return;     // too far to see
            Vector3 axis = d.Agent.T.right;
            for (int i = 0; i < d.LegTop.Length; i++)
            {
                var top = d.LegTop[i]; var paw = d.LegPaw[i];
                if (top == null || paw == null) continue;
                RaycastHit g;
                if (!Physics.Raycast(paw.position + Vector3.up * 0.6f, Vector3.down, out g, 1.5f, Floor, QueryTriggerInteraction.Ignore)) continue;
                float sink = g.point.y + 0.035f - paw.position.y;          // the paw bone sits a few cm inside the paw
                if (sink <= 0.003f) continue;
                Vector3 arm = paw.position - top.position; float len = arm.magnitude;
                if (len < 0.05f) continue;
                float ang = Mathf.Min(35f, Mathf.Asin(Mathf.Clamp01(sink / len)) * Mathf.Rad2Deg * 1.1f);
                // turn the way that raises the paw
                float up1 = (Quaternion.AngleAxis(ang, axis) * arm).y, up2 = (Quaternion.AngleAxis(-ang, axis) * arm).y;
                top.rotation = Quaternion.AngleAxis(up1 >= up2 ? ang : -ang, axis) * top.rotation;
            }
        }

        /// <summary>Job "Presentation.DogBiteLate" via DogBite.LateTick (LateUpdate): samples every native dog's clip onto its bones.</summary>
        internal static void LateTick()
        { foreach (var d in Dogs.Values) if (d.Companion == null) SampleDog(d); }
        /// <summary>Samples the companion's clip (called by the companion bridge in LateUpdate).</summary>
        internal static void LateCompanion(GameObject owner)
        { Dog d; if (owner != null && Dogs.TryGetValue(owner.GetInstanceID(), out d)) SampleDog(d); }
        /// <summary>Writes the playing clip's pose (cross-faded, front lifted when lying, paws on the ground) onto the dog's bones.</summary>
        private static void SampleDog(Dog d)
        {
            if (!Eligible(d) || string.IsNullOrEmpty(d.Playing)) return;
            if (d.Dashing) d.Agent.T.rotation = Quaternion.LookRotation(d.DashDirection);
            for (int i = 0; i < d.Bones.Length; i++) if (d.Bones[i] != null) { d.NativePosition[i] = d.Bones[i].localPosition; d.NativeRotation[i] = d.Bones[i].localRotation; }
            var clip = d.Clips[d.Playing]; bool loop = d.Pose == Pose.Locomotion || d.Pose == Pose.Rest || d.Pose == Pose.Sitting;
            float time = loop ? d.Time % clip.length : Mathf.Min(d.Time, clip.length);
            clip.SampleAnimation(d.Animator.gameObject, time);
            float blend = Mathf.Clamp01(d.Fade / .16f);
            if (blend < 1f) for (int i = 0; i < d.Bones.Length; i++) if (d.Bones[i] != null) { d.Bones[i].localPosition = Vector3.Lerp(d.FadePosition[i], d.Bones[i].localPosition, blend); d.Bones[i].localRotation = Quaternion.Slerp(d.FadeRotation[i], d.Bones[i].localRotation, blend); }
            if (d.Pose == Pose.LieDown || d.Pose == Pose.Rest || d.Pose == Pose.GetUp) { var cam = Core.GameRefs.MainCamera; if (cam == null || (cam.transform.position - d.Agent.T.position).sqrMagnitude <= 60f * 60f) LevelFront(d); PawsOnGround(d); }
            if (d.Companion != null) AlignCompanion(d);
            if (d.Companion != null)
            {
                for (int i = 0; i < d.Bones.Length; i++) if (d.Bones[i] != null) { d.DisplayPosition[i] = d.Bones[i].localPosition; d.DisplayRotation[i] = d.Bones[i].localRotation; }
                d.HasDisplay = true;
            }
            d.Applied = true;
        }

        /// <summary>The companion positions its pivot pawDepth above the support surface. Retargeted poses have another paw height. Correct only the visual Rig, in the dog's frame so the same placement works on a tilted car seat.</summary>
        private static void AlignCompanion(Dog d)
        {
            if (d.SupportPaws == null || d.SupportPaws.Length == 0) return;
            float lowest = float.MaxValue;
            foreach (var paw in d.SupportPaws) if (paw != null) lowest = Mathf.Min(lowest, d.Agent.T.InverseTransformPoint(paw.position).y);
            if (lowest == float.MaxValue) return;
            if (!d.Aligned) d.AnimatorPosition = d.Animator.transform.localPosition;
            float soleOffset = d.SupportMarker != null ? d.SupportMarker.localPosition.y : 0f;
            soleOffset = Mathf.Lerp(d.FadeSoleOffset, soleOffset, Mathf.Clamp01(d.Fade / .16f));
            d.SoleOffset = soleOffset;
            d.Animator.transform.position += d.Agent.T.TransformVector(Vector3.up * (d.Companion.PawHeight - lowest - soleOffset));
            d.Aligned = true;
        }
    }
}
