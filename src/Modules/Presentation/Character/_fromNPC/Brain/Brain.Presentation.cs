using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Brain part owned by Presentation: driving the NPC character body (ApBody rig queue, Drive, aim freeze, strafe clip, crouch, held weapon / gun poses, hop animation).</summary>
    internal static partial class Brain
    {
        private static readonly Queue<Npc> _rigs = new Queue<Npc>();
        private static int _rigFrame = -1;

        /// <summary>Empties the rig build queue.</summary>
        private static void ClearRigs() { while (_rigs.Count > 0) _rigs.Dequeue().RigQueued = false; _rigFrame = -1; }

        // A holding gunman keeps the gun up: the Movement FSM's shooting animation (attack 2 / attack 4: a 0.2-0.6 s clip that starts with the gun
        // raised) is put on its first frame and the Animator frozen there. The burst plays it from the start again (Unfreeze in Tick), and after
        // the burst the Attack FSM's Animal_Run comes back as Animal_Idle (BeforeSendEvent) followed by this pose again.
        // (1.2.0) With Apocaplayer (2.2.0+) the body is animated by its ModAPI - the player's third-person body for an NPC: this tells it every frame
        // what the gunman does (crouched, aiming, firing, in the air, where the target is) and when he reloads / cocks his gun; the legs follow
        // the Rigidbody's real motion by themselves (8 directions, walk / run / sprint, crouched, turns in place, the stop). Aiming = squared up to
        // the target (Hold / Rest / in cover / backing up / the hit sidestep) or in a burst; moving after the target = the relaxed low-ready set.
        // Dead, seated (Apocapatrol) or in a melee swing the body is suspended: the game's own clips and the gun back in its own hand.
        private static readonly int GroundProbeMask = ~((1 << 2) | (1 << 4) | (1 << 5) | (1 << 12) | (1 << 13) | (1 << 22));
        /// <summary>Tells the NPC's character body (ApBody) every frame what the gunman does: crouched, aiming, firing, airborne, target, reloads.</summary>
        private static void Drive(Npc n, string state, float now, float dt)
        {
            var body = n.Body;
            if (!ApBody.Alive(body)) { n.Body = null; n.RigTried = now; n.SpeedCap = 0f; return; }      // destroyed with its Animator: TryRig may make a new one
            // the gun: the WeaponType FSM picks one of the hand's gun models a frame after the spawn (and the game may toggle them later):
            // whenever the game toggled an object of this NPC (AfterActivate) or the held model went inactive, find the active one again
            if (n.WeaponDirty || (n.Weapon == null ? now >= n.RigTried + 0.5f : !n.Weapon.gameObject.activeInHierarchy))
            {
                n.WeaponDirty = false; n.RigTried = now;      // (nothing held: looked again twice a second at most)
                var w = HeldWeapon(n);
                if (w != n.Weapon)
                {
                    n.Weapon = w; n.WeaponKey = ApBody.WeaponKey(w);
                    n.Shots = 0; n.Reloading = false; n.ReloadShown = false; n.Magazine = MagazineOf(n.WeaponKey);
                    n.Cocks = w != null && ApBody.Cocks(n.WeaponKey);
                    if (NpcModule.VerboseLog.Value) NpcModule.Log.LogInfo("Brain: " + n.Owner.name + " now holds " + (w != null ? n.WeaponKey + (n.Cocks ? " (cocked after each burst)" : "") : "nothing"));
                }
                ApBody.SetWeapon(body, w != null && w.gameObject.activeInHierarchy && GunPoses(n, now) ? w : null, n.WeaponKey);   // a gun goes to the right hand; anything else stays where it is
            }
            bool gun = n.Weapon != null && GunPoses(n, now);     // a gun the player's table knows (a machete out: the game's clips)
            bool dead = n.HpVar != null && n.HpMax > 0f && n.HpVar.Value <= 0f;
            bool melee = state == "attack_melee";
            bool off = !gun || dead || melee || n.T.parent != null || !NpcModule.ApocaplayerClips.Value;
            ApBody.Suspend(body, off);
            if (off) { n.Strafing = false; n.SpeedCap = 0f; n.WasBurst = false; return; }
            if (n.Strafing && now >= n.MakeWayUntil) n.Strafing = false;
            if (n.Anim != null && n.Anim.speed != 1f) n.Anim.speed = 1f;          // a vanilla aim freeze from before the body was attached
            n.Frozen = false;
            bool cover = n.Mode == Mode.Cover, coverHold = cover && n.CoverArrived;
            bool burst = state == "attack_ranged";
            bool facing = n.Mode == Mode.Hold || n.Mode == Mode.Rest || coverHold || n.Mode == Mode.BackUp || n.Strafing || now < n.FaceTargetUntil;   // squared up to the target
            bool aiming = burst || facing;
            // the reload (NeedsReload starts it): the player's reload clip on the hands over whatever the legs do - the whole clip fitted to the
            // reload time, or one round at a time (the clip's round part once per round)
            bool reloading = n.Reloading && now < n.ReloadUntil;                      // (NeedsReload clears the flag only at the next burst)
            if (reloading && !n.ReloadShown) { ApBody.Reload(body, Mathf.Max(0.3f, n.ReloadUntil - now), n.ReloadRounds); n.ReloadShown = true; }
            else if (!reloading && n.ReloadShown)
            {
                n.ReloadShown = false; ApBody.CancelReload(body);
                if (n.Cocks) ApBody.Pump(body);                                                   // a pump / bolt gun is racked after loading
            }
            // a pump shotgun / bolt rifle is cocked after each burst (unless it is empty: the reload comes first)
            if (n.WasBurst && !burst && n.Cocks && !reloading && (n.Magazine <= 0 || n.Shots < n.Magazine)) ApBody.Pump(body);
            n.WasBurst = burst;
            // in the air (a hop over a lip): nothing under the feet
            bool air = false;
            if (n.Col != null && n.Rb != null && Mathf.Abs(n.Rb.velocity.y) > 0.5f)
            {
                var b = n.Col.bounds;
                air = !Physics.Raycast(new Vector3(b.center.x, b.min.y + 0.2f, b.center.z), Vector3.down, 0.45f, GroundProbeMask, QueryTriggerInteraction.Ignore);
            }
            // aiming at the target: the spine bends toward its height (the player's aim pitch)
            float pitch = 0f;
            var tgo = n.Target != null ? n.Target.Value : null;
            if (aiming && tgo != null)
            {
                Vector3 from = n.T.position + Vector3.up * 1.45f, to = tgo.transform.position + Vector3.up * (n.Crouched ? 1.0f : 1.2f);
                Vector3 d = to - from; float flat = new Vector2(d.x, d.z).magnitude;
                if (flat > 0.5f) pitch = Mathf.Clamp(-Mathf.Atan2(d.y, flat) * Mathf.Rad2Deg, -40f, 40f);
            }
            ApBody.Frame(body, n.Crouched, aiming, burst, air, pitch);
            n.SpeedCap = ApBody.SpeedCap(body);                                                  // no faster than the clips carry the feet
        }

        // (perf) WeaponOf over the NPC's cached hand bones (scanned again if the skeleton went away)
        /// <summary>The weapon model in the NPC's hands (cached hand bones).</summary>
        private static Transform HeldWeapon(Npc n)
        {
            var c = n.Hands;
            if (c == null || (c.Hands.Length > 0 && c.Hands[0] == null)) n.Hands = c = WeaponRanges.ScanHands(n.Owner);
            return WeaponRanges.WeaponOf(n.Owner, c);
        }

        // (perf) ApBody.HasPoses(WeaponKey), cached per key and re-read once a second (a pose table edited live is seen within 1 s)
        /// <summary>True when the player has poses for this weapon (cached per key, re-read once a second).</summary>
        private static bool GunPoses(Npc n, float now)
        {
            if (now >= n.GunPosesAt || !ReferenceEquals(n.GunPosesKey, n.WeaponKey)) { n.GunPosesKey = n.WeaponKey; n.GunPoses = ApBody.HasPoses(n.WeaponKey); n.GunPosesAt = now + 1f; }
            return n.GunPoses;
        }

        /// <summary>Freezes the Animator on the first frame of the shooting clip (gun up between bursts).</summary>
        private static void Aim(Npc n)
        {
            if (n.Body != null) return;     // Drive() tells the body every frame
            if (!NpcModule.AimPose.Value || n.Anim == null || string.IsNullOrEmpty(n.AimState)) return;
            try
            {
                n.Anim.Play(n.AimState, 0, 0f);
                n.Anim.speed = 0f;
                n.Frozen = true;
            }
            catch (Exception) { n.AimState = null; }
        }

        /// <summary>Lets the Animator run again (the burst plays the clip from the start).</summary>
        private static void Unfreeze(Npc n)
        {
            if (!n.Frozen) return;
            n.Frozen = false;
            if (n.Body != null) return;     // Drive() suspends the body when the mode / state calls for it
            if (n.Anim != null) n.Anim.speed = 1f;
        }

        // (1.1.1) the sidestep clip for Hit(): left = toward -right
        /// <summary>Plays the sidestep clip for Hit() (left = toward -right).</summary>
        private static void StrafeClip(Npc n, bool left)
        {
            if (n.Body == null) return;
            n.MakeWayLeft = left; n.Strafing = true;      // Drive() plays the (Fire)Strafe clip while MakeWayUntil lasts
        }

        /// <summary>Kneels / stands the NPC: capsule height and the crouch clip.</summary>
        private static void Crouch(Npc n, bool on)
        {
            if (on == n.Crouched) return;
            if (on)
            {
                // (1.1.2) only with Apocaplayer's crouch clips: the clip and a shorter capsule (about 0.45 m lower) so bullets aimed at the empty air
                // above him miss. (1.2.0) Pistolmen kneel again: ModAPI puts the pistol's hands over the crouched legs, as on the player.
                if (n.Body == null || !n.Ranged) return;
                if (n.Capsule == null) { n.Capsule = n.Owner.GetComponent<CapsuleCollider>(); if (n.Capsule != null) { n.CapHeight = n.Capsule.height; n.CapCenter = n.Capsule.center; } }
                n.Crouched = true; n.Drop = 0.45f;
                if (n.Capsule != null && n.CapHeight > 0f)
                {
                    float bottom = n.CapCenter.y - n.CapHeight * 0.5f;
                    float h = Mathf.Max(n.Capsule.radius * 2f, n.CapHeight - n.Drop);
                    n.Capsule.height = h; n.Capsule.center = new Vector3(n.CapCenter.x, bottom + h * 0.5f, n.CapCenter.z);
                }
                if (NpcModule.VerboseLog.Value) NpcModule.Log.LogInfo("Brain: " + n.Owner.name + " crouches");
            }
            else
            {
                n.Crouched = false;
                if (n.Capsule != null && n.CapHeight > 0f) { n.Capsule.height = n.CapHeight; n.Capsule.center = n.CapCenter; }
            }
        }

        // (1.2.0) Apocaplayer's ModAPI for this gunman: its Animator (humanoid avatar) animated with the player's clips, its gun in the right hand
        /// <summary>Queues the NPC for a character rig (Apocaplayer ModAPI body).</summary>
        private static void TryRig(Npc n)
        {
            if (n.Body != null || n.Anim == null || !n.Ranged || Time.time < n.RigTried + 2f) return;
            if (!ApBody.Available || !NpcModule.ApocaplayerClips.Value) return;
            if (n.RigQueued) return;
            n.RigQueued = true; _rigs.Enqueue(n);
        }

        /// <summary>Sliced LateUpdate job: builds one queued rig per frame within the frame budget.</summary>
        internal static bool BuildRig()
        {
            if (!NpcModule.Running || _rigFrame == Time.frameCount || _rigs.Count == 0 || Core.Scheduler.FrameMs >= Core.Scheduler.FrameBudgetMs) return false;
            var n = _rigs.Dequeue(); n.RigQueued = false;
            if (n.Owner == null || n.T.parent != null || !n.Owner.activeInHierarchy || n.Body != null || n.Anim == null || !n.Ranged
                || !ApBody.Available || !NpcModule.ApocaplayerClips.Value) return _rigs.Count > 0;
            _rigFrame = Time.frameCount; n.RigTried = Time.time;
            try
            {
                n.Body = ApBody.Attach(n.Anim);
                if (n.Body == null) { n.RigTried = Time.time + 30f; return false; }      // not a humanoid: the game's clips (asked again much later)
                n.WeaponDirty = true;      // Drive() finds the held gun model (and follows the game's swaps of it)
                if (NpcModule.VerboseLog.Value) NpcModule.Log.LogInfo("Brain: " + n.Owner.name + " animated by Apocaplayer's ModAPI");
            }
            catch (Exception e) { n.Body = null; NpcModule.Warn("Brain: Apocaplayer's ModAPI for " + n.Owner.name + ": " + e.Message); }
            return false;
        }

        // (1.2.0) a hop over a low lip (the brain's or Idle's): the player's jump on the body (ModAPI: the take-off, held in the air, the landing)
        /// <summary>A hop over a low lip: the player's jump animation on the NPC body.</summary>
        internal static void HopAnim(GameObject owner)
        {
            var n = Get(owner);
            if (n != null && n.Body != null && !ApBody.Suspended(n.Body)) ApBody.Jump(n.Body);
        }
    }
}
