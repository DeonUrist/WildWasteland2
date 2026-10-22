using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPCAI
{
    // (1.2.0) NPC bodies animated through the player-animation framework (ex Apocaplayer's ModAPI, now the IAnimationApi provided by
    // the Presentation module): the player's own clips and logic - idle, 8 directions x walk / run / sprint, crouch idle and 8 crouched walks
    // for every weapon, the relaxed low-ready walk / run, aiming and firing sets, turns in place, reloads (the whole clip, or one round at a
    // time for the revolver, the shotguns, the bolt rifle and the double barrel), ShotgunPump after each burst of a pump / bolt gun, jumps
    // for the hops, the melee swing, the gun in the right hand at the player's weapon poses and the aim lift.
    //
    // The framework is optional (Presentation module off = no IAnimationApi provider): every call into it is in this class; the Npc keeps its
    // character as a plain object. As before the framework is probed only after 3 s (it loads its bundle first) and, until it reports Ready,
    // probed again every few seconds.
    internal static class ApBody
    {
        private static bool _ok, _logged;
        private static float _nextProbe;
        private static IAnimationApi _api;

        /// <summary>True when the animation framework (IAnimationApi) is up and its bundle ready (probed every 5 s until it is).</summary>
        internal static bool Available { get { Init(); return _ok; } }
        /// <summary>Forgets the framework probe (NPC scene reset).</summary>
        internal static void Reset() { _ok = false; _api = null; _nextProbe = 0f; _logged = false; }

        /// <summary>Probes the IAnimationApi provider after 3 s and then every 5 s until its bundle is ready; logs the state changes.</summary>
        private static void Init()
        {
            var api = ModApi.Get<IAnimationApi>();
            if (api == null) { if (_ok || _api != null) { _ok = false; _api = null; _logged = false; Core.Log.Deep(NpcaiRuntime.Module, "NPC animations: the player animation framework is off - the game's clips, no sidesteps"); } return; }
            if (_ok && ReferenceEquals(api, _api)) return;
            if (Time.unscaledTime < 3f || Time.unscaledTime < _nextProbe) return;     // let the framework finish loading its bundle
            _nextProbe = Time.unscaledTime + 5f;
            _api = api;
            try { _ok = Probe(api); }
            catch (Exception e) { _ok = false; Core.Log.Warn(NpcaiRuntime.Module, "NPC animations: the animation framework failed (" + e.GetType().Name + ") - the game's clips"); return; }
            if (_ok) { if (Core.Log.L2) Core.Log.Deep(NpcaiRuntime.Module, "NPC animations: the player animation framework (ModAPI " + api.ApiVersion + ", " + api.ClipNames().Length + " player clips) in use"); }
            else if (!_logged) { _logged = true; Core.Log.Deep(NpcaiRuntime.Module, "NPC animations: the animation bundle is not ready (yet) - the game's clips"); }
        }
        /// <summary>True when the framework reports its bundle ready.</summary>
        private static bool Probe(IAnimationApi api) { return api.Ready; }

        // ---------------------------------------------------------------- weapon helpers
        /// <summary>Weapon-pose / magazine table key of a held model ("akm_trash_model" -> "akm_trash", "9mm_borz_smg" -> "borz_smg"); the
        /// shared Presentation name rule (Props.Norm, the same as IAnimationApi.WeaponKey), also while the animation framework is off.
        /// (B05) the copy of the rule and the unused IsPistol were deleted (A3-6).</summary>
        internal static string WeaponKey(Transform weapon) { return weapon == null ? null : Core.Names.Weapon(weapon.name); }

        // ---------------------------------------------------------------- per NPC (only when Available)
        /// <summary>Attaches the player animation set to an NPC Animator (turns in place and walk-to-stop on); null when not possible.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static object Attach(Animator a)
        {
            var api = ModApi.Get<IAnimationApi>(); if (api == null) return null;
            var c = api.Attach(a);
            if (c == null) return null;
            c.TurnInPlace = true; c.WalkToStop = true;
            return c;
        }
        /// <summary>Releases an NPC body.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Detach(object body) { var c = body as IAnimatedCharacter; if (c != null) c.Dispose(); }
        /// <summary>True while the body is valid.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool Alive(object body) { var c = body as IAnimatedCharacter; return c != null && c.Valid; }
        /// <summary>Puts the NPC's weapon model into the body's right hand (null = none).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void SetWeapon(object body, Transform model, string key) { var c = body as IAnimatedCharacter; if (c != null) c.SetWeapon(model, model != null ? key : null); }
        /// <summary>Suspends (the game's clips) or resumes the body.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Suspend(object body, bool off) { var c = body as IAnimatedCharacter; if (c != null) c.Suspended = off; }
        /// <summary>True when there is no body or it is suspended.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool Suspended(object body) { var c = body as IAnimatedCharacter; return c == null || c.Suspended; }
        /// <summary>this frame's situation</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Frame(object body, bool crouched, bool aiming, bool firing, bool airborne, float aimPitch)
        {
            var c = body as IAnimatedCharacter; if (c == null) return;
            c.Crouched = crouched; c.Aiming = aiming; c.Firing = firing; c.Airborne = airborne; c.AimPitch = aimPitch;
        }
        /// <summary>Speed cap of the body's current stance, m/s (0 without a body).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static float SpeedCap(object body) { var c = body as IAnimatedCharacter; return c != null ? c.SpeedCap : 0f; }
        /// <summary>Plays a reload fitted to the seconds (rounds &gt; 0 = one round at a time).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Reload(object body, float seconds, int rounds) { var c = body as IAnimatedCharacter; if (c != null) c.Reload(seconds, rounds); }
        /// <summary>Cancels the body's reload.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CancelReload(object body) { var c = body as IAnimatedCharacter; if (c != null) c.CancelReload(); }
        /// <summary>Plays the pump / bolt cocking.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Pump(object body) { var c = body as IAnimatedCharacter; if (c != null) c.Pump(); }
        /// <summary>Plays the jump take-off.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Jump(object body) { var c = body as IAnimatedCharacter; if (c != null) c.Jump(); }
        /// <summary>Plays a melee strike lasting the seconds.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Strike(object body, float seconds) { var c = body as IAnimatedCharacter; if (c != null) c.Strike(seconds); }
        /// <summary>Short state text of a body (weapon, hands/action/upper clips) for diagnostics.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static string Describe(object body)
        {
            var c = body as IAnimatedCharacter; if (c == null) return "-";
            return c.WeaponKey + " (" + c.WeaponKind + ") hands " + c.HandsClip + (c.ActionClip != "" ? ", action " + c.ActionClip : "") + (c.UpperClip != "" ? ", upper " + c.UpperClip : "");
        }

        // ---------------------------------------------------------------- the player's weapon facts (only when Available)
        /// <summary>True when the player has poses for the weapon key.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool HasPoses(string key) { var api = ModApi.Get<IAnimationApi>(); return api != null && api.HasWeaponPoses(key); }
        /// <summary>True when the player's copy of the weapon reloads one round at a time.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool OneRoundAtATime(string key) { var api = ModApi.Get<IAnimationApi>(); return api != null && api.ReloadsOneRoundAtATime(key); }
        /// <summary>True when the player's copy of the weapon cocks after a shot.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool Cocks(string key) { var api = ModApi.Get<IAnimationApi>(); return api != null && api.CocksAfterShot(key); }
        /// <summary>Length of the weapon's reload clip, seconds (0 when unknown).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static float ReloadClipSeconds(string key) { var api = ModApi.Get<IAnimationApi>(); return api != null ? api.ReloadClipSeconds(key) : 0f; }
        /// <summary>One round of a one-round-at-a-time reload, seconds.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static float RoundSeconds(string key) { var api = ModApi.Get<IAnimationApi>(); return api != null ? api.RoundSeconds(key) : 0f; }
        /// <summary>Native ground speed of a clip tier, m/s.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static float NativeSpeed(string tier) { var api = ModApi.Get<IAnimationApi>(); return api != null ? api.NativeSpeed(tier) : 0f; }
    }
}
