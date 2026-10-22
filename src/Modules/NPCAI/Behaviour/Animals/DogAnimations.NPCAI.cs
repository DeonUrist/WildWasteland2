using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using Dog=WildWasteland.Core.CanineState;


namespace WildWasteland.Modules.NPC
{
    /// <summary>NPC-owned canine attack, movement, idle and native damage decisions composed with the Presentation rig provider.</summary>
    internal sealed class CanineBehaviour : Core.ICanineBehaviour
    {
        internal static readonly CanineBehaviour Instance=new CanineBehaviour();
        private const int Solid=(1<<0)|(1<<8)|(1<<9)|(1<<11)|(1<<14)|(1<<16);
        private readonly List<PlayMakerFSM> _fsms=new List<PlayMakerFSM>();
        /// <summary>Reads the current Presentation rig provider, including after module restart.</summary>
        private ICaninePresentation View { get { return ModApi.Get<ICaninePresentation>(); } }
        /// <summary>Reports whether this provider currently permits its owned feature.</summary>
        public bool Enabled { get { return Senses.On && NpcaiSettings.DogBiteEnabled.Value; } }
        /// <summary>Reports whether native NPC AI currently controls the supplied actor.</summary>
        public bool Controls(GameObject actor) { return Senses.On && NpcaiSettings.BrainEnabled.Value && Senses.AgentFor(actor)!=null; }
        /// <summary>Returns the configured maximum distance for driven canine animation.</summary>
        public float MaxDistance { get { return NpcaiSettings.MaxDistance.Value; } }
        /// <summary>Preserves NPC travel ownership during a visual attack movement.</summary>
        public void Travel(GameObject actor,bool on) { Brain.SetTravel(actor,on); }
        /// <summary>Applies or releases the canine movement hold through the NPC brain.</summary>
        public void Halt(GameObject actor,bool held) { Brain.Halt(actor,held); }
        /// <summary>Returns the native or companion run speed for canine decision timing.</summary>
        public float RunSpeed(GameObject actor) { return Brain.RunSpeed(actor); }
        /// <summary>Checks current rig, actor and distance gates before applying canine decisions.</summary>
        private bool Eligible(Dog d) { return View!=null && View.Eligible(d); }
        /// <summary>Captures native visual state before the Presentation driver takes over.</summary>
        private void Prepare(Dog d) { View.Prepare(d); }
        /// <summary>Restores native canine state through its current Presentation owner.</summary>
        private void Release(Dog d) { View.Release(d); }
        /// <summary>Restores the last native bone sample before another animation pass.</summary>
        private void Restore(Dog d) { View.Restore(d); }
        /// <summary>Requests a pose change with the existing clip cross-fade state.</summary>
        private void Switch(Dog d,CaninePose pose,string clip) { View.Switch(d,pose,clip); }
        /// <summary>Restores velocity, drag and movement ownership after a committed dash.</summary>
        private void ReleaseDash(Dog d) { View.ReleaseDash(d); }
        /// <summary>Restores native drag and movement ownership after a jumping bite.</summary>
        private void ReleaseJump(Dog d) { View.ReleaseJump(d); }
        /// <summary>Checks native or companion feet against the existing ground layers.</summary>
        private bool Grounded(Dog d) { return View.Grounded(d); }
        /// <summary>Stops or releases the actual companion or native NPC movement owner.</summary>
        private void Hold(Dog d,bool held) { if(d.Companion!=null)d.Companion.Halt(held);else Brain.Halt(d.Agent.Owner,held); }
        /// <summary>Reads planar speed from the companion or native rigidbody.</summary>
        private float Speed(Dog d) { return d.Companion!=null?d.Companion.Speed:new Vector2(d.Body.velocity.x,d.Body.velocity.z).magnitude; }
        /// <summary>Returns the native or companion run speed for canine decision timing.</summary>
        private float RunSpeed(Dog d) { return d.Companion!=null?d.Companion.RunSpeed:Brain.RunSpeed(d.Agent.Owner); }
        /// <summary>Returns the reach probe point above the native or companion feet.</summary>
        private Vector3 Origin(Dog d) { return (d.Companion!=null?d.Companion.Feet:d.Agent.T.position)+Vector3.up*.25f; }
        /// <summary>Random stand / lie duration between DogIdlePoseMinSeconds and DogIdlePoseMaxSeconds.</summary>
        private float IdleSpan() { float lo = NpcaiSettings.DogIdlePoseMin.Value, hi = Mathf.Max(lo,NpcaiSettings.DogIdlePoseMax.Value); return UnityEngine.Random.Range(lo, hi); }

