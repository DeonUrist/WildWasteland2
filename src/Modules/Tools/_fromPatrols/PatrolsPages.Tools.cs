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
    /// <summary>F7 pages of the Patrols editor tools: patrol editor and template recorder (Wave A split of PatrolsPages.cs; ToolPages() in Patrols lists them).</summary>
    public sealed partial class PatrolsModule
    {
        void PatrolEditorPage(ToolLayout l)
        {
            l.Text("The patrol / convoy / merchant / loot editor (the \"ledger\"): patrol and convoy groups with their minimum distance, boss kills, chance, faction and roster; the car and truck templates (driver, passenger, turrets, loot, explode chance); merchant shop types; car and truck loot profiles. It pauses the game while open and saves PatrolsAndLoot.json and the template files.");
            l.Heading("Spawn controls");
            l.Text("With AllowDebugSpawns on, the editor shows: \"Spawn template\" (the selected template parked 8 m ahead of you, setup only), an \"S\" button per group and SPAWN in the footer (a real departure of that group from a nearby camp - these two need New arrival or Fury road in Game > World > Enemy Vehicles).");
            l.Toggle(AllowDebugSpawns, "Show spawn controls (AllowDebugSpawns)");
            l.Heading("Templates");
            l.Value("Loaded", CarTemplates.Summary);
            l.Value("Data folder", DataDir);
            if (l.Button("Reload templates from disk", Running, false, 320)) CarTemplates.Refresh(true);
        }

        void RecorderPage(ToolLayout l)
        {
            l.Text("The template recorder (ex Apocatemplater): sit in a vehicle you built, open it, choose name, faction, driver, passenger, loot and turrets, and the vehicle - frame, parts, hinge offsets, trailer, suspension - is saved as a new template in Patrols\\PlayerTemplates. Only while you sit in a vehicle.");
            bool seated = Running && TemplateExporter.Seated;
            if (seated)
            {
                if (l.Button("Record the vehicle I sit in", InPlay, true, 320))
                    ToolWindow.CloseThen(() => { if (Instance != null) Instance.OpenRecorder(); });
            }
            else l.Text("You are not seated in a vehicle: get in one, then open this page again (or use the hotkey).", Ledger.Danger);
            l.Heading("Hotkey");
            l.Value("Template recorder key", KeyName(TemplateDumpKey));
            l.Entry(TemplateDumpKey);
            l.Value("Saves to", CarTemplates.PlayerFolder ?? Path.Combine(DataDir, "PlayerTemplates"));
        }
    }
}
