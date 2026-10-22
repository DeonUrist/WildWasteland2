using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Patrols;
namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Owns native driving access and reversible input leases.</summary>
    internal sealed class VehicleDriveApi : IVehicleDrive
    {
        /// <summary>Dispatches the typed operation to the actual native driving implementation.</summary>
        public bool EngineRunning(GameObject car) { return Nwh.EngineRunning(car); }
        /// <summary>Dispatches the typed operation to the actual native driving implementation.</summary>
        public void StartEngine(GameObject car) { Nwh.StartEngine(car); }
        /// <summary>Dispatches the typed operation to the actual native driving implementation.</summary>
        public void StopEngine(GameObject car) { Nwh.StopEngine(car); }
        /// <summary>Dispatches the typed operation to the actual native driving implementation.</summary>
        public bool AutoInput(GameObject car, bool on) { return Nwh.AutoInput(car, on); }
        /// <summary>Dispatches the typed operation to the actual native driving implementation.</summary>
        public int GearIndex(GameObject car) { return Nwh.GearIndex(car); }
        /// <summary>Dispatches the typed operation to the actual native driving implementation.</summary>
        public void ShiftInto(GameObject car, int gear) { Nwh.ShiftInto(car, gear); }
        /// <summary>Dispatches the typed operation to the actual native driving implementation.</summary>
        public string Diag(GameObject car) { return Nwh.Diag(car); }
        /// <summary>Dispatches the typed operation to the actual native driving implementation.</summary>
        public string Gear(GameObject car) { return Nwh.Gear(car); }
        /// <summary>Dispatches the typed operation to the actual native driving implementation.</summary>
        public float MaxSteerAngle(GameObject car) { return Nwh.MaxSteerAngle(car); }
        /// <summary>Dispatches the typed operation to the actual native driving implementation.</summary>
        public void SetHandbrake(GameObject car, float value) { Nwh.SetHandbrake(car, value); }
        /// <summary>Dispatches the typed operation to the actual native driving implementation.</summary>
        public void SetInput(GameObject car, float throttle, float steering, float brakes) { Nwh.SetInput(car, throttle, steering, brakes); }
        /// <summary>Dispatches the typed operation to the actual native driving implementation.</summary>
        public void ForgetInput(GameObject car) { Nwh.ForgetInput(car); }
        /// <summary>Restores every held input lease before Vehicles withdraws its API.</summary>
        internal static void ReleaseAll() { InputControl.ReleaseAll(); }
        /// <summary>Creates a native lease without taking control until explicitly requested.</summary>
        public IVehicleInputLease InputLease(GameObject car) { return new InputControl(car); }
    }
}