using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Typed native driving operations; Vehicles owns NWH controller and input caches.</summary>
    public interface IVehicleDrive
    {
        /// <summary>Reads engine state.</summary>
        bool EngineRunning(GameObject car);
        /// <summary>Starts the native engine.</summary>
        void StartEngine(GameObject car);
        /// <summary>Stops the native engine.</summary>
        void StopEngine(GameObject car);
        /// <summary>Sets automatic input ownership and returns the previous state.</summary>
        bool AutoInput(GameObject car, bool on);
        /// <summary>Reads the selected gear.</summary>
        int GearIndex(GameObject car);
        /// <summary>Requests the native gear.</summary>
        void ShiftInto(GameObject car, int gear);
        /// <summary>Reads bounded diagnostic state.</summary>
        string Diag(GameObject car);
        /// <summary>Reads gear display text.</summary>
        string Gear(GameObject car);
        /// <summary>Reads the actual maximum steering angle.</summary>
        float MaxSteerAngle(GameObject car);
        /// <summary>Writes handbrake input.</summary>
        void SetHandbrake(GameObject car, float value);
        /// <summary>Writes cached throttle, steering and brake inputs.</summary>
        void SetInput(GameObject car, float throttle, float steering, float brakes);
        /// <summary>Invalidates cached inputs before ownership changes.</summary>
        void ForgetInput(GameObject car);
        /// <summary>Creates a reversible native input and automatic gearbox lease.</summary>
        IVehicleInputLease InputLease(GameObject car);
    }
    /// <summary>Reversible driving ownership, restored before player handoff.</summary>
    public interface IVehicleInputLease { bool Taken { get; } void Take(); void Release(); }
}
