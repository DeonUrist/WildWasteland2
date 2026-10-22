using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyPages part (split from PlayerBody/PlayerBodyPages.cs in Wave A): the F7 "Camera" page (third person, dynamic crosshair,
    /// camera culling). Target: Presentation camera rig (B06).
    /// </summary>
    internal static partial class PlayerBodyPages
    {
        /// <summary>Adds this part's F7 pages to the PlayerBody page list (called by Create, in the old page order).</summary>
        static partial void AddCameraPages(PlayerBodyModule m, List<ToolPage> pages)
        {
            pages.Add(new ActionPage(m, G, "Camera", CameraPage));
        }

        // ------------------------------------------------------------------ Camera
        
        static void CameraPage(ToolLayout l)
        {
            l.Heading("State");
            l.Value("Third person", ThirdPerson.On ? (ThirdPerson.Peek ? "on (binoculars / scope: first-person view)" : "on") : "off");
            l.Value("Orbiting / aim zoom", YesNo(ThirdPerson.Orbiting) + " / " + YesNo(ThirdPerson.AimZoom));
            l.Value("Dynamic crosshair", ThirdPerson.HasCrosshair ? "at " + ThirdPerson.CrosshairScreen.ToString("F0") : "centred");
            l.Value("Camera culling", !PlayerBodyModule.OcclusionPrototype.Value ? "off" : Cave.Inside ? "paused (in a cave)" : OcclusionCutaway.Blockers + " blocker(s), " + OcclusionCutaway.Renders + " extra views so far");
            l.Heading("Settings");
            l.Toggle(PlayerBodyModule.ThirdPersonOnFoot);
            l.Toggle(PlayerBodyModule.ToggleMiddleMouse);
            l.Toggle(PlayerBodyModule.DynamicCrosshair);
            l.Toggle(PlayerBodyModule.PickAssistOn);
            l.Toggle(PlayerBodyModule.FirstPersonSwayOn);
            l.Toggle(PlayerBodyModule.SmoothAimDownSights);
            l.Toggle(PlayerBodyModule.OcclusionPrototype);
            l.Toggle(PlayerBodyModule.OcclusionInVehicle);
            l.Entry(PlayerBodyModule.ObserveKey);
        }
    }
}