        /// <summary>True when the dog's current target is within bite reach (line of reach checked).</summary>
        private bool InReach(Dog d, GameObject target)
        {
            if (target == null || !target.activeInHierarchy || d.Agent.Target != target) return false;
            if (d.Companion != null && !d.Companion.TargetAlive(target)) return false;
            Vector3 origin = Origin(d);
            Vector3 point = ReachPoint(d, target, origin);
            Vector3 to = point - origin;
            if (to.magnitude > d.Reach + .2f || Mathf.Abs(to.y) > 1.2f) return false;
            Vector3 planar = to; planar.y = 0f;
            if (planar.sqrMagnitude > .01f && Vector3.Dot(d.Agent.T.forward, planar.normalized) < (d.Pose == CaninePose.RunningBite ? .65f : .45f)) return false;
            if (d.ReachLine == 0)
            {
                RaycastHit hit;
                d.ReachLine = (sbyte)(!Physics.Linecast(origin, point, out hit, Solid, QueryTriggerInteraction.Ignore) || IntendedTarget(hit.collider, target) ? 1 : 2);
            }
            return d.ReachLine == 1;
        }
        // the target's point nearest origin, once per frame per (target, origin); a new point clears the remembered line of reach
        /// <summary>The target's point nearest origin, once per frame per (target, origin).</summary>
        private Vector3 ReachPoint(Dog d, GameObject target, Vector3 origin)
        {
            int frame = UnityEngine.Time.frameCount;
            if (d.ReachFrame == frame && ReferenceEquals(d.ReachOf, target) && d.ReachOrigin.x == origin.x && d.ReachOrigin.y == origin.y && d.ReachOrigin.z == origin.z) return d.ReachPoint;
            var col = TargetCollider(d, target);
            d.ReachFrame = frame; d.ReachOf = target; d.ReachOrigin = origin; d.ReachLine = 0;
            d.ReachPoint = col != null ? col.ClosestPoint(origin) : target.transform.position;
            return d.ReachPoint;
        }
        /// <summary>The collider of the target used for reach (cached per dog).</summary>
        private Collider TargetCollider(Dog d, GameObject target)
        {
            float now = UnityEngine.Time.time;
            if (!ReferenceEquals(d.ColOf, target) || now >= d.ColAt + 2f || (!ReferenceEquals(d.TargetCol, null) && d.TargetCol == null))
            {
                d.ColOf = target; d.ColAt = now;
                d.TargetCol = target.GetComponent<Collider>() ?? target.GetComponentInChildren<Collider>();
            }
            return d.TargetCol;
        }
        /// <summary>True when the collider belongs to the target.</summary>
        private bool IntendedTarget(Collider collider, GameObject target)
        { return collider != null && target != null && (collider.gameObject == target || collider.transform.IsChildOf(target.transform)); }
        /// <summary>The target's reach point seen from the dog's mouth origin.</summary>
        private Vector3 TargetPoint(Dog d, GameObject target) { return ReachPoint(d, target, Origin(d)); }
        /// <summary>True when the native damage may be applied now (reach at contact).</summary>
        private bool DamageReady(Dog d)
        {
            if (d.Companion != null) return !d.Companion.Riding && d.Companion.TargetAlive(d.Agent.Target);
            // (WW perf, D10) no enabled Damage FSM: looked for again at most every 0.5 s (was GetComponents + LINQ every frame)
            if ((d.DamageComponent == null || !d.DamageComponent.enabled) && UnityEngine.Time.time >= d.NextDamageLook)
            {
                d.NextDamageLook = UnityEngine.Time.time + .5f;
                PlayMakerFSM found = null;
                d.Agent.Owner.GetComponents(_fsms);
                for (int i = 0; i < _fsms.Count; i++) { var f = _fsms[i]; if (f != null && f.enabled && f.FsmName == "Damage") { found = f; break; } }
                _fsms.Clear();
                d.DamageComponent = found;
            }
            return d.DamageComponent != null && d.DamageComponent.enabled && d.DamageComponent.Fsm != null && d.DamageComponent.Fsm.Initialized && d.DamageComponent.Fsm.ActiveStateName == "trigger";
        }
        /// <summary>Starts a bite / running bite / jump attack against the Damage FSM's target.</summary>
        private void StartAttack(Dog d, Fsm damage, bool running)
        {
            d.BiteTarget = d.Agent.Target; d.DamageFsm = damage; d.HitSent = false;
            Switch(d, running ? CaninePose.RunningBite : CaninePose.Bite, running ? "MovingBite" : "Bite");
            d.AttackRate = d.Clips[d.Playing].length / (running ? .70f : .48f);
            d.NextBite = Time.time + (running ? 1.02f : .58f) + UnityEngine.Random.Range(0f, .04f);
            if (d.Companion != null) d.Companion.SetCooldown(d.NextBite);
            d.RecoveryUntil = Time.time + (running ? .90f : .56f);
            if (!running) return;
            Vector3 direction = TargetPoint(d, d.BiteTarget) - d.Agent.T.position; direction.y = 0f;
            d.DashDirection = direction.sqrMagnitude > .001f ? direction.normalized : d.Agent.T.forward;
            d.DashSpeed = Mathf.Clamp(Mathf.Max(RunSpeed(d), Speed(d)), 5f, 7f);
            // Once committed, pursue the snapshot direction rather than the moving target.
            if (d.Held) { d.Held = false; Hold(d, false); }
            if (d.Companion != null) { d.Dashing = true; return; }
            d.Drag = d.Body.drag; d.Body.drag = 0f; d.Dashing = true;
            Brain.SetTravel(d.Agent.Owner, true); d.Agent.Travel = true;
        }
        /// <summary>Tries an attack when the target is in reach and the dog is ready.</summary>
        private void TryAttack(Dog d)
        {
            var a = d.Agent;
            if (d.Pose != CaninePose.Locomotion || !a.Combat || a.Target == null || Time.time < d.NextBite || !DamageReady(d)) return;
            Vector3 to = TargetPoint(d, a.Target) - a.T.position; to.y = 0f;
            float distance = to.magnitude;
            var damage = d.Companion != null ? null : d.DamageComponent.Fsm;
            if (InReach(d, a.Target)) { StartAttack(d, damage, false); return; }
            float speed = Mathf.Clamp(Mathf.Max(RunSpeed(d), Speed(d)), 5f, 7f);
            if (d.Clips.ContainsKey("MovingBite") && distance <= d.Reach + speed * .22f && Grounded(d) && (distance < .05f || Vector3.Dot(a.T.forward, to / distance) >= .55f))
                StartAttack(d, damage, true);
        }
        /// <summary>Moves a dashing (running bite) dog toward its target each frame.</summary>
        private void DriveDash(Dog d, float dt)
        {
            float seconds = d.Time / Mathf.Max(.01f, d.AttackRate);
            float speed = seconds < .12f ? 2f : seconds < .48f ? d.DashSpeed : 2f;
            if (d.Companion != null) { d.Companion.Dash(d.DashDirection, speed, dt, d.BiteTarget); return; }
            RaycastHit hit;
            bool blocked = d.Body.SweepTest(d.DashDirection, out hit, speed * Mathf.Max(dt, .016f) + .08f, QueryTriggerInteraction.Ignore);
            if (blocked && (IntendedTarget(hit.collider, d.BiteTarget) || hit.normal.y > .6f)) blocked = false;
            Vector3 velocity = d.DashDirection * (blocked ? 0f : speed); velocity.y = d.Body.velocity.y;
            d.Agent.T.rotation = Quaternion.LookRotation(d.DashDirection); d.Body.velocity = velocity;
        }
        /// <summary>Wait.OnEnter prefix: the Damage FSM's wait of a driven dog follows the animation.</summary>
        public static void BeforeDamageWait(Wait __instance) { Instance.DamageWait(__instance); }
        /// <summary>Preserves native damage wait timing while a driven canine attacks.</summary>
        private void DamageWait(Wait __instance)
        {
            if (View==null || !NpcaiRuntime.Running) return;      // (WW) cheap filter: Wait.OnEnter runs everywhere
            var fsm = __instance.Fsm; Dog d;
            if (fsm == null || fsm.Name != "Damage" || fsm.ActiveStateName != "attack" || fsm.GameObject == null || (d=View.Find(fsm.GameObject))==null || !Eligible(d) || __instance.time == null) return;
            if (!d.OriginalWaits.ContainsKey(__instance)) d.OriginalWaits[__instance] = __instance.time.Value;
            __instance.time.Value = d.Pose == CaninePose.RunningBite ? .48f : .32f;
        }

