using System;
using System.Collections.Generic;
using System.IO;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    internal static class QuestContent
    {
        /// <summary>GUID of the standalone Apocalypter Quests plugin (ConflictsWith: WW replaces it).</summary>
        internal const string LegacyGuid = "local.apocalypterquests";

        /// <summary>
        /// Installs the packaged default content (quests/factions defaults and the voice recordings listed in voice-files.txt) into
        /// destination when a file is missing; yields each file written. Consumed during engine start, before authored data is parsed.
        /// (D12: the import from the standalone ApocalypterQuests folders and the Apocasetter disabled cache was removed.)
        /// </summary>
        internal static IEnumerable<string> Import(string destination)
        {
            var assembly = typeof(QuestContent).Assembly;
            const string prefix = "Quests.Defaults.";
            foreach (string resource in WildWasteland.Core.DiskResources.Names)
            {
                if (!resource.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string target = Path.Combine(destination, resource.Substring(prefix.Length));
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(destination);
                using (var input = WildWasteland.Core.DiskResources.Open(resource))
                using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write)) input.CopyTo(output);
                yield return target;
            }
            using (var index = WildWasteland.Core.DiskResources.Open("Quests.voice-files.txt"))
            {
                if (index == null) yield break;
                using (var reader = new StreamReader(index))
                {
                    string relative;
                    while ((relative = reader.ReadLine()) != null)
                    {
                        string target = Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(target)) continue;
                        using (var input = WildWasteland.Core.DiskResources.Open("Quests." + relative.Replace('/', '.')))
                        {
                            if (input == null) continue;
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write)) input.CopyTo(output);
                        }
                        yield return target;
                    }
                }
            }
        }
    }
}
