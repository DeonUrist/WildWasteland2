using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using PatrolSettings = WildWasteland.Modules.Patrols.PatrolsModule;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrols rows of the settings window: WORLD enemy vehicles and foot patrols.</summary>
    public sealed partial class PatrolsModule
    {
        /// <summary>Contributes world patrol controls.</summary>
        public override void Settings(SettingsShell shell)
        {
            shell.Custom(this, SettingsShell.Page.World, "", WorldPatrols, 10);
            shell.Custom(this, SettingsShell.Page.World, "", l => { if (ConvoyEnabled.Value) Limit(l, "Scrapyard patrols limit", MaxPatrols, 0, 64); }, 11, 0);
            shell.Custom(this, SettingsShell.Page.World, "", l => { if (ConvoyEnabled.Value) Limit(l, "Scrapyard convoy limit", MaxConvoys, 0, 64); }, 11, 1);
            shell.Custom(this, SettingsShell.Page.World, "", l => { if (ConvoyEnabled.Value) Limit(l, "Coyotes patrols limit", MaxCoyotePatrols, 0, 64); }, 12, 0);
            shell.Custom(this, SettingsShell.Page.World, "", l => { if (ConvoyEnabled.Value) Limit(l, "Coyotes merchants limit", MaxMerchants, 0, 3); }, 12, 1);
            shell.Custom(this, SettingsShell.Page.World, "", l => { if (ConvoyEnabled.Value) Limit(l, "Event vehicle limit", EventVehicleLimit, 1, 48); }, 13, 0);
            shell.Custom(this, SettingsShell.Page.World, "", SimulatedBattlesRow, 13, 1);
            shell.Custom(this, SettingsShell.Page.World, "", l => SettingsShell.Switch(l, "Foot patrols", FootPatrols, "Enables groups of NPCs walking between points of interest"), 14);
            shell.Custom(this, SettingsShell.Page.World, "", l => { if (FootPatrols.Value) Limit(l, "Scrapyard foot patrols limit", FootMaxActive, 0, 32); }, 15, 0);
            shell.Custom(this, SettingsShell.Page.World, "", l => { if (FootPatrols.Value) Limit(l, "Coyotes foot patrols limit", CoyoteFootMaxActive, 0, 32); }, 15, 1);
        }
        /// <summary>Draws a compact labelled integer field within the shell's two-column row.</summary>
        private static void Limit(ToolLayout l, string label, ConfigEntry<int> entry, int min, int max)
        {
            if (!l.Matches(label)) return;
            var row = l.Next(); Ledger.RowHighlight(row);
            Ledger.Label(new Rect(row.x + 22, row.y, row.width - 126, row.height), label, Ledger.Body, Ledger.Text);
            int value = Mathf.RoundToInt(Ledger.Number(new Rect(row.xMax - 96, row.y, 96, row.height), "Patrol limits/" + entry.Definition.Key, entry.Value, min, max, () => { }, true));
            if (value != entry.Value) entry.Value = value;
            l.Hint(entry.Description.Description);
        }
        /// <summary>Aligns the checkbox and state with the neighbouring numeric value fields.</summary>
        private static void SimulatedBattlesRow(ToolLayout l)
        {
            if (!ConvoyEnabled.Value || !l.Matches("Simulated battles")) return;
            var row = l.Next(); Ledger.RowHighlight(row);
            bool hover = GUI.enabled && row.Contains(Event.current.mousePosition);
            if (GUI.Button(row, GUIContent.none, GUIStyle.none)) SimulatedBattles.Value = !SimulatedBattles.Value;
            Ledger.Label(new Rect(row.x + 22, row.y, row.width - 126, row.height), "Simulated battles", Ledger.Body, !GUI.enabled ? Ledger.Muted : hover ? Ledger.Accent : Ledger.Text);
            float x = row.xMax - 96;
            Ledger.Panel(new Rect(x, row.center.y - 14, 28, 28), Color.clear, Ledger.Accent);
            if (SimulatedBattles.Value) Ledger.Fill(new Rect(x + 6, row.center.y - 8, 16, 16), Ledger.Accent);
            Ledger.Label(new Rect(x + 44, row.y, 52, row.height), SimulatedBattles.Value ? "ON" : "OFF", Ledger.GameButton, GUI.enabled ? Ledger.Text : Ledger.Muted);
            l.Hint(SimulatedBattles.Description.Description);
        }
        /// <summary>WORLD rows: enemy vehicle mode (Loner / New arrival / Fury road) and foot patrols.</summary>
        private static void WorldPatrols(ToolLayout l)
        {
            l.Heading("Patrols");
            int vehicles = !PatrolSettings.ConvoyEnabled.Value ? 0 : PatrolSettings.FuryRoad.Value ? 2 : 1;
            int next = l.Choices("Enemy Vehicles", vehicles, "Loner", "New arrival", "Fury road");
            if (next != vehicles)
            {
                PatrolSettings.ConvoyEnabled.Value = next != 0;
                PatrolSettings.FuryRoad.Value = next == 2;
            }
            l.Hint(next == 0 ? "No NPC vehicles on map" : next == 1 ? "NPC vehicle groups become harder and unlock with distance and boss kills" : "The world is full of dangers. All vehicle patrol types can be met on the map from the start");
        }
    }
}
