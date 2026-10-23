using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Temporary reactions borrow native dead-prefab joint/collider data on the living rig; no corpse or saved object is spawned.</summary>
    internal sealed class SkillReactions : ISkillReactions
    {
        private sealed class Held
        {
            internal GameObject Owner;
            internal float Until;
            internal Rigidbody Root;
            internal bool Kinematic;
            internal bool Ragdoll, Recovering;
            internal Vector3 AnchorOffset;
            internal readonly List<Behaviour> Disabled = new List<Behaviour>();
            internal readonly List<PlayMakerFSM> Paused = new List<PlayMakerFSM>();
            internal readonly List<Collider> Colliders = new List<Collider>();
            internal readonly List<Component> Added = new List<Component>();
            internal readonly Dictionary<Transform, Pose> Poses = new Dictionary<Transform, Pose>();
            internal readonly List<Rigidbody> Bones = new List<Rigidbody>();
        }
        private static readonly Dictionary<GameObject,Held> Active = new Dictionary<GameObject,Held>();
        private readonly List<GameObject> _done = new List<GameObject>();
        private readonly Dictionary<GameObject,float> _maximum = new Dictionary<GameObject,float>();
        private readonly Dictionary<GameObject,HashSet<Collider>> _contacts = new Dictionary<GameObject,HashSet<Collider>>();
        private static readonly HashSet<GameObject> BodyColliders = new HashSet<GameObject>();
        private float _nextCleanup;
        private static int _pauseBit;
        internal static bool Busy(GameObject npc) { return npc != null && Active.ContainsKey(npc); }
        internal void Init(IModule owner)
        { _pauseBit = FsmPause.Bit(owner,"Skill reaction"); Scheduler.EveryFrame(owner,"NPC.Skill reactions",Tick,Phase.LateUpdateLast); Hooks.FsmPhysicsCallback.Prefix(owner,Contact); Hooks.CreateObjectEnter.Prefix(owner,BeforeCorpse); }
        private static bool BeforeCorpse(CreateObject action)
        { Held held; if (action.Fsm.Name == "Health" && action.Fsm.ActiveStateName == "spawn" && Active.TryGetValue(action.Owner,out held)) RestoreBones(held); return true; }
        public PerkEnemyClass ClassOf(GameObject npc)
        {
            if (npc == null) return PerkEnemyClass.Other;
            var agent = Senses.AgentFor(npc); string name = Names.Base(npc.name);
            if (name.IndexOf("Juggernaut",System.StringComparison.OrdinalIgnoreCase) >= 0) return PerkEnemyClass.Juggernaut;
            if (agent != null && agent.Human || name.IndexOf("Zombie",System.StringComparison.OrdinalIgnoreCase) >= 0) return PerkEnemyClass.Humanoid;
            if (name.IndexOf("Rat",System.StringComparison.OrdinalIgnoreCase) >= 0 || name == "Lizard" || name == "Bat") return PerkEnemyClass.Small;
            if (name.IndexOf("hound",System.StringComparison.OrdinalIgnoreCase) >= 0 || name == "Dog" || name == "Herbivore" || name == "Arachnid" || name == "Burrower") return PerkEnemyClass.Quadruped;
            if (FsmUtil.Find(npc,"Health") != null) return PerkEnemyClass.LargeMutant;
            return PerkEnemyClass.Other;
        }
        public float MaximumHealth(GameObject npc)
        {
            float max;
            if (_maximum.TryGetValue(npc,out max)) return max;
            var prefab = PrefabCatalog.Find(Names.Base(npc.name)); var health = prefab != null ? FsmUtil.Find(prefab,"Health") : null;
            var hp = health != null ? health.FsmVariables.FindFsmFloat("Health") : null;
            max = hp != null ? hp.Value : ActorQuery.HealthOf(npc);
            if (Bosses.IsTracked(npc) && NpcaiSettings.BossHealth.Value) max *= Mathf.Max(1,NpcaiSettings.BossHpPercent.Value) / 100f;
            _maximum[npc] = Mathf.Max(max,ActorQuery.HealthOf(npc)); return _maximum[npc];
        }
        public bool Unaware(GameObject npc) { return Senses.IsIdle(npc); }
        public bool IsHeld(GameObject npc) { return Busy(npc); }
        public bool IsBodyCollider(GameObject hit) { return hit != null && BodyColliders.Contains(hit); }
        public void React(GameObject npc, Vector3 push, float seconds, bool ragdoll)
        {
            if (npc == null || ActorQuery.HealthOf(npc) < 1 || npc.transform.parent != null) return;
            if (ragdoll && !(ModApi.Get<INpcRecovery>()?.Available(npc) ?? false)) { seconds = 0; ragdoll = false; }
            var rb = npc.GetComponent<Rigidbody>();
            if (push.sqrMagnitude > 0 && rb != null && !rb.isKinematic) rb.AddForce(push,ForceMode.VelocityChange);
            if (seconds <= 0 || Active.ContainsKey(npc)) return;
            var held = new Held { Owner = npc, Until = Time.time + seconds, Root = rb, Kinematic = rb != null && rb.isKinematic, Ragdoll = ragdoll };
            Active.Add(npc,held);
            foreach (var f in npc.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Attack" || f.FsmName == "Movement" || f.FsmName == "Rotate" || f.FsmName == "Detection" || f.FsmName == "Damage" || f.FsmName == "Damage Ranged" || f.FsmName == "RangedAttackWait" || f.FsmName == "Unstuck") { held.Paused.Add(f); FsmPause.Set(f.Fsm,_pauseBit); }
            foreach (var a in npc.GetComponentsInChildren<Animator>()) if (a.enabled) { held.Disabled.Add(a); a.enabled = false; }
            foreach (var a in npc.GetComponentsInChildren<Animation>()) if (a.enabled) { held.Disabled.Add(a); a.enabled = false; }
            if (rb != null) { if (!rb.isKinematic) rb.velocity = new Vector3(push.x,rb.velocity.y,push.z); if (ragdoll) rb.isKinematic = true; }
            if (ragdoll) BorrowRagdoll(held,push);
        }
        private static string Path(Transform node, Transform root)
        { string path = node.name; while (node.parent != null && node.parent != root) { node = node.parent; path = node.name + "/" + path; } return path; }
        private static void BorrowRagdoll(Held held,Vector3 push)
        {
            var hp = FsmUtil.Find(held.Owner,"Health"); GameObject prefab = null;
            foreach (var state in hp.FsmStates) foreach (var action in state.Actions)
            { var create = action as CreateObject; var go = create?.gameObject?.Value; if (go != null && go.name.Contains("_Dead")) { prefab = go; break; } }
            if (prefab == null)
            { var animator = held.Owner.GetComponentInChildren<Animator>(true); if (animator != null && animator.isHuman) prefab = PrefabCatalog.Find("Zombie_Runner_Dead"); }
            if (prefab == null) return;
            var map = new Dictionary<Rigidbody,Rigidbody>();
            foreach (var source in prefab.GetComponentsInChildren<Rigidbody>(true))
            {
                if (source.transform == prefab.transform) continue;
                var bone = held.Owner.transform.Find(Path(source.transform,prefab.transform)); if (bone == null || bone.GetComponent<Rigidbody>() != null) continue;
                held.Poses[bone] = new Pose(bone.localPosition,bone.localRotation);
                var body = bone.gameObject.AddComponent<Rigidbody>(); body.mass = source.mass; body.drag = source.drag; body.angularDrag = source.angularDrag; body.interpolation = RigidbodyInterpolation.Interpolate;
                held.Added.Add(body); held.Bones.Add(body); map[source] = body;
                foreach (var collider in source.GetComponents<Collider>())
                {
                    Collider copy = null; var cap = collider as CapsuleCollider; var box = collider as BoxCollider; var sphere = collider as SphereCollider;
                    if (cap != null) { var c = bone.gameObject.AddComponent<CapsuleCollider>(); c.center = cap.center; c.radius = cap.radius; c.height = cap.height; c.direction = cap.direction; copy = c; }
                    else if (box != null) { var c = bone.gameObject.AddComponent<BoxCollider>(); c.center = box.center; c.size = box.size; copy = c; }
                    else if (sphere != null) { var c = bone.gameObject.AddComponent<SphereCollider>(); c.center = sphere.center; c.radius = sphere.radius; copy = c; }
                    if (copy != null) { copy.sharedMaterial = collider.sharedMaterial; held.Added.Add(copy); }
                }
            }
            if (map.Count == 0) return;
            held.AnchorOffset = held.Bones[0].position - held.Owner.transform.position;
            foreach (var body in held.Bones) if (FsmUtil.Find(body.gameObject,"Bodypart") == null) AddBodypart(held,body.gameObject);
            foreach (var collider in held.Owner.GetComponentsInChildren<Collider>(true)) if (collider.enabled && !held.Added.Contains(collider)) { held.Colliders.Add(collider); collider.enabled = false; }
            foreach (var pair in map)
            {
                var source = pair.Key.GetComponent<CharacterJoint>(); Rigidbody connected;
                if (source == null || source.connectedBody == null || !map.TryGetValue(source.connectedBody,out connected)) continue;
                var joint = pair.Value.gameObject.AddComponent<CharacterJoint>(); joint.connectedBody = connected; joint.anchor = source.anchor; joint.axis = source.axis; joint.swingAxis = source.swingAxis;
                joint.lowTwistLimit = source.lowTwistLimit; joint.highTwistLimit = source.highTwistLimit; joint.swing1Limit = source.swing1Limit; joint.swing2Limit = source.swing2Limit; joint.enableProjection = true; held.Added.Add(joint);
            }
            foreach (var body in held.Bones) body.velocity = push + Vector3.up;
        }
        private static void AddBodypart(Held held,GameObject bone)
        {
            BodyColliders.Add(bone);
            var part = bone.AddComponent<PlayMakerFSM>(); part.enabled = false; part.Fsm.Name = "Bodypart";
            var damage = new FsmFloat { Name = "Damage", Value = 0 }; part.FsmVariables.FloatVariables = new[] { damage }; part.FsmVariables.Init();
            var off = new FsmState(part.Fsm) { Name = "off", Actions = new FsmStateAction[0], Transitions = new FsmTransition[0] };
            var hit = new FsmState(part.Fsm) { Name = "damage", Actions = new FsmStateAction[] { new FsmFloatAdd { gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = new FsmGameObject(held.Owner) }, fsmName = "Health", variableName = "Health", add = damage, everyFrame = false, perSecond = false } },
                Transitions = new[] { new FsmTransition { FsmEvent = FsmEvent.GetFsmEvent("FINISHED"), ToState = "off" } } };
            part.Fsm.States = new[] { off,hit }; part.Fsm.StartState = "off"; part.Fsm.GlobalTransitions = new[] { new FsmTransition { FsmEvent = FsmEvent.GetFsmEvent("Damage"), ToState = "damage" } };
            part.Fsm.Events = new[] { FsmEvent.GetFsmEvent("Damage"), FsmEvent.GetFsmEvent("FINISHED") }; off.SaveActions(); hit.SaveActions();
            part.Fsm.InitData(); part.Fsm.Preprocess(part); part.Fsm.Init(part); part.enabled = true; held.Added.Add(part);
        }
        private bool Contact(ref Hooks.FsmPhysicsArgs args)
        {
            if (args.Fsm.GameObject != GameRefs.Player || args.Collision == null) return true;
            var npc = ActorQuery.Creature(args.Collision.transform); if (npc == null) return true;
            HashSet<Collider> contacts;
            if (args.Kind == Hooks.FsmPhysicsKind.CollisionExit) { if (_contacts.TryGetValue(npc.gameObject,out contacts)) { contacts.Remove(args.Collision.collider); if (contacts.Count == 0) _contacts.Remove(npc.gameObject); } return true; }
            if (args.Kind != Hooks.FsmPhysicsKind.CollisionEnter) return true;
            if (!_contacts.TryGetValue(npc.gameObject,out contacts)) { contacts = new HashSet<Collider>(); _contacts.Add(npc.gameObject,contacts); }
            bool fresh = contacts.Count == 0; contacts.Add(args.Collision.collider);
            if (!fresh || !GameRefs.InPlay || SaveBus.Busy || !SkillPerks.Has("Paintrain") || PlayerState.MoveState != "Running" || ClassOf(npc.gameObject) != PerkEnemyClass.Humanoid) return true;
            var velocity = GameRefs.Player.GetComponent<Rigidbody>().velocity; velocity.y = 0;
            React(npc.gameObject,velocity.normalized * 4,2.5f,true); return true;
        }
        private static void Restore(Held held)
        {
            ModApi.Get<INpcRecovery>()?.End(held.Owner);
            RestoreBones(held);
            if (held.Root != null) held.Root.isKinematic = held.Kinematic;
            foreach (var fsm in held.Paused) if (fsm != null) FsmPause.Clear(fsm.Fsm,_pauseBit);
            foreach (var behaviour in held.Disabled) if (behaviour != null) behaviour.enabled = true;
        }
        private static void RestoreBones(Held held)
        {
            if (held.Owner != null && held.Bones.Count > 0 && held.Bones[0] != null)
            {
                Vector3 destination = held.Bones[0].position - held.AnchorOffset; destination.y = held.Owner.transform.position.y;
                RaycastHit floor; if (Physics.Raycast(destination + Vector3.up * 2,Vector3.down,out floor,6,(1 << 0) | (1 << 14),QueryTriggerInteraction.Ignore)) destination.y = floor.point.y;
                held.Owner.transform.position = destination;
            }
            foreach (var item in held.Added) { var col = item as Collider; if (col != null) col.enabled = false; var body = item as Rigidbody; if (body != null) body.isKinematic = true; if (item != null) Object.Destroy(item); }
            foreach (var pose in held.Poses) if (pose.Key != null) { pose.Key.localPosition = pose.Value.position; pose.Key.localRotation = pose.Value.rotation; }
            foreach (var col in held.Colliders) if (col != null) col.enabled = true;
            held.Added.Clear(); held.Poses.Clear(); held.Bones.Clear(); held.Colliders.Clear();
        }
        private void Tick()
        {
            _done.Clear(); foreach (var pair in Active)
            {
                var held = pair.Value;
                if (pair.Key != null && ActorQuery.HealthOf(pair.Key) >= 1 && Time.time >= held.Until && held.Ragdoll && !held.Recovering)
                {
                    Vector3 front = pair.Key.transform.forward;
                    var animator = pair.Key.GetComponentInChildren<Animator>(true);
                    if (animator != null && animator.isHuman) { var chest = animator.GetBoneTransform(HumanBodyBones.Chest); if (chest != null) front = chest.forward; }
                    RestoreBones(held); if (animator != null) animator.enabled = true;
                    float duration = ModApi.Get<INpcRecovery>()?.Begin(pair.Key,front) ?? 0;
                    held.Recovering = true; held.Until = Time.time + duration;
                }
                if (pair.Key == null || Time.time >= held.Until || ActorQuery.HealthOf(pair.Key) < 1) { Restore(held); _done.Add(pair.Key); }
            }
            foreach (var npc in _done) Active.Remove(npc);
            if (Time.time >= _nextCleanup) { _nextCleanup = Time.time + 1; BodyColliders.RemoveWhere(go => go == null); _done.Clear(); foreach (var pair in _maximum) if (pair.Key == null) _done.Add(pair.Key); foreach (var npc in _done) { _maximum.Remove(npc); _contacts.Remove(npc); } }
        }
        internal void Clear() { foreach (var held in Active.Values) Restore(held); Active.Clear(); BodyColliders.Clear(); _maximum.Clear(); _contacts.Clear(); _nextCleanup = 0; }
    }
}
