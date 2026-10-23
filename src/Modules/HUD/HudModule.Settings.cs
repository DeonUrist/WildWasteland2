using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using HudSettings = WildWasteland.Modules.HUD.HudModule;
namespace WildWasteland.Modules.HUD
{
    /// <summary>Combat HUD settings contributions and preview preparation.</summary>
    public sealed partial class HudModule
    {
        /// <summary>Registers only combat-owned rows in their original HUD order.</summary>
        public override void Settings(SettingsShell shell)
        {
            CleanSettings(shell);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawCombatStart, 100);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawGrabbingDot, 110, 0);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawHitMarkers, 120, 0);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawFloatingDamage, 130, 0);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawHeadshot, 130, 1);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawUnavailable, 240);
            foreach (SettingsShell.Page page in new[] { SettingsShell.Page.Player, SettingsShell.Page.World, SettingsShell.Page.Hud })
                shell.OnShow(this, page, CrosshairController.PreparePreview);
        }
        /// <summary>Original HUD heading, crosshair cards, selection dot and floating damage toggle.</summary>
        private static void DrawCombatStart(ToolLayout l)
        {
            CrosshairSelection(l);
            l.Hint("Choose the crosshair shown while aiming at the world");
        }
        /// <summary>Shows the interaction targeting dot.</summary>
        private static void DrawGrabbingDot(ToolLayout l) { SettingsShell.Switch(l, "Grabbing dot", SelectionDot, "Show a dot when targeting an item you can grab"); }
        /// <summary>Shows damage values above hit targets.</summary>
        private static void DrawFloatingDamage(ToolLayout l)
        {
            bool damage = HudEnabled.Value && DamageNumbers.Value == 2;
            bool show = l.Choices("Floating damage", damage ? 1 : 0, "Off", "On") == 1;
            if (show != damage) { DamageNumbers.Value = show ? 2 : 0; if (show) HudEnabled.Value = true; }
            l.Hint("Show damage numbers above targets when you hit them");
        }
        /// <summary>Hit-marker switch enables its owning combat HUD.</summary>
        private static void DrawHitMarkers(ToolLayout l) { FeedbackSwitch(l, "Hit markers", HitMarker); l.Hint("Show a crosshair marker when you hit a target"); }
        /// <summary>Headshot-audio switch enables its owning combat HUD.</summary>
        private static void DrawHeadshot(ToolLayout l) { FeedbackSwitch(l, "Headshot sound", HeadshotSound); l.Hint("Play a sound when you land a headshot"); }
        /// <summary>Explains the existing combat HUD module requirement.</summary>
        private static void DrawUnavailable(ToolLayout l) { if (!ModuleHost.IsRunning(Instance)) l.Text("Enable Combat HUD in Modules to use these HUD settings."); }
        /// <summary>Off/On switch that is only "on" while the combat HUD is enabled; turning it on enables the HUD.</summary>
        static void FeedbackSwitch(ToolLayout l, string label, ConfigEntry<bool> entry)
        {
            bool value = HudSettings.HudEnabled.Value && entry.Value;
            bool next = l.Choices(label, value ? 1 : 0, "Off", "On") == 1;
            if (next != value) { entry.Value = next; if (next) HudSettings.HudEnabled.Value = true; }
        }

        /// <summary>Crosshair choice cards with live previews.</summary>
        static readonly string[] _crosshairs = { "None", "Vanilla", "Lines", "Dot" };
        /// <summary>Crosshair choice cards (None / Vanilla / Lines / Dot) with previews from the HUD.</summary>
        static void CrosshairSelection(ToolLayout l)
        {
            l.Text("Crosshair");
            var r = l.Next(74); float width = (r.width - 24) / 4;
            for (int i = 0; i < _crosshairs.Length; i++)
            {
                var card = new Rect(r.x + i * (width + 8), r.y, width, r.height);
                bool selected = HudSettings.Crosshair.Value == i;
                Ledger.Panel(card, selected ? Ledger.Selected : Ledger.Field, selected ? Ledger.Accent : Ledger.Line);
                float previewSize = i == 3 ? 27 : 36;
                WildWasteland.Modules.HUD.CrosshairController.DrawPreview(new Rect(card.center.x - previewSize / 2, card.y + 23 - previewSize / 2, previewSize, previewSize), i);
                Ledger.Label(new Rect(card.x, card.yMax - 29, card.width, 26), _crosshairs[i], Ledger.Body, selected ? Ledger.Accent : Ledger.Text, TextAnchor.MiddleCenter);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none) && !selected) HudSettings.Crosshair.Value = i;
            }
        }

    }
}