        /// <summary>Fsm.ProcessEvent prefix: native damage events of driven dogs only when the bite really connects.</summary>
        public bool BeforeDamageEvent(Fsm __instance, FsmEvent fsmEvent, FsmEventData eventData)
        {
            if (View==null || !NpcaiRuntime.Running) return true;      // (WW) Fsm.ProcessEvent is the hottest method in the game: no dog, no string compare
            if (__instance == null || fsmEvent == null || fsmEvent.Name != "attack" || __instance.Name != "Damage" || __instance.GameObject == null) return true;
            Dog d;
            if ((d=View.Find(__instance.GameObject))==null || !Eligible(d) || d.Committing) return true;
            if (d.Pose == CaninePose.Bite || d.Pose == CaninePose.RunningBite || d.Pose == CaninePose.JumpAttack || d.Pose == CaninePose.LieDown || d.Pose == CaninePose.Rest || d.Pose == CaninePose.GetUp || Time.time < d.NextBite) return false;
            if (!d.Agent.Combat || d.Agent.Target == null) return true;
            StartAttack(d, __instance, false);
            bool leap = NpcaiSettings.DogJumpAttack.Value && d.Clips.ContainsKey("JumpAttack") && Grounded(d) && !Senses.PlayerInCar && UnityEngine.Random.value < .25f;
            if (leap) { Switch(d, CaninePose.JumpAttack, "JumpAttack"); d.NextBite = Time.time + d.Clips[d.Playing].length; }
            if (leap)
            {
                Vector3 direction = d.BiteTarget.transform.position - d.Agent.T.position; direction.y = 0f;
                RaycastHit hit = new RaycastHit();
                bool blocked = direction.sqrMagnitude > .01f && d.Body.SweepTest(direction.normalized, out hit, .6f, QueryTriggerInteraction.Ignore);
                if (blocked && hit.collider != null && (hit.collider.gameObject == d.BiteTarget || hit.collider.transform.IsChildOf(d.BiteTarget.transform))) blocked = false;
                if (direction.sqrMagnitude > .01f && !blocked)
                {
                    d.Drag = d.Body.drag; d.Body.drag = 0f; d.Jumping = true; d.JumpSince = Time.time;
                    Brain.SetTravel(d.Agent.Owner, true); d.Agent.Travel = true;
                    d.Body.velocity = direction.normalized * 1.2f + Vector3.up * 2.2f;
                }
                else StartAttack(d, __instance, false);
            }
            return false;
        }

