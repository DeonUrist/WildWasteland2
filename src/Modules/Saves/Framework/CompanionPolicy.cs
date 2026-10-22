using WildWasteland.Core;
using System;
using System.IO;
using System.Text;

namespace WildWasteland.Modules.Saves
{
    /// <summary>
    /// Per-slot companion files that travel with a save, and the two owner rules applied when a snapshot is published under a new
    /// identity. The owners (NPCAI, Patrols, POI, HUD, Quests) are asked to register their own patterns and rules
    /// (REFACTOR-REQUEST B04-2/B04-3); until they do, Saves registers today's list under its own name so every participant stays
    /// journaled, backed up and copied exactly as before. Registering the same pattern again from the owner is harmless:
    /// <see cref="SaveFiles.Prepare"/> ignores duplicate paths.
    /// </summary>
    internal static class CompanionPolicy
    {
        /// <summary>Sidecar patterns relative to config/WildWasteland (unchanged paths of the old hard-coded participant list).</summary>
        static readonly string[] Patterns =
        {
            "NPC/Saves/{slot}.senses.txt", "Patrols/Saves/{slot}.bin", "Patrols/Saves/{slot}.bin.bak", "POI/Saves/{slot}.poi.txt",
            "CleanHUD/Saves/{slot}.fog.bin", "CleanHUD/Saves/{slot}.locations.txt", "Quests/Saves/{slot}.crew.txt",
        };
        /// <summary>ES3 key whose presence makes the Towns sidecar folder redundant in new snapshots (the embedded record wins).</summary>
        const string TownsWorldKey = "WildWasteland.Towns.World.v1";

        /// <summary>Registers the current companion patterns through the Core sidecar registry (BindConfig, also while Saves is off).</summary>
        internal static void RegisterDefaults(IModule saves)
        {
            foreach (string pattern in Patterns) Sidecars.Register(saves, pattern);
        }

        /// <summary>False for a Towns sidecar of a source save that already embeds its town record; everything else is copied.</summary>
        internal static bool IncludeInSnapshot(string path, string source)
        {
            // The verified embedded record is authoritative; do not create Towns sidecars for new snapshots.
            return !(path.StartsWith(Path.Combine(Assets.ConfigDir, "Towns", "Saves") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && ES3.KeyExists(TownsWorldKey, SaveFiles.Disk(source)));
        }

        /// <summary>Rewrites the slot identity header of a staged NPC senses copy ("v1 &lt;slot&gt;"); other companions are untouched.</summary>
        internal static void RemapStaged(string original, string staged, string source, string target)
        {
            if (!original.EndsWith(".senses.txt", StringComparison.Ordinal)) return;
            string text = File.ReadAllText(staged), header = "v1 " + source;
            if (!text.StartsWith(header + "\n", StringComparison.Ordinal) && !text.StartsWith(header + "\r\n", StringComparison.Ordinal)) throw new IOException("NPC senses companion identity is invalid");
            SaveFiles.WriteDurable(staged, Encoding.UTF8.GetBytes("v1 " + target + text.Substring(header.Length)));
        }
    }
}
