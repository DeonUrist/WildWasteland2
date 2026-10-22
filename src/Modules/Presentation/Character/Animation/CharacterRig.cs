using System;
using UnityEngine;
using WildWasteland.Modules.PlayerBody;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// Rig helpers shared by the two Mixamo drivers (the player's Body/Loco and the ModAPI character, README 6.1 / 7 "two systems for
    /// one job"): the upper-body avatar mask, bone turns in a character frame, the facing of a bone pair and the two-bone arm lift.
    /// Both drivers had their own identical copies; the maths is unchanged.
    /// </summary>
    internal static class CharacterRig
    {
        static AvatarMask _upper;

        /// <summary>The shared upper-body avatar mask (body, head, arms, fingers, hand IK), created once and kept loaded.</summary>
        internal static AvatarMask UpperMask()
        {
            if (_upper != null) return _upper;
            _upper = new AvatarMask { hideFlags = HideFlags.DontUnloadUnusedAsset };
            foreach (AvatarMaskBodyPart part in Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                if (part == AvatarMaskBodyPart.LastBodyPart) continue;
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                          || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                _upper.SetHumanoidBodyPartActive(part, upper);
            }
            return _upper;
        }

        /// <summary>Rotation of <paramref name="t"/> in the space of <paramref name="frame"/> (product of the local rotations up to it).</summary>
        internal static Quaternion SpaceOf(Transform t, Transform frame)
        {
            var q = Quaternion.identity;
            for (var x = t; x != null && x != frame; x = x.parent) q = x.localRotation * q;
            return q;
        }

        /// <summary>Turns a bone by <paramref name="deg"/> about an axis given in the character frame's space (local rotations only, mirror-safe).</summary>
        internal static void Turn(Transform t, Transform frame, Vector3 axis, float deg)
        {
            if (t == null) return;
            var q = SpaceOf(t, frame);
            t.localRotation = t.localRotation * Quaternion.Inverse(q) * Quaternion.AngleAxis(deg, axis) * q;
        }

        /// <summary>Facing of the line a-b (pointing to the character's right) about the frame's up axis, degrees (+ = right).</summary>
        internal static float YawIn(Transform frame, Transform a, Transform b)
        {
            var s = frame.InverseTransformDirection(b.position - a.position); s.y = 0f;
            var f = Vector3.Cross(s, Vector3.up);
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        /// <summary>Moves the hand <paramref name="h"/> by <paramref name="d"/> with a two-bone elbow solve (shoulder s, elbow e), keeping the hand's rotation.</summary>
        internal static void LiftArm(Transform s, Transform e, Transform h, Vector3 d)
        {
            var hr = h.rotation;
            var t = h.position + d;
            Vector3 sp = s.position, ep = e.position, hp = h.position; double nx, ny, nz;   // (WW perf) no double[] per call
            AimLift.Elbow(sp.x, sp.y, sp.z, ep.x, ep.y, ep.z, hp.x, hp.y, hp.z, t.x, t.y, t.z, out nx, out ny, out nz);
            var E = new Vector3((float)nx, (float)ny, (float)nz);
            s.rotation = Quaternion.FromToRotation(e.position - s.position, E - s.position) * s.rotation;
            e.rotation = Quaternion.FromToRotation(h.position - e.position, t - e.position) * e.rotation;
            h.rotation = hr;
        }
    }
}
