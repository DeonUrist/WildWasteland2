namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// Presentation part of Gunplay's WeaponTransition (B05): empty since Wave B. The lowering pose is drawn by the Presentation
    /// WeaponPoseService from the WeaponSwitchPhase event (jobs "Presentation.WeaponLowering" / "Presentation.WeaponLoweringRestore").
    /// Gunplay still registers job "Gunplay.WeaponTransitionPose" and calls Restore() (request B05 to the coordinator, B09-5: delete them).
    /// </summary>
    internal static partial class WeaponTransition
    {
        /// <summary>Empty (B05): the pose is undone by WeaponPoseService.Restore.</summary>
        private static void Restore() { }

        /// <summary>Empty (B05): the pose is drawn by WeaponPoseService.Pose.</summary>
        private static void Pose() { }
    }
}
