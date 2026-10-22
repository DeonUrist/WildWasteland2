using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// Config entries of the player's driver's seat (first-person driving view offsets, camera forward shift, seat drop). Bound by
    /// <see cref="PresentationModule"/> (World part) in tools.cfg <c>[Presentation / Car seat]</c>; captured defaults of
    /// <c>[PlayerBody / Car seat]</c> are used as defaults (D12: no value migration).
    /// </summary>
    internal static class CarSeatSettings
    {
        internal const string Section = "Car seat";
        const string Old = "PlayerBody / Car seat";
        /// <summary>The driver's-seat view offset Denis set in game (1.5.3: X -0.03, Y 0.02, Z 0.02): the built-in base; the F7 values add to it.</summary>
        internal static readonly Vector3 CarViewBase = new Vector3(-0.03f, 0.02f, 0.02f);
        internal static ConfigEntry<float> FpCarCamX, FpCarCamY, FpCarCamZ, CarCameraForward, SeatDrop;

        /// <summary>Binds the first-person driving offsets (clamped sliders), the camera forward shift and the seat drop.</summary>
        internal static void Bind(ModuleConfig cfg)
        {
            FpCarCamX = Slider(cfg, "FirstPersonDrivingX"); FpCarCamY = Slider(cfg, "FirstPersonDrivingY"); FpCarCamZ = Slider(cfg, "FirstPersonDrivingZ");
            CarCameraForward = PresentationModule.WorldTool(cfg, Section, Old, "CarCameraForward", 0.12f, "Driving in first person with the body shown: the view is drawn this far (m) in front of the game's eye.");
            SeatDrop = PresentationModule.WorldTool(cfg, Section, Old, "SeatDrop", 0.05f, "Driver's seat: the body sits this much (m) lower than the game's seated man (his pose put her a bit high for the steering wheel).");
        }

        /// <summary>One first-person view offset slider (-0.3..0.3 m), an out-of-range stored value clamped back (copy of PlayerBodyModule.Slider, A2-8).</summary>
        static ConfigEntry<float> Slider(ModuleConfig cfg, string key)
        {
            var e = PresentationModule.WorldTool(cfg, Section, Old, key, 0f, "First-person view offset (m, view right / up / forward): the body moves the opposite way under the camera.", new AcceptableValueRange<float>(-0.3f, 0.3f));
            if (e.Value < -0.3f || e.Value > 0.3f) e.Value = Mathf.Clamp(e.Value, -0.3f, 0.3f);
            return e;
        }
    }
}
