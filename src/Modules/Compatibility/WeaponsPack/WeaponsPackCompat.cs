using System;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Old name of the Weapons Pack NPC part, kept for NPC Senses until it reads the noise factor through ModAPI (Wave C deletes it).</summary>
    internal static class WeaponsPackCompat
    {
        /// <summary>True while the pack's chainsaw is drawn.</summary>
        [Obsolete("refactor: use WeaponsPackFear.ChainsawDrawn through a NPCAI noise query (REFACTOR-REQUEST B17-2)")]
        internal static bool ChainsawDrawn { get { return WeaponsPackFear.ChainsawDrawn; } }
    }
}
