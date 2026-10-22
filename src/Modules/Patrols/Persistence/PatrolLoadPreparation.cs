namespace WildWasteland.Modules.Patrols
{
    /// <summary>Contributes patrol preparation to Core's native final-load handoff barrier.</summary>
    internal static class PatrolLoadPreparation
    {
        private static bool _loading;
        /// <summary>Registers readiness under the patrol module so Core withdraws it on shutdown.</summary>
        internal static void Install(Core.IModule owner)
        {
            Core.LoadingProgress.RegisterHold(owner, "Patrols", ReadyForHandoff);
        }
        /// <summary>Warms required vehicle assets during native loading and clears preparation state at handoff.</summary>
        internal static void StateChanged(string state)
        {
            if (state == "LoadGame" || state == "setSeed")
            { _loading = true; VehiclePreparation.WarmPark(); }
            if (_loading && state == "Wait for Terrain Gen")
            { Prefabs.Refresh(); VehiclePreparation.WarmPark();  }
            if (state == "isPlay")
            {
                if (_loading) Core.Log.Always("[Patrols] Load handoff ready: vehicle builds cached, saved patrol AI prepared; native vehicle/item deltas retained.");
                _loading = false;
            }
        }
        /// <summary>Allows handoff once vehicle warming and saved patrol restoration have completed.</summary>
        private static bool ReadyForHandoff()
        {
            if (!_loading || !PatrolsModule.Running) return true;
            return !VehiclePreparation.Warming && !PatrolPersistence.Restoring;
        }
        /// <summary>Clears pending work when a scene or patrol session ends.</summary>
        internal static void Reset() { _loading = false; }
    }
}
