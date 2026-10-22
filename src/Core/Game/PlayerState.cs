using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Core
{
    /// <summary>
    /// Player queries shared by every module (README 4.2), superset of PlayerBody Game.cs (rig, InCar/Movement/Jump FSMs, Dead,
    /// MoveState, StandingHeight, Velocity, CarRoot, CarFsm, DriveUse), Patrols PlayerRef (Car from InCar.Car with the
    /// DriveTrigger/VehicleController fallbacks, GodMode, ram Target per physics step, Kind) and Quests Game.cs (Player
    /// transform, UnderPlayer). Weapon/arms/reload queries stay with Gunplay/Presentation.
    /// Every answer is cached (per frame, per physics step or per owning object) and nothing allocates after the first lookup;
    /// references come from GameRefs and are re-resolved at most once per second while missing and cleared on scene load.
    /// </summary>
    public static class PlayerState
    {
        static GameObject _player;
        static PlayMakerFSM _inCar, _movement, _jump, _health, _driveUse;
        static FsmGameObject _inCarVar;
        static FsmFloat _healthVar, _standingVar;
        static Transform _holder, _camera, _weaponsArm, _weaponsParent;
        static Camera _cam;
        static Rigidbody _rb;
        static float _nextFind;
        static bool _installed;
        static readonly List<Component> _components = new List<Component>();
        static readonly List<PlayMakerFSM> _fsms = new List<PlayMakerFSM>(8);

        /// <summary>Scene load: forget everything (GameRefs does the same for its own references).</summary>
        public static void Reset()
        {
            _player = null; _inCar = _movement = _jump = _health = _driveUse = null; _inCarVar = null; _healthVar = _standingVar = null;
            _holder = _camera = _weaponsArm = _weaponsParent = null; _cam = null; _rb = null; _nextFind = 0f;
            _car = null; _carRb = null; _carDrive = null; _deadFrame = -1; _carRootFrame = -1; _godStamp = -1f; _targetStamp = -1f; _lastStamp = -1f;
        }

        /// <summary>Resolves the player object, its FSMs and the camera rig (throttled to once per second while incomplete).</summary>
        static void Resolve()
        {
            if (!_installed) { _installed = true; SceneManager.sceneLoaded += SceneLoaded; }
            if (_player != null && _inCar != null && _movement != null && _jump != null && _health != null && _camera != null && _cam != null) return;
            if (Time.unscaledTime < _nextFind) return;
            _nextFind = Time.unscaledTime + 1f;
            if (_player == null || _inCar == null || _movement == null || _jump == null || _health == null)
            {
                var go = GameRefs.Player;
                if (!ReferenceEquals(go, _player))
                {
                    _player = null; _inCar = _movement = _jump = _health = null; _inCarVar = null; _healthVar = _standingVar = null; _rb = null;
                    _deadFrame = -1; _carRootFrame = -1;
                }
                if (_health == null) _healthVar = null;
                if (_movement == null) _standingVar = null;
                if (go != null)
                {
                    _fsms.Clear(); go.GetComponents(_fsms);
                    for (int i = 0; i < _fsms.Count; i++)
                    {
                        var f = _fsms[i]; if (f == null) continue;
                        switch (f.FsmName)
                        {
                            case "InCar": if (_inCar == null) _inCar = f; break;
                            case "Movement": if (_movement == null) _movement = f; break;
                            case "Jump": if (_jump == null) _jump = f; break;
                            case "Health": if (_health == null) _health = f; break;
                        }
                    }
                    _fsms.Clear();
                    if (_inCar != null) { _player = go; _rb = go.GetComponent<Rigidbody>(); }
                }
            }
            if (_camera == null || _cam == null)
            {
                var holder = GameRefs.PlayerCameraHolder;
                if (holder != null)
                {
                    _holder = holder.transform;
                    _camera = _holder.Find("PlayerCamera");
                    _cam = _camera != null ? _camera.GetComponent<Camera>() : null;
                    _weaponsArm = _camera != null ? _camera.Find("WeaponsArm") : null;
                    _weaponsParent = _weaponsArm != null ? _weaponsArm.Find("Parent") : null;
                    _driveUse = _camera != null ? FsmUtil.Find(_camera.gameObject, "DriveUse") : null;
                }
            }
        }

        /// <summary>Scene-load handler.</summary>
        static void SceneLoaded(Scene scene, LoadSceneMode mode) { Reset(); }

        /// <summary>The "Player" root (capsule with InCar/Movement FSMs; parented under the car's sitPos while driving), or null.</summary>
        public static GameObject Player { get { Resolve(); return _player; } }
        /// <summary>The player transform (Quests Game.Player()), or null.</summary>
        public static Transform Transform { get { Resolve(); return _player != null ? _player.transform : null; } }
        /// <summary>PlayerCameraHolder transform.</summary>
        public static Transform CameraHolder { get { Resolve(); return _holder; } }
        /// <summary>PlayerCameraHolder/PlayerCamera transform.</summary>
        public static Transform PlayerCamera { get { Resolve(); return _camera; } }
        /// <summary>The first-person Camera component.</summary>
        public static Camera Camera { get { Resolve(); return _cam; } }
        /// <summary>Native first-person weapons arm, resolved with the camera rig.</summary>
        public static Transform WeaponsArm { get { Resolve(); return _weaponsArm; } }
        /// <summary>Native first-person weapon models parent.</summary>
        public static Transform WeaponsParent { get { Resolve(); return _weaponsParent; } }
        /// <summary>Player [InCar] FSM.</summary>
        public static PlayMakerFSM InCarFsm { get { Resolve(); return _inCar; } }
        /// <summary>Player [Movement] FSM.</summary>
        public static PlayMakerFSM MovementFsm { get { Resolve(); return _movement; } }
        /// <summary>PlayerCamera [DriveUse] FSM (the game's own enter/exit).</summary>
        public static PlayMakerFSM DriveUse { get { Resolve(); return _driveUse; } }

        /// <summary>Player, InCar FSM and PlayerCamera are all present (PlayerBody Game.Ready).</summary>
        public static bool Ready { get { Resolve(); return _player != null && _inCar != null && _camera != null; } }

        /// <summary>Whether native simulation time is stopped.</summary>
        public static bool Paused { get { return Time.timeScale < .01f; } }

        /// <summary>InCar FSM is in "InCar" (PlayerBody Game.InCar).</summary>
        public static bool InCar
        {
            get
            {
                Resolve();
                try { return _inCar != null && _inCar.ActiveStateName == "InCar"; }
                catch (Exception) { return false; }
            }
        }

        /// <summary>Whether the native InCar FSM is in any transitional or seated state other than OnFoot (save eligibility).</summary>
        public static bool InVehicleTransition { get { Resolve(); return _inCar != null && _inCar.ActiveStateName != "OnFoot"; } }

        /// <summary>Current player health, or zero until the native Health variable is available.</summary>
        public static float Health
        {
            get
            {
                Resolve();
                if (_health == null) return 0f;
                if (_healthVar == null) _healthVar = FsmIndex.Float(_health, "Health");
                return _healthVar != null ? _healthVar.Value : 0f;
            }
        }

        /// <summary>Movement FSM state: Standing / Running / Crouching / Crawling (+ checkAbove*); "" when unknown or disabled.</summary>
        public static string MoveState
        {
            get
            {
                Resolve();
                try { return _movement != null && _movement.enabled ? _movement.ActiveStateName : ""; }
                catch (Exception) { return ""; }
            }
        }

        /// <summary>Jump FSM state: Idle / Jump / Grounded / Falling; "" when unknown or disabled.</summary>
        public static string JumpState
        {
            get
            {
                Resolve();
                try { return _jump != null && _jump.enabled ? _jump.ActiveStateName : ""; }
                catch (Exception) { return ""; }
            }
        }

        /// <summary>Capsule height the Movement FSM tweens to (1.7 standing, 1.0 crouched, 0.56 crawling); 1.7 when unknown.</summary>
        public static float StandingHeight
        {
            get
            {
                Resolve();
                if (_movement == null) return 1.7f;
                if (_standingVar == null) _standingVar = _movement.FsmVariables.GetFsmFloat("standingHeight");
                return _standingVar != null && _standingVar.Value > 0.1f ? _standingVar.Value : 1.7f;
            }
        }

        /// <summary>Velocity of the player's Rigidbody (zero when none).</summary>
        public static Vector3 Velocity { get { Resolve(); return _rb != null ? _rb.velocity : Vector3.zero; } }

        static int _deadFrame = -1; static bool _dead;
        /// <summary>The game dies below 0.4 health, then spends five seconds in playerDeath (PlayerBody Game.Dead; answered once per frame).</summary>
        public static bool Dead
        {
            get
            {
                Resolve();
                if (_player == null) return false;
                if (_deadFrame == Time.frameCount) return _dead;
                _deadFrame = Time.frameCount;
                _dead = false;
                if (_health == null || _health.Fsm == null || !_health.Fsm.Initialized) return false;
                if (_healthVar == null) _healthVar = _health.FsmVariables.FindFsmFloat("Health");
                string st = _health.ActiveStateName;
                _dead = st == "playerDeath" || st == "backToMenu" || _healthVar != null && _healthVar.Value < 0.4f;
                return _dead;
            }
        }

        static float _godStamp = -1f; static bool _god;
        /// <summary>The player's GODMODE / GODMODEMovement FSM is in "active" (Patrols PlayerRef.GodMode; re-read twice a second).</summary>
        public static bool GodMode
        {
            get
            {
                if (Time.unscaledTime - _godStamp < 0.5f) return _god;
                _godStamp = Time.unscaledTime; _god = false;
                var p = Player; if (p == null) return false;
                _fsms.Clear(); p.GetComponents(_fsms);
                for (int i = 0; i < _fsms.Count; i++)
                {
                    var f = _fsms[i];
                    if (f != null && (f.FsmName == "GODMODE" || f.FsmName == "GODMODEMovement") && f.enabled && f.Fsm.Initialized && f.ActiveStateName == "active") { _god = true; break; }
                }
                _fsms.Clear();
                return _god;
            }
        }

        // ------------------------------------------------------------------ car

        static GameObject _car; static Rigidbody _carRb; static PlayMakerFSM _carDrive;

        /// <summary>
        /// The car the player drives, or null (Patrols PlayerRef.PlayerCar): InCar FSM state "InCar" + its Car variable; while
        /// that is not available, the last car whose DriveTrigger [Drive] is in "inCar", else the first ancestor with a
        /// VehicleController.
        /// </summary>
        public static GameObject Car
        {
            get
            {
                Resolve();
                if (_player == null) return SetCar(null);
                if (_inCar != null && _inCar.Fsm != null && _inCar.Fsm.Initialized)
                {
                    if (_inCar.ActiveStateName != "InCar") return SetCar(null);
                    if (_inCarVar == null) _inCarVar = _inCar.FsmVariables.GetFsmGameObject("Car");
                    var car = _inCarVar != null ? _inCarVar.Value : null;
                    if (car != null) return SetCar(car);
                }
                if (_car != null && _carDrive != null && _carDrive.Fsm.Initialized && _carDrive.ActiveStateName == "inCar") return _car;
                for (var a = _player.transform.parent; a != null; a = a.parent)
                    if (HasVehicleController(a)) return SetCar(a.gameObject);
                return SetCar(null);
            }
        }

        /// <summary>Rigidbody of <see cref="Car"/> (looked up once per car change).</summary>
        public static Rigidbody CarRigidbody { get { return Car != null ? _carRb : null; } }

        /// <summary>Records a car change and looks up its Drive FSM and Rigidbody once.</summary>
        static GameObject SetCar(GameObject car)
        {
            if (car == _car) return _car;
            _car = car;
            _carDrive = null;
            if (car != null)
            {
                // Patrol.FindFsm(car, "DriveTrigger", "Drive"): any descendant named DriveTrigger, inactive included (once per car change)
                _fsms.Clear(); car.GetComponentsInChildren(true, _fsms);
                for (int i = 0; i < _fsms.Count; i++)
                {
                    var f = _fsms[i];
                    if (f != null && f.FsmName == "Drive" && f.gameObject.name == "DriveTrigger") { _carDrive = f; break; }
                }
                _fsms.Clear();
            }
            _carRb = car != null ? car.GetComponent<Rigidbody>() : null;
            return _car;
        }

        /// <summary>True when t carries a component whose type is named VehicleController (NWH; by name, no type reference).</summary>
        public static bool HasVehicleController(Transform t)
        {
            if (t == null) return false;
            _components.Clear();
            t.GetComponents(_components);
            for (int i = 0; i < _components.Count; i++)
            {
                var c = _components[i];
                if (c != null && c.GetType().Name == "VehicleController") { _components.Clear(); return true; }
            }
            _components.Clear();
            return false;
        }

        static int _carRootFrame = -1; static Transform _carRoot;
        /// <summary>The ancestor of the player with a DriveTrigger child (PlayerBody Game.CarRoot; answered once per frame).</summary>
        public static Transform CarRoot
        {
            get
            {
                Resolve();
                if (_player == null) return null;
                if (_carRootFrame == Time.frameCount) return _carRoot;
                _carRootFrame = Time.frameCount; _carRoot = null;
                for (var t = _player.transform.parent; t != null; t = t.parent) if (t.Find("DriveTrigger") != null) { _carRoot = t; break; }
                return _carRoot;
            }
        }

        /// <summary>FSM named name on the DriveTrigger of the nearest ancestor that has one (PlayerBody Game.CarFsm).</summary>
        public static PlayMakerFSM CarFsm(string name)
        {
            Resolve();
            if (_player == null) return null;
            for (var t = _player.transform.parent; t != null; t = t.parent)
            {
                var dt = t.Find("DriveTrigger");
                if (dt == null) continue;
                return FsmUtil.Find(dt.gameObject, name);
            }
            return null;
        }

        // ------------------------------------------------------------------ targeting (Patrols)

        static float _targetStamp = -1f, _lastStamp = -1f;
        static bool _targetOk;
        static Vector3 _targetPos, _targetVel, _lastWorld, _vel;
        static GameObject _targetCar;

        /// <summary>
        /// What an AI car should ram (Patrols PlayerRef.Target): the player's car (centre of mass, velocity) while driving, the
        /// player on foot otherwise (velocity smoothed from the position delta per physics step). One evaluation per physics step.
        /// On-foot deltas are measured in Origin world space, so a floating-origin jump never reads as velocity (Patrols shifted
        /// its cached position in OnOriginShift instead; same result).
        /// </summary>
        public static bool Target(out Vector3 pos, out Vector3 vel, out GameObject car)
        {
            if (_targetStamp != Time.fixedTime)
            {
                _targetStamp = Time.fixedTime;
                _targetOk = TargetNow(out _targetPos, out _targetVel, out _targetCar);
            }
            pos = _targetPos; vel = _targetVel; car = _targetCar;
            return _targetOk;
        }

        /// <summary>One evaluation of <see cref="Target"/>.</summary>
        static bool TargetNow(out Vector3 pos, out Vector3 vel, out GameObject car)
        {
            car = Car;
            var p = Transform;
            if (car != null)
            {
                var rb = _carRb;
                pos = rb != null ? rb.worldCenterOfMass : car.transform.position;
                vel = rb != null ? rb.velocity : Vector3.zero;
                return true;
            }
            if (p == null) { pos = Vector3.zero; vel = Vector3.zero; return false; }
            pos = p.position;
            var world = Origin.World(pos);
            if (_lastStamp != Time.fixedTime)
            {
                float dt = _lastStamp < 0f ? 0f : Time.fixedTime - _lastStamp;
                if (dt > 0f && dt < 1f) { var v = (world - _lastWorld) / dt; _vel = Vector3.Lerp(_vel, v, 0.3f); }
                else _vel = Vector3.zero;
                _lastWorld = world; _lastStamp = Time.fixedTime;
            }
            vel = _vel;
            return true;
        }

        /// <summary>0 = neither, 1 = the player on foot (or part of it), 2 = the car the player is driving (Patrols PlayerRef.Kind).</summary>
        public static int Kind(Transform t)
        {
            if (t == null) return 0;
            var p = Transform;
            var car = Car;
            if (car != null && t.IsChildOf(car.transform)) return 2;
            if (p != null && car == null)
                for (var a = p.parent; a != null; a = a.parent)
                    if (t.IsChildOf(a) && HasVehicleController(a)) return 2;
            if (p != null && (t == p || t.IsChildOf(p))) return car != null ? 2 : 1;
            return 0;
        }

        /// <summary>True when t is the player rig or under it (an ancestor within 16 levels named Player, PlayerCamera or PlayerCameraHolder; Quests Game.UnderPlayer).</summary>
        public static bool UnderPlayer(Transform t)
        {
            for (int i = 0; t != null && i < 16; i++, t = t.parent)
            {
                string n = t.name;
                if (n == "PlayerCamera" || n == "PlayerCameraHolder" || n == "Player") return true;
            }
            return false;
        }
    }
}
