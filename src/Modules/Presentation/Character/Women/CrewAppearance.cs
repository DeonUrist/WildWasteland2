using System;
using UnityEngine;
using WildWasteland.Modules.NPC;

namespace WildWasteland.Modules.Presentation
{
    internal sealed class CrewAppearance : WildWasteland.Core.ICrewAppearance
    {
        /// <summary>Names also resolve with NPC off, so saved female crew still has a native fallback body.</summary>
        public string BasePrefab(string name)
        {
            if (string.Equals(name, "Gungirl", StringComparison.OrdinalIgnoreCase)) return "Flexa";
            if (string.Equals(name, "Shoota", StringComparison.OrdinalIgnoreCase)) return "Sprokka";
            if (string.Equals(name, "Gungirl_Dead", StringComparison.OrdinalIgnoreCase)) return "Flexa_Dead";
            if (string.Equals(name, "Shoota_Dead", StringComparison.OrdinalIgnoreCase)) return "Sprokka_Dead";
            return name;
        }

        /// <summary>Female identity of a crew root by its name (Gungirl/Shoota), else the fallback.</summary>
        public string Identity(GameObject root, string fallback)
        {
            if (root == null) return fallback;
            string name = root.name; int suffix = name.IndexOf('(');
            if (suffix >= 0) name = name.Substring(0, suffix);
            return BasePrefab(name) != name ? name : fallback;
        }

        /// <summary>Dresses a crew member: alias names (Gungirl, Shoota, their corpses) rename the root, then the Women of Wasteland body applies (randomize = population roll).</summary>
        public void Dress(GameObject root, string requested, bool randomize = true)
        {
            if (root == null) return;
            if (requested != BasePrefab(requested))
            {
                string native = BasePrefab(requested);
                requested = (native.StartsWith("Flexa", StringComparison.Ordinal) ? "Gungirl" : "Shoota")
                    + (native.EndsWith("_Dead", StringComparison.Ordinal) ? "_Dead" : "");
                int suffix = root.name.IndexOf('(');
                root.name = requested + (suffix >= 0 ? root.name.Substring(suffix) : "");
            }
            if (WomenFeature.Active) Gungirl.DressCrew(root, requested, randomize);   // (B05) same module: the Women of Wasteland body directly
        }

        /// <summary>Carries a female identity over to a replacement object (corpse, bailed-out NPC).</summary>
        public void Transfer(GameObject source, GameObject destination)
        {
            string identity = Identity(source, null);
            if (identity != null)
            {
                if (destination != null && destination.name.StartsWith(BasePrefab(identity) + "_Dead", StringComparison.Ordinal)) identity += "_Dead";
                Dress(destination, identity, false);
            }
        }
    }
}
