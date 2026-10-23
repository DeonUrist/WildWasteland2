using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using PatrolSettings = WildWasteland.Modules.Patrols.PatrolsModule;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrols rows of the settings window (Wave A split): WORLD enemy vehicles/foot patrols, Debug patrol editor and ground recovery.</summary>
    public sealed partial class PatrolsModule
    {
        /// <summary>Contributes authored patrol controls and editor entry points.</summary>
        public override void Settings(SettingsShell shell)
        {
            shell.Custom(this, SettingsShell.Page.World, "", WorldPatrols, 10);
            shell.Custom(this, SettingsShell.Page.Debug, "", DrawPatrolTools, 300);
        }
        /// <summary>Draws patrol-specific Debug controls from their owning module.</summary>
        private void DrawPatrolTools(ToolLayout l) { DebugSectionPatrols(l, this); DebugSwitchesPatrols(l, this); }
        /// <summary>WORLD rows: enemy vehicle mode (Loner / New arrival / Fury road) and foot patrols.</summary>
        private static void WorldPatrols(ToolLayout l)
        {
            int vehicles = !PatrolSettings.ConvoyEnabled.Value ? 0 : PatrolSettings.FuryRoad.Value ? 2 : 1;
            int next = l.Choices("Enemy Vehicles", vehicles, "Loner", "New arrival", "Fury road");
            if (next != vehicles)
            {
                PatrolSettings.ConvoyEnabled.Value = next != 0;
                PatrolSettings.FuryRoad.Value = next == 2;
            }
            l.Hint(next == 0 ? "No NPC vehicles on map" : next == 1 ? "NPC vehicle groups become harder and unlock with distance and boss kills" : "The world is full of dangers. All vehicle patrol types can be met on the map from the start");
            SettingsShell.Switch(l, "Foot patrols", PatrolSettings.FootPatrols, "Enables groups of NPCs walking between points of interest");
        }

        /// <summary>Debug page, Patrols category: editor hotkeys and the open-editor button.</summary>
        private static void DebugSectionPatrols(ToolLayout l, IModule owner)
        {

        }

        /// <summary>Debug page, Patrols switches: War Rig ground recovery first.</summary>
        private static void DebugSwitchesPatrols(ToolLayout l, IModule owner)
        {
        }
    }
}
