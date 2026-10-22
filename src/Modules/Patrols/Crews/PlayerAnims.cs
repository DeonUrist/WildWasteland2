using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // (2.6.1) Human turrets animated by the player animation framework (ex Apocaplayer's ModAPI): the player's own crouch set on the roof -
    // a Warboy crouches (CrouchIdle) and throws his lance over the crouched legs (ThrowRight on the upper body), stands up before a
    // motorcycle leap, jumps with the player's Jump; a gunner crouches with its gun in the right hand at the player's weapon poses,
    // aiming (RifleCrouchAim / the pistol's) and pitching its spine at the target, the whole body turned by Pose. The body never walks:
    // it rides, its velocity is held at zero.
    //
    // WW: the provider is Core.Services.Animation (the PlayerBody module); null = the old "Apocaplayer not installed" path: the bundle
    // clips by name (Rider) and the bone squat (Pose). A body is kept as a plain object (an IAnimatedCharacter).
    // A character some other module already animates (NPC gunmen, by the same Animator) is left to it: Attach returns null.
    internal static class PlayerAnims
    {
        private static int _logged;   // 1 = "not available" logged, 2 = "available" logged

        internal static bool Available
        {
            get
            {
                var api = Core.Services.Animation;
                bool ok = api != null && api.ApiVersion >= 1 && api.Ready;
                if (ok && _logged != 2) { _logged = 2; Core.Log.Info("[Patrols] Human turrets: animated by the player animation framework"); }
                else if (!ok && _logged == 0) { _logged = 1; Core.Log.Info("[Patrols] Human turrets: player animations not available (PlayerBody off or bundle missing) - the old crouch"); }
                return ok;
            }
        }

        // ---------------------------------------------------------------- per body (only when Available)
        // a riding character: never walks (zero velocity), no turn-in-place / walk-to-stop clips (the body is turned by our code)
        internal static object Attach(Animator a)
        {
            var api = Core.Services.Animation;
            if (api == null || a == null || !a.isHuman) return null;
            if (api.Find(a) != null) { PatrolsModule.Verbose("Human turrets: " + a.gameObject.name + " is already animated by another mod - left to it"); return null; }
            var c = api.Attach(a);
            if (c == null) return null;
            c.ManualVelocity = true; c.Velocity = Vector3.zero;
            c.TurnInPlace = false; c.WalkToStop = false;
            return c;
        }
        internal static void Dispose(object body) { var c = body as Core.IAnimatedCharacter; if (c != null) c.Dispose(); }
        // false as well when the provider went away (PlayerBody switched off): the body falls back to the old crouch
        internal static bool Alive(object body) { var c = body as Core.IAnimatedCharacter; return c != null && Core.Services.Animation != null && c.Valid && !c.Suspended; }
        // this frame's situation (call every frame)
        internal static void Set(object body, bool crouched, bool aiming, bool airborne, float aimPitch)
        {
            var c = body as Core.IAnimatedCharacter; if (c == null) return;
            c.ManualVelocity = true; c.Velocity = Vector3.zero;
            c.Crouched = crouched; c.Aiming = aiming; c.Firing = false; c.Airborne = airborne; c.AimPitch = aimPitch;
        }
        internal static void Throw(object body) { var c = body as Core.IAnimatedCharacter; if (c != null) c.Throw(true); }
        internal static void Jump(object body) { var c = body as Core.IAnimatedCharacter; if (c != null) { c.Airborne = true; c.Jump(); } }
        // the gun model in the right hand (null = none: bare hands)
        internal static void SetWeapon(object body, Transform model, string key) { var c = body as Core.IAnimatedCharacter; if (c != null) c.SetWeapon(model, model != null ? key : null); }
        internal static string WeaponKey(string objectName) { var api = Core.Services.Animation; return api != null ? api.WeaponKey(objectName) : null; }
        internal static bool HasWeaponPoses(string key) { var api = Core.Services.Animation; return api != null && api.HasWeaponPoses(key); }
        // how long the right-hand throw takes on the hands (CharPlan: the clip's length, 0.5..2 s)
        internal static float ThrowSeconds()
        {
            var api = Core.Services.Animation;
            var clip = api == null ? null : (api.GetClip("ThrowRight") ?? api.GetClip("Throw"));
            return clip != null ? Mathf.Clamp(clip.length, 0.5f, 2f) : 0.9f;
        }
        // the framework's humanoid clips by name (ex reflection on Apocaplayer.Anims.Get) - null without the provider or the clip
        internal static AnimationClip Clip(string name)
        {
            var api = Core.Services.Animation;
            if (api == null) return null;
            try { return api.GetClip(name); } catch (System.Exception) { return null; }
        }
    }
}
