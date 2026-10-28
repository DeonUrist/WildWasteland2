using System;
using System.Reflection;
using NWH.Common.Vehicles;
using NWH.VehiclePhysics2.GroundDetection;
using UnityEngine;

namespace WildWasteland.Core
{
    internal static partial class VistasWorldGen
    {
        private static Action<WheelUAPI, int> _releaseSurface;
        private static GameObject _surfacePlayerCar;
        private static int _surfacePlayerFrame = -1;

        private static void InstallPlayerSurface(Assembly assembly)
        {
            var surface = assembly.GetType("ApocalpyterWorldAmp.LooseSurfacePatch", true);
            _releaseSurface = (Action<WheelUAPI, int>)Delegate.CreateDelegate(typeof(Action<WheelUAPI, int>),
                Find(surface, "Release", typeof(void), typeof(WheelUAPI), typeof(int)));
            Hook(Find(surface, "Postfix", typeof(void), typeof(GroundDetection), typeof(WheelUAPI),
                typeof(int).MakeByRefType(), typeof(SurfacePreset).MakeByRefType()), prefix: "PlayerSurfaceOnly");
            Log.Main(_owner, "Vistas surface physics: player-driven vehicle only; other vehicles retain native NWH surfaces.");
        }

        private static bool PlayerSurfaceOnly(GroundDetection __0, WheelUAPI __1, int __2)
        {
            if (_surfacePlayerFrame != Time.frameCount)
            {
                _surfacePlayerFrame = Time.frameCount;
                _surfacePlayerCar = PlayerState.Car;
            }
            if (__0 != null && __0.vehicleController != null && _surfacePlayerCar != null
                && __0.vehicleController.gameObject == _surfacePlayerCar) return true;
            // Restore a previous player's fallback tyre override before native surface handling continues.
            if (__1 != null) _releaseSurface(__1, __2);
            return false;
        }
    }
}
