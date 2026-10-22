using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// Old Patrols entry part (Wave A split of PatrolsSettings.cs) for the seat / turret poses. Wave B (B06): the entries are bound by the
    /// Presentation module (<see cref="SeatPoseSettings"/>, sections <c>[Presentation / Seat pose ...]</c>, <c>[Presentation / Turret pose]</c>);
    /// the partial methods are empty and the old statics forward to the new entries until Wave C deletes them.
    /// </summary>
    public sealed partial class PatrolsModule
    {
        /// <summary>Old turret pose entry; forwards to <see cref="SeatPoseSettings.TurretThigh"/>.</summary>
        [Obsolete("refactor: use SeatPoseSettings (Presentation)")] internal static ConfigEntry<float> TurretThigh { get { return SeatPoseSettings.TurretThigh; } }
        /// <summary>Old turret pose entry; forwards to <see cref="SeatPoseSettings.TurretKnee"/>.</summary>
        [Obsolete("refactor: use SeatPoseSettings (Presentation)")] internal static ConfigEntry<float> TurretKnee { get { return SeatPoseSettings.TurretKnee; } }
        /// <summary>Old turret pose entry; forwards to <see cref="SeatPoseSettings.TurretHipHeight"/>.</summary>
        [Obsolete("refactor: use SeatPoseSettings (Presentation)")] internal static ConfigEntry<float> TurretHipHeight { get { return SeatPoseSettings.TurretHipHeight; } }
        /// <summary>Old lance prop entry; forwards to <see cref="SeatPoseSettings.RiderLanceOffset"/>.</summary>
        [Obsolete("refactor: use SeatPoseSettings (Presentation)")] internal static ConfigEntry<string> RiderLanceOffset { get { return SeatPoseSettings.RiderLanceOffset; } }
        /// <summary>Old lance prop entry; forwards to <see cref="SeatPoseSettings.RiderLanceRotation"/>.</summary>
        [Obsolete("refactor: use SeatPoseSettings (Presentation)")] internal static ConfigEntry<string> RiderLanceRotation { get { return SeatPoseSettings.RiderLanceRotation; } }
        /// <summary>Old seat offset entry (read by Patrol.cs); forwards to <see cref="SeatPoseSettings.DriverOffsetX"/>.</summary>
        [Obsolete("refactor: use ISeatPoses / SeatPoseSettings (Presentation)")] internal static PoseFloat DriverOffsetX { get { return SeatPoseSettings.DriverOffsetX; } }
        /// <summary>Old seat offset entry (read by Patrol.cs); forwards to <see cref="SeatPoseSettings.DriverOffsetY"/>.</summary>
        [Obsolete("refactor: use ISeatPoses / SeatPoseSettings (Presentation)")] internal static PoseFloat DriverOffsetY { get { return SeatPoseSettings.DriverOffsetY; } }
        /// <summary>Old seat offset entry (read by Patrol.cs); forwards to <see cref="SeatPoseSettings.DriverOffsetZ"/>.</summary>
        [Obsolete("refactor: use ISeatPoses / SeatPoseSettings (Presentation)")] internal static PoseFloat DriverOffsetZ { get { return SeatPoseSettings.DriverOffsetZ; } }
        /// <summary>Old seated-pose switch (read by Patrol.cs); forwards to <see cref="SeatPoseSettings.PoseEnabled"/>.</summary>
        [Obsolete("refactor: use ISeatPoses / SeatPoseSettings (Presentation)")] internal static PoseBool PoseEnabled { get { return SeatPoseSettings.PoseEnabled; } }
        /// <summary>Old pose entry; forwards to <see cref="SeatPoseSettings.PoseThigh"/>.</summary>
        [Obsolete("refactor: use SeatPoseSettings (Presentation)")] internal static PoseFloat PoseThigh { get { return SeatPoseSettings.PoseThigh; } }
        /// <summary>Old pose entry; forwards to <see cref="SeatPoseSettings.PoseKnee"/>.</summary>
        [Obsolete("refactor: use SeatPoseSettings (Presentation)")] internal static PoseFloat PoseKnee { get { return SeatPoseSettings.PoseKnee; } }
        /// <summary>Old pose entry; forwards to <see cref="SeatPoseSettings.PoseArm"/>.</summary>
        [Obsolete("refactor: use SeatPoseSettings (Presentation)")] internal static PoseFloat PoseArm { get { return SeatPoseSettings.PoseArm; } }
        /// <summary>Old pose entry; forwards to <see cref="SeatPoseSettings.PoseElbow"/>.</summary>
        [Obsolete("refactor: use SeatPoseSettings (Presentation)")] internal static PoseFloat PoseElbow { get { return SeatPoseSettings.PoseElbow; } }
        /// <summary>Old per-human arm profiles; forwards to <see cref="SeatPoseSettings.HumanArmPoses"/>.</summary>
        [Obsolete("refactor: use SeatPoseSettings (Presentation)")] internal static Dictionary<string, ArmPoseProfile> HumanArmPoses { get { return SeatPoseSettings.HumanArmPoses; } }
        /// <summary>Old human type list; forwards to <see cref="SeatPoseSettings.HumanTypes"/>.</summary>
        [Obsolete("refactor: use SeatPoseSettings (Presentation)")] internal static string[] HumanTypes { get { return SeatPoseSettings.HumanTypes; } }

        /// <summary>Old Patrols bind step for the turret pose entries; empty (Presentation binds <see cref="SeatPoseSettings"/>).</summary>
        partial void BindPresentationTurret(ModuleConfig cfg) { }

        /// <summary>Old Patrols bind step for the seat pose entries; empty (Presentation binds <see cref="SeatPoseSettings"/>).</summary>
        partial void BindPoses(ModuleConfig cfg) { }
    }
}
