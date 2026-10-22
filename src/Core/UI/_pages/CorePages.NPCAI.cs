using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace WildWasteland.Core.UI
{
    /// <summary>NPCAI rows of the settings window (Wave A split): Shout + Shout modifier keys on the HUD page.</summary>
    internal static partial class CorePages
    {
        /// <summary>HUD page row: Shout key pair (NPC).</summary>
        static partial void HudNpc(ToolLayout l)
        {
            l.KeyPair("Shout", WildWasteland.Modules.NPC.NpcModule.ShoutKey, "Shout modifier", WildWasteland.Modules.NPC.NpcModule.ShoutModifier);
        }
    }
}
