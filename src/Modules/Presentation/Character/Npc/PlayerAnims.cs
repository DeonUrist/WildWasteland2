using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    // (2.6.1) Human turrets animated by the player animation framework (ex Apocaplayer's ModAPI): the player's own crouch set on the roof -
    // a Warboy crouches (CrouchIdle) and throws his lance over the crouched legs (ThrowRight on the upper body), stands up before a
    // motorcycle leap, jumps with the player's Jump; a gunner crouches with its gun in the right hand at the player's weapon poses,
    // aiming (RifleCrouchAim / the pistol's) and pitching its spine at the target, the whole body turned by Pose. The body never walks:
    // it rides, its velocity is held at zero.
    //
    // WW: the provider is ModApi.Get<IAnimationApi>() (the PlayerBody module); null = the old "Apocaplayer not installed" path: the bundle
    // clips by name (Rider) and the bone squat (Pose). A body is kept as a plain object (an IAnimatedCharacter).
    // A character some other module already animates (NPC gunmen, by the same Animator) is left to it: Attach returns null.
    internal static class PlayerAnims
    {
        private static int _logged;   // 1 = "not available" logged, 2 = "available" logged

        internal static bool Available
        {
            get
            {
                var api = ModApi.Get<IAnimationApi>();
                bool ok = api != null && api.ApiVersion >= 1 && api.Ready;
                if (ok && _logged != 2) { _logged = 2; Core.Log.Main(PresentationModule.Instance, "Human turrets: animated by the player animation framework"); }
                else if (!ok && _logged == 0) { _logged = 1; Core.Log.Main(PresentationModule.Instance, "Human turrets: player animations not available (PlayerBody off or bundle missing) - the old crouch"); }
                return ok;
            }
        }

        // ---------------------------------------------------------------- per body (only when Available)
        /// <summary>a riding character: never walks (zero velocity), no turn-in-place / walk-to-stop clips (the body is turned by our code)</summary>
        internal static object Attach(Animator a)
        {
            var api = ModApi.Get<IAnimationApi>();
            if (api == null || a == null || !a.isHuman) return null;
            if (api.Find(a) != null) { Core.Log.Deep(PresentationModule.Instance,"Human turrets: " + a.gameObject.name + " is already animated by another mod - left to it"); return null; }
            var c = api.Attach(a);
            if (c == null) return null;
            c.ManualVelocity = true; c.Velocity = Vector3.zero;
            c.TurnInPlace = false; c.WalkToStop = false;
            return c;
        }
        /// <summary>Releases a turret rider's body.</summary>
        internal static void Dispose(object body) { var c = body as Core.IAnimatedCharacter; if (c != null) c.Dispose(); }
        /// <summary>false as well when the provider went away (PlayerBody switched off): the body falls back to the old crouch</summary>
        internal static bool Alive(object body) { var c = body as Core.IAnimatedCharacter; return c != null && ModApi.Get<IAnimationApi>() != null && c.Valid && !c.Suspended; }
        /// <summary>this frame's situation (call every frame)</summary>
        internal static void Set(object body, bool crouched, bool aiming, bool airborne, float aimPitch)
        {
            var c = body as Core.IAnimatedCharacter; if (c == null) return;
            c.ManualVelocity = true; c.Velocity = Vector3.zero;
            c.Crouched = crouched; c.Aiming = aiming; c.Firing = false; c.Airborne = airborne; c.AimPitch = aimPitch;
        }
        /// <summary>Plays the right-hand throw (lance).</summary>
        internal static void Throw(object body) { var c = body as Core.IAnimatedCharacter; if (c != null) c.Throw(true); }
        /// <summary>Plays the jump take-off and marks the rider airborne.</summary>
        internal static void Jump(object body) { var c = body as Core.IAnimatedCharacter; if (c != null) { c.Airborne = true; c.Jump(); } }
        /// <summary>the gun model in the right hand (null = none: bare hands)</summary>
        internal static void SetWeapon(object body, Transform model, string key) { var c = body as Core.IAnimatedCharacter; if (c != null) c.SetWeapon(model, model != null ? key : null); }
        /// <summary>Weapon-pose key of a weapon object name (null without the framework).</summary>
        internal static string WeaponKey(string objectName) { var api = ModApi.Get<IAnimationApi>(); return api != null ? api.WeaponKey(objectName) : null; }
        /// <summary>True when the player has poses for the weapon key.</summary>
        internal static bool HasWeaponPoses(string key) { var api = ModApi.Get<IAnimationApi>(); return api != null && api.HasWeaponPoses(key); }
        /// <summary>how long the right-hand throw takes on the hands (CharPlan: the clip's length, 0.5..2 s)</summary>
        internal static float ThrowSeconds()
        {
            var api = ModApi.Get<IAnimationApi>();
            var clip = api == null ? null : (api.GetClip("ThrowRight") ?? api.GetClip("Throw"));
            return clip != null ? Mathf.Clamp(clip.length, 0.5f, 2f) : 0.9f;
        }
        /// <summary>the framework's humanoid clips by name (ex reflection on Apocaplayer.Anims.Get) - null without the provider or the clip</summary>
        internal static AnimationClip Clip(string name)
        {
            var api = ModApi.Get<IAnimationApi>();
            if (api == null) return null;
            try { return api.GetClip(name); } catch (System.Exception) { return null; }
        }
    }
}
