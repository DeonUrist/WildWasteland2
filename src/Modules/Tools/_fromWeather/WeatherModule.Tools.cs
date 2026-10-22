using System.Collections.Generic;
using System.Globalization;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Weather
{
    /// <summary>
    /// Tools part of the Weather entry (Wave A split, owner B16): the F3 "Weather visual preview" page (palette, storm settings,
    /// preview spawn buttons); its diagnostic controls are drawn by the Debug part.
    /// </summary>
    public sealed partial class WeatherModule
    {
        /// <summary>Debug part: diagnostic controls drawn at the end of the preview page (WeatherModule.Debug.cs).</summary>
        partial void DrawDebugTools(ToolLayout l);

        /// <summary>Draws the Weather preview page: palette, live storm settings, active count and preview spawn/clear buttons.</summary>
        private void DrawTools(ToolLayout l)
        {
            WeatherPalette.Draw(l);
            l.Text("Native encounters are replaced by moving WW storms. Critical storms lift vehicles and apply vanilla damage in hurricane sand walls, with safe eyes and War Rig shelter protection. Manual critical storms form over 20-40 gameplay seconds and last ten minutes; manual storms are not saved. Natural storms retain native encounter/save bookkeeping. Up to four nearest storms are presented at once; manual previews reserve their slots.");
            l.Value("Active storms", _count.ToString(CultureInfo.InvariantCulture) + " / 4");
            l.Toggle(_mapAreas, "Storm areas on the map");
            l.Entry(_density); l.Entry(_width); l.Entry(_depth);
            UpdateLayout(); l.Value("Hurricanes per critical storm", _columnCount.ToString(CultureInfo.InvariantCulture));
            bool available = _running && GameRefs.InPlay;
            if (l.Button("Dust storm ahead", available)) ToolWindow.CloseThen(SpawnDust);
            if (l.Button("Critical dust storm preview ahead", available)) ToolWindow.CloseThen(SpawnHurricane);
            if (l.Button("Dust storm around player", available)) ToolWindow.CloseThen(SpawnHere);
            if (l.Button("Clear weather previews", _running && _count > 0)) Clear();
            DrawDebugTools(l);
        }
        /// <summary>Preview button: ordinary dust storm ahead of the player.</summary>
        private void SpawnDust() { Spawn(false, false); }
        /// <summary>Preview button: critical (hurricane) dust storm ahead of the player.</summary>
        private void SpawnHurricane() { Spawn(true, false); }
        /// <summary>Preview button: ordinary dust storm centred on the player.</summary>
        private void SpawnHere() { Spawn(false, true); }
        /// <summary>Returns the Weather preview page (Debug group, tool page).</summary>
        public override IEnumerable<ToolPage> ToolPages()
        {
            yield return new ActionPage(this, ToolGroups.Debug, "Weather visual preview", DrawTools) { IsTool = true };
        }
    }
}
