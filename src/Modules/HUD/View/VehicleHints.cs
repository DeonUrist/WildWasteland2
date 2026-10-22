using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>Owns cockpit hint and fallback status drawing using only the Vehicles presentation protocol.</summary>
    internal static class VehicleHints
    {
        private static GUIStyle _hintStyle;
        /// <summary>Returns whether the cockpit currently has a visible presentation.</summary>
        internal static bool Wanted() { var source = ModApi.Get<IVehicleHudData>(); return source != null && source.Active; }
        /// <summary>Renders the original native-font controls hints and status icons while driving.</summary>
        internal static void Draw()
        {
            var source = ModApi.Get<IVehicleHudData>(); if (source == null || !source.Active) return;
            if (HudModule.VehicleStatusHint.Value) VehicleHud.Draw(source.IgnitionOff, source.HandbrakeOn, source.Playing, source.Volume);
            if (!HudModule.VehicleHotkeyHint.Value || string.IsNullOrEmpty(source.Hint)) return;
            if (_hintStyle == null) _hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, wordWrap = false };
            var style = _hintStyle; style.fontSize = source.FontSize; style.font = source.Font; style.fontStyle = source.FontStyle;
            var rect = new Rect(Screen.width * .025f, Screen.height * .62f, Screen.width * .5f, style.fontSize * 8f);
            style.normal.textColor = new Color(0f, 0f, 0f, .8f); GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), source.Hint, style);
            style.normal.textColor = source.HintColor; GUI.Label(rect, source.Hint, style);
        }
    }
}
