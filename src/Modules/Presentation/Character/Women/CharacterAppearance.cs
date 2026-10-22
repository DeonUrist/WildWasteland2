using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.NPC;
using WildWasteland.Modules.Patrols;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// ICharacterAppearance provider (README 6.1, D9/D10): one appearance service over Women of Wasteland (Gungirl/Shoota bodies and
    /// voices) and the crew identity aliases (CrewAppearance). Dress keeps CrewAppearance.Dress semantics (alias rename, then the female
    /// body when Women of Wasteland runs); RollVariant is the female population roll as a seeded query (the CreateObject spawn hook keeps
    /// its own roll so saved names stay identical).
    /// </summary>
    internal sealed class CharacterAppearance : ICharacterAppearance
    {
        /// <summary>Applies the identity's look to <paramref name="root"/> (alias names rename the root first; randomize = population roll).</summary>
        public void Dress(GameObject root, string identity, bool randomize) { new CrewAppearance().Dress(root, identity, randomize); }

        /// <summary>Model and texture file paths of a female identity (Gungirl, Shoota).</summary>
        public bool TryPaths(string identity, out string model, out string texture) { return WomenFeature.Paths(identity, out model, out texture); }

        /// <summary>Female variant of a fresh native spawn ("Flexa" -> "Gungirl", "Sprokka" -> "Shoota") at [Presentation / Women of
        /// Wasteland] FemalePopulation %, decided by <paramref name="seed"/>; null = keep the native look (also when disabled/inactive).</summary>
        public string RollVariant(string prefabName, int seed)
        {
            if (!WomenFeature.Active || !CharacterSettings.EnableFemaleNpc.Value) return null;
            string female = Gungirl.VariantOf(prefabName);
            if (female == null) return null;
            int roll = (int)((uint)(seed * 2654435761u) % 100u);
            return roll < CharacterSettings.FemalePopulation.Value ? female : null;
        }

        /// <summary>Female identity applied to an NPC or corpse (by its saved name), null = native look.</summary>
        public string IdentityOf(GameObject npc) { return npc != null ? Gungirl.IdentityOf(npc.name) : null; }

        /// <summary>True when the NPC (or corpse) is a Women of Wasteland female.</summary>
        public bool IsFemale(GameObject npc) { return Gungirl.IsFemale(npc); }

        /// <summary>Voice profile of an identity: the female voice folder for Gungirl/Shoota, null for native voices.</summary>
        public string VoiceOf(string identity)
        {
            return identity == "Gungirl" || identity == "Shoota" ? CharacterSettings.GungirlVoice.Value : null;
        }
    }
}
