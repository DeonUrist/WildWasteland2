using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// AimLift part (split from PlayerBody/Animation/AimLift.cs in Wave A): the aim-lift / arm-offset editing of the weapon pose editor
    /// (Page Up/Down, Home/End, Insert/Delete keys, F7 arm buttons). Target: Developer (B16), through Presentation pose authoring in Wave B.
    /// </summary>
    internal static partial class AimLift
    {
        /// <summary>Publishes the complete live aim/head/arm record and invalidates resolved pose caches.</summary>
        public static void Set(string weapon, string clip, bool crouched, float[] v) { _v[weapon + "|" + Key(clip, crouched)] = v; Dirty = true; Revision++; }
        internal static string EditWeapon, EditClip;
        internal static int EditFrame = -1;
        internal static bool EditCrouched;
        internal static bool CanEditArms { get { return WildWasteland.Modules.Presentation.PresentationModule.Running && Game.Ready && !Game.Dead && !Game.InCar && GunPose.Editing && ThirdPerson.On && EditFrame >= UnityEngine.Time.frameCount - 1 && GunPose.CurrentFrame >= UnityEngine.Time.frameCount - 1 && !string.IsNullOrEmpty(EditWeapon) && !string.IsNullOrEmpty(EditClip); } }
        static float[] _armKeys;
        static int _armKeysRevision;
        static string _armWeapon, _armClip; static bool _armCrouched;
        /// <summary>Saves an active arm edit on release and drops the live edit buffer.</summary>
        internal static void EndArmKeys() { if (_armKeys != null) SaveIfDirty(); _armKeys = null; }
        /// <summary>Applies bounded movement and shoulder rotation to the eligible current arm pose.</summary>
        internal static void ArmKeys(bool left, UnityEngine.Vector3 move, UnityEngine.Vector3 rot, float dt)
        {
            if (!CanEditArms || move == UnityEngine.Vector3.zero && rot == UnityEngine.Vector3.zero) { EndArmKeys(); return; }
            if (_armKeys == null || _armWeapon != EditWeapon || _armClip != EditClip || _armCrouched != EditCrouched || _armKeysRevision != Revision)
            {
                EndArmKeys(); _armWeapon = EditWeapon; _armClip = EditClip; _armCrouched = EditCrouched;
                _armKeys = (float[])Get(EditWeapon, EditClip, EditCrouched).Clone(); Set(EditWeapon, EditClip, EditCrouched, _armKeys); _armKeysRevision = Revision;
            }
            int i = left ? 3 : 8;
            _armKeys[i] = UnityEngine.Mathf.Clamp(_armKeys[i] + move.y * dt, -45f, 45f);
            _armKeys[i + 1] = UnityEngine.Mathf.Clamp(_armKeys[i + 1] + move.x * dt, -45f, 45f);
            _armKeys[i + 2] = UnityEngine.Mathf.Clamp(_armKeys[i + 2] - rot.x * dt, -90f, 90f);
            _armKeys[i + 3] = UnityEngine.Mathf.Clamp(_armKeys[i + 3] + rot.y * dt, -90f, 90f);
            _armKeys[i + 4] = UnityEngine.Mathf.Clamp(_armKeys[i + 4] + rot.z * dt, -90f, 90f);
            Dirty = true;
        }
        /// <summary>Adjusts one arm dimension immediately and saves the complete pose record.</summary>
        internal static void NudgeArm(bool left, int axis, float delta)
        {
            if (!CanEditArms) return;
            var v = (float[])Get(EditWeapon, EditClip, EditCrouched).Clone();
            int i = 3 + (left ? 0 : 5) + axis;
            float limit = axis < 2 ? 45f : 90f;
            v[i] = Math.Max(-limit, Math.Min(limit, v[i] + delta));
            Set(EditWeapon, EditClip, EditCrouched, v); Save();
        }
        /// <summary>Clears one arm's five offsets while retaining aim, head and the other arm.</summary>
        internal static void ResetArm(bool left)
        {
            if (!CanEditArms) return;
            var v = (float[])Get(EditWeapon, EditClip, EditCrouched).Clone();
            Array.Clear(v, 3 + (left ? 0 : 5), 5);
            Set(EditWeapon, EditClip, EditCrouched, v); Save();
        }
    }
}
