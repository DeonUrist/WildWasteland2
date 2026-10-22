using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (split from PlayerBody/PlayerBodyModule.cs in Wave A): the body in the driver's seat - first-person
    /// driving view offsets, seat drop, the steering-wheel arms job. Target: Presentation seat poses (B06).
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        // the driver's-seat view offset Denis set in game (1.5.3: X -0.03, Y 0.02, Z 0.02) is the built-in base; the F7 values add to it
        internal static readonly Vector3 CarViewBase = new Vector3(-0.03f, 0.02f, 0.02f);

        // ------------------------------------------------------------------ F7 (tools.cfg)
        internal static ConfigEntry<float> FpCarCamX, FpCarCamY, FpCarCamZ, CarCameraForward, SeatDrop;

        /// <summary>Binds [PlayerBody / Car seat] (tools.cfg): first-person driving offsets, camera forward shift, seat drop.</summary>
        partial void BindSeatsTools(ModuleConfig cfg)
        {
            const string Seat = "Car seat";
            FpCarCamX = Slider(cfg, Seat, "FirstPersonDrivingX"); FpCarCamY = Slider(cfg, Seat, "FirstPersonDrivingY"); FpCarCamZ = Slider(cfg, Seat, "FirstPersonDrivingZ");
            CarCameraForward = cfg.Tool(Seat, "CarCameraForward", 0.12f, "Driving in first person with the body shown: the view is drawn this far (m) in front of the game's eye.");
            SeatDrop = cfg.Tool(Seat, "SeatDrop", 0.05f, "Driver's seat: the body sits this much (m) lower than the game's seated man (his pose put her a bit high for the steering wheel).");
        }

        /// <summary>Init: the steering-wheel arms job (LateUpdateLast, before the NPC traversal poses).</summary>
        partial void InitSeatsJobs()
        {
            Scheduler.EveryFrame(this, "PlayerBody.WheelArms", BodyRunner.WheelTick, Phase.LateUpdateLast);
        }
    }
}
