using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ES3Internal;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// Who writes VehicleController.input: the car's DriveTrigger/INPUT FSMs (INPUT_AxisInput, INPUT_AxisSteering, ...) copy the legacy axes
    /// into input.Throttle/Brakes/Clutch/Steering every frame, player inside or not. While the mod drives, they are disabled and the gearbox
    /// is set to automatic with an automatic clutch; Release() restores everything (single-state FSMs: PlayMaker's restart-on-enable is harmless).
    /// </summary>
    internal class InputControl : IVehicleInputLease
    {
        private static readonly HashSet<InputControl> Active=new HashSet<InputControl>();
        /// <summary>Restores only actually held native control leases.</summary>
        internal static void ReleaseAll() { foreach(var lease in new List<InputControl>(Active))lease.Release();Active.Clear(); }
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

        /// <summary>Input control for one car (nothing taken yet).</summary>
        internal InputControl(GameObject car) { _car = car; }

        /// <summary>True while the mod owns the car's input.</summary>
        public bool Taken { get { return _owns; } }

        /// <summary>Pauses the INPUT FSMs, turns off auto input, and switches the gearbox to automatic with RequireShiftInput and an automatic clutch.</summary>
        public void Take()
        {
            if (_owns) return;
            _owns = true; Active.Add(this);
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
                if (Log.L2) Log.Deep(VehiclesModule.Instance, "AI gearbox: " + _car.name + " " + _shiftWas + " -> " + transmission.transmissionType + "; native RPM shifting, automatic clutch");
            }
            if (Log.L2) Log.Deep(VehiclesModule.Instance, "Input control taken: " + _taken.Count + " INPUT FSMs paused, autoSetInput was " + _autoWas);
        }

        /// <summary>Restores the gearbox settings, the INPUT FSMs and auto input.</summary>
        public void Release()
        {
            if (!_owns) return;
            _owns = false; Active.Remove(this);
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
            Log.Deep(VehiclesModule.Instance, "Input control released");
        }
    }
}
