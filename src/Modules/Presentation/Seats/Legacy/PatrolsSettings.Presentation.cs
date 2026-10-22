using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Seated-occupant and turret pose settings (Pose, Pose.&lt;Human&gt;, Turret pose) and their config classes; Wave A split of PatrolsSettings.cs.</summary>
    public sealed partial class PatrolsModule
    {
        internal static ConfigEntry<float> TurretThigh, TurretKnee, TurretHipHeight;
        internal static ConfigEntry<string> RiderLanceOffset, RiderLanceRotation;
        internal static PoseFloat DriverOffsetX, DriverOffsetY, DriverOffsetZ;
        internal static PoseBool PoseEnabled;
        internal static PoseFloat PoseThigh, PoseKnee, PoseArm, PoseElbow, PoseLeftLegCloser, PoseRightLegCloser, PoseLeftArmCloser, PoseRightArmCloser, PoseBikeArm, PoseBikeArmSpread;
        internal static readonly Dictionary<string, ArmPoseProfile> HumanArmPoses = new Dictionary<string, ArmPoseProfile>(StringComparer.OrdinalIgnoreCase);
        internal static readonly string[] HumanTypes = { "Boltjaw", "Flexa", "Lugnut", "Scrud", "Sprokka", "Scraffa", "Spanna" };

        /// <summary>Binds the Turret pose entries (crouched gunner pose and the Warboy lance prop).</summary>
        partial void BindPresentationTurret(ModuleConfig cfg)
        {
            // turret rider pose (crouched gunner + the Warboy's lance prop)
            TurretThigh = cfg.Tool(TTurret, "TurretThigh", 80f, "Crouched human turret gunner: thigh angle forward, degrees", new AcceptableValueRange<float>(0f, 150f));
            TurretKnee = cfg.Tool(TTurret, "TurretKnee", 130f, "Crouched human turret gunner: knee bend, degrees", new AcceptableValueRange<float>(0f, 170f));
            TurretHipHeight = cfg.Tool(TTurret, "TurretHipHeight", 0.4f, "Crouched human turret gunner: hips above the spot (feet), m", new AcceptableValueRange<float>(0f, 1.2f));
            RiderLanceOffset = cfg.Tool(TTurret, "RiderLanceOffset", "0, 0, 0", "Warboy's lance prop: position in the throwing hand, hand-local metres x, y, z");
            RiderLanceRotation = cfg.Tool(TTurret, "RiderLanceRotation", "0, 0, 0", "Warboy's lance prop: rotation in the throwing hand, degrees x, y, z");
        }

        // ---------------------------------------------------------------- F7 poses (always editable; the PoseConfiguration gate is gone)
        /// <summary>Binds the seated-occupant pose entries (Patrols / Pose, Patrols / Pose.&lt;Human&gt;) and builds the per-human arm pose profiles.</summary>
        partial void BindPoses(ModuleConfig cfg)
        {
            var offsetRange = new AcceptableValueRange<float>(-2f, 2f);
            DriverOffsetX = PoseFloat.Create(cfg, TPose, "OffsetX", 0f, "Where each occupant's hips go: offset from its seat anchor, right (m)", offsetRange);
            DriverOffsetY = PoseFloat.Create(cfg, TPose, "OffsetY", -0.3f, "Where each occupant's hips go: offset from its seat anchor, up (m)", offsetRange);
            DriverOffsetZ = PoseFloat.Create(cfg, TPose, "OffsetZ", 0f, "Where each occupant's hips go: offset from its seat anchor, forward (m)", offsetRange);
            PoseEnabled = PoseBool.Create(cfg, TPose, "Enabled", true, "Seated pose for live occupants");
            var bodyRange = new AcceptableValueRange<float>(0f, 130f);
            var closerRange = new AcceptableValueRange<float>(-60f, 60f);
            PoseThigh = PoseFloat.Create(cfg, TPose, "ThighAngle", 85f, "Thigh angle", bodyRange);
            PoseKnee = PoseFloat.Create(cfg, TPose, "KneeAngle", 30f, "Knee angle", bodyRange);
            PoseArm = PoseFloat.Create(cfg, TPose, "ArmAngle", 65f, "Fallback arm angle", bodyRange);
            PoseLeftLegCloser = PoseFloat.Create(cfg, TPose, "LeftLegCloser", 10f, "Left leg inward angle", closerRange);
            PoseRightLegCloser = PoseFloat.Create(cfg, TPose, "RightLegCloser", 10f, "Right leg inward angle", closerRange);
            PoseLeftArmCloser = PoseFloat.Create(cfg, TPose, "LeftArmCloser", 10f, "Fallback left arm inward angle", closerRange);
            PoseRightArmCloser = PoseFloat.Create(cfg, TPose, "RightArmCloser", 10f, "Fallback right arm inward angle", closerRange);
            PoseBikeArm = PoseFloat.Create(cfg, TPose, "BikeArmAngle", 55f, "Motorcycle riders (Motorcycle, Halfbreed): arm angle down to the handlebar", bodyRange);
            PoseBikeArmSpread = PoseFloat.Create(cfg, TPose, "BikeArmSpread", 22f, "Motorcycle riders: each arm this far outward, hands on the handlebar grips", closerRange);
            PoseElbow = PoseFloat.Create(cfg, TPose, "ElbowAngle", 25f, "Fallback elbow angle", bodyRange);

            HumanArmPoses.Clear();
            foreach (var human in HumanTypes)
            {
                var profile = ArmPoseProfile.Create(cfg, human, Array.IndexOf(PassengerGuard.RangedHumans, human) >= 0);
                HumanArmPoses[human] = profile;
            }
        }
    }

    internal class BonePoseConfig
    {
        internal PoseFloat Vertical, Horizontal, Rotation;

        internal static BonePoseConfig Create(ModuleConfig config, string section, string bone, float vertical, float horizontal, float twist)
        {
            var range = new AcceptableValueRange<float>(-180f, 180f);
            return new BonePoseConfig
            {
                Vertical = PoseFloat.Create(config, section, bone + "Vertical", vertical, bone + " vertical angle: positive swings forward/up (degrees)", range),
                Horizontal = PoseFloat.Create(config, section, bone + "Horizontal", horizontal, bone + " horizontal angle: positive turns toward the occupant's right (degrees)", range),
                Rotation = PoseFloat.Create(config, section, bone + "Rotation", twist, bone + " axial rotation around the limb/joint (degrees)", range)
            };
        }
    }

    // WW: always backed by an F7 tools.cfg entry (the old in-memory default path for "PoseConfiguration = false" is gone)
    internal class PoseFloat
    {
        internal readonly ConfigEntry<float> Entry;
        internal float Value { get { return Entry.Value; } }
        private PoseFloat(ConfigEntry<float> entry) { Entry = entry; }
        internal static PoseFloat Create(ModuleConfig config, string sub, string key, float value, string description, AcceptableValueRange<float> range)
        {
            return new PoseFloat(config.Tool(sub, key, value, description, range));
        }
    }

    internal class PoseBool
    {
        internal readonly ConfigEntry<bool> Entry;
        internal bool Value { get { return Entry.Value; } }
        private PoseBool(ConfigEntry<bool> entry) { Entry = entry; }
        internal static PoseBool Create(ModuleConfig config, string sub, string key, bool value, string description)
        {
            return new PoseBool(config.Tool(sub, key, value, description));
        }
    }

    internal class ArmPoseProfile
    {
        internal BonePoseConfig LeftArm, LeftElbow, LeftHand, RightArm, RightElbow, RightHand;
        internal WeaponPoseConfig Weapon;

        private static readonly Dictionary<string, float[]> Defaults = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Boltjaw", new[] { 80f,35f,-15.6f, 25f,0f,0f, 0f,0f,0f, -20f,50f,-20f, 125f,0f,25f, 0f,25f,-25f, 0f,0f,0f,-15f,20f,0f } },
            { "Flexa", new[] { 80f,35f,-15.652f, 25f,0f,0f, 0f,0f,0f, 5f,-10f,0f, 120f,0f,0f, 0f,25f,-45f, 0f,0f,0f,-15f,20f,0f } },
            { "Lugnut", new[] { 85f,34f,0f, 0f,0f,0f, 5f,15f,-10f, 80f,60f,20f, 20f,-140f,0f, -40f,50f,0f, 0f,-0.1f,0f,-15f,0f,0f } },
            { "Scrud", new[] { 78f,20f,30f, 25f,0f,0f, 0f,0f,0f, 75f,-20f,-30f, 25f,0f,0f, 0f,0f,0f, 0f,0f,0f,-25f,35f,0f } },
            { "Sprokka", new[] { 105f,10f,0f, 0f,0f,0f, 0f,0f,0f, 15f,-27f,-30f, 25f,0f,0f, 0f,0f,0f, 0f,0f,0.03f,0f,15f,0f } },
            { "Scraffa", new[] { 65f,10f,0f, 25f,0f,0f, 0f,0f,0f, 65f,-10f,0f, 25f,0f,0f, 0f,0f,0f } },
            { "Spanna", new[] { 65f,10f,0f, 25f,0f,0f, 0f,0f,0f, 65f,-10f,0f, 25f,0f,0f, 0f,0f,0f } }
        };

        // F7 section "Patrols / Pose.<Human>" (the old cfg section was [Pose.<Human>])
        internal static ArmPoseProfile Create(ModuleConfig config, string human, bool ranged)
        {
            string section = "Pose." + human;
            float[] d = Defaults[human];
            return new ArmPoseProfile
            {
                LeftArm = BonePoseConfig.Create(config, section, "LeftArm", d[0], d[1], d[2]),
                LeftElbow = BonePoseConfig.Create(config, section, "LeftElbow", d[3], d[4], d[5]),
                LeftHand = BonePoseConfig.Create(config, section, "LeftHand", d[6], d[7], d[8]),
                RightArm = BonePoseConfig.Create(config, section, "RightArm", d[9], d[10], d[11]),
                RightElbow = BonePoseConfig.Create(config, section, "RightElbow", d[12], d[13], d[14]),
                RightHand = BonePoseConfig.Create(config, section, "RightHand", d[15], d[16], d[17]),
                Weapon = ranged ? WeaponPoseConfig.Create(config, section, d) : null
            };
        }
    }

    internal class WeaponPoseConfig
    {
        internal PoseFloat PositionX, PositionY, PositionZ;
        internal PoseFloat RotationX, RotationY, RotationZ;

        internal static WeaponPoseConfig Create(ModuleConfig config, string section, float[] d)
        {
            var positionRange = new AcceptableValueRange<float>(-2f, 2f);
            var rotationRange = new AcceptableValueRange<float>(-180f, 180f);
            return new WeaponPoseConfig
            {
                PositionX = PoseFloat.Create(config, section, "WeaponPositionX", d[18], "Weapon local position along the hand's X axis (m)", positionRange),
                PositionY = PoseFloat.Create(config, section, "WeaponPositionY", d[19], "Weapon local position along the hand's Y axis (m)", positionRange),
                PositionZ = PoseFloat.Create(config, section, "WeaponPositionZ", d[20], "Weapon local position along the hand's Z axis (m)", positionRange),
                RotationX = PoseFloat.Create(config, section, "WeaponRotationX", d[21], "Weapon local rotation around its X axis (degrees)", rotationRange),
                RotationY = PoseFloat.Create(config, section, "WeaponRotationY", d[22], "Weapon local rotation around its Y axis (degrees)", rotationRange),
                RotationZ = PoseFloat.Create(config, section, "WeaponRotationZ", d[23], "Weapon local rotation around its Z axis (degrees)", rotationRange)
            };
        }
    }
}
