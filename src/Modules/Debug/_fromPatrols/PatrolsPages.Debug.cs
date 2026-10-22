using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>F7 AI overlay and debug spawns page (Wave A split of PatrolsPages.cs).</summary>
    public sealed partial class PatrolsModule
    {
        void DebugPage(ToolLayout l)
        {
            l.Text("On-screen diagnostics and the editor's test spawns.");
            l.Toggle(AiOverlay, "AI overlay (one line per AI car, or the world traffic state)");
            if (Running)
            {
                l.Value("Driving AI cars", Pilot.All.Count.ToString());
                l.Value("Raider cars", PatrolMarker.All.Count.ToString());
                l.Value("Automatic raiders", Convoy.OverlayLine());
            }
            l.Toggle(AllowDebugSpawns, "Spawn controls in the patrol editor (AllowDebugSpawns)");
            l.Heading("Other");
            l.Entry(HideFsmWarnings);
            l.Text("Verbose logging of spawns, builds, crews and AI state changes is the shared [Debug] VerboseLog switch (Debug > Other settings).");
        }
    }
}
