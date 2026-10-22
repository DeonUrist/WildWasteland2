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
    /// PlayerBodyPages part (split from PlayerBody/PlayerBodyPages.cs in Wave A): the F7 "Climbing" page (ledges and ladders).
    /// Target: Presentation traversal (B06).
    /// </summary>
    internal static partial class PlayerBodyPages
    {
        /// <summary>Adds this part's F7 pages to the PlayerBody page list (called by Create, in the old page order).</summary>
        static partial void AddTraversalPages(PlayerBodyModule m, List<ToolPage> pages)
        {
            pages.Add(new ActionPage(m, G, "Climbing", ClimbingPage));
        }

        // ------------------------------------------------------------------ Climbing
        /// <summary>F7 "Climbing" page: climb / ladder state and settings.</summary>
        static void ClimbingPage(ToolLayout l)
        {
            var c = ClimbingController.Instance;
            l.Heading("State");
            l.Value("Climbing clips", ClimbingController.ClipsLoaded ? "loaded" : "MISSING (Models/apocaplayer_climbing.bundle) - climbing off");
            l.Value("Climbing now", YesNo(c != null && c.Climbing));
            l.Value("Can climb here", YesNo(ModAPI.PlayerCanTraverse));
            l.Value("Last climb", string.IsNullOrEmpty(ClimbingController.LastResult) ? "-" : ClimbingController.LastResult);
            l.Value("Last rejection", string.IsNullOrEmpty(ClimbingController.LastReject) ? "-" : ClimbingController.LastReject);
            l.Value("Last ladder", string.IsNullOrEmpty(LadderController.LastResult) ? "-" : LadderController.LastResult);
            l.Heading("Settings");
            l.Toggle(PlayerBodyModule.ClimbingOn);
            l.Toggle(PlayerBodyModule.JumpTriggersClimbing);
            l.Toggle(PlayerBodyModule.Ladders);
            l.Entry(PlayerBodyModule.LadderSpeed);
            l.Entry(PlayerBodyModule.LadderOffset);
            l.Entry(PlayerBodyModule.LadderEyeForward);
            l.Entry(PlayerBodyModule.LadderNeckBack);
            l.Entry(PlayerBodyModule.ClimbKey);
        }
    }
}
