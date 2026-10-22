using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MapMagic.Terrains;
using Den.Tools;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// POI dresser (editor) lifecycle and the Debug tool pages "Gas stations" / "POI dresser".
    /// Wave A (A6): entry split of Towns/TownRuntime.cs (Tools part, B16).
    /// </summary>
    internal sealed partial class TownRuntime
    {
        private PoiDresser _dresser;
        internal IEnumerable<ToolPage> ToolPages()
        { return new ToolPage[] { new ActionPage(Owner, ToolGroups.Debug, "Gas stations", DrawGasStation) { DebugOnly = true, IsTool = true },
            new ActionPage(Owner, ToolGroups.Debug, "POI dresser", DrawDresser) { DebugOnly = true, IsTool = true } }; }
        private void DrawDresser(ToolLayout l)
        { if (_dresser != null) _dresser.DrawPage(l, _settings); else l.Entry(_settings.PoiDresserKey); }
        private void DrawGasStation(ToolLayout l)
        {
            if (_gasStation != null) _gasStation.Draw(l);
            else { l.Entry(_settings.DesertGasStationChance); l.Entry(_settings.TestGasStation); l.Entry(_settings.GasStationScale); }
        }
        /// <summary>Creates the POI dresser on top of the dressing runtime (after PoiDressing.Install, as before).</summary>
        partial void InitTools() { _dresser = new PoiDresser(_dressing); _dresser.Install(Owner, _settings); }
        /// <summary>Drops the dresser reference (its jobs/hotkeys are removed by Core cleanup).</summary>
        partial void ShutdownTools() { _dresser = null; }
        /// <summary>Shifts the dresser camera/draft positions on a floating-origin jump.</summary>
        partial void ShiftTools(Vector3 delta) { if (_dresser != null) _dresser.Shift(delta); }
    }
}
