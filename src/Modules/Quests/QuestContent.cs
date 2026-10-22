using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    internal static class QuestContent
    {
        internal const string LegacyGuid = "local.apocalypterquests";

        // The module will consume this iterator during initialization, before parsing authored data.
        // Import user files first; packaged defaults only fill the holes. Never copy a legacy DLL.
        internal static IEnumerable<string> Import(string destination)
        {
            foreach (string source in LegacyRoots())
            {
                if (!Directory.Exists(source)) continue;
                foreach (string file in Directory.GetFiles(source, "*.txt"))
                {
                    string target = Path.Combine(destination, Path.GetFileName(file));
                    if (CopyMissing(file, target)) yield return target;
                }
                foreach (string file in Directory.GetFiles(source, "*.txt.example"))
                {
                    string target = Path.Combine(destination, Path.GetFileName(file));
                    if (CopyMissing(file, target)) yield return target;
                }
                string voices = Path.Combine(source, "Voice Actors");
                foreach (string file in CopyTree(voices, Path.Combine(destination, "Voice Actors"))) yield return file;
            }

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

        static IEnumerable<string> LegacyRoots()
        {
            PluginInfo info;
            string loaded = Chainloader.PluginInfos.TryGetValue(LegacyGuid, out info) ? Path.GetDirectoryName(info.Location) : null;
            if (!string.IsNullOrEmpty(loaded)) yield return loaded;
            string active = Assets.LegacyPluginDir("ApocalypterQuests");
            if (!string.Equals(active, loaded, StringComparison.OrdinalIgnoreCase)) yield return active;

            // Apocasetter's disabled manifest records "plugins\\ApocalypterQuests" relative to this GUID root.
            // The legacy active-folder helper cannot discover this installation.
            string disabled = Path.Combine(Paths.BepInExRootPath, "cache", "Apocasetter", "disabled", LegacyGuid);
            if (File.Exists(Path.Combine(disabled, "manifest.json"))) yield return Path.Combine(disabled, "plugins", "ApocalypterQuests");
        }

        static IEnumerable<string> CopyTree(string source, string destination)
        {
            if (!Directory.Exists(source)) yield break;
            foreach (string file in Directory.GetFiles(source))
            {
                string target = Path.Combine(destination, Path.GetFileName(file));
                if (CopyMissing(file, target)) yield return target;
            }
            foreach (string child in Directory.GetDirectories(source))
                foreach (string file in CopyTree(child, Path.Combine(destination, Path.GetFileName(child)))) yield return file;
        }

        static bool CopyMissing(string source, string target)
        {
            if (File.Exists(target)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, false);
            return true;
        }
    }
}
