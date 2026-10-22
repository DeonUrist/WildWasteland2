using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Body part (split from PlayerBody/Animation/Loco.cs in Wave A): the aim-lift editor keys of the weapon pose editor (Page Up/Down,
    /// Home/End, Insert/Delete) and its hint line. Target: Developer (B16), through Presentation pose authoring in Wave B.
    /// </summary>
    internal sealed partial class Body
    {
        private void AimKeys(string weapon, string clip, float dt)
        {
            if (!GunPose.Editing || string.IsNullOrEmpty(weapon) || !ThirdPerson.On) { GunPose.AimLine = ""; return; }
            if (clip == null)
            {
                GunPose.AimLine = "AIM LIFT: aim (right mouse) or pick an aim clip (RifleAim, RifleCrouchAim, PistolFire ...) with 9/3 to adjust it here.";
                if (_aimDirty) { AimLift.Save(); _aimDirty = false; }
                return;
            }
            float lift = (GameBindings.KeyHeld(KeyCode.PageUp) ? 1f : 0f) - (GameBindings.KeyHeld(KeyCode.PageDown) ? 1f : 0f);
            float head = (GameBindings.KeyHeld(KeyCode.End) ? 1f : 0f) - (GameBindings.KeyHeld(KeyCode.Home) ? 1f : 0f);
            float fwd = (GameBindings.KeyHeld(KeyCode.Insert) ? 1f : 0f) - (GameBindings.KeyHeld(KeyCode.Delete) ? 1f : 0f);
            var v = (float[])AimLift.Get(weapon, clip, _aimCrouched).Clone();
            if (lift != 0f || head != 0f || fwd != 0f)
            {
                v[0] = Mathf.Clamp(v[0] + lift * 5f * dt, -20f, 45f);
                v[1] = Mathf.Clamp(v[1] + head * 15f * dt, -30f, 40f);
                v[2] = Mathf.Clamp(v[2] + fwd * 5f * dt, -20f, 30f);
                AimLift.Set(weapon, clip, _aimCrouched, v); _aimDirty = true;
            }
            else if (_aimDirty) { AimLift.Save(); _aimDirty = false; }
            // how far the top of the gun is under the shots' start (the camera = her eyes), along her up axis
            string gap = "";
            if (_prop != null && Game.PlayerCamera != null)
            {
                var rs = _prop.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    float g = Vector3.Dot(Game.PlayerCamera.position - b.center, _anim.up) * 100f - b.extents.y * 100f;
                    gap = "  -  the top of the gun is " + g.ToString("0") + " cm " + (g >= 0f ? "below" : "above") + " where the shots start (her eyes)";
                }
            }
            GunPose.AimLine = "AIM LIFT " + weapon + " | " + clip + (_aimCrouched ? " CROUCHED" : " STANDING") + ": lift " + v[0].ToString("0.0") + " cm, head down " + v[1].ToString("0") + "°, forward " + v[2].ToString("0.0") + " cm"
                + (AimLift.Own(weapon, clip, _aimCrouched) ? "" : _aimCrouched && AimLift.Own(weapon, clip, false) ? " (from standing)" : " (default)") + gap + "\nPage Up/Down = lift, Home/End = head, Insert/Delete = forward. Saved in config/WildWasteland/PlayerBody/aim-lift.txt";
        }
    }
}
