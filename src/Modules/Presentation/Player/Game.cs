using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    // References to the game's player objects, re-found after a scene load / when lost.
    [Obsolete("refactor: use Core.PlayerState for native player queries")]
    internal static partial class Game
    {
        /// <summary>Forgets presentation-only rig/weapon caches; Core resets the shared native player state.</summary>
        public static void Reset()
        {
            _handUse = _toolArm = _repairArm = _adjustArm = null; _drawnFrame = -1; _drawnParent = _drawnChild = null;
            _kickFsm = null; _reloadFsms.Clear(); _perRound.Clear(); _arms.Clear(); _attackFsms.Clear(); _adsFsm = null;
            _rigCamera = null; _grenadeFsm = null;
        }
        private static Transform _handUse, _toolArm, _repairArm, _adjustArm, _rigCamera;
        private static PlayMakerFSM _grenadeFsm;
        /// <summary>Shared native player capsule.</summary>
        public static GameObject Player { get { return Core.PlayerState.Player; } }
        /// <summary>Shared native enter/exit state machine.</summary>
        public static PlayMakerFSM InCarFsm { get { return Core.PlayerState.InCarFsm; } }
        /// <summary>Shared native movement state machine.</summary>
        public static PlayMakerFSM MovementFsm { get { return Core.PlayerState.MovementFsm; } }
        /// <summary>Shared native camera holder.</summary>
        public static Transform CameraHolder { get { return Core.PlayerState.CameraHolder; } }
        /// <summary>Shared native first-person camera transform.</summary>
        public static Transform PlayerCamera { get { return Core.PlayerState.PlayerCamera; } }
        /// <summary>Shared native first-person camera component.</summary>
        public static Camera Cam { get { return Core.PlayerState.Camera; } }
        /// <summary>Shared native first-person weapon models parent.</summary>
        public static Transform WeaponsParent { get { return Core.PlayerState.WeaponsParent; } }
        /// <summary>Grenade attack state machine of this presentation rig.</summary>
        public static PlayMakerFSM GrenadeFsm { get { ResolvePresentationRig(); return _grenadeFsm; } }
        /// <summary>Refreshes presentation-specific arms only when the shared native camera changes.</summary>
        private static void ResolvePresentationRig()
        {
            var camera = PlayerCamera;
            if (camera == _rigCamera) return;
            _rigCamera = camera; _handUse = _toolArm = _repairArm = _adjustArm = null; _grenadeFsm = null;
            if (camera == null) return;
            _handUse = camera.Find("HandItemUse");
            _toolArm = FindDeep(camera, "ToolArm"); _repairArm = FindDeep(camera, "RepairArm"); _adjustArm = FindDeep(camera, "AdjustArm");
            var grenade = camera.Find("QuickItems/grenade");
            _grenadeFsm = grenade != null ? Core.FsmUtil.Find(grenade.gameObject, "Attack") : null;
        }
        /// <summary>Shared readiness plus presentation arm references for existing body consumers.</summary>
        public static bool Ready { get { bool ready = Core.PlayerState.Ready; ResolvePresentationRig(); return ready; } }
        /// <summary>Shared native death state, evaluated once per frame.</summary>
        public static bool Dead { get { return Core.PlayerState.Dead; } }
        /// <summary>Whether the native player is seated.</summary>
        public static bool InCar { get { return Core.PlayerState.InCar; } }
        /// <summary>Shared movement state name.</summary>
        public static string MoveState { get { return Core.PlayerState.MoveState; } }
        /// <summary>Shared native standing capsule height.</summary>
        public static float StandingHeight { get { return Core.PlayerState.StandingHeight; } }
        /// <summary>Shared native player velocity.</summary>
        public static Vector3 Velocity { get { return Core.PlayerState.Velocity; } }
        /// <summary>Whether the first-person camera hierarchy is rendering.</summary>
        public static bool FirstPersonCameraOn { get { return Cam != null && Cam.isActiveAndEnabled; } }
        private static PlayMakerFSM _kickFsm;
        private static readonly Dictionary<string, PlayMakerFSM[]> _reloadFsms = new Dictionary<string, PlayMakerFSM[]>();

        /// <summary>Shared native jump state name.</summary>
        public static string JumpState { get { return Core.PlayerState.JumpState; } }
        /// <summary>Whether native simulation is paused.</summary>
        public static bool Paused { get { return Core.PlayerState.Paused; } }
        /// <summary>DriveTrigger state machine of the shared seated vehicle.</summary>
        public static PlayMakerFSM CarFsm(string name) { return Core.PlayerState.CarFsm(name); }
        /// <summary>Shared ancestor carrying the seated vehicle DriveTrigger.</summary>
        public static Transform CarRoot { get { return Core.PlayerState.CarRoot; } }
        /// <summary>Shared native first-person enter/exit interaction state machine.</summary>
        public static PlayMakerFSM DriveUse { get { return Core.PlayerState.DriveUse; } }

        public static string PathOf(Transform t)
        {
            if (t == null) return "";
            string p = t.name;
            for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
