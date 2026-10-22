using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ES3Internal;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // =============================================================== who writes VehicleController.input
    // The car's DriveTrigger/INPUT FSMs (INPUT_AxisInput, INPUT_AxisSteering, ...) copy the legacy axes into input.Throttle/Brakes/
    // Clutch/Steering every frame, player inside or not. While the mod drives, they are disabled; Release() restores them
    // (single-state FSMs, so PlayMaker's restart-on-enable is harmless).
    internal class InputControl
    {
        private static readonly string[] Names = { "INPUT_AxisInput", "INPUT_AxisSteering", "INPUT_NormalizedAxisInput", "INPUT_MouseSteering",
            "Bool_AutomaticGearbox", "Bool_AutoClutch", "Bool_HShifter", "INPUT_HShifter", "INPUT_ShiftUpDown", "INPUT_GearChange" };
        private readonly List<PlayMakerFSM> _taken = new List<PlayMakerFSM>();
        private readonly GameObject _car;
        private bool _autoWas;
        private bool _owns, _swapWas, _holdGearWas;
        private NWH.VehiclePhysics2.VehicleController _vc;
        private NWH.VehiclePhysics2.Powertrain.TransmissionComponent.TransmissionShiftType _shiftWas;
        private NWH.VehiclePhysics2.Powertrain.TransmissionComponent.AutomaticTransmissionDNRShiftType _dnrWas;
        private NWH.VehiclePhysics2.Powertrain.ClutchComponent.ClutchControlType _clutchWas;

        internal InputControl(GameObject car) { _car = car; }

        internal bool Taken { get { return _owns; } }

        internal void Take()
        {
            if (_owns) return;
            _owns = true;
            _taken.Clear();
            Nwh.ForgetInput(_car);
            foreach (var f in _car.GetComponentsInChildren<PlayMakerFSM>(true))
                if (f.gameObject.name == "INPUT" && f.enabled && Array.IndexOf(Names, f.FsmName) >= 0) { f.enabled = false; _taken.Add(f); }
            _autoWas = Nwh.AutoInput(_car, false);
            _vc = _car.GetComponent<NWH.VehiclePhysics2.VehicleController>();
            if (_vc != null)
            {
                var transmission = _vc.powertrain.transmission; var clutch = _vc.powertrain.clutch;
                _shiftWas = transmission.transmissionType; _dnrWas = transmission.automaticTransmissionDNRShiftType;
                _holdGearWas = transmission.holdToKeepInGear; _clutchWas = clutch.controlType; _swapWas = _vc.input.swapInputInReverse;
                if (transmission.transmissionType == NWH.VehiclePhysics2.Powertrain.TransmissionComponent.TransmissionShiftType.Manual)
                    transmission.transmissionType = NWH.VehiclePhysics2.Powertrain.TransmissionComponent.TransmissionShiftType.Automatic;
                transmission.automaticTransmissionDNRShiftType = NWH.VehiclePhysics2.Powertrain.TransmissionComponent.AutomaticTransmissionDNRShiftType.RequireShiftInput;
                transmission.holdToKeepInGear = false;
                clutch.controlType = NWH.VehiclePhysics2.Powertrain.ClutchComponent.ClutchControlType.Automatic;
                _vc.input.swapInputInReverse = false; // Pilot explicitly selects R, then supplies throttle.
                PatrolsModule.Verbose("AI gearbox: " + _car.name + " " + _shiftWas + " -> " + transmission.transmissionType + "; native RPM shifting, automatic clutch");
            }
            PatrolsModule.Verbose("Input control taken: " + _taken.Count + " INPUT FSMs paused, autoSetInput was " + _autoWas);
        }

        internal void Release()
        {
            if (!_owns) return;
            _owns = false;
            if (_vc != null)
            {
                var transmission = _vc.powertrain.transmission;
                transmission.transmissionType = _shiftWas; transmission.automaticTransmissionDNRShiftType = _dnrWas;
                transmission.holdToKeepInGear = _holdGearWas; _vc.powertrain.clutch.controlType = _clutchWas; _vc.input.swapInputInReverse = _swapWas;
                _vc = null;
            }
            foreach (var f in _taken) if (f != null) f.enabled = true;
            _taken.Clear();
            if (_car != null) Nwh.AutoInput(_car, _autoWas);
            PatrolsModule.Verbose("Input control released");
        }
    }
}
