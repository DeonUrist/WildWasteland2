using System;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// BodyRunner part (split from PlayerBody/BodyRunner.cs in Wave A): the module's one IMGUI job (weapon pose editor hint, vehicle
    /// hints/status, steering-wheel editor hint). Target: HUD (B10); the editor hints go to Developer in Wave B.
    /// </summary>
    internal static partial class BodyRunner
    {
        // the same conditions GunPose.OnGUI / Car.OnGUI draw under (an editor hint, or seated in a car)
        internal static bool GuiWanted() { return !string.IsNullOrEmpty(GunPose.HintText) || Car.GuiWanted || WheelArms.EditorVisible; }
        internal static void Gui() { try { GunPose.OnGUI(); Car.OnGUI(); WheelArms.DrawEditorHint(); } catch (Exception) { } }
    }
}
