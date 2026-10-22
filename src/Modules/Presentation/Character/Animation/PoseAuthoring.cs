using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>One named pose set served through <see cref="PoseAuthoring"/> (the World part registers e.g. "WheelGrips").</summary>
    internal interface IPoseSetSource
    {
        /// <summary>Keys of the set (weapon keys, vehicle keys...).</summary>
        void Keys(List<string> into);
        /// <summary>Pose of one key/slot.</summary>
        bool TryGetPose(string key, string slot, out Vector3 localPosition, out Quaternion localRotation);
        /// <summary>Sets a pose live (not saved yet).</summary>
        void SetPose(string key, string slot, Vector3 localPosition, Quaternion localRotation);
        /// <summary>Writes the set to disk.</summary>
        bool Save();
        /// <summary>Reloads the set from disk.</summary>
        void Revert();
    }

    /// <summary>
    /// IPoseAuthoring provider (Developer editors, README 6.11): the "WeaponPoses" set (weapon model under the right hand per clip:
    /// key = weapon key, slot = clip name; position in metres, rotation as stored) and the "AimLift" set (key = weapon key, slot = aim
    /// clip, "@crouch" suffix for crouched; localPosition = (lift cm, head degrees, forward cm), rotation unused = identity). Other parts
    /// of the module add sets with <see cref="Register"/> (B06: steering-wheel grips).
    /// </summary>
    internal sealed class PoseAuthoring : IPoseAuthoring
    {
        internal const string WeaponPoses = "WeaponPoses", AimLiftSet = "AimLift";
        static readonly List<string> _names = new List<string> { WeaponPoses, AimLiftSet };
        static readonly Dictionary<string, IPoseSetSource> _extra = new Dictionary<string, IPoseSetSource>(StringComparer.Ordinal);

        /// <summary>Adds (or replaces) a named pose set served by another part of the Presentation module.</summary>
        internal static void Register(string set, IPoseSetSource source)
        {
            if (string.IsNullOrEmpty(set) || source == null) return;
            if (!_names.Contains(set)) _names.Add(set);
            _extra[set] = source;
        }

        /// <summary>Pose set names.</summary>
        public IList<string> Sets { get { return _names.AsReadOnly(); } }

        /// <summary>Keys within a set (weapon keys for the two character sets).</summary>
        public void Keys(string set, List<string> into)
        {
            if (into == null) return;
            into.Clear();
            IPoseSetSource src;
            if (set == WeaponPoses) GunPose.Weapons(into);
            else if (set == AimLiftSet) AimLift.Weapons(into);
            else if (set != null && _extra.TryGetValue(set, out src)) src.Keys(into);
            into.Sort(StringComparer.Ordinal);
        }

        /// <summary>Pose of one key/slot (see the class summary for the units of each set).</summary>
        public bool TryGetPose(string set, string key, string slot, out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = Vector3.zero; localRotation = Quaternion.identity;
            IPoseSetSource src;
            if (set == WeaponPoses)
            {
                var v = GunPose.Own(key, slot);
                if (v == null) return false;
                localPosition = new Vector3(v[0], v[1], v[2]) * 0.01f; localRotation = Quaternion.Euler(v[3], v[4], v[5]);
                return true;
            }
            if (set == AimLiftSet)
            {
                bool crouched; string clip = Split(slot, out crouched);
                if (key == null || clip == null) return false;
                var v = AimLift.Get(key, clip, crouched);
                if (v == null) return false;
                localPosition = new Vector3(v[0], v[1], v[2]);
                return true;
            }
            return set != null && _extra.TryGetValue(set, out src) && src.TryGetPose(key, slot, out localPosition, out localRotation);
        }

        /// <summary>Sets a pose live (written by <see cref="Save"/>).</summary>
        public void SetPose(string set, string key, string slot, Vector3 localPosition, Quaternion localRotation)
        {
            IPoseSetSource src;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(slot)) return;
            if (set == WeaponPoses) { GunPose.PutUser(key, slot, GunPose.Abs(localPosition, localRotation)); return; }
            if (set == AimLiftSet)
            {
                bool crouched; string clip = Split(slot, out crouched);
                var v = (float[])AimLift.Get(key, clip, crouched).Clone();
                v[0] = localPosition.x; v[1] = localPosition.y; v[2] = localPosition.z;
                AimLift.Put(key, AimLift.Key(clip, crouched), v);
                return;
            }
            if (set != null && _extra.TryGetValue(set, out src)) src.SetPose(key, slot, localPosition, localRotation);
        }

        /// <summary>Writes the set to disk (weapon-poses.txt / aim-lift.txt in config/WildWasteland/Presentation).</summary>
        public bool Save(string set)
        {
            IPoseSetSource src;
            try
            {
                if (set == WeaponPoses) { GunPose.SaveUser(); return true; }
                if (set == AimLiftSet) { AimLift.Save(); return true; }
                return set != null && _extra.TryGetValue(set, out src) && src.Save();
            }
            catch (Exception e) { Log.Warn(PresentationModule.Instance, "pose set " + set + ": " + e.Message); return false; }
        }

        /// <summary>Reloads the set from disk, dropping live edits.</summary>
        public void Revert(string set)
        {
            IPoseSetSource src;
            if (set == WeaponPoses) GunPose.Bind();
            else if (set == AimLiftSet) AimLift.Load(PresentationModule.DataDir);
            else if (set != null && _extra.TryGetValue(set, out src)) src.Revert();
        }

        /// <summary>"RifleCrouchAim@crouch" -> clip "RifleCrouchAim", crouched true.</summary>
        static string Split(string slot, out bool crouched)
        {
            crouched = slot != null && slot.EndsWith("@crouch", StringComparison.Ordinal);
            return crouched ? slot.Substring(0, slot.Length - 7) : slot;
        }
    }
}
