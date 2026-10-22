using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using Patrols = WildWasteland.Modules.Patrols.PatrolsModule;

namespace WildWasteland.Core.UI
{
    /// <summary>Patrols rows of the settings window (Wave A split): WORLD enemy vehicles/foot patrols, Debug patrol editor and ground recovery.</summary>
    internal static partial class CorePages
    {
        /// <summary>WORLD rows: enemy vehicle mode (Loner / New arrival / Fury road) and foot patrols.</summary>
        static partial void WorldPatrols(ToolLayout l)
        {
            int vehicles = !Patrols.ConvoyEnabled.Value ? 0 : Patrols.FuryRoad.Value ? 2 : 1;
            int next = l.Choices("Enemy Vehicles", vehicles, "Loner", "New arrival", "Fury road");
            if (next != vehicles)
            {
                Patrols.ConvoyEnabled.Value = next != 0;
                Patrols.FuryRoad.Value = next == 2;
            }
            l.Text("Loner: no automatic patrols. New arrival: groups unlock with distance and boss kills. Fury road: all patrol types are available from the start.");
            Switch(l, "Foot patrols", Patrols.FootPatrols);
        }

        /// <summary>Debug page, Patrols category: editor hotkeys and the open-editor button.</summary>
        static partial void DebugSectionPatrols(ToolLayout l, IModule owner)
        {
            if (owner != null && owner.Id == "Patrols")
            {
                l.Heading("Patrol editor");
                l.Entry(Patrols.MenuKey); l.Entry(Patrols.TemplateDumpKey);
                if (l.Button("Open the patrol editor", Patrols.Running && GameRefs.InPlay, true, 320))
                    ToolWindow.CloseThen(() => { if (Patrols.Instance != null && !Patrols.Instance.OpenEditor()) Log.Warn("[Patrols] the patrol editor opens only in play (not in a menu or while another editor is open)"); });
            }
        }

        /// <summary>Debug page, Patrols switches: War Rig ground recovery first.</summary>
        static partial void DebugSwitchesPatrols(ToolLayout l, IModule owner)
        {
            if (owner != null && owner.Id == "Patrols") l.Toggle(WildWasteland.Modules.Patrols.WarRig.S.GroundRecoveryEnabled, "Ground recovery");
        }
    }
}
