using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// Settings-window and F7 pages of the Presentation character part: the PLAYER page rows (character portraits, body/arms switches and
    /// the World part's climbing / third-person / occlusion switches), "Animation framework", "Body &amp; model" and the Women of Wasteland
    /// "Female appearance &amp; voices" page. Everything written here is live (SettingChanged / read every frame).
    /// </summary>
    internal static class CharacterPages
    {
        const string G = ToolGroups.Player;

        /// <summary>"yes" / "no" for page values.</summary>
        static string YesNo(bool b) { return b ? "yes" : "no"; }

        /// <summary>Adds the character part's F7 pages in the old order (player group, then the female NPC page of the NPC group).</summary>
        internal static void Add(PresentationModule m, List<ToolPage> pages)
        {
            pages.Add(new ActionPage(m, G, "Animation framework", Framework));
            pages.Add(new ActionPage(m, G, "Body & model", BodyPage));
            pages.Add(new ConfigPage(m, ToolGroups.Npc, "Female appearance & voices", m.Config.Main, m.Config.Section("Gungirl"), m.Config.Section("Shoota"))
            { Intro = "Women of Wasteland models, textures, accessories and voice settings. Paths are relative to plugins/WildWasteland/Presentation. Model, texture and voice changes require a restart." });
        }

        // ------------------------------------------------------------------ settings window
        /// <summary>PLAYER page: character choice, full body, first-person arms, climbing, third person, occlusion.</summary>
        internal static void DrawPlayer(ToolLayout l)
        {
            l.Heading("Player");
            BodySelection(l);
            int body = !CharacterSettings.BodyFirstPerson.Value ? 0 : CharacterSettings.FullBodyOnlyOnFoot.Value ? 1 : 2;
            int next = l.Choices("Full Body", body, "Off", "Only on foot", "On");
            if (next != body) { CharacterSettings.FullBodyOnlyOnFoot.Value = next == 1; CharacterSettings.BodyFirstPerson.Value = next != 0; }
            int arms = !CharacterSettings.EmptyHandArms.Value ? 0 : CharacterSettings.FirstPersonArmsOnlyOnFoot.Value ? 1 : 2;
            next = l.Choices("First Person Arms", arms, "Off", "On foot", "On");
            if (next != arms) { CharacterSettings.FirstPersonArmsOnlyOnFoot.Value = next == 1; CharacterSettings.EmptyHandArms.Value = next != 0; }
            SettingsShell.Switch(l, "Climbing", TraversalSettings.ClimbingOn);
            SettingsShell.Switch(l, "Third Person Camera", CameraSettings.ThirdPersonOnFoot);
            SettingsShell.Switch(l, "Occlusion", CameraSettings.OcclusionPrototype);   // third person on foot only; in cars it stays off
            l.Text("Full Body shows your body in first person. Use the game's Change Camera key for third person.");
            if (!ModuleHost.IsRunning(PresentationModule.Instance)) l.Text("Enable Presentation in Modules to use these player settings.");
        }

        /// <summary>Character portrait cards; a click selects the body (Presentation Character entry).</summary>
        static void BodySelection(ToolLayout l)
        {
            var order = CharacterPortraits.Order;
            var r = l.Next(288); float width = (r.width - 8 * (order.Length - 1)) / order.Length;
            for (int i = 0; i < order.Length; i++)
            {
                int choice = order[i];
                var card = new Rect(r.x + i * (width + 8), r.y, width, r.height);
                bool selected = (int)CharacterSettings.Character.Value == choice;
                Ledger.Panel(card, selected ? Ledger.Selected : Ledger.Field, selected ? Ledger.Accent : Ledger.Line);
                var texture = CharacterPortraits.Picture(choice);
                if (texture != null) GUI.DrawTexture(new Rect(card.x + 6, card.y + 6, card.width - 12, card.height - 12), texture, ScaleMode.ScaleToFit);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none) && !selected) CharacterSettings.Character.Value = (CharacterChoice)choice;
            }
        }

        // ------------------------------------------------------------------ Animation framework
        /// <summary>F7 "Animation framework" page: ModAPI service state and attached characters.</summary>
        static void Framework(ToolLayout l)
        {
            l.Text("The Mixamo animation set of the player's body, offered to other modules as the IAnimationApi ModAPI service (ex Apocaplayer ModAPI): NPC gunmen and patrol turret riders are animated with it. Per-character decision logs: Debug log level 2.");
            l.Heading("State");
            l.Value("Service registered", YesNo(ModApi.Get<IAnimationApi>() != null));
            l.Value("API version / source", ModAPI.ApiVersion + " / Apocaplayer " + ModAPI.PluginVersion);
            string bundle = PresentationModule.AssetPath(CharacterSettings.AnimBundleFile.Value);
            l.Value("Animation bundle", !Anims.Tried ? "not loaded yet (on first use)" : Anims.BundleOpen ? "loaded, " + Anims.Count + " clips" : File.Exists(bundle) ? "FAILED to open (see log)" : "missing: " + bundle);
            l.Value("Ready (player set present)", YesNo(ModAPI.Ready));
            l.Value("Clips offered", ModAPI.Ready ? ModAPI.ClipNames().Length.ToString(CultureInfo.InvariantCulture) : "-");
            l.Value("Attached characters", ModAPI.Count.ToString(CultureInfo.InvariantCulture));
            l.Toggle(CharacterSettings.AnimationLod);
            int shown = 0;
            for (int i = 0; i < ModAPI.Count && shown < 24; i++)
            {
                var c = ModAPI.CharacterAt(i);
                if (c == null || c.Animator == null) continue;
                shown++;
                l.Value("  " + c.Animator.gameObject.name, (c.Suspended ? "suspended" : c.WeaponKind) + (c.WeaponKey != "" ? " (" + c.WeaponKey + ")" : "")
                    + (c.HandsClip != "" ? ", hands " + c.HandsClip : "") + (c.ActionClip != "" ? ", " + c.ActionClip : ""));
            }
            if (ModAPI.Count > shown) l.Text("(" + (ModAPI.Count - shown) + " more)");
        }

        // ------------------------------------------------------------------ Body & model
        /// <summary>F7 "Body &amp; model" page: body state, character choice and body toggles.</summary>
        static void BodyPage(ToolLayout l)
        {
            var b = BodyRunner.PlayerBody;
            l.Heading("State");
            l.Value("Player found", YesNo(Game.Ready));
            l.Value("Body", b == null ? "not built (rebuilt every 2 s while missing)" : b.IsRagdoll ? "ragdoll (dead)" : "built");
            l.Value("Character / model", CharacterSettings.Character.Value + " / " + CharacterSettings.BodyModelFile);
            l.Value("Driver's seat", CarSeat.Seat != null ? CarSeat.Seat.root.name : "-");
            if (l.Button("Rebuild the body now", Game.Ready, false, 260)) BodyRunner.CharacterChanged();
            l.Heading("Settings");
            int character = System.Array.IndexOf(CharacterPortraits.Order, (int)CharacterSettings.Character.Value);
            int selected = l.Choices("Character", character, "Male", "Merchant", "Bodyguard", "Sprokka", "Organic Mechanic", "Female", "Gungirl", "Shoota");
            if (selected != character) CharacterSettings.Character.Value = (CharacterChoice)CharacterPortraits.Order[selected];
            l.Toggle(CharacterSettings.BodyFirstPerson);
            l.Toggle(CharacterSettings.EmptyHandArms);
            l.Toggle(CharacterSettings.FemaleArms);
            l.Toggle(CharacterSettings.ReplaceDriver);
            l.Toggle(CharacterSettings.EquipmentOnBody);
            l.Toggle(CharacterSettings.PainSounds);
        }
    }
}
