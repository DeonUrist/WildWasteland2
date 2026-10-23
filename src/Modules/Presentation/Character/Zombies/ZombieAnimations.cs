using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>Zombie pose decisions over the native sensing, attack ray, damage and corpse FSMs.</summary>
    internal static class ZombieAnimations
    {
        enum Pose { Locomotion, Rest, LieDown, GetUp, Agony, Scream, Attack }
        sealed class Zombie
        {
            internal GameObject Owner, Victim, LastTarget;
            internal Rigidbody Body;
            internal Collider Collider;
            internal PlayMakerFSM Attack, Movement, Recovery, Rotation;
            internal FsmGameObject Target;
            internal FsmFloat Health;
            internal ZombieRig Rig;
            internal Micosmo.SensorToolkit.RaySensor Ray;
            internal Vector3 AttackDirection;
            internal Vector3 LastPosition;
            internal Pose Pose;
            internal string Clip, GetUp;
            internal float Since, Until, IdleAt, Sample, GroundAt, PoseAt, NativeReach, AttackSpeed;
            internal bool Held, Hit, OnGround, Settling, Traversing;
            internal ICanineBehaviour MovementOwner;
        }
        static readonly Dictionary<int, Zombie> Live = new Dictionary<int, Zombie>();
        static readonly Queue<GameObject> Pending = new Queue<GameObject>();
        static readonly HashSet<int> Queued = new HashSet<int>();
        static readonly List<int> Dead = new List<int>();
        static readonly string[] RestStyles = { "GetUp1", "GetUp2", "GetUp3" };
        static int _pauseBit;
        const float PunchSeconds = 1f;
        const float GetUpSeconds = 3.2f, ScreamDistance = 12f;
        const int Floor = (1 << 0) | (1 << 8) | (1 << 11) | (1 << 14);
        static bool On { get { return PresentationModule.Running && CharacterSettings.ZombieAnimations.Value && ZombieRig.Ready; } }
        internal static int Count { get { return Live.Count; } }

        internal static void Traversal(Animator animator, bool active)
        {
            if (animator == null || animator.transform.parent == null) return;
            Zombie z;
            if (!Live.TryGetValue(animator.transform.parent.gameObject.GetInstanceID(), out z)) return;
            z.Traversing = active; z.LastPosition = z.Owner.transform.position; z.Rig.Suspend(active);
            if (!active && Alive(z)) ResumeRun(z);
        }

        internal static void Initialize(PresentationModule module)
        {
            ZombieRig.Load();
            _pauseBit = FsmPause.Bit(module, "Zombie stationary animation");
            Hooks.SensorDetectionsDo.Prefix(module, BeforeDetection);
            Hooks.SetVelocityDo.Prefix(module, BeforeVelocity);
            Hooks.ProcessEvent.Prefix(module, BeforeEvent);
            Hooks.SmoothLookAtDo.Prefix(module, BeforeLook);
            ModEvents.Subscribe<Core.Events.NpcSpawned>(module, Spawned);
            Scheduler.EveryFrame(module, "Presentation.ZombieBehavior", Tick, Phase.Update);
            Scheduler.EveryFrame(module, "Presentation.ZombieHold", FixedTick, Phase.EarlyFixedUpdate);
            Scheduler.EveryFrame(module, "Presentation.ZombiePoses", LateTick, Phase.LateUpdateLast);
        }

        static bool Candidate(GameObject owner)
        { return owner != null && owner.transform.parent == null && (Names.BaseEquals(owner.name, "Zombie_Runner") || Names.BaseEquals(owner.name, "Blast Zombie")); }

        static void Queue(GameObject owner)
        {
            if (!On || !Candidate(owner)) return;
            int id = owner.GetInstanceID();
            if (!Live.ContainsKey(id) && Queued.Add(id)) Pending.Enqueue(owner);
        }
        static void Spawned(ref Core.Events.NpcSpawned spawned) { Queue(Entities.Find(spawned.Npc)); }
        static bool BeforeDetection(SensorGetDetections action)
        { if (On && action.Fsm != null && action.Fsm.Name == "Detection") Queue(action.Fsm.GameObject); return true; }

        /// <summary>Native Movement also discovers manual Apocaspawner clones when NPC AI is disabled.</summary>
        static bool BeforeVelocity(SetVelocity action)
        {
            if (!On || action.Fsm == null || action.Fsm.Name != "Movement") return true;
            var owner = action.Fsm.GameObject; Zombie zombie;
            if (owner == null) return true;
            if (!Live.TryGetValue(owner.GetInstanceID(), out zombie)) { Queue(owner); return !Queued.Contains(owner.GetInstanceID()); }
            return !zombie.Held;
        }

        static bool Alive(Zombie z)
        { return z.Owner != null && z.Owner.activeInHierarchy && z.Owner.transform.parent == null && z.Rig.Valid && (z.Health == null || z.Health.Value >= 1f); }
        static bool Grounded(Zombie z)
        {
            if (Time.time < z.GroundAt) return z.OnGround;
            z.GroundAt = Time.time + .2f;
            Vector3 point = z.Owner.transform.position;
            if (z.Collider != null) point.y = z.Collider.bounds.min.y;
            z.OnGround = Physics.Raycast(point + Vector3.up * .25f, Vector3.down, .6f, Floor, QueryTriggerInteraction.Ignore);
            return z.OnGround;
        }
        static bool Quiet(Zombie z)
        {
            var npc = ModApi.Get<INpcApi>();
            return z.Target.Value == null && (npc == null ? z.Attack == null || !z.Attack.enabled : npc.IsIdle(z.Owner));
        }

        /// <summary>Builds at most one named native rig per frame; no hierarchy or world scan.</summary>
        static void Register(GameObject owner)
        {
            if (!Candidate(owner) || !owner.activeInHierarchy) return;
            var animRoot = owner.transform.Find("Anim");
            var animator = animRoot != null ? animRoot.GetComponent<Animator>() : null;
            var attack = FsmUtil.Find(owner, "Attack"); var detection = FsmUtil.Find(owner, "Detection");
            var health = FsmUtil.Find(owner, "Health");
            var target = detection != null ? detection.FsmVariables.FindFsmGameObject("detectedObj") : null;
            if (animator == null || !animator.isHuman || attack == null || target == null || ModAPI.Find(animator) != null) return;
            var z = new Zombie { Owner = owner, LastPosition = owner.transform.position, Body = owner.GetComponent<Rigidbody>(), Collider = owner.GetComponent<Collider>(),
                Attack = attack, Movement = FsmUtil.Find(owner, "Movement"), Recovery = FsmUtil.Find(owner, "Unstuck"), Rotation = FsmUtil.Find(owner, "Rotate"), Target = target,
                Health = health != null ? health.FsmVariables.FindFsmFloat("Health") : null,
                GetUp = RestStyles[UnityEngine.Random.Range(0, RestStyles.Length)] };
            if (z.Body == null || (z.Health != null && z.Health.Value < 1f)) return;
            z.Rig = new ZombieRig(animator);
            var rayRoot = owner.transform.Find("AttackRaycast");
            z.Ray = rayRoot != null ? rayRoot.GetComponent<Micosmo.SensorToolkit.RaySensor>() : null;
            if (z.Ray != null) { z.NativeReach = z.Ray.Length; z.Ray.Length = Mathf.Max(z.NativeReach, 2f); }
            Live[owner.GetInstanceID()] = z;
            bool resting = UnityEngine.Random.value < .5f;
            z.Settling = resting && !Grounded(z);
            Switch(z, resting ? Pose.Rest : Pose.Locomotion, resting ? z.GetUp : "Idle");
            z.IdleAt = Time.time + UnityEngine.Random.Range(10f, 25f);
            if (Log.L2) Log.Deep(PresentationModule.Instance, "Zombie animations: registered " + owner.name + "; pose=" + z.Pose + "; stand-up=" + z.GetUp);
        }

        static void Switch(Zombie z, Pose pose, string clip)
        {
            z.Pose = pose; z.Clip = clip; z.Since = Time.time;
            z.Until = z.Since + ZombieRig.Length(clip);
            if (pose == Pose.Attack) z.Until = z.Since + PunchSeconds;
            if (pose == Pose.GetUp || pose == Pose.LieDown) z.Until = z.Since + Mathf.Min(ZombieRig.Length(clip), GetUpSeconds);
            z.Sample = pose == Pose.LieDown ? z.Until - z.Since : 0f;
            z.Rig.Play(clip, z.Sample, .18f);
            Hold(z, pose != Pose.Locomotion);
        }

        /// <summary>Uses the same typed movement lease as the canine animation driver; sensing stays live.</summary>
        static void Hold(Zombie z, bool held)
        {
            var provider = ModApi.Get<ICanineBehaviour>();
            if (z.MovementOwner != provider)
            {
                if (z.Held && z.MovementOwner != null) z.MovementOwner.Travel(z.Owner, false);
                z.MovementOwner = provider;
                if (held && provider != null) provider.Travel(z.Owner, true);
            }
            else if ((held || z.Held != held) && provider != null) provider.Travel(z.Owner, held);
            z.Held = held;
            Pause(z.Recovery, held); Pause(z.Rotation, held);
            if (held && z.Pose != Pose.Attack) Stop(z);
        }
        static void Pause(PlayMakerFSM fsm, bool held)
        { if (fsm != null) { if (held) FsmPause.Set(fsm.Fsm, _pauseBit); else FsmPause.Clear(fsm.Fsm, _pauseBit); } }
        static void Stop(Zombie z)
        {
            if (z.Body == null || z.Body.isKinematic) return;
            var velocity = z.Body.velocity; velocity.x = 0f; velocity.z = 0f; z.Body.velocity = velocity;
        }
        /// <summary>A running punch commits to its wind-up direction; a sidestep is never followed mid-strike.</summary>
        static void DriveAttack(Zombie z)
        {
            if (z.Body == null || z.Body.isKinematic) return;
            float speed = Time.time < z.Since + .5f ? z.AttackSpeed : 2f;
            RaycastHit hit;
            bool blocked = z.Body.SweepTest(z.AttackDirection, out hit, speed * Time.fixedDeltaTime + .08f, QueryTriggerInteraction.Ignore);
            if (blocked && (hit.normal.y > .6f || (z.Victim != null && (hit.collider.gameObject == z.Victim || hit.collider.transform.IsChildOf(z.Victim.transform))))) blocked = false;
            Vector3 velocity = z.AttackDirection * (blocked ? 0f : speed); velocity.y = z.Body.velocity.y;
            z.Owner.transform.rotation = Quaternion.LookRotation(z.AttackDirection); z.Body.velocity = velocity;
        }

        static void ResumeRun(Zombie z)
        {
            Pose previous = z.Pose;
            bool run = z.Target.Value != null;
            Switch(z, Pose.Locomotion, run ? "Run" : "Idle");
            if (run && z.Movement != null)
            {
                z.Movement.SendEvent("Animal_Run");
                Vector3 direction = z.Target.Value.transform.position - z.Owner.transform.position; direction.y = 0f;
                if (direction.sqrMagnitude > .01f && z.Body != null && !z.Body.isKinematic)
                { direction.Normalize(); Vector3 velocity = direction * 5f; velocity.y = z.Body.velocity.y; z.Owner.transform.rotation = Quaternion.LookRotation(direction); z.Body.velocity = velocity; }
                if (Log.L2) Log.Deep(PresentationModule.Instance, "Zombie animations: " + z.Owner.name + " running after " + previous);
            }
        }

        static void Wake(Zombie z)
        {
            if (z.Pose == Pose.GetUp) return;
            float from = z.Pose == Pose.LieDown ? z.Sample : 0f;
            Switch(z, Pose.GetUp, z.GetUp);
            z.Since -= from; z.Until -= from;
            z.Rig.Play(z.GetUp, from, .18f);
            if (Log.L2) Log.Deep(PresentationModule.Instance, "Zombie animations: " + z.Owner.name + " standing through " + z.GetUp + "; native recovery paused=" + (z.Recovery != null && FsmPause.Held(z.Recovery.Fsm, _pauseBit)));
        }

        /// <summary>Native ray events start the running punch and must still authorize its contact.</summary>
        static bool BeginAttack(Zombie z)
        {
            if (z.Pose == Pose.Rest || z.Pose == Pose.LieDown) { Wake(z); return false; }
            if (z.Pose != Pose.Locomotion || z.Target.Value == null) return false;
            z.Hit = false; z.Victim = z.Target.Value;
            Vector3 direction = z.Victim.transform.position - z.Owner.transform.position; direction.y = 0f;
            z.AttackDirection = direction.sqrMagnitude > .01f ? direction.normalized : z.Owner.transform.forward;
            z.AttackSpeed = Mathf.Clamp(Mathf.Max(new Vector2(z.Body.velocity.x, z.Body.velocity.z).magnitude, 5f), 5f, 7f);
            Switch(z, Pose.Attack, "Attack");
            DriveAttack(z);
            if (Log.L2) Log.Deep(PresentationModule.Instance, "Zombie animations: " + z.Owner.name + " running punch; reach=" + (z.Ray != null ? z.Ray.Length : 0f) + "; contact=0.3-0.5 s; fixed heading.");
            return true;
        }

        /// <summary>Delays the native attack/hit transitions for get-up, scream and visible wind-up.</summary>
        static bool BeforeEvent(Fsm fsm, FsmEvent ev, FsmEventData data)
        {
            if (!On || fsm == null || ev == null || (fsm.Name != "Attack" && fsm.Name != "Damage")) return true;
            var owner = fsm.GameObject; Zombie z;
            if (owner == null) return true;
            if (!Live.TryGetValue(owner.GetInstanceID(), out z))
            {
                if (ev.Name == "attack") { Queue(owner); return !Queued.Contains(owner.GetInstanceID()); }
                return true;
            }
            if (!Alive(z)) return true;
            if (z.Traversing && ev.Name == "attack") return false;
            if (fsm.Name == "Attack")
            {
                if (ev.Name == "back" && z.Pose == Pose.Attack && Time.time < z.Until) return false;
                if (ev.Name == "attack" && fsm.ActiveStateName == "trigger") return BeginAttack(z);
            }
            else if (ev.Name == "attack" && fsm.ActiveStateName == "trigger")
            {
                if (z.Pose != Pose.Attack) { BeginAttack(z); return false; }
                float hitAt = z.Since + PunchSeconds * .3f;
                if (z.Hit || Time.time < hitAt || Time.time > z.Since + .5f || z.Victim == null || z.Target.Value != z.Victim) return false;
                Vector3 direction = z.Victim.transform.position - z.Owner.transform.position; direction.y = 0f;
                if (direction.sqrMagnitude > .01f && Vector3.Dot(z.AttackDirection, direction.normalized) < .7f) return false;
                z.Hit = true;
                if (Log.L2) Log.Deep(PresentationModule.Instance, "Zombie animations: " + owner.name + " native hit through " + z.Clip);
            }
            return true;
        }

        static bool Held(Fsm fsm)
        { Zombie z; return On && fsm != null && fsm.GameObject != null && Live.TryGetValue(fsm.GameObject.GetInstanceID(), out z) && Alive(z) && z.Held; }
        static bool BeforeLook(SmoothLookAt action)
        { return action.Fsm == null || action.Fsm.Name != "Attack" || !Held(action.Fsm); }

        static void FinishAttack(Zombie z)
        {
            ResumeRun(z); z.Victim = null;
            // Attack's one-shot Wait may already have fired while the animation owned its recovery.
            if (z.Attack != null && z.Attack.enabled && z.Attack.ActiveStateName == "attack")
                z.Attack.SendEvent("back");
        }

        internal static void Tick()
        {
            if (!On) { if (Live.Count > 0 || Pending.Count > 0) Clear(); return; }
            if (Pending.Count > 0)
            {
                var owner = Pending.Dequeue();
                if (owner != null) { Queued.Remove(owner.GetInstanceID()); Register(owner); }
            }
            Dead.Clear();
            foreach (var pair in Live)
            {
                var z = pair.Value;
                if (!Alive(z)) { Release(z); Dead.Add(pair.Key); continue; }
                if (Time.timeScale < .01f || SaveBus.Busy || z.Traversing) { z.LastPosition = z.Owner.transform.position; continue; }
                if (ModApi.Get<ISkillReactions>()?.IsHeld(z.Owner) ?? false) { z.Rig.Suspend(true); continue; } z.Rig.Suspend(false);
                Hold(z, z.Held);
                Update(z);
                z.LastPosition = z.Owner.transform.position;
            }
            for (int i = 0; i < Dead.Count; i++) Live.Remove(Dead[i]);
        }

        static void Update(Zombie z)
        {
            float now = Time.time;
            bool quiet = Quiet(z), grounded = Grounded(z);
            float speed = new Vector2(z.Body.velocity.x, z.Body.velocity.z).magnitude;
            if (z.Body.isKinematic) { Vector3 delta = z.Owner.transform.position - z.LastPosition; speed = new Vector2(delta.x, delta.z).magnitude / Mathf.Max(Time.deltaTime, .001f); }
            var target = z.Target.Value;
            if (target != z.LastTarget)
            {
                z.LastTarget = target;
                if (z.Pose == Pose.Locomotion && target != null && target == GameRefs.Player &&
                    (target.transform.position - z.Owner.transform.position).sqrMagnitude >= ScreamDistance * ScreamDistance && UnityEngine.Random.value < .25f)
                { z.Victim = target; Switch(z, Pose.Scream, "Scream"); if (Log.L2) Log.Deep(PresentationModule.Instance, "Zombie animations: " + z.Owner.name + " distant sighting scream (25% roll, at least 12 m)."); }
            }
            if (z.Pose == Pose.Rest)
            {
                if (z.Settling)
                {
                    if (grounded) { z.Settling = false; z.IdleAt = now + UnityEngine.Random.Range(10f, 25f); }
                    else if (now < z.Since + 3f) return;
                    else z.Settling = false;
                }
                if (!quiet || now >= z.IdleAt || !grounded)
                    Wake(z);
            }
            else if (z.Pose == Pose.LieDown)
            {
                if (!quiet || !grounded) Wake(z);
                else if (now >= z.Until) { Switch(z, Pose.Rest, z.GetUp); z.IdleAt = now + UnityEngine.Random.Range(10f, 25f); }
            }
            else if (z.Pose == Pose.Agony)
            {
                if (!quiet || now >= z.Until) { Switch(z, Pose.Locomotion, "Idle"); z.IdleAt = now + UnityEngine.Random.Range(10f, 25f); }
            }
            else if (z.Pose == Pose.GetUp)
            { if (now >= z.Until) { ResumeRun(z); z.IdleAt = now + UnityEngine.Random.Range(10f, 25f); } }
            else if (z.Pose == Pose.Scream)
            {
                if (quiet || z.Victim == null || z.Target.Value != z.Victim || now >= z.Until || (z.Victim.transform.position - z.Owner.transform.position).sqrMagnitude < 16f) ResumeRun(z);
            }
            else if (z.Pose == Pose.Attack)
            { if (now >= z.Until || quiet || z.Target.Value != z.Victim) FinishAttack(z); else DriveAttack(z); }
            else
            {
                z.Clip = speed > 2f ? "Run" : speed > .15f ? "Walk" : "Idle";
                if (quiet && grounded && speed < .15f && now >= z.IdleAt)
                {
                    bool agony = UnityEngine.Random.value < .5f;
                    Switch(z, agony ? Pose.Agony : Pose.LieDown, agony ? "Agony" : z.GetUp);
                }
            }
            if (z.Pose == Pose.Scream && z.Target.Value != null)
            {
                Vector3 direction = z.Target.Value.transform.position - z.Owner.transform.position; direction.y = 0f;
                if (direction.sqrMagnitude > .01f) z.Owner.transform.rotation = Quaternion.RotateTowards(z.Owner.transform.rotation, Quaternion.LookRotation(direction), 180f * Time.deltaTime);
            }
            float elapsed = Mathf.Max(0f, now - z.Since), length = ZombieRig.Length(z.Clip);
            if (z.Pose == Pose.LieDown) length = Mathf.Min(length, GetUpSeconds);
            z.Sample = z.Pose == Pose.Rest ? 0f : z.Pose == Pose.LieDown ? Mathf.Max(0f, length - elapsed) :
                z.Pose == Pose.Locomotion ? Mathf.Repeat(elapsed, length) : z.Pose == Pose.Attack ? Mathf.Min(elapsed / PunchSeconds, 1f) * length : Mathf.Min(elapsed, length);
            z.Rig.Play(z.Clip, z.Sample, .18f);
        }

        static void FixedTick()
        { if (On && !SaveBus.Busy && Time.timeScale > .01f) foreach (var z in Live.Values) if (!z.Traversing && Alive(z)) { if (z.Pose == Pose.Attack) DriveAttack(z); else if (z.Held) Stop(z); } }
        static void LateTick()
        {
            if (!On) return;
            var camera = GameRefs.MainCamera;
            foreach (var z in Live.Values)
            {
                if (!Alive(z) || Time.time < z.PoseAt) continue;
                float interval = 0f;
                if (CharacterSettings.AnimationLod.Value && camera != null)
                {
                    float distance = (camera.transform.position - z.Owner.transform.position).sqrMagnitude;
                    interval = distance > 120f * 120f ? .2f : distance > 60f * 60f ? .1f : 0f;
                }
                z.PoseAt = Time.time + interval;
                z.Rig.Sample(Time.timeScale < .01f || SaveBus.Busy ? 0f : Mathf.Max(Time.deltaTime, interval));
            }
        }

        static void Release(Zombie z)
        {
            if (z.Ray != null) z.Ray.Length = z.NativeReach;
            Pause(z.Recovery, false); Pause(z.Rotation, false);
            if (z.Held && z.MovementOwner != null && z.Owner != null) z.MovementOwner.Travel(z.Owner, false);
            z.Held = false; z.Rig.Dispose();
            if (z.Owner != null && z.Owner.activeInHierarchy && (z.Health == null || z.Health.Value >= 1f))
            {
                if (z.Attack != null && z.Attack.enabled && z.Attack.ActiveStateName == "attack") z.Attack.SendEvent("back");
                if (z.Movement != null) z.Movement.SendEvent(z.Target.Value != null ? "Animal_Run" : "Animal_Idle");
            }
        }
        internal static void Clear()
        { foreach (var z in Live.Values) Release(z); Live.Clear(); Pending.Clear(); Queued.Clear(); Dead.Clear(); }
    }
}
