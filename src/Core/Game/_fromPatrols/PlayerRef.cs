using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // Cached references to the player and, while driving, the player's car. Refreshed lazily; reset on scene load.
    internal static class PlayerRef
    {
        private static Transform _player;
        private static float _nextFind;
        private static GameObject _playerCar;
        private static Rigidbody _playerCarRb;
        private static PlayMakerFSM _playerCarDrive;
        // Target() answers every pilot on the same physics step from one evaluation
        private static float _targetStamp = -1f;
        private static bool _targetOk;
        private static Vector3 _targetPos, _targetVel;
        private static GameObject _targetCar;
        private static readonly List<Component> _components = new List<Component>();   // non-allocating GetComponents buffer
        private static PlayMakerFSM _inCar;             // Player [InCar]: state InCar / OnFoot, var Car = the car the player sits in
        private static HutongGames.PlayMaker.FsmGameObject _inCarVar;
        private static Transform _inCarOwner;
        private static Vector3 _lastPos, _vel;
        private static float _lastStamp = -1f;

        internal static void Reset() { _player = null; _playerCar = null; _playerCarRb = null; _playerCarDrive = null; _inCar = null; _inCarVar = null; _inCarOwner = null; _nextFind = 0f; _lastStamp = -1f; _targetStamp = -1f; PatrolPoi.Reset(); }

        internal static void OriginShift(Vector3 shift)
        {
            _lastPos += shift;
            _targetPos += shift;
            _targetStamp = -1f; // never treat a coordinate jump as on-foot velocity
            PatrolPoi.Reset();
        }

        internal static Transform Player
        {
            get
            {
                if (_player == null && Time.unscaledTime >= _nextFind)
                {
                    _nextFind = Time.unscaledTime + 2f;
                    var go = Core.GameRefs.Player;
                    _player = go != null ? go.transform : null;
                }
                return _player;
            }
        }

        // The car the player is driving, or null. The Player's own InCar FSM says so (state InCar, variable Car); nothing is
        // scanned. Fallback while that FSM is not there yet: the Player object is parented under <car>/DriveTrigger/sitPos while driving.
        internal static GameObject PlayerCar
        {
            get
            {
                var p = Player;
                if (p == null) return SetCar(null);
                if (InCarFsm(p) && _inCar.Fsm.Initialized)
                {
                    if (_inCar.ActiveStateName != "InCar") return SetCar(null);
                    if (_inCarVar == null) _inCarVar = _inCar.FsmVariables.GetFsmGameObject("Car");
                    var car = _inCarVar != null ? _inCarVar.Value : null;
                    if (car != null) return SetCar(car);
                }
                if (_playerCar != null && _playerCarDrive != null && _playerCarDrive.Fsm.Initialized && _playerCarDrive.ActiveStateName == "inCar") return _playerCar;
                for (var a = p.parent; a != null; a = a.parent)
                    if (HasVehicleController(a)) return SetCar(a.gameObject);
                return SetCar(null);
            }
        }

        // the car's Drive FSM and Rigidbody are looked up once per car change, not per caller
        private static GameObject SetCar(GameObject car)
        {
            if (car == _playerCar) return _playerCar;
            _playerCar = car;
            _playerCarDrive = car != null ? Patrol.FindFsm(car, "DriveTrigger", "Drive") : null;
            _playerCarRb = car != null ? car.GetComponent<Rigidbody>() : null;
            return _playerCar;
        }

        private static bool InCarFsm(Transform p)
        {
            if (_inCar != null && _inCarOwner == p) return true;
            _inCar = null; _inCarVar = null; _inCarOwner = null;
            foreach (var f in p.GetComponents<PlayMakerFSM>()) if (f.FsmName == "InCar") { _inCar = f; break; }
            if (_inCar == null) return false;
            _inCarOwner = p;
            return true;
        }

        internal static bool HasVehicleController(Transform t)
        {
            _components.Clear();
            t.GetComponents(_components);
            for (int i = 0; i < _components.Count; i++)
            {
                var c = _components[i];
                if (c != null && c.GetType().Name == "VehicleController") return true;
            }
            return false;
        }

        // Position and velocity of what an AI car should ram: the player's car while driving, the player on foot otherwise.
        // Evaluated once per physics step however many pilots ask.
        // the player's GODMODE / GODMODEMovement FSM is in "active" (2.3.2: human turrets leave a god-mode player alone); re-read twice a second
        private static float _godStamp = -1f; private static bool _god;
        internal static bool GodMode
        {
            get
            {
                if (Time.unscaledTime - _godStamp < 0.5f) return _god;
                _godStamp = Time.unscaledTime; _god = false;
                var p = Player; if (p == null) return false;
                _components.Clear(); p.GetComponents(typeof(PlayMakerFSM), _components);
                foreach (var c in _components)
                {
                    var f = c as PlayMakerFSM;
                    if (f != null && (f.FsmName == "GODMODE" || f.FsmName == "GODMODEMovement") && f.enabled && f.Fsm.Initialized && f.ActiveStateName == "active") { _god = true; break; }
                }
                return _god;
            }
        }

        internal static bool Target(out Vector3 pos, out Vector3 vel, out GameObject car)
        {
            if (_targetStamp != Time.fixedTime)
            {
                _targetStamp = Time.fixedTime;
                _targetOk = TargetNow(out _targetPos, out _targetVel, out _targetCar);
            }
            pos = _targetPos; vel = _targetVel; car = _targetCar;
            return _targetOk;
        }

        private static bool TargetNow(out Vector3 pos, out Vector3 vel, out GameObject car)
        {
            car = PlayerCar;
            var p = Player;
            if (car != null)
            {
                var rb = _playerCarRb;
                pos = rb != null ? rb.worldCenterOfMass : car.transform.position;
                vel = rb != null ? rb.velocity : Vector3.zero;
                return true;
            }
            if (p == null) { pos = Vector3.zero; vel = Vector3.zero; return false; }
            pos = p.position;
            // on foot: velocity from the position delta, once per physics step (several pilots may ask)
            if (_lastStamp != Time.fixedTime)
            {
                float dt = _lastStamp < 0f ? 0f : Time.fixedTime - _lastStamp;
                if (dt > 0f && dt < 1f) { var v = (pos - _lastPos) / dt; _vel = Vector3.Lerp(_vel, v, 0.3f); }
                else _vel = Vector3.zero;
                _lastPos = pos; _lastStamp = Time.fixedTime;
            }
            vel = _vel;
            return true;
        }

        // 0 = neither, 1 = the player on foot, 2 = the car the player is driving
        internal static int Kind(Transform t)
        {
            if (t == null) return 0;
            var p = Player;
            var car = PlayerCar;
            if (car != null && t.IsChildOf(car.transform)) return 2;
            // the player object may not be parented under its car: anything the player is inside of counts
            if (p != null && car == null)
            {
                for (var a = p.parent; a != null; a = a.parent)
                    if (t.IsChildOf(a) && HasVehicleController(a)) return 2;
            }
            if (p != null && (t == p || t.IsChildOf(p))) return car != null ? 2 : 1;
            return 0;
        }
    }
}
