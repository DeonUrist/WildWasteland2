using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// Config entries of the crew seat poses (live occupants, per-human arm/weapon profiles, crouched turret gunners, the Warboy lance prop).
    /// Bound by <see cref="PresentationModule"/> (World part) in tools.cfg <c>[Presentation / Seat pose]</c>, <c>[Presentation / Seat pose.&lt;Human&gt;]</c>
    /// and <c>[Presentation / Turret pose]</c>; the captured defaults of the old <c>[Patrols / Pose ...]</c> sections are used as defaults (D12).
    /// </summary>
    internal static class SeatPoseSettings
    {
        internal const string PoseSection = "Seat pose", TurretSection = "Turret pose";
        internal const string OldPose = "Patrols / Pose", OldTurret = "Patrols / Turret pose";
        internal static ConfigEntry<float> TurretThigh, TurretKnee, TurretHipHeight;
        internal static ConfigEntry<string> RiderLanceOffset, RiderLanceRotation;
        internal static PoseFloat DriverOffsetX, DriverOffsetY, DriverOffsetZ;
        internal static PoseBool PoseEnabled;
        internal static PoseFloat PoseThigh, PoseKnee, PoseArm, PoseElbow, PoseLeftLegCloser, PoseRightLegCloser, PoseLeftArmCloser, PoseRightArmCloser, PoseBikeArm, PoseBikeArmSpread;
        internal static readonly Dictionary<string, ArmPoseProfile> HumanArmPoses = new Dictionary<string, ArmPoseProfile>(StringComparer.OrdinalIgnoreCase);
        internal static readonly string[] HumanTypes = { "Boltjaw", "Flexa", "Lugnut", "Scrud", "Sprokka", "Scraffa", "Spanna" };
        /// <summary>Humans whose profile carries a weapon pose (copy of PassengerGuard.RangedHumans, the NPCAI list of ranged crew humans).</summary>
        static readonly string[] RangedHumans = { "Boltjaw", "Flexa", "Lugnut", "Scrud", "Sprokka" };

        /// <summary>Binds the seat pose entries, the per-human profiles, then the turret pose entries (old order: Pose before Turret pose).</summary>
        internal static void Bind(ModuleConfig cfg)
        {
            BindPoses(cfg);
            BindTurret(cfg);
        }

        /// <summary>Binds the seated-occupant pose entries and builds the per-human arm pose profiles.</summary>
        static void BindPoses(ModuleConfig cfg)
        {
            var offsetRange = new AcceptableValueRange<float>(-2f, 2f);
            DriverOffsetX = PoseFloat.Create(cfg, PoseSection, OldPose, "OffsetX", 0f, "Where each occupant's hips go: offset from its seat anchor, right (m)", offsetRange);
            DriverOffsetY = PoseFloat.Create(cfg, PoseSection, OldPose, "OffsetY", -0.3f, "Where each occupant's hips go: offset from its seat anchor, up (m)", offsetRange);
            DriverOffsetZ = PoseFloat.Create(cfg, PoseSection, OldPose, "OffsetZ", 0f, "Where each occupant's hips go: offset from its seat anchor, forward (m)", offsetRange);
            PoseEnabled = PoseBool.Create(cfg, PoseSection, OldPose, "Enabled", true, "Seated pose for live occupants");
            var bodyRange = new AcceptableValueRange<float>(0f, 130f);
            var closerRange = new AcceptableValueRange<float>(-60f, 60f);
            PoseThigh = PoseFloat.Create(cfg, PoseSection, OldPose, "ThighAngle", 85f, "Thigh angle", bodyRange);
            PoseKnee = PoseFloat.Create(cfg, PoseSection, OldPose, "KneeAngle", 30f, "Knee angle", bodyRange);
            PoseArm = PoseFloat.Create(cfg, PoseSection, OldPose, "ArmAngle", 65f, "Fallback arm angle", bodyRange);
            PoseLeftLegCloser = PoseFloat.Create(cfg, PoseSection, OldPose, "LeftLegCloser", 10f, "Left leg inward angle", closerRange);
            PoseRightLegCloser = PoseFloat.Create(cfg, PoseSection, OldPose, "RightLegCloser", 10f, "Right leg inward angle", closerRange);
            PoseLeftArmCloser = PoseFloat.Create(cfg, PoseSection, OldPose, "LeftArmCloser", 10f, "Fallback left arm inward angle", closerRange);
            PoseRightArmCloser = PoseFloat.Create(cfg, PoseSection, OldPose, "RightArmCloser", 10f, "Fallback right arm inward angle", closerRange);
            PoseBikeArm = PoseFloat.Create(cfg, PoseSection, OldPose, "BikeArmAngle", 55f, "Motorcycle riders (Motorcycle, Halfbreed): arm angle down to the handlebar", bodyRange);
            PoseBikeArmSpread = PoseFloat.Create(cfg, PoseSection, OldPose, "BikeArmSpread", 22f, "Motorcycle riders: each arm this far outward, hands on the handlebar grips", closerRange);
            PoseElbow = PoseFloat.Create(cfg, PoseSection, OldPose, "ElbowAngle", 25f, "Fallback elbow angle", bodyRange);

            HumanArmPoses.Clear();
            foreach (var human in HumanTypes)
                HumanArmPoses[human] = ArmPoseProfile.Create(cfg, human, Array.IndexOf(RangedHumans, human) >= 0);
        }

        /// <summary>Binds the Turret pose entries (crouched gunner pose and the Warboy lance prop).</summary>
        static void BindTurret(ModuleConfig cfg)
        {
            TurretThigh = PresentationModule.WorldTool(cfg, TurretSection, OldTurret, "TurretThigh", 80f, "Crouched human turret gunner: thigh angle forward, degrees", new AcceptableValueRange<float>(0f, 150f));
            TurretKnee = PresentationModule.WorldTool(cfg, TurretSection, OldTurret, "TurretKnee", 130f, "Crouched human turret gunner: knee bend, degrees", new AcceptableValueRange<float>(0f, 170f));
            TurretHipHeight = PresentationModule.WorldTool(cfg, TurretSection, OldTurret, "TurretHipHeight", 0.4f, "Crouched human turret gunner: hips above the spot (feet), m", new AcceptableValueRange<float>(0f, 1.2f));
            RiderLanceOffset = PresentationModule.WorldTool(cfg, TurretSection, OldTurret, "RiderLanceOffset", "0, 0, 0", "Warboy's lance prop: position in the throwing hand, hand-local metres x, y, z");
            RiderLanceRotation = PresentationModule.WorldTool(cfg, TurretSection, OldTurret, "RiderLanceRotation", "0, 0, 0", "Warboy's lance prop: rotation in the throwing hand, degrees x, y, z");
        }
    }
}
