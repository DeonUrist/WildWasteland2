using WildWasteland.Core.UI;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>F7 "Camera" page of the Presentation World part: third-person state and the camera settings.</summary>
    internal static class CameraPage
    {
        /// <summary>Draws the state rows (third person, orbit / aim zoom, crosshair, culling) and the camera toggles.</summary>
        internal static void Draw(ToolLayout l)
        {
            l.Heading("State");
            l.Value("Third person", ThirdPerson.On ? (ThirdPerson.Peek ? "on (binoculars / scope: first-person view)" : "on") : "off");
            l.Value("Orbiting / aim zoom", YesNo(ThirdPerson.Orbiting) + " / " + YesNo(ThirdPerson.AimZoom));
            l.Value("Dynamic crosshair", ThirdPerson.HasCrosshair ? "at " + ThirdPerson.CrosshairScreen.ToString("F0") : "centred");
            l.Value("Camera culling", !CameraSettings.OcclusionPrototype.Value ? "off" : Cave.Inside ? "paused (in a cave)" : OcclusionCutaway.Blockers + " blocker(s), " + OcclusionCutaway.Renders + " extra views so far");
            l.Heading("Settings");
            l.Toggle(CameraSettings.ThirdPersonOnFoot);
            l.Toggle(CameraSettings.ToggleMiddleMouse);
            l.Toggle(CameraSettings.DynamicCrosshair);
            l.Toggle(CameraSettings.PickAssistOn);
            l.Toggle(PlayerBodyModule.FirstPersonSwayOn);
            l.Toggle(PlayerBodyModule.SmoothAimDownSights);
            l.Toggle(CameraSettings.OcclusionPrototype);
            l.Toggle(CameraSettings.OcclusionInVehicle);
            l.Entry(CameraSettings.ObserveKey);
        }

        /// <summary>"yes" / "no".</summary>
        static string YesNo(bool b) { return b ? "yes" : "no"; }
    }
}
