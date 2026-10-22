using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// The player input adapter's view of the game's player (B05): presentation-only rig references (hand-use arms, grenade FSM) plus
    /// [Obsolete] forwarders of the shared native player state to Core PlayerState. The type and member names stay because the ChaseCamera
    /// mod binds "Apocaplayer.Game" through the Compatibility adapter (B17-8); Presentation code itself uses PlayerState.
    /// </summary>
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

        /// <summary>Shared native enter/exit state machine.</summary>

        /// <summary>Shared native movement state machine.</summary>

        /// <summary>Shared native camera holder.</summary>

        /// <summary>Shared native first-person camera transform.</summary>

        /// <summary>Shared native first-person camera component.</summary>

        /// <summary>Shared native first-person weapon models parent.</summary>

        /// <summary>Grenade attack state machine of this presentation rig.</summary>
        public static PlayMakerFSM GrenadeFsm { get { ResolvePresentationRig(); return _grenadeFsm; } }
        /// <summary>Refreshes presentation-specific arms only when the shared native camera changes.</summary>
        private static void ResolvePresentationRig()
        {
            var camera = Core.PlayerState.PlayerCamera;
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

        /// <summary>Whether the native player is seated.</summary>

        /// <summary>Shared movement state name.</summary>

        /// <summary>Shared native standing capsule height.</summary>

        /// <summary>Shared native player velocity.</summary>

        /// <summary>Whether the first-person camera hierarchy is rendering.</summary>
        public static bool FirstPersonCameraOn { get { return Core.PlayerState.Camera != null && Core.PlayerState.Camera.isActiveAndEnabled; } }
        private static PlayMakerFSM _kickFsm;
        private static readonly Dictionary<string, PlayMakerFSM[]> _reloadFsms = new Dictionary<string, PlayMakerFSM[]>();

        /// <summary>Shared native jump state name.</summary>

        /// <summary>Whether native simulation is paused.</summary>

        /// <summary>DriveTrigger state machine of the shared seated vehicle.</summary>

        /// <summary>Shared ancestor carrying the seated vehicle DriveTrigger.</summary>

        /// <summary>Shared native first-person enter/exit interaction state machine.</summary>


        /// <summary>Builds the complete slash-separated transform hierarchy path.</summary>
        public static string PathOf(Transform t)
        {
            if (t == null) return "";
            string p = t.name;
            for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }

        /// <summary>Finds a named transform recursively in the existing child order.</summary>
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
