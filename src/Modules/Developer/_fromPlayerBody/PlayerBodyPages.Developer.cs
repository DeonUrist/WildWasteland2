using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyPages part (split from PlayerBody/PlayerBodyPages.cs in Wave A): the F7 tool pages "Weapon pose editor" and
    /// "Steering-wheel hands". Target: Developer (B16).
    /// </summary>
    internal static partial class PlayerBodyPages
    {
        /// <summary>Adds this part's F7 pages to the PlayerBody page list (called by Create, in the old page order).</summary>
        static partial void AddDeveloperPages(PlayerBodyModule m, List<ToolPage> pages)
        {
            pages.Add(new ActionPage(m, G, "Weapon pose editor", PoseEditor) { ShowWhenModuleOff = false, IsTool = true });
            pages.Add(new ActionPage(m, G, "Steering-wheel hands", WheelArms.Draw) { ShowWhenModuleOff = false, IsTool = true });
        }

        // ------------------------------------------------------------------ Weapon pose editor
        static string _msg = "";
        static void PoseEditor(ToolLayout l)
        {
            l.Text("Edits where each weapon sits in the character's right hand, per animation clip, and the aim lift of the aim / fire clips. "
                 + "Switch it on, close this window, go to third person and draw a weapon: the editor's line shows at the bottom of the screen. "
                 + "Its keys do nothing while it is off.");
            l.Toggle(PlayerBodyModule.WeaponAdjust, "Weapon pose editor on");
            l.Heading("Keys (while on, third person, weapon drawn)");
            l.Text("Numpad 8/2 up/down, 6/4 right/left, 7/1 forward/back (or muzzle up/down, right/left, roll when ROTATING).\n"
                 + "Defaults: Numpad 0 = WEAPON / LEFT ARM / RIGHT ARM; Numpad 5 = MOVING <-> ROTATING. Numpad 9/3 = next / previous clip (played standing still).\n"
                 + "Numpad - = delete the clip's weapon pose. Numpad / = copy weapon, both arms, head tilt and aim lift. Numpad * = paste and save all of them into the current clip.\n"
                 + "Aiming (right mouse, or an aim clip picked with 9/3): Page Up/Down = lift the hands, Home/End = head tilt, Insert/Delete = hands forward.");
            l.Heading("Current");
            bool live = PlayerBodyModule.WeaponAdjust.Value && GunPose.CurrentFrame >= 0 && !string.IsNullOrEmpty(GunPose.CurrentWeapon);
            l.Value("Weapon", live ? GunPose.CurrentWeapon : "(none: draw a weapon in third person)");
            l.Value("Clip", live ? GunPose.CurrentPose + (GunPose.Selected >= 0 ? "  (selected " + (GunPose.Selected + 1) + "/" + GunPose.ClipCount + ")" : "  (what she plays)") : "-");
            var v = GunPose.CurrentShown;
            l.Value("Pose (cm, degrees)", live && v != null && v.Length >= 6 ? F(v[0]) + ", " + F(v[1]) + ", " + F(v[2]) + "  /  " + F(v[3]) + ", " + F(v[4]) + ", " + F(v[5]) : "-");
            l.Value("Mode", GunPose.RotateMode ? "ROTATING" : "MOVING");
            l.Value("Editing target", GunPose.TargetName);
            l.Value("Clipboard", GunPose.HasCopy ? "weapon, arms and head copied" : "empty");
            l.Value("Tables", GunPose.UserCount + " own poses, " + GunPose.BuiltinCount + " built in; " + AimLift.UserCount + " aim-lift entries");
            int hit = l.Buttons("Move / rotate", "Previous clip", "Next clip", "Back to playing");
            if (hit == 0) GunPose.RotateMode = !GunPose.RotateMode;
            else if (hit == 1) GunPose.StepClip(-1);
            else if (hit == 2) GunPose.StepClip(+1);
            else if (hit == 3) GunPose.ResetSelection();
            l.Heading("Live editor key bindings");
            l.Text("Click a binding and press a key; Backspace clears it. Close F7 to edit while watching the pose. Arm movement uses up/down and left/right; rotation also uses the forward/back pair for roll. Arm edits save when the keys are released.");
            GunPose.DrawEditorBindings(l);
            l.Text("Each arm can move up/down and left/right, and rotate around its shoulder. These offsets follow this weapon and clip, with separate standing/crouched values; menu buttons save immediately.");
            ArmButtons(l, true); ArmButtons(l, false);
            hit = l.Buttons("Copy pose", "Paste pose", "Delete clip's pose");
            if (hit == 0) _msg = GunPose.CopyShown() ? "copied weapon, arms and head" : "nothing to copy: enable the editor and draw a weapon in third person";
            else if (hit == 1) _msg = GunPose.PasteCurrent() ? "pasted into " + GunPose.CurrentWeapon + " | " + GunPose.CurrentPose : "nothing copied / no weapon";
            else if (hit == 2) _msg = GunPose.DeleteCurrent() ? "deleted " + GunPose.CurrentWeapon + " | " + GunPose.CurrentPose : "no weapon / clip";
            hit = l.Buttons("SAVE now", "RESET (reload files)");
            if (hit == 0) { GunPose.SaveNow(); AimLift.Save(); _msg = "saved " + GunPose.FilePath + " and aim-lift.txt"; }
            else if (hit == 1) { GunPose.ReloadFiles(); AimLift.Load(PlayerBodyModule.DataDir); _msg = "reloaded from " + PlayerBodyModule.DataDir + " (unsaved edits dropped)"; }
            if (_msg != "") l.Text(_msg, Ledger.Accent);
            l.Text("Every save also writes BuiltinPoses.generated.cs next to weapon-poses.txt: the whole table as C#, to hard-code the poses in the mod.");
        }
        static void ArmButtons(ToolLayout l, bool left)
        {
            l.Heading(left ? "Left arm" : "Right arm");
            bool enabled = GUI.enabled; GUI.enabled = enabled && AimLift.CanEditArms;
            if (AimLift.CanEditArms)
            {
                var v = AimLift.Get(AimLift.EditWeapon, AimLift.EditClip, AimLift.EditCrouched); int i = left ? 3 : 8;
                l.Value("Clip", AimLift.EditClip + (AimLift.EditCrouched ? " (crouched)" : " (standing)"));
                l.Value("Up / right (cm)", F(v[i]) + " / " + F(v[i + 1]));
                l.Value("Pitch / yaw / roll", F(v[i + 2]) + " / " + F(v[i + 3]) + " / " + F(v[i + 4]));
            }
            int hit = l.Buttons("Raise 1 cm", "Lower 1 cm", "Left 1 cm", "Right 1 cm");
            if (hit >= 0) AimLift.NudgeArm(left, hit < 2 ? 0 : 1, hit == 0 || hit == 3 ? 1f : -1f);
            hit = l.Buttons("Pitch -1 degree", "Pitch +1 degree"); if (hit >= 0) AimLift.NudgeArm(left, 2, hit == 0 ? -1f : 1f);
            hit = l.Buttons("Yaw -1 degree", "Yaw +1 degree"); if (hit >= 0) AimLift.NudgeArm(left, 3, hit == 0 ? -1f : 1f);
            hit = l.Buttons("Roll -1 degree", "Roll +1 degree"); if (hit >= 0) AimLift.NudgeArm(left, 4, hit == 0 ? -1f : 1f);
            if (l.Button("Reset this arm")) AimLift.ResetArm(left);
            GUI.enabled = enabled;
        }
    }
}
