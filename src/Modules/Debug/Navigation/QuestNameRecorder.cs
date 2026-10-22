using System;
using System.Collections.Generic;
using System.IO;
using WildWasteland.Core;
namespace WildWasteland.Modules.Debug
{
    /// <summary>Writes deduplicated quest-authoring name/location lists from copied identity observations.</summary>
    internal sealed class QuestNameRecorder
    {
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Preserves the old text file line formats while moving output ownership to Debug.</summary>
        internal void Record(NpcDiagnosticIdentity identity)
        {
            if (string.IsNullOrEmpty(identity.Name)) return;
            try
            {
                if (Core.Log.L1) Core.Log.Main(DebugModule.Instance, "Talking to NPC: \"" + identity.Name + "\"");
                if (_seen.Add(identity.Name)) AppendUnique("npc_names.txt", identity.Name);
                if (string.IsNullOrEmpty(identity.Location)) return;
                string entry = identity.Name + " @ " + identity.Location;
                if (Core.Log.L1) Core.Log.Main(DebugModule.Instance, "NPC location: " + entry + " (" + (int)identity.Distance + " m)  faction: " + identity.Faction);
                AppendUnique("npc_locations.txt", entry);
            }
            catch (Exception error) { Core.Log.Error(DebugModule.Instance, "Could not record NPC name", error); }
        }
        /// <summary>Appends only a new line; these authoring lists retain their existing additive text format.</summary>
        private static void AppendUnique(string file, string line)
        {
            string path = Path.Combine(Assets.ModuleConfigDir("Debug"), file);
            if (File.Exists(path)) foreach (string known in File.ReadAllLines(path)) if (string.Equals(known.Trim(), line, StringComparison.OrdinalIgnoreCase)) return;
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }
}
