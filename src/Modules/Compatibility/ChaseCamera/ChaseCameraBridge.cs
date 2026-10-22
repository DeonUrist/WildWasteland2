using WildWasteland.Core;
using WildWasteland.Modules.HUD;
using WildWasteland.Modules.PlayerBody;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// The ChaseCamera (local.apocalypter.chasecamera) bridge: the player-body type adapter (<see cref="ChaseCameraCompat"/>), the HUD
    /// readings observer (<see cref="ChaseCameraHud"/>) and the <see cref="IDrivingTelemetry"/> provider, under the
    /// Compatibility.ChaseCamera scope. Merges the former PlayerBody and CleanHUD parts (file map rows 177/370).
    /// </summary>
    internal static class ChaseCameraBridge
    {
        private static IModule _scope;
        private static Job _hudInstall;

        /// <summary>Starts the type adapter, schedules the HUD observer install and provides the telemetry query.</summary>
        internal static void Start(IModule scope)
        {
            _scope = scope;
            ChaseCameraHud.Clear();
            ChaseCameraCompat.Start(scope);
            ChaseCameraHud.Install(scope);
            _hudInstall = Scheduler.Interval(scope, "Compatibility.ChaseCamera.HudInstall", 1f, InstallHud);
            ModApi.Provide<IDrivingTelemetry>(scope, new ChaseCameraTelemetry());
            ModApi.Provide<IDrivingHud>(scope, new DrivingHudAdapter());
        }

        /// <summary>Retries the HUD observer until the mod's plugin instance exists (the HUD attach job used to retry it), then stops.</summary>
        private static void InstallHud()
        {
            ChaseCameraHud.Install(_scope);
            if (ChaseCameraHud.Attempted && _hudInstall != null) { _hudInstall.Remove(); _hudInstall = null; }
        }

        /// <summary>Withdraws the telemetry, unbinds the mod from the player body and clears the HUD observer.</summary>
        internal static void Shutdown()
        {
            if (_scope != null) { ModApi.Withdraw<IDrivingTelemetry>(_scope); ModApi.Withdraw<IDrivingHud>(_scope); }
            ChaseCameraHud.Clear(); ChaseCameraCompat.Clear();
            _hudInstall = null; _scope = null;
        }
    }
}
