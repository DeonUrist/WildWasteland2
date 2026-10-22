using System;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    internal static class CrewAppearance
    {
        // Names also resolve with NPC off, so saved female crew still has a native fallback body.
        internal static string BasePrefab(string name)
        {
            if (string.Equals(name, "Gungirl", StringComparison.OrdinalIgnoreCase)) return "Flexa";
            if (string.Equals(name, "Shoota", StringComparison.OrdinalIgnoreCase)) return "Sprokka";
            if (string.Equals(name, "Gungirl_Dead", StringComparison.OrdinalIgnoreCase)) return "Flexa_Dead";
            if (string.Equals(name, "Shoota_Dead", StringComparison.OrdinalIgnoreCase)) return "Sprokka_Dead";
            return name;
        }

        internal static string Identity(GameObject root, string fallback)
        {
            if (root == null) return fallback;
            string name = root.name; int suffix = name.IndexOf('(');
            if (suffix >= 0) name = name.Substring(0, suffix);
            return BasePrefab(name) != name ? name : fallback;
        }

        internal static void Dress(GameObject root, string requested, bool randomize = true)
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
            var api = Core.Services.NpcAppearance;
            if (api != null) api.Dress(root, requested, randomize);
        }

        internal static void Transfer(GameObject source, GameObject destination)
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
