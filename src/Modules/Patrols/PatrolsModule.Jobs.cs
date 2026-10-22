using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Owns named scheduler boundaries for patrol progression, discovery, traffic and combat publication.</summary>
    public sealed partial class PatrolsModule
    {
        /// <summary>Advances live patrol maintenance after the module has started.</summary>
        private void TickPatrol() { if (_patrol != null) _patrol.Tick(); }
        /// <summary>Refreshes distance and boss progression without per-frame editor work.</summary>
        private void TickConvoy() { if (_convoy != null) _convoy.Tick(); }
        /// <summary>Advances virtual journeys and physical representations at the existing quarter-second cadence.</summary>
        private void TickTraffic() { if (_traffic != null) _traffic.Tick(); }
        /// <summary>Walks the existing bounded location-discovery slice only during active traffic gameplay.</summary>
        private void TickLocations() { if (TrafficReady()) TrafficLocations.Tick(); }
        /// <summary>Checks camp residents independently so profiler totals identify camp work.</summary>
        private void TickCamps() { if (TrafficReady()) TrafficCamps.Tick(); }
        /// <summary>Publishes road worker results independently of journey simulation.</summary>
        private void TickRoads() { if (TrafficReady() && TrafficRoadRouting.Value) TrafficRoads.Tick(); }
        /// <summary>Retains loading, pause, restoration and traffic-enable gates for the split jobs.</summary>
        private bool TrafficReady() { return _traffic != null && _patrol != null && _patrol.InGame() && ConvoyEnabled.Value && !SaveBus.Busy && !PatrolPersistence.Restoring; }
        /// <summary>Checks abandoned-car eligibility through the existing internally paced cleanup.</summary>
        private void TickCleanup() { if (_cleanup != null) _cleanup.Tick(); }
        /// <summary>Draws the traffic overlay when its normal visibility predicate admits it.</summary>
        private void DrawTraffic() { if (_traffic != null) _traffic.Gui(); }
        /// <summary>Stops IMGUI dispatch while traffic has no requested overlay.</summary>
        private bool TrafficGuiWanted() { return _traffic != null && _traffic.GuiWanted(); }
        /// <summary>Notifies Saves of active vehicle combat at four hertz without a cross-module polling delegate.</summary>
        private void PublishCombatActivity()
        {
            if (!Running || !GameRefs.InGame || !VehicleCombatActive()) return;
            var activity = new CombatActivity { Kind = CombatActivityKind.VehicleCombat, Actor = EntityId.None,
                WorldPos = GameRefs.Player != null ? Origin.World(GameRefs.Player.transform.position) : UnityEngine.Vector3.zero };
            ModEvents.Publish(ref activity);
        }
    }
}
