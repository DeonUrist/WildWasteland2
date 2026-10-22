using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (split from PlayerBody/PlayerBodyModule.cs in Wave A): vehicle controls - the Controls-screen "Ignition"
    /// row and key, cassette / volume keys, the Car and steering patches, the "Ignition control" job. Target: Vehicles (B12).
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        internal static ConfigEntry<bool> IgnitionControlOn;
        internal static ConfigEntry<Key> CassetteKey, VolumeDownKey, VolumeUpKey;
        internal static KeyCode CassetteKeyCode { get { return KeyMap.Code(CassetteKey.Value); } }
        internal static KeyCode VolumeDownKeyCode { get { return KeyMap.Code(VolumeDownKey.Value); } }
        internal static KeyCode VolumeUpKeyCode { get { return KeyMap.Code(VolumeUpKey.Value); } }
        // mirror of the game's Controls "Ignition" key (IgnitionControl), for the moment before the row exists (was a never-saved entry)
        internal static KeyCode IgnitionKeyCode = KeyCode.I;
        internal static Job IgnitionJob;

        /// <summary>Binds [PlayerBody / Vehicles] IgnitionKey and its live reaction.</summary>
        partial void BindVehicles(ModuleConfig cfg)
        {
            IgnitionControlOn = cfg.Player("Vehicles", "IgnitionKey", true, "Adds an \"Ignition\" row to the game's Controls screen (default I): each press performs one ignition/start panel click (ignition on, crank, then stop on standard cars).");
            IgnitionControlOn.SettingChanged += (s, e) => { if (Running && IgnitionJob != null) IgnitionJob.RunNow(); };
        }

        /// <summary>Binds the [Keys] cassette start/stop and volume keys.</summary>
        partial void BindVehiclesKeys(ModuleConfig cfg)
        {
            CassetteKey = cfg.Hotkey("Cassette start/stop", Key.Z, "Driving: start / stop the cassette player. Starting at zero volume sets 0.5. None = no hotkey.");
            VolumeDownKey = cfg.Hotkey("Cassette volume down", Key.Minus, "Driving: cassette volume -0.1 (also keypad minus). None = no hotkey.");
            VolumeUpKey = cfg.Hotkey("Cassette volume up", Key.Equals, "Driving: cassette volume +0.1 (+ on the main keyboard, also keypad plus). None = no hotkey.");
        }

        /// <summary>Init: ignition row keys (ignition-key.txt, default I), Car MousePickEvent patch, steering patch, "Ignition control" job.</summary>
        partial void InitVehicles()
        {
            IgnitionControl.Load(KeyCode.I);   // (D12) the old Apocaplayer [VEHICLE]/[General] IgnitionKey fallback is gone: I
            Car.Patch(this);
            WheelArms.Patch(this);
            IgnitionJob = Scheduler.Interval(this, "Ignition control", 0.5f, IgnitionControl.Tick);
        }

        /// <summary>Shutdown: the job reference dropped and the Controls-screen row removed.</summary>
        partial void ShutdownVehicles()
        {
            IgnitionJob = null;
            try { IgnitionControl.Remove(); } catch (Exception e) { Warn("ignition row: " + e.Message); }
        }
    }
}
