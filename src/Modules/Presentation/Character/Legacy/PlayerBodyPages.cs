using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.PlayerBody
{
    // F7 tool window pages of the PlayerBody shell (group "Player body & animation"): only the pages of the parts other packages still own
    // in the shell (camera, climbing: B06; pose editors: B16). The character pages moved to PresentationModule (CharacterPages, B05).
    internal static partial class PlayerBodyPages
    {
        const string G = ToolGroups.Player;
        /// <summary>Two-decimal invariant number for page values.</summary>
        static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        /// <summary>"yes" / "no" for page values.</summary>
        static string YesNo(bool b) { return b ? "yes" : "no"; }

        /// <summary>The shell's F7 pages in the old order (each remaining part adds its pages through its partial method).</summary>
        internal static IEnumerable<ToolPage> Create(PlayerBodyModule m)
        {
            // (Wave A) the pages of the other targets are added by their partial files, in the old order
            var pages = new List<ToolPage>();   // (B05) "Animation framework" / "Body & model" moved to PresentationModule (CharacterPages)
            AddCameraPages(m, pages);
            AddTraversalPages(m, pages);
            AddDeveloperPages(m, pages);
            return pages;
        }
        /// <summary>Adds the Camera page (PlayerBodyPages.Camera.cs).</summary>
        static partial void AddCameraPages(PlayerBodyModule m, List<ToolPage> pages);
        /// <summary>Adds the Climbing page (PlayerBodyPages.Traversal.cs).</summary>
        static partial void AddTraversalPages(PlayerBodyModule m, List<ToolPage> pages);
        /// <summary>Adds the weapon pose editor and steering-wheel pages (PlayerBodyPages.Developer.cs).</summary>
        static partial void AddDeveloperPages(PlayerBodyModule m, List<ToolPage> pages);
    }
}
