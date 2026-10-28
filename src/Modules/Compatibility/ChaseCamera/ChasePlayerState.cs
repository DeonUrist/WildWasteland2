using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Compatibility {
    /// <summary>Explicit Apocaplayer-shaped native player projection consumed by ChaseCamera 0.2.5 reflection.</summary>
    internal static class ChasePlayerState {
        internal static System.Func<string> Weapon;
        public static string DrawnWeapon=>Weapon();
        // The source bridge reads Cam as a field, after Ready resolves the current native rig.
        public static bool Ready { get { bool ready=PlayerState.Ready;Cam=PlayerState.Camera;return ready; } }
        public static GameObject Player=>PlayerState.Player;
        public static PlayMakerFSM MovementFsm=>PlayerState.MovementFsm;
        public static PlayMakerFSM InCarFsm=>PlayerState.InCarFsm;
        public static Transform CameraHolder=>PlayerState.CameraHolder;
        public static Transform PlayerCamera=>PlayerState.PlayerCamera;
        public static Camera Cam;
        public static Transform WeaponsParent=>PlayerState.WeaponsParent;
        public static bool Dead=>PlayerState.Dead;
        public static bool InCar=>PlayerState.InCar;
        public static string MoveState=>PlayerState.MoveState;
        public static float StandingHeight=>PlayerState.StandingHeight;
        public static Vector3 Velocity=>PlayerState.Velocity;
        public static string JumpState=>PlayerState.JumpState;
        public static bool Paused=>PlayerState.Paused;
        public static Transform CarRoot=>PlayerState.CarRoot;
        public static PlayMakerFSM DriveUse=>PlayerState.DriveUse;
        public static PlayMakerFSM CarFsm(string name)=>PlayerState.CarFsm(name);
    }
}
