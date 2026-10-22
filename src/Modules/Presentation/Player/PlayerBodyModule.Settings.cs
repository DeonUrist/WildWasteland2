using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Player = WildWasteland.Modules.PlayerBody.PlayerBodyModule;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>Presentation rows of the settings window (Wave A split): PLAYER page (character portraits, body/arms/climbing/camera switches).</summary>
    public sealed partial class PlayerBodyModule
    {
        /// <summary>Contributes the player page and vehicle hints from the character module.</summary>
        public override void Settings(SettingsShell shell)
        {
            shell.Custom(this, SettingsShell.Page.Player, "", DrawPlayer, 0);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawVehicleHints, 220);
            shell.Custom(this, SettingsShell.Page.Hud, "", DrawVehicleIcons, 240);
        }
        /// <summary>Player-owned vehicle hint switch at its original HUD position.</summary>
        private static void DrawVehicleHints(ToolLayout l) { SettingsShell.Switch(l, "Vehicle Hints", VehicleHotkeyHint); }
        /// <summary>Player-owned vehicle icon switch at its original HUD position.</summary>
        private static void DrawVehicleIcons(ToolLayout l) { SettingsShell.Switch(l, "Vehicle Icons", VehicleStatusHint); }

        /// <summary>PLAYER page: character choice, full body, first-person arms, climbing, third person, occlusion.</summary>
        static void DrawPlayer(ToolLayout l)
        {
            l.Heading("Player");
            BodySelection(l);
            int body = !Player.BodyFirstPerson.Value ? 0 : Player.FullBodyOnlyOnFoot.Value ? 1 : 2;
            int next = l.Choices("Full Body", body, "Off", "Only on foot", "On");
            if (next != body) { Player.FullBodyOnlyOnFoot.Value = next == 1; Player.BodyFirstPerson.Value = next != 0; }
            int arms = !Player.EmptyHandArms.Value ? 0 : Player.FirstPersonArmsOnlyOnFoot.Value ? 1 : 2;
            next = l.Choices("First Person Arms", arms, "Off", "On foot", "On");
            if (next != arms) { Player.FirstPersonArmsOnlyOnFoot.Value = next == 1; Player.EmptyHandArms.Value = next != 0; }
            SettingsShell.Switch(l, "Climbing", Player.ClimbingOn);
            SettingsShell.Switch(l, "Third Person Camera", Player.ThirdPersonOnFoot);
            SettingsShell.Switch(l, "Occlusion", Player.OcclusionPrototype);   // third person on foot only; in cars it stays off
            l.Text("Full Body shows your body in first person. Use the game's Change Camera key for third person.");
            if (!ModuleHost.IsRunning(Player.Instance)) l.Text("Enable Player body & animation in Modules to use these player settings.");
        }
        /// <summary>Character portrait cards; a click selects the body (PlayerBody Character entry).</summary>
        static void BodySelection(ToolLayout l)
        {
            var order = WildWasteland.Modules.PlayerBody.CharacterPortraits.Order;
            var r = l.Next(288); float width = (r.width - 8 * (order.Length - 1)) / order.Length;
            for (int i = 0; i < order.Length; i++)
            {
                int choice = order[i];
                var card = new Rect(r.x + i * (width + 8), r.y, width, r.height);
                bool selected = (int)Player.Character.Value == choice;
                Ledger.Panel(card, selected ? Ledger.Selected : Ledger.Field, selected ? Ledger.Accent : Ledger.Line);
                var texture = WildWasteland.Modules.PlayerBody.CharacterPortraits.Picture(choice);
                if (texture != null) GUI.DrawTexture(new Rect(card.x + 6, card.y + 6, card.width - 12, card.height - 12), texture, ScaleMode.ScaleToFit);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none) && !selected) Player.Character.Value = (Player.Gender)choice;
            }
        }
    }
}
