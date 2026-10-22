using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    // References to the game's player objects, re-found after a scene load / when lost.
    internal static partial class Game
    {
        public static GameObject Player;            // root "Player" (capsule, Movement/InCar FSMs); parented under the car's sitPos while driving
        public static PlayMakerFSM InCarFsm, MovementFsm;
        private static PlayMakerFSM _healthFsm;
        public static Transform CameraHolder, PlayerCamera;
        public static Camera Cam;
        public static Transform WeaponsParent;
        public static PlayMakerFSM GrenadeFsm;      // PlayerCamera/QuickItems/grenade [Attack]: on -> (Throw Grenade) -> checkGrenade -> fire 0.4 s -> wait 0.35 s -> throw      // PlayerCamera/WeaponsArm/Parent: one child per first-person weapon, the drawn one active
        private static float _nextFind;
        private static Transform _handUse, _toolArm, _repairArm, _adjustArm;

        public static void Reset() { _handUse = _toolArm = _repairArm = _adjustArm = null; _drawnFrame = -1; _drawnParent = _drawnChild = null; _deadFrame = -1; _healthVar = null; _healthVarFor = null; _rb = null; _rbFor = null; _jumpFsm = _kickFsm = null; _reloadFsms.Clear(); _perRound.Clear(); _arms.Clear(); _attackFsms.Clear(); _adsFsm = null; GrenadeFsm = null; Player = null; InCarFsm = MovementFsm = null; CameraHolder = PlayerCamera = WeaponsParent = null; Cam = null; _nextFind = 0f; }

        public static bool Ready
        {
            get
            {
                if (Player != null && InCarFsm != null && PlayerCamera != null) return true;
                if (Time.unscaledTime < _nextFind) return false;
                _nextFind = Time.unscaledTime + 1f;
                Find();
                return Player != null && InCarFsm != null && PlayerCamera != null;
            }
        }

        // (WW) the Player / PlayerCameraHolder come from Core.GameRefs (cached, re-found after a scene load, at most once a second while
        // missing) instead of GameObject.Find; the rest is as before
        private static void Find()
        {
            if (Player == null || InCarFsm == null)
            {
                Player = null; InCarFsm = MovementFsm = null;
                var go = Core.GameRefs.Player;
                if (go != null)
                    foreach (var f in go.GetComponents<PlayMakerFSM>())
                    {
                        if (f.FsmName == "InCar") InCarFsm = f;
                        else if (f.FsmName == "Movement") MovementFsm = f;
                    }
                if (InCarFsm != null) { Player = go; PlayerBodyModule.Verbose("Found the Player"); }
            }
            if (PlayerCamera == null)
            {
                var holder = Core.GameRefs.PlayerCameraHolder;
                if (holder != null)
                {
                    CameraHolder = holder.transform;
                    PlayerCamera = CameraHolder.Find("PlayerCamera");
                    if (PlayerCamera != null)
                    {
                        _handUse = PlayerCamera.Find("HandItemUse");
                        _toolArm = FindDeep(PlayerCamera, "ToolArm"); _repairArm = FindDeep(PlayerCamera, "RepairArm"); _adjustArm = FindDeep(PlayerCamera, "AdjustArm");
                        Cam = PlayerCamera.GetComponent<Camera>();
                        var wa = PlayerCamera.Find("WeaponsArm");
                        WeaponsParent = wa != null ? wa.Find("Parent") : null;
                        var gr = PlayerCamera.Find("QuickItems/grenade");
                        if (gr != null) foreach (var f in gr.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Attack") GrenadeFsm = f;
                        PlayerBodyModule.Verbose("Found the PlayerCamera" + (GrenadeFsm != null ? " (+ grenade)" : "") + (WeaponsParent != null ? " and WeaponsArm/Parent" : " (no WeaponsArm/Parent!)"));
                    }
                }
            }
        }

        // The game dies below 0.4 health, then spends five seconds in playerDeath.
        // (WW perf) asked ~10 times a frame: the answer is kept for the frame, the Health variable per FSM (was FindFsmFloat by name per call)
        private static int _deadFrame = -1; private static bool _dead; private static GameObject _deadPlayer;
        private static FsmFloat _healthVar; private static PlayMakerFSM _healthVarFor;
        public static bool Dead
        {
            get
            {
                if (Player == null) return false;
                if (_deadFrame == Time.frameCount && ReferenceEquals(_deadPlayer, Player)) return _dead;
                _dead = DeadNow(); _deadFrame = Time.frameCount; _deadPlayer = Player;
                return _dead;
            }
        }
        private static bool DeadNow()
        {
            if (_healthFsm == null || _healthFsm.gameObject != Player)
            {
                _healthFsm = null;
                foreach (var f in Player.GetComponents<PlayMakerFSM>())
                    if (f.FsmName == "Health") { _healthFsm = f; break; }
            }
            if (_healthFsm == null || !_healthFsm.Fsm.Initialized) return false;
            if (!ReferenceEquals(_healthVarFor, _healthFsm) || _healthVar == null) { _healthVarFor = _healthFsm; _healthVar = _healthFsm.FsmVariables.FindFsmFloat("Health"); }
            var health = _healthVar;
            string st = _healthFsm.ActiveStateName;
            return st == "playerDeath" || st == "backToMenu"
                || health != null && health.Value < 0.4f;
        }

        public static bool InCar
        {
            get
            {
                try { return InCarFsm != null && InCarFsm.ActiveStateName == "InCar"; }
                catch (Exception) { return false; }
            }
        }

        // Standing / Running / Crouching / Crawling (+ checkAbove*)
        public static string MoveState
        {
            get
            {
                try { return MovementFsm != null && MovementFsm.enabled ? MovementFsm.ActiveStateName : ""; }
                catch (Exception) { return ""; }
            }
        }

        // the capsule height the Movement FSM tweens to (1.7 standing, 1.0 crouched, 0.56 crawling)
        public static float StandingHeight
        {
            get
            {
                if (MovementFsm == null) return 1.7f;
                var v = MovementFsm.FsmVariables.GetFsmFloat("standingHeight");
                return v != null && v.Value > 0.1f ? v.Value : 1.7f;
            }
        }

        // (WW) the Player's Rigidbody cached per Player object (was GetComponent on every call)
        private static Rigidbody _rb; private static GameObject _rbFor;
        public static Vector3 Velocity
        {
            get
            {
                if (Player == null) return Vector3.zero;
                if (!ReferenceEquals(_rbFor, Player) || _rb == null) { _rbFor = Player; _rb = Player.GetComponent<Rigidbody>(); }
                return _rb != null ? _rb.velocity : Vector3.zero;
            }
        }


        // the first-person camera is rendering (false while the car's third-person camera is on: the game deactivates the PlayerCamera hierarchy)
        public static bool FirstPersonCameraOn { get { return Cam != null && Cam.isActiveAndEnabled; } }

        private static PlayMakerFSM _jumpFsm, _kickFsm;
        private static readonly Dictionary<string, PlayMakerFSM[]> _reloadFsms = new Dictionary<string, PlayMakerFSM[]>();

        // Player [Jump]: Idle -> (Jump button) Jump -> Grounded -> Idle; Falling when walking off an edge
        public static string JumpState
        {
            get
            {
                try
                {
                    if (_jumpFsm == null && Player != null) foreach (var f in Player.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Jump") _jumpFsm = f;
                    return _jumpFsm != null && _jumpFsm.enabled ? _jumpFsm.ActiveStateName : "";
                }
                catch (Exception) { return ""; }
            }
        }


        public static bool Paused { get { return Time.timeScale < 0.01f; } }


        // the FSM <name> on the DriveTrigger of the car she sits in (the Player rides under the car's sitPos): Camera (1st / 3rd), Drive (exit)
        public static PlayMakerFSM CarFsm(string name)
        {
            if (Player == null) return null;
            for (var t = Player.transform.parent; t != null; t = t.parent)
            {
                var dt = t.Find("DriveTrigger");
                if (dt == null) continue;
                foreach (var f in dt.GetComponents<PlayMakerFSM>()) if (f.FsmName == name) return f;
                return null;
            }
            return null;
        }

        // the car she sits in (the ancestor of the Player with a DriveTrigger child)
        private static int _carRootFrame = -1; private static Transform _carRoot; private static GameObject _carRootPlayer;
        public static Transform CarRoot
        {
            get
            {
                if (Player == null) return null;
                if (_carRootFrame == Time.frameCount && _carRootPlayer == Player) return _carRoot;     // (2.2.6) asked many times a frame
                _carRootFrame = Time.frameCount; _carRootPlayer = Player; _carRoot = null;
                for (var t = Player.transform.parent; t != null; t = t.parent) if (t.Find("DriveTrigger") != null) { _carRoot = t; break; }
                return _carRoot;
            }
        }

        // PlayerCamera [DriveUse]: the game's own enter/exit (Use while looking at the car's trigger, first person only)
        public static PlayMakerFSM DriveUse
        {
            get
            {
                if (PlayerCamera == null) return null;
                foreach (var f in PlayerCamera.GetComponents<PlayMakerFSM>()) if (f.FsmName == "DriveUse") return f;
                return null;
            }
        }

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
