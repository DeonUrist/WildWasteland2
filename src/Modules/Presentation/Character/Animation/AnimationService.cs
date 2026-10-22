using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    // Core.ModApi.Get<Core.IAnimationApi>(): the cross-module face of ModAPI (AGENTS.md 5.1 / PORTING.md 6). NPC and Patrols call this instead of
    // Apocaplayer.ModAPI + a Chainloader GUID check + Rider.Clip reflection. Pure forwarding: every call lands on the static ModAPI
    // (which stays public in this module's namespace). One adapter object per ModAPI.Character, cached, so Attach/Find return the
    // same IAnimatedCharacter for the same Animator, as ModAPI does.
    internal sealed class AnimationService : IAnimationApi
    {
        private readonly Dictionary<ModAPI.Character, CharacterAdapter> _wrapped = new Dictionary<ModAPI.Character, CharacterAdapter>();
        private readonly List<ModAPI.Character> _dead = new List<ModAPI.Character>();

        /// <summary>IAnimationApi: contract version (ModAPI.ApiVersion).</summary>
        public int ApiVersion { get { return ModAPI.ApiVersion; } }
        /// <summary>IAnimationApi: the Apocaplayer version the API mirrors.</summary>
        public string Version { get { return ModAPI.PluginVersion; } }
        /// <summary>IAnimationApi: the animation bundle with the player's Mixamo set is loaded.</summary>
        public bool Ready { get { return ModAPI.Ready; } }
        /// <summary>IAnimationApi: the player camera is in third person (not peeking through binoculars).</summary>
        public bool PlayerThirdPerson { get { return ThirdPerson.On && !ThirdPerson.Peek; } }
        /// <summary>IAnimationApi: the player's shout clip for the chosen character (male/female), or null.</summary>
        public AudioClip PlayerShout { get { return FemalePain.PlayerShout; } }
        /// <summary>IAnimationApi: head position of the player's body during a jump (scene space).</summary>
        public bool TryPlayerJumpHead(GameObject player, out Vector3 position) { return BodyRunner.TryPlayerJumpHead(player, out position); }
        /// <summary>IAnimationApi: muzzle of the third-person weapon prop while the player is in third person and alive.</summary>
        public bool TryPlayerWeaponMuzzle(out Vector3 position)
        {
            position = Vector3.zero;
            var body = BodyRunner.PlayerBody;
            return PresentationModule.Running && PlayerThirdPerson && !PlayerState.Dead && body != null && body.Alive && body.TryWeaponMuzzle(out position);
        }
        /// <summary>IAnimationApi: every clip name the player's body uses.</summary>
        public string[] ClipNames() { return ModAPI.ClipNames(); }
        /// <summary>IAnimationApi: true for a clip the player's body uses.</summary>
        public bool IsPlayerClip(string name) { return ModAPI.IsPlayerClip(name); }
        /// <summary>IAnimationApi: a player clip by name (null for clips outside the player set).</summary>
        public AnimationClip GetClip(string name) { return ModAPI.GetClip(name); }
        /// <summary>IAnimationApi: the weapon-pose table key of a weapon object name.</summary>
        public string WeaponKey(string objectName) { return ModAPI.WeaponKey(objectName); }
        /// <summary>IAnimationApi: hold style name of a weapon key (None, Rifle, Pistol, Melee, Throw).</summary>
        public string WeaponKind(string weaponKey) { return ModAPI.WeaponKind(weaponKey); }
        /// <summary>IAnimationApi: true when the weapon has entries in the weapon-pose table.</summary>
        public bool HasWeaponPoses(string weaponKey) { return ModAPI.HasWeaponPoses(weaponKey); }
        /// <summary>IAnimationApi: the weapon's local pose under the right hand in a clip.</summary>
        public bool WeaponPose(string weaponKey, string clip, out Vector3 localPosition, out Quaternion localRotation) { return ModAPI.WeaponPose(weaponKey, clip, out localPosition, out localRotation); }
        /// <summary>IAnimationApi: the reload clip of a weapon, or null.</summary>
        public string ReloadClip(string weaponKey) { return ModAPI.ReloadClip(weaponKey); }
        /// <summary>IAnimationApi: length of the weapon's reload clip, seconds.</summary>
        public float ReloadClipSeconds(string weaponKey) { return ModAPI.ReloadClipSeconds(weaponKey); }
        /// <summary>IAnimationApi: one round of a one-round-at-a-time reload, seconds.</summary>
        public float RoundSeconds(string weaponKey) { return ModAPI.RoundSeconds(weaponKey); }
        /// <summary>IAnimationApi: true for weapons the player's copy reloads one round at a time.</summary>
        public bool ReloadsOneRoundAtATime(string weaponKey) { return ModAPI.ReloadsOneRoundAtATime(weaponKey); }
        /// <summary>IAnimationApi: true for weapons the player's copy cocks after each shot.</summary>
        public bool CocksAfterShot(string weaponKey) { return ModAPI.CocksAfterShot(weaponKey); }
        /// <summary>IAnimationApi: native ground speed of a clip tier ("walk", "run", "sprint", "crouch").</summary>
        public float NativeSpeed(string tier) { return ModAPI.NativeSpeed(tier); }
        /// <summary>IAnimationApi: number of characters animated through ModAPI.</summary>
        public int Count { get { return ModAPI.Count; } }
        /// <summary>IAnimationApi: starts animating a humanoid Animator with the player's clips (the same adapter for the same Animator).</summary>
        public IAnimatedCharacter Attach(Animator animator) { return Wrap(ModAPI.Attach(animator)); }
        /// <summary>IAnimationApi: the adapter of the character driving this Animator, or null.</summary>
        public IAnimatedCharacter Find(Animator animator) { return Wrap(ModAPI.Find(animator)); }
        /// <summary>IAnimationApi: a ladder / climb animation lease on an NPC Animator (the World part's NpcTraversal).</summary>
        public INpcTraversalAnimation BeginNpcTraversal(Animator animator, string kind) { return NpcTraversal.Begin(animator, kind); }
        public float LadderCycleRise { get { var c = PoseClip.Shared("ClimbingLadder"); return c != null ? c.Rise : 1.24f; } }
        public float LadderToeDepth { get { var c = PoseClip.Shared("ClimbingLadder"); return c != null ? c.ToeDepth : .44f; } }

        /// <summary>Returns the cached adapter of a ModAPI character, creating it on first use (dead adapters are swept now and then).</summary>
        private IAnimatedCharacter Wrap(ModAPI.Character c)
        {
            if (c == null) return null;
            CharacterAdapter a;
            if (_wrapped.TryGetValue(c, out a)) return a;
            if (_wrapped.Count >= 32 && _wrapped.Count > ModAPI.Count * 2) Sweep();
            a = new CharacterAdapter(c);
            _wrapped[c] = a;
            return a;
        }

        /// <summary>adapters of disposed characters (the character itself is already out of ModAPI's list)</summary>
        private void Sweep()
        {
            _dead.Clear();
            foreach (var kv in _wrapped) if (!kv.Key.Valid) _dead.Add(kv.Key);
            for (int i = 0; i < _dead.Count; i++) _wrapped.Remove(_dead[i]);
            _dead.Clear();
        }

        /// <summary>Drops the adapter of a disposed character.</summary>
        internal void Forget(ModAPI.Character c) { if (c != null) _wrapped.Remove(c); }
        /// <summary>Drops every cached adapter (scene load, module stop).</summary>
        internal void Clear() { _wrapped.Clear(); }

        private sealed class CharacterAdapter : IAnimatedCharacter
        {
            private readonly ModAPI.Character _c;
            /// <summary>Wraps one ModAPI character.</summary>
            internal CharacterAdapter(ModAPI.Character c) { _c = c; }

            /// <summary>IAnimatedCharacter: the driven Animator.</summary>
            public Animator Animator { get { return _c.Animator; } }
            /// <summary>IAnimatedCharacter: false once disposed or destroyed.</summary>
            public bool Valid { get { return _c.Valid; } }
            /// <summary>IAnimatedCharacter: the held weapon's key.</summary>
            public string WeaponKey { get { return _c.WeaponKey; } }
            /// <summary>IAnimatedCharacter: the held weapon's hold style.</summary>
            public string WeaponKind { get { return _c.WeaponKind; } }
            /// <summary>IAnimatedCharacter: a reload is playing.</summary>
            public bool Reloading { get { return _c.Reloading; } }
            /// <summary>IAnimatedCharacter: a pump / bolt cocking is playing.</summary>
            public bool Pumping { get { return _c.Pumping; } }
            /// <summary>IAnimatedCharacter: the clip that moves the hands now ("" when none).</summary>
            public string HandsClip { get { return _c.HandsClip; } }
            /// <summary>IAnimatedCharacter: the whole-body action clip ("" when none).</summary>
            public string ActionClip { get { return _c.ActionClip; } }
            /// <summary>IAnimatedCharacter: the upper-body one-shot clip ("" when none).</summary>
            public string UpperClip { get { return _c.UpperClip; } }
            /// <summary>IAnimatedCharacter: the speed cap of the current stance, m/s.</summary>
            public float SpeedCap { get { return _c.SpeedCap; } }
            /// <summary>IAnimatedCharacter: velocity is set by the caller instead of measured.</summary>
            public bool ManualVelocity { get { return _c.ManualVelocity; } set { _c.ManualVelocity = value; } }
            /// <summary>IAnimatedCharacter: planar velocity for the locomotion blend.</summary>
            public Vector3 Velocity { get { return _c.Velocity; } set { _c.Velocity = value; } }
            /// <summary>IAnimatedCharacter: crouched stance.</summary>
            public bool Crouched { get { return _c.Crouched; } set { _c.Crouched = value; } }
            /// <summary>IAnimatedCharacter: aiming stance.</summary>
            public bool Aiming { get { return _c.Aiming; } set { _c.Aiming = value; } }
            /// <summary>IAnimatedCharacter: firing stance.</summary>
            public bool Firing { get { return _c.Firing; } set { _c.Firing = value; } }
            /// <summary>IAnimatedCharacter: in the air.</summary>
            public bool Airborne { get { return _c.Airborne; } set { _c.Airborne = value; } }
            /// <summary>IAnimatedCharacter: aim pitch, degrees.</summary>
            public float AimPitch { get { return _c.AimPitch; } set { _c.AimPitch = value; } }
            /// <summary>IAnimatedCharacter: turn-in-place clips enabled.</summary>
            public bool TurnInPlace { get { return _c.TurnInPlace; } set { _c.TurnInPlace = value; } }
            /// <summary>IAnimatedCharacter: walk-to-stop clip enabled.</summary>
            public bool WalkToStop { get { return _c.WalkToStop; } set { _c.WalkToStop = value; } }
            /// <summary>IAnimatedCharacter: the graph is suspended (the Animator's own controller shows).</summary>
            public bool Suspended { get { return _c.Suspended; } set { _c.Suspended = value; } }
            /// <summary>IAnimatedCharacter: sets the held weapon model (key from its name).</summary>
            public void SetWeapon(Transform model) { _c.SetWeapon(model); }
            /// <summary>IAnimatedCharacter: sets the held weapon model with an explicit key.</summary>
            public void SetWeapon(Transform model, string weaponKey) { _c.SetWeapon(model, weaponKey); }
            /// <summary>IAnimatedCharacter: plays a shot.</summary>
            public void Shoot() { _c.Shoot(); }
            /// <summary>IAnimatedCharacter: plays a shot lasting the given seconds.</summary>
            public void Shoot(float seconds) { _c.Shoot(seconds); }
            /// <summary>IAnimatedCharacter: plays a whole reload fitted to the given seconds.</summary>
            public void Reload(float seconds) { _c.Reload(seconds); }
            /// <summary>IAnimatedCharacter: plays a reload of the given rounds fitted to the given seconds.</summary>
            public void Reload(float seconds, int rounds) { _c.Reload(seconds, rounds); }
            /// <summary>IAnimatedCharacter: cancels a running reload.</summary>
            public void CancelReload() { _c.CancelReload(); }
            /// <summary>IAnimatedCharacter: plays the pump / bolt cocking.</summary>
            public void Pump() { _c.Pump(); }
            /// <summary>IAnimatedCharacter: plays the jump take-off.</summary>
            public void Jump() { _c.Jump(); }
            /// <summary>IAnimatedCharacter: plays a kick.</summary>
            public void Kick() { _c.Kick(); }
            /// <summary>IAnimatedCharacter: plays the quick left-hand throw.</summary>
            public void Throw() { _c.Throw(); }
            /// <summary>IAnimatedCharacter: plays a throw with the chosen hand.</summary>
            public void Throw(bool rightHand) { _c.Throw(rightHand); }
            /// <summary>IAnimatedCharacter: plays a melee strike / punch.</summary>
            public void Strike() { _c.Strike(); }
            /// <summary>IAnimatedCharacter: plays a melee strike lasting the given seconds.</summary>
            public void Strike(float seconds) { _c.Strike(seconds); }
            /// <summary>IAnimatedCharacter: releases the Animator and forgets the adapter.</summary>
            public void Dispose()
            {
                _c.Dispose();
                var s = PresentationModule.Animation;
                if (s != null) s.Forget(_c);
            }
        }
    }
}
