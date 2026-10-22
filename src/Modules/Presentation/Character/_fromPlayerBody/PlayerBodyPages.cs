using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.PlayerBody
{
    // F7 tool window pages of the PlayerBody module (group "Player body & animation"). Live state on top, then the module's F6 toggles
    // that belong to the page and its F7 (tools.cfg) settings. Everything written here is live (SettingChanged / read every frame).
    internal static partial class PlayerBodyPages
    {
        const string G = ToolGroups.Player;
        static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        static string YesNo(bool b) { return b ? "yes" : "no"; }

        internal static IEnumerable<ToolPage> Create(PlayerBodyModule m)
        {
            // (Wave A) the pages of the other targets are added by their partial files, in the old order
            var pages = new List<ToolPage>
            {
                new ActionPage(m, G, "Animation framework", Framework),
                new ActionPage(m, G, "Body & model", BodyPage),
            };
            AddCameraPages(m, pages);
            AddTraversalPages(m, pages);
            AddDeveloperPages(m, pages);
            return pages;
        }
        static partial void AddCameraPages(PlayerBodyModule m, List<ToolPage> pages);
        static partial void AddTraversalPages(PlayerBodyModule m, List<ToolPage> pages);
        static partial void AddDeveloperPages(PlayerBodyModule m, List<ToolPage> pages);

        // ------------------------------------------------------------------ Animation framework
        static void Framework(ToolLayout l)
        {
            l.Text("The Mixamo animation set of the player's body, offered to other modules as Core.Services.Animation (ex Apocaplayer ModAPI): NPC gunmen and patrol turret riders are animated with it.");
            l.Heading("State");
            l.Value("Service registered", YesNo(Services.Animation != null));
            l.Value("API version / source", ModAPI.ApiVersion + " / Apocaplayer " + ModAPI.PluginVersion);
            string bundle = PlayerBodyModule.ModPath(PlayerBodyModule.AnimBundleFile.Value);
            l.Value("Animation bundle", !Anims.Tried ? "not loaded yet (on first use)" : Anims.BundleOpen ? "loaded, " + Anims.Count + " clips" : File.Exists(bundle) ? "FAILED to open (see log)" : "missing: " + bundle);
            l.Value("Ready (player set present)", YesNo(ModAPI.Ready));
            l.Value("Clips offered", ModAPI.Ready ? ModAPI.ClipNames().Length.ToString(CultureInfo.InvariantCulture) : "-");
            l.Value("Attached characters", ModAPI.Count.ToString(CultureInfo.InvariantCulture));
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
            l.Heading("Debug");
            l.Entry(PlayerBodyModule.VerboseModApi);
        }

        // ------------------------------------------------------------------ Body & model
        static void BodyPage(ToolLayout l)
        {
            var b = BodyRunner.PlayerBody;
            l.Heading("State");
            l.Value("Player found", YesNo(Game.Ready));
            l.Value("Body", b == null ? "not built (rebuilt every 2 s while missing)" : b.IsRagdoll ? "ragdoll (dead)" : "built");
            l.Value("Character / model", PlayerBodyModule.Character.Value + " / " + PlayerBodyModule.BodyModelFile);
            l.Value("Driver's seat", CarSeat.Seat != null ? CarSeat.Seat.root.name : "-");
            if (l.Button("Rebuild the body now", Game.Ready, false, 260)) BodyRunner.CharacterChanged();
            l.Heading("Settings");
            int character = System.Array.IndexOf(CharacterPortraits.Order, (int)PlayerBodyModule.Character.Value);
            int selected = l.Choices("Character", character, "Male", "Merchant", "Bodyguard", "Sprokka", "Organic Mechanic", "Female", "Gungirl", "Shoota");
            if (selected != character) PlayerBodyModule.Character.Value = (PlayerBodyModule.Gender)CharacterPortraits.Order[selected];
            l.Toggle(PlayerBodyModule.BodyFirstPerson);
            l.Toggle(PlayerBodyModule.EmptyHandArms);
            l.Toggle(PlayerBodyModule.FemaleArms);
            l.Toggle(PlayerBodyModule.ReplaceDriver);
            l.Toggle(PlayerBodyModule.EquipmentOnBody);
            l.Toggle(PlayerBodyModule.PainSounds);
        }
    }
}