        /// <summary>SetVelocity.DoSetVelocity prefix: held / dashing dogs are moved by this module, not the FSM.</summary>
        public bool BeforeSetVelocity(SetVelocity __instance)
        {
            if (View==null || !NpcaiRuntime.Running) return true;
            var fsm = __instance.Fsm; Dog d;
            return fsm == null || fsm.Name != "Movement" || fsm.GameObject == null || (d=View.Find(fsm.GameObject))==null || !Eligible(d) || (!d.Held && !d.Dashing);
        }
        /// <summary>One dog's frame: pose choice (rest, bite, jump, dash, locomotion) and clip time.</summary>
        public void Tick(Dog d, float dt)
        {
            // Transition blends start from the prior displayed pose before restoring the native baseline.
            if (!Eligible(d)) { Release(d); return; }
            Prepare(d);
            var a = d.Agent; float speed = Speed(d);
            if (d.Companion != null && a.Combat && d.Playing != null && d.Playing.StartsWith("Sneak", StringComparison.Ordinal))
            {
                Restore(d); d.HasDisplay = false;
                Switch(d, CaninePose.Locomotion, "Idle");
            }
            bool riding = d.Companion != null && d.Companion.Riding;
            if (riding)
            {
                if (d.Pose != CaninePose.SitDown && d.Pose != CaninePose.Sitting) { ReleaseDash(d); d.BiteTarget = null; d.DamageFsm = null; Switch(d, CaninePose.SitDown, "SitDown"); }
            }
            else if (d.Pose == CaninePose.SitDown || d.Pose == CaninePose.Sitting) Switch(d, CaninePose.Locomotion, "Idle");
            bool quiet = d.Companion == null && ModApi.Get<ICanineRestPolicy>()?.Resting==true && !d.Boss && a.Quiet && a.Target == null && !a.HasGhost && speed < .15f && Grounded(d);
            if (quiet && !d.WasQuiet) d.NextIdleChange = Time.time + IdleSpan();
            if (!quiet) d.NextIdleChange = 0f;
            d.WasQuiet = quiet;
            if (d.Companion != null && d.BiteTarget != null && (a.Target != d.BiteTarget || !d.Companion.TargetAlive(d.BiteTarget)))
            { ReleaseDash(d); d.BiteTarget = null; Switch(d, CaninePose.Locomotion, "Idle"); }
            if (d.Pose == CaninePose.Locomotion)
            {
                if (quiet && Time.time >= d.NextIdleChange) Switch(d, CaninePose.LieDown, "LieDown");
                else
                {
                    string clip = speed > 3f ? "Run" : speed > .15f && d.Clips.ContainsKey("Walk") ? "Walk" : "Idle";
                    if (d.Companion != null && d.Companion.Sneaking && d.Clips.ContainsKey("SneakIdle") && d.Clips.ContainsKey("SneakWalk")) clip = speed > .15f ? "SneakWalk" : "SneakIdle";
                    if (clip != d.Playing) Switch(d, CaninePose.Locomotion, clip);
                }
                if (a.Combat && a.Target != null)
                {
                    Vector3 to = TargetPoint(d, a.Target) - a.T.position; to.y = 0f;
                    if (to.sqrMagnitude > .0025f && to.magnitude <= d.Reach + 2f)
                        a.T.rotation = Quaternion.RotateTowards(a.T.rotation, Quaternion.LookRotation(to), 1080f * dt);
                    TryAttack(d);
                }
            }
            else if ((d.Pose == CaninePose.Rest || d.Pose == CaninePose.LieDown) && !quiet) Switch(d, CaninePose.GetUp, "GetUp");
            else if (d.Pose == CaninePose.Rest && quiet && Time.time >= d.NextIdleChange) Switch(d, CaninePose.GetUp, "GetUp");
            d.Fade += dt;
            float rate = d.Pose == CaninePose.Bite || d.Pose == CaninePose.RunningBite ? d.AttackRate : d.Playing == "Run" ? Mathf.Clamp(speed / Math.Max(2f, RunSpeed(d)), .35f, 2f) : 1f;
            if (d.Playing == "SneakWalk") rate = Mathf.Clamp(speed / 1.3f, .35f, 1.6f);
            d.Time += dt * rate;
            if (d.Dashing) DriveDash(d, dt);
            if (d.Jumping && ((Time.time - d.JumpSince > .12f && d.Body.velocity.y <= 0f && Grounded(d)) || Time.time - d.JumpSince > 2f)) ReleaseJump(d);
            if ((d.Pose == CaninePose.Bite || d.Pose == CaninePose.RunningBite || d.Pose == CaninePose.JumpAttack) && !d.HitSent && d.Time >= d.Clips[d.Playing].length * (d.Pose == CaninePose.Bite ? .38f : .45f))
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
                if (d.Pose == CaninePose.SitDown) Switch(d, CaninePose.Sitting, "SitIdle");
                else if (d.Pose == CaninePose.LieDown) { Switch(d, CaninePose.Rest, "LieIdle"); d.NextIdleChange = Time.time + IdleSpan(); }
                else if (d.Pose == CaninePose.GetUp || d.Pose == CaninePose.Bite || d.Pose == CaninePose.RunningBite || (d.Pose == CaninePose.JumpAttack && !d.Jumping))
                {
                    ReleaseDash(d); d.DamageFsm = null; d.BiteTarget = null; Switch(d, CaninePose.Locomotion, "Idle");
                    if (quiet) d.NextIdleChange = Time.time + IdleSpan();
                }
            }
            // Only hold melee position where a bite can actually reach. Outside it, continue chasing or commit to a running bite.
            bool close = a.Combat && a.Target != null && InReach(d, a.Target);
            bool held = !d.Dashing && (close || d.Pose != CaninePose.Locomotion || Time.time < d.RecoveryUntil);
            if (held != d.Held) { d.Held = held; Hold(d, held); }
            else if (held) Hold(d, true); // Brain may have registered after the rest/attack began.
            if (d.Companion == null && held && !d.Jumping && !d.Dashing) d.Body.velocity = new Vector3(0f, d.Body.velocity.y, 0f);
            Restore(d);
        }

    }
}
