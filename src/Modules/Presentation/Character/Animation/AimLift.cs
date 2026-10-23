using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WildWasteland.Modules.Presentation
{
    // (2.1.7) Aiming a gun, the game shoots from the camera (her eyes) - but the Mixamo aim clips hold the rifle ~30 cm lower, so the shots seemed
    // to leave from her mouth. While an aim / fire clip moves her hands, both hands are lifted (and pushed forward) together by a two-bone arm IK
    // (shoulders stay, elbows bend, the grip and the weapon pose in the hand are untouched) and her head tilts down onto the stock.
    // Per weapon and aim clip: lift (cm), head down (degrees), forward (cm) - in config/WildWasteland/PlayerBody/aim-lift.txt (its own file: weapon-poses.txt
    // is never touched). WeaponAdjustment on: Page Up / Page Down = lift, Home / End = head, Insert / Delete = forward, live.
    // The math here has no Unity types (tools/locotest checks it outside the game).
    internal static partial class AimLift
    {
        public static readonly string[] AimClips = { "RifleAim", "RifleCrouchAim", "RifleFire", "RifleCrouchFire", "PistolFire" };
        /// <summary>True for the aim clips that carry aim lift.</summary>
        public static bool IsAimClip(string clip) { return Array.IndexOf(AimClips, clip) >= 0; }

        /// <summary>the defaults when there is no entry: rifles lifted most of the way to the eyes (the right hand sits ~32 cm under them in RifleAim, the sights ~8 cm over the grip), the head tilted onto the stock; pistols as they are (PistolFire holds the gun at eye level)</summary>
        public static float[] Default(string clip)
        {
            var v = new float[13];
            if (clip != null && clip.StartsWith("Rifle")) { v[0] = 15f; v[1] = 10f; }
            return v;
        }

        private static readonly Dictionary<string, float[]> _v = new Dictionary<string, float[]>();
        // (2.1.9) Denis's tuned values, hard-coded (aim-lift.txt overrides them; the same "weapon|clip[@crouch]=lift, head, forward")
        // (2.2.3) Denis's final tuning (config/WildWasteland/PlayerBody/aim-lift.txt of 2026-10-08 02:43), standing and crouched
        private static readonly string[] BuiltinData =
        {
            "22_pipe_pistol|PistolFire=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_pistol|PistolFire@crouch=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_pistol|RifleCrouchTurnLeft@crouch=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_pistol|RifleCrouchTurnRight@crouch=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_pistol|RifleTurnLeft=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_pistol|RifleTurnRight=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_revolver|PistolFire=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_revolver|PistolFire@crouch=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_revolver|RifleCrouchTurnLeft@crouch=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_revolver|RifleCrouchTurnRight@crouch=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_revolver|RifleTurnLeft=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_revolver|RifleTurnRight=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_smg|PistolFire=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_smg|PistolFire@crouch=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_smg|RifleCrouchTurnLeft@crouch=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_smg|RifleCrouchTurnRight@crouch=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_smg|RifleTurnLeft=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "22_pipe_smg|RifleTurnRight=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_drum|CrouchStrafeLeft@crouch=0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_drum|LeftTurn=15, 10, 0, 0, 0, 13.2, 0, 0, 0, 0, 6, 0, 0",
            "akm_drum|RifleAim=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_drum|RifleCrouchAim@crouch=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_drum|RifleCrouchStrafeLeft@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_drum|RifleCrouchStrafeRight@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_drum|RifleCrouchTurnLeft@crouch=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_drum|RifleCrouchTurnRight@crouch=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_drum|RifleCrouchWalk@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_drum|RifleCrouchWalkBackRight@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_drum|RifleCrouchWalkForwardRight@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_drum|RifleIdle=15, 10, 0, 0, 0, 13.2, 0, 0, 0, 0, 6, 0, 0",
            "akm_drum|RifleRunBack=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_drum|RifleRunLow=-3.4, -28.8, 0, -20.7, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_drum|RifleTurnLeft=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_drum|RifleTurnRight=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_drum|RifleWalkLow=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_drum|RifleWalkToStop=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_drum|RightTurn=15, 10, 0, 0, 0, 13.2, 0, 0, 0, 0, 6, 0, 0",
            "akm_trash|LeftTurn=0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_trash|RifleAim=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_trash|RifleCrouchAim@crouch=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_trash|RifleCrouchStrafeLeft@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_trash|RifleCrouchStrafeRight@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_trash|RifleCrouchTurnLeft@crouch=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_trash|RifleCrouchTurnRight@crouch=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_trash|RifleCrouchWalkBackRight@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_trash|RifleCrouchWalkForwardLeft@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_trash|RifleCrouchWalkForwardRight@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_trash|RifleIdle=15, 10, 0, 0, 0, 13.2, 0, 0, 0, 0, 6, 0, 0",
            "akm_trash|RifleRunLow=-3.4, -28.8, 0, -20.7, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_trash|RifleTurnLeft=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_trash|RifleTurnRight=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akm_trash|RifleWalkToStop=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akm_trash|RightTurn=0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akms|CrouchStrafeLeft@crouch=0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akms|LeftTurn=15, 10, 0, 0, 0, 13.2, 0, 0, 0, 0, 6, 0, 0",
            "akms|RifleAim=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akms|RifleCrouchAim@crouch=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akms|RifleCrouchStrafeLeft@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akms|RifleCrouchStrafeRight@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akms|RifleCrouchTurnLeft@crouch=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akms|RifleCrouchTurnRight@crouch=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akms|RifleCrouchWalk@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akms|RifleCrouchWalkBackRight@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akms|RifleCrouchWalkForwardLeft@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akms|RifleCrouchWalkForwardRight@crouch=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akms|RifleIdle=15, 10, 0, 0, 0, 13.2, 0, 0, 0, 0, 6, 0, 0",
            "akms|RifleRunBack=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akms|RifleRunLow=-3.4, -28.8, 0, -20.7, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akms|RifleTurnLeft=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akms|RifleTurnRight=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "akms|RifleWalkToStop=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "akms|RightTurn=15, 10, 0, 0, 0, 13.2, 0, 0, 0, 0, 6, 0, 0",
            "borz_smg|LeftTurn=-0.6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "borz_smg|PistolFire=-0.6, 0, 0",
            "borz_smg|PistolIdle=-0.6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "borz_smg|RifleCrouchTurnLeft@crouch=-0.6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "borz_smg|RifleCrouchTurnRight@crouch=-0.6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "borz_smg|RifleTurnLeft=-0.6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "borz_smg|RifleTurnRight=-0.6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "borz_smg|RightTurn=-0.6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "crossbow|CrouchStrafeLeft@crouch=0, 0, 0, 0, 0, 0, 0.6, 0, 0, 0, 0, 0, 0",
            "crossbow|LeftTurn=-10.2, 10, 0, -9.3, 8.9, 0, 0, 0, 0, 0, 10.8, 9, 0",
            "crossbow|RifleAim=-10.2, 10, 0, -9.3, 8.9, 0, 0, 0, 0, 0, 10.8, 9, 0",
            "crossbow|RifleCrouchAim@crouch=0.5, -4.8, 0, 0, 6.4, 0, 0, 0, 0, 0, 0, 0, 0",
            "crossbow|RifleCrouchIdle@crouch=15, 10, 0, -2.5, 4.4, 0, 0, 0, 0, 0, 0, 0, 0",
            "crossbow|RifleCrouchTurnLeft@crouch=0.5, -4.8, 0, 0, 6.4, 0, 0, 0, 0, 0, 0, 0, 0",
            "crossbow|RifleCrouchTurnRight@crouch=0.5, -4.8, 0, 0, 6.4, 0, 0, 0, 0, 0, 0, 0, 0",
            "crossbow|RifleIdle=-10.2, 10, 0, -9.3, 8.9, 0, 0, 0, 0, 0, 10.8, 9, 0",
            "crossbow|RifleReload=15, 10, 0, 0, 0, 19.2, 0, 0, 0, 0, 0, 0, 0",
            "crossbow|RifleRunForwardRight=-10.2, 10, 0, 14.1, 11.4, 43.8, 0, 0, -0.5, 0, 10.8, 9, 0",
            "crossbow|RifleRunLow=-10.2, 10, 0, 14.1, 11.4, 43.8, 0, 0, -0.5, 0, 10.8, 9, 0",
            "crossbow|RifleSprintBackLeft=-10.2, 10, 0, 14.1, 11.4, 43.8, 0, 0, -0.5, 0, 10.8, 9, 0",
            "crossbow|RifleSprintForwardLeft=-10.2, 10, 0, 14.1, 11.4, 43.8, 0, 0, -0.5, 0, 10.8, 9, 0",
            "crossbow|RifleSprintStrafeLeft=-10.2, 10, 0, 14.1, 11.4, 43.8, 0, 0, -0.5, 0, 10.8, 9, 0",
            "crossbow|RifleTurnLeft=-10.2, 10, 0, -9.3, 8.9, 0, 0, 0, 0, 0, 10.8, 9, 0",
            "crossbow|RifleTurnRight=-10.2, 10, 0, -9.3, 8.9, 0, 0, 0, 0, 0, 10.8, 9, 0",
            "crossbow|RifleWalkBack=15, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "crossbow|RightTurn=-10.2, 10, 0, -9.3, 8.9, 0, 0, 0, 0, 0, 10.8, 9, 0",
            "folk_17|PistolFire=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "folk_17|PistolFire@crouch=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "folk_17|RifleCrouchTurnLeft@crouch=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "folk_17|RifleCrouchTurnRight@crouch=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "folk_17|RifleTurnLeft=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "folk_17|RifleTurnRight=0.7, -5.8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "m16a1|RifleAim=12.9, -30, 0, -17.5, 9.3, 0, 0, 0, -17.7, -1.1, 0, 0, 0",
            "m16a1|RifleCrouchAim@crouch=12.9, -30, 0, -17.5, 9.3, 0, 0, 0, -17.7, -1.1, 0, 0, 0",
            "m16a1|RifleCrouchTurnLeft@crouch=12.9, -30, 0, -17.5, 9.3, 0, 0, 0, -17.7, -1.1, 0, 0, 0",
            "m16a1|RifleCrouchTurnRight@crouch=12.9, -30, 0, -17.5, 9.3, 0, 0, 0, -17.7, -1.1, 0, 0, 0",
            "m16a1|RifleIdle=-3.4, -28.8, 0, 1.3, 11.4, 0, 0, 0, -1.1, 2.6, 0, 0.6, 6.6",
            "m16a1|RifleTurnLeft=12.9, -30, 0, -17.5, 9.3, 0, 0, 0, -17.7, -1.1, 0, 0, 0",
            "m16a1|RifleTurnRight=12.9, -30, 0, -17.5, 9.3, 0, 0, 0, -17.7, -1.1, 0, 0, 0",
            "redmark_m11_scoped|RifleAim=0, 0, 0, -7.5, 6, 0, 7.8, 0, -8.3, 0.5, 0, 0.6, 0",
            "redmark_m11_scoped|RifleCrouchAim@crouch=0, 0, 0, -7.7, 3, 0, 7.8, 0, -8.3, 0.5, 0, 0.6, 0",
            "redmark_m11_scoped|RifleCrouchTurnLeft@crouch=0, 0, 0, -7.7, 3, 0, 7.8, 0, -8.3, 0.5, 0, 0.6, 0",
            "redmark_m11_scoped|RifleCrouchTurnRight@crouch=0, 0, 0, -7.7, 3, 0, 7.8, 0, -8.3, 0.5, 0, 0.6, 0",
            "redmark_m11_scoped|RifleIdle=15, 10, 0, -2.1, -0.2, 0, 0, 0, 0, 0, 0, 0, 0",
            "redmark_m11_scoped|RifleReload=15, 10, 0, -19.4, -9.4, 0, 0, 0, 0, 0, 0, 0, 0",
            "redmark_m11_scoped|RifleTurnLeft=0, 0, 0, -7.5, 6, 0, 7.8, 0, -8.3, 0.5, 0, 0.6, 0",
            "redmark_m11_scoped|RifleTurnRight=0, 0, 0, -7.5, 6, 0, 7.8, 0, -8.3, 0.5, 0, 0.6, 0",
            "redmark_m11|RifleAim=0, 0, 0, -7.5, 6, 0, 7.8, 0, -8.3, 0.5, 0, 0.6, 0",
            "redmark_m11|RifleCrouchAim@crouch=0, 0, 0, -7.7, 3, 0, 7.8, 0, -8.3, 0.5, 0, 0.6, 0",
            "redmark_m11|RifleCrouchTurnLeft@crouch=0, 0, 0, -7.7, 3, 0, 7.8, 0, -8.3, 0.5, 0, 0.6, 0",
            "redmark_m11|RifleCrouchTurnRight@crouch=0, 0, 0, -7.7, 3, 0, 7.8, 0, -8.3, 0.5, 0, 0.6, 0",
            "redmark_m11|RifleIdle=15, 10, 0, -2.1, -0.2, 0, 0, 0, 0, 0, 0, 0, 0",
            "redmark_m11|RifleReload=15, 10, 0, -19.4, -9.4, 0, 0, 0, 0, 0, 0, 0, 0",
            "redmark_m11|RifleTurnLeft=0, 0, 0, -5.4, 6.2, 0, 7.8, 0, -8.3, 0.5, 0, 0.6, 0",
            "redmark_m11|RifleTurnRight=0, 0, 0, -5.4, 6.2, 0, 7.8, 0, -8.3, 0.5, 0, 0.6, 0",
            "rochester_m24_chopped|LeftTurn=0, 0, 0, 5.2, 6, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "rochester_m24_chopped|RifleAim=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "rochester_m24_chopped|RifleCrouchAim@crouch=0, 0, 0, 0.6, 3.9, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "rochester_m24_chopped|RifleCrouchIdle@crouch=0, 0, 0, 0.6, 3.9, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "rochester_m24_chopped|RifleCrouchTurnLeft@crouch=0, 0, 0, 0.6, 3.9, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "rochester_m24_chopped|RifleCrouchTurnRight@crouch=0, 0, 0, 0.6, 3.9, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "rochester_m24_chopped|RifleIdle=0, 0, 0, 5.2, 6, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "rochester_m24_chopped|RifleRunLow=0, 0, 0, -13, 3.9, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "rochester_m24_chopped|RifleTurnLeft=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "rochester_m24_chopped|RifleTurnRight=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "rochester_m24_chopped|RifleWalkToStop=0, 0, 0, 5.2, 6, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "rochester_m24_chopped|RightTurn=0, 0, 0, 5.2, 6, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "rochester_m24|RifleAim=11.6, -15.4, 0",
            "rochester_m24|RifleCrouchAim@crouch=11.4, -3.4, 0",
            "slamberg_500_chopped|RifleAim=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500_chopped|RifleCrouchAim@crouch=0, 0, 0, 0, 1.9, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500_chopped|RifleCrouchTurnLeft@crouch=0, 0, 0, 0, 1.9, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500_chopped|RifleCrouchTurnRight@crouch=0, 0, 0, 0, 1.9, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500_chopped|RifleIdle=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500_chopped|RifleReload=6, -8.2, 0, 3, 15.7, 36.6, -14.4, 0, -10.6, 1, 0, 0.6, 0",
            "slamberg_500_chopped|RifleRunLow=6, -8.2, 0, 0.2, 26.6, 36.6, -14.4, 0, -10.3, 1, 0, 0.6, 0",
            "slamberg_500_chopped|RifleTurnLeft=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500_chopped|RifleTurnRight=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500_chopped|RifleWalkForwardRight=15, 10, 0, -4.1, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "slamberg_500_chopped|RifleWalkToStop=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500|RifleAim=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500|RifleCrouchAim@crouch=0, 0, 0, -2, 3.2, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500|RifleCrouchTurnLeft@crouch=0, 0, 0, -2, 3.2, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500|RifleCrouchTurnRight@crouch=0, 0, 0, -2, 3.2, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500|RifleIdle=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500|RifleTurnLeft=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500|RifleTurnRight=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamberg_500|RifleWalkLow=15, 10, 0, 1.3, 3.7, 0, 0, 0, 0, 0, 0, 0, 0",
            "slamberg_500|RifleWalkToStop=0, 0, 0, 1.8, 6.7, 0, 7.8, 0, 0, 0, 0, 0, 0",
            "slamfire_shotgun|LeftTurn=15, 10, 0, -2.7, 2.1, 0, 0, 0, 0, 0, 0, 0, 0",
            "slamfire_shotgun|RifleAim=12, -17.6, 0, 1.2, 10.6, 0, 0, 0, 0, 0, 0, 7.8, 0",
            "slamfire_shotgun|RifleCrouchAim@crouch=12, -17.6, 0, 0.9, 8.7, 0, 0, 0, 0, 0, 0, 7.8, 0",
            "slamfire_shotgun|RifleCrouchIdle@crouch=15, 10, 0, -2.4, 0.6, 0, 0, 0, 0, 0, 0, 0, 0",
            "slamfire_shotgun|RifleCrouchTurnLeft@crouch=15, 10, 0, -2.4, 0.6, 0, 0, 0, 0, 0, 0, 0, 0",
            "slamfire_shotgun|RifleCrouchTurnRight@crouch=15, 10, 0, -2.4, 0.6, 0, 0, 0, 0, 0, 0, 0, 0",
            "slamfire_shotgun|RifleIdle=15, 10, 0, -2.7, 2.1, 0, 0, 0, 0, 0, 0, 0, 0",
            "slamfire_shotgun|RifleRunLow=12, -17.6, 0, -19.8, 9.9, 0, 0, 0, 0, 0, 0, 7.8, 0",
            "slamfire_shotgun|RifleTurnLeft=12, -17.6, 0, 1.2, 10.6, 0, 0, 0, 0, 0, 0, 7.8, 0",
            "slamfire_shotgun|RifleTurnRight=12, -17.6, 0, 1.2, 10.6, 0, 0, 0, 0, 0, 0, 7.8, 0",
            "slamfire_shotgun|RifleWalkToStop=15, 10, 0, -2.7, 2.1, 0, 0, 0, 0, 0, 0, 0, 0",
            "slamfire_shotgun|RightTurn=15, 10, 0, -2.7, 2.1, 0, 0, 0, 0, 0, 0, 0, 0",
        };
        private static Dictionary<string, float[]> _builtin;
        /// <summary>Built-in value of a "weapon|clip[@crouch]" key (Denis's tuning), or null.</summary>
        private static float[] Builtin(string key)
        {
            if (_builtin == null)
            {
                _builtin = new Dictionary<string, float[]>();
                foreach (var line in BuiltinData)
                {
                    int eq = line.IndexOf('='); if (eq <= 0) continue;
                    var p = line.Substring(eq + 1).Split(',');
                    if (p.Length < 3) continue;
                    var v = new float[13]; bool ok = true;
                    for (int i = 0; i < Math.Min(p.Length, v.Length); i++) ok &= float.TryParse(p[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]);
                    if (ok) _builtin[line.Substring(0, eq).Trim()] = v;
                }
            }
            float[] r; return _builtin.TryGetValue(key, out r) ? r : null;
        }
        private static string _file;
        /// <summary>Loads config/WildWasteland/Presentation/aim-lift.txt (user values over the built-in table).</summary>
        public static void Load(string dir)
        {
            _file = Path.Combine(dir, "aim-lift.txt");
            EditWeapon = EditClip = null; EditFrame = -1; _armKeys = null; Revision++;
            _v.Clear();
            if (!File.Exists(_file)) return;
            foreach (var raw in File.ReadAllLines(_file))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('='); if (eq <= 0) continue;
                var p = line.Substring(eq + 1).Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 3) continue;
                var v = new float[13]; bool ok = true;
                for (int i = 0; i < Math.Min(p.Length, v.Length); i++) ok &= float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]);
                if (ok) _v[line.Substring(0, eq).Trim()] = v;
            }
        }
        // (WW) module Shutdown / F7 save button: write only when something was changed since the last save
        internal static bool Dirty;
        /// <summary>Writes the file when something changed since the last save.</summary>
        internal static void SaveIfDirty() { if (Dirty) Save(); }
        /// <summary>Number of user entries.</summary>
        internal static int UserCount { get { return _v.Count; } }
        /// <summary>Path of aim-lift.txt.</summary>
        internal static string FilePath { get { return _file; } }
        /// <summary>Writes every user entry to aim-lift.txt (temp file + atomic replace).</summary>
        public static void Save()
        {
            if (_file == null) return;
            Dirty = false;
            var sb = new StringBuilder();
            sb.AppendLine("# Wild Wasteland (PlayerBody) aim lift: weapon|aim clip[@crouch] = lift (cm), head down (degrees), forward (cm). Edited live with WeaponAdjustment on:");
            sb.AppendLine("# Page Up / Page Down = lift, Home / End = head, Insert / Delete = forward (while she aims, or the aim clip picked with numpad 9/3).");
            sb.AppendLine("# Optional values after forward: left arm up/right (cm), pitch/yaw/roll (degrees), then right arm up/right, pitch/yaw/roll. Missing values are zero; edited with F7 weapon pose buttons.");
            var keys = new List<string>(_v.Keys); keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys) sb.AppendLine(k + " = " + string.Join(", ", Array.ConvertAll(_v[k], x => x.ToString("0.#", CultureInfo.InvariantCulture))));
            string tmp = _file + ".tmp";   // (D17) temp + same-directory atomic replace, never an in-place overwrite
            File.WriteAllText(tmp, sb.ToString());
            Core.AtomicFile.Replace(tmp, _file);
        }
        // (2.1.8) standing and crouched have their own values: the key is the clip + "@crouch" when crouched; a crouched aim without its own entry
        // starts from the standing one (so a tuned weapon doesn't jump back to the default)
        /// <summary>Stores a user value set for (weapon, clip[@crouch]) and marks the table dirty (IPoseAuthoring.SetPose).</summary>
        internal static void Put(string weapon, string clipKey, float[] v) { _v[weapon + "|" + clipKey] = v; Dirty = true; Revision++; }
        /// <summary>Adds every weapon key with a file or built-in aim lift to <paramref name="into"/> (no duplicates, unsorted).</summary>
        internal static void Weapons(List<string> into)
        {
            Builtin("");
            foreach (var k in _v.Keys) AddWeapon(k, into);
            foreach (var k in _builtin.Keys) AddWeapon(k, into);
        }
        /// <summary>Adds the weapon part of a "weapon|clip" key once.</summary>
        static void AddWeapon(string key, List<string> into) { int bar = key.IndexOf('|'); if (bar > 0) { string w = key.Substring(0, bar); if (!into.Contains(w)) into.Add(w); } }
        /// <summary>Table key of a clip: "clip@crouch" when crouched.</summary>
        public static string Key(string clip, bool crouched) { return crouched ? clip + "@crouch" : clip; }
        // (WW perf) resolved per (weapon, clip, crouched) until the next Set/Load (Revision): Get ran a few concats per call, every frame.
        // The returned arrays are never written by the callers (edits Clone first); Default(clip) is shared per clip from here on.
        private static readonly Dictionary<string, Dictionary<string, float[]>>[] _got = { new Dictionary<string, Dictionary<string, float[]>>(), new Dictionary<string, Dictionary<string, float[]>>() };
        private static int _gotRevision = int.MinValue;
        /// <summary>Aim lift values of (weapon, clip, crouched), cached until the table changes (never written by callers).</summary>
        public static float[] Get(string weapon, string clip, bool crouched)
        {
            if (weapon == null || clip == null) return Resolve(weapon, clip, crouched);
            if (_gotRevision != Revision) { _got[0].Clear(); _got[1].Clear(); _gotRevision = Revision; }
            var byWeapon = _got[crouched ? 1 : 0];
            Dictionary<string, float[]> byClip;
            if (!byWeapon.TryGetValue(weapon, out byClip)) byWeapon[weapon] = byClip = new Dictionary<string, float[]>();
            float[] r;
            if (!byClip.TryGetValue(clip, out r)) byClip[clip] = r = Resolve(weapon, clip, crouched);
            return r;
        }
        /// <summary>Resolves a value: user entry, built in, the standing entry for a crouched clip, else the clip's default.</summary>
        private static float[] Resolve(string weapon, string clip, bool crouched)
        {
            float[] v;
            string k = weapon + "|" + Key(clip, crouched), k0 = weapon + "|" + clip;
            if (_v.TryGetValue(k, out v)) return v;
            if ((v = Builtin(k)) != null) return v;
            if (crouched && _v.TryGetValue(k0, out v)) return v;
            if (crouched && (v = Builtin(k0)) != null) return v;
            return Default(clip);
        }
        /// <summary>True when (weapon, clip, crouched) has its own user or built-in entry.</summary>
        public static bool Own(string weapon, string clip, bool crouched) { string k = weapon + "|" + Key(clip, crouched); return _v.ContainsKey(k) || Builtin(k) != null; }

        internal static int Revision;

        // ---- two-bone IK: the new elbow position so the hand reaches t, the arm bending in the plane it bends in now
        /// <summary>s, e, h = shoulder, elbow, hand positions; returns the elbow (the caller turns the upper arm onto it, then the forearm onto t) (WW perf) the math on plain doubles, no arrays (WW2: the array version used only by tools/locotest is gone, README 7)</summary>
        public static void Elbow(double sx, double sy, double sz, double ex, double ey, double ez, double hx, double hy, double hz, double tx, double ty, double tz,
                                 out double rx, out double ry, out double rz)
        {
            rx = ex; ry = ey; rz = ez;
            double dx = sx - ex, dy = sy - ey, dz = sz - ez;
            double a = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            dx = ex - hx; dy = ey - hy; dz = ez - hz;
            double b = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double stx = tx - sx, sty = ty - sy, stz = tz - sz;
            double stl = Math.Sqrt(stx * stx + sty * sty + stz * stz), c = stl;
            if (c < 1e-6 || a < 1e-6 || b < 1e-6) return;
            c = Math.Max(Math.Abs(a - b) + 1e-4, Math.Min(a + b - 1e-4, c));
            double k = 1.0 / stl;
            double ux = stx * k, uy = sty * k, uz = stz * k;
            double sex = ex - sx, sey = ey - sy, sez = ez - sz;
            double dot = sex * ux + sey * uy + sez * uz;
            double px = sex - ux * dot, py = sey - uy * dot, pz = sez - uz * dot;
            double pl = Math.Sqrt(px * px + py * py + pz * pz);
            if (pl < 1e-6)
            {
                double m = -uy;
                px = 0.0 - ux * m; py = -1.0 - uy * m; pz = 0.0 - uz * m;
                pl = Math.Sqrt(px * px + py * py + pz * pz);
                if (pl < 1e-6) return;
            }
            k = 1.0 / pl;
            px = px * k; py = py * k; pz = pz * k;
            double cosA = Math.Max(-1.0, Math.Min(1.0, (a * a + c * c - b * b) / (2 * a * c))), sinA = Math.Sqrt(1 - cosA * cosA);
            double ka = a * cosA, kb = a * sinA;
            rx = sx + (ux * ka + px * kb); ry = sy + (uy * ka + py * kb); rz = sz + (uz * ka + pz * kb);
        }
    }
}
