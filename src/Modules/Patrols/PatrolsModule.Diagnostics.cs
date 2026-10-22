using System.Collections.Generic;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrol-private diagnostic data extraction; Debug owns controls and screen rendering.</summary>
    public sealed partial class PatrolsModule
    {
        /// <summary>Registers the real live data selector alongside the module's other Core APIs.</summary>
        private void RegisterDiagnostics() { ModApi.Provide<IPatrolDiagnosticSource>(this, new PatrolDiagnosticSource()); }
    }
    /// <summary>Copies strings only while a user requests the overlay or status panel.</summary>
    internal sealed class PatrolDiagnosticSource : IPatrolDiagnosticSource
    {
        /// <summary>Current driving/raider counts plus the existing convoy status text.</summary>
        public string Status { get { return "Driving AI cars: " + Pilot.All.Count + "; raider cars: " + PatrolMarker.All.Count + "; " + Convoy.OverlayLine(); } }
        /// <summary>Preserves pilot order and blank slots so screen label positions remain the same.</summary>
        public void CollectOverlayLines(List<string> into) { if (PatrolsModule.Running) Patrol.CollectDiagnosticLines(into); }
    }
}
