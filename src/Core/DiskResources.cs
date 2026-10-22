using System;
using System.Collections.Generic;
using System.IO;

namespace WildWasteland.Core
{
    // One startup index replaces assembly resources. Decoded textures/models and module data keep their existing caches.
    internal static class DiskResources
    {
        private static readonly Dictionary<string, string> Paths = new Dictionary<string, string>(StringComparer.Ordinal);
        internal static string[] Names { get; private set; }
        /// <summary>Loads the packaged resource-name to relative-path index once for external asset lookup.</summary>
        internal static void Initialize()
        {
            Paths.Clear();
            string root = Path.GetFullPath(Assets.Root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string index = Path.Combine(root, "asset-index.tsv");
            if (!File.Exists(index)) throw new FileNotFoundException("Wild Wasteland runtime assets are missing. Install the asset folders and asset-index.tsv beside the DLL.", index);
            var names = new List<string>();
            foreach (string line in File.ReadAllLines(index))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0 || tab == line.Length - 1) throw new InvalidDataException("Invalid asset index row");
                string name = line.Substring(0, tab), relative = line.Substring(tab + 1).Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                if (Path.IsPathRooted(relative)) throw new InvalidDataException("Absolute runtime asset path: " + name);
                string path = Path.GetFullPath(Path.Combine(root, relative));
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Runtime asset outside plugin folder: " + name);
                Paths.Add(name, path); names.Add(name);
            }
            Names = names.ToArray();
        }
        /// <summary>Resolves the finite historical resource identifiers without old on-disk folder fallbacks.</summary>
        private static string Canonical(string name)
        {
            if(Paths.ContainsKey(name))return name;
            string[,] prefixes={
                {"CleanHUD.DefaultSettings.","Core.DefaultSettings."},{"CleanHUD.DefaultProfiles.PlayerBody.","Presentation.DefaultProfiles."},{"CleanHUD.DefaultProfiles.CleanHUD.","HUD.DefaultProfiles.HUD."},
                {"NPC.NavMaps.","NPCAI.NavMaps."},{"NPC.PoiNav.","NPCAI.PoiNav."},{"NPC.WomenOfWasteland.","Presentation."},
                {"NPC.Models.","Presentation.Models."},{"NPC.Sounds.","Presentation.Sounds."},
                {"PlayerBody.CharacterPortraits.","Presentation.Portraits."},{"PlayerBody.Textures.female_kick.png","Presentation.Models.Player2_female_kick.png"},
                {"PlayerBody.","Presentation."},{"Guns.","Gunplay."},{"CleanHUD.","HUD."},{"Towns.","POI."},{"Weather.","Survival."},
                {"Patrols.Textures.","Presentation.VehicleSkins."},{"Patrols.WarRig.","Vehicles.WarRig."},{"Patrols.Sounds.","Presentation.Sounds."}
            };
            for(int i=0;i<prefixes.GetLength(0);i++)if(name.StartsWith(prefixes[i,0],StringComparison.Ordinal))return prefixes[i,1]+name.Substring(prefixes[i,0].Length);
            return name;
        }
        /// <summary>Resolves an indexed asset to its installed path, or returns null when absent.</summary>
        internal static string FilePath(string name)
        {
            string path;
            if (!Paths.TryGetValue(Canonical(name), out path)) throw new FileNotFoundException("Runtime asset is not listed in asset-index.tsv: " + name);
            return path;
        }
        /// <summary>Opens an indexed disk asset or the matching assembly resource.</summary>
        internal static Stream Open(string name)
        {
            string path; if (!Paths.TryGetValue(Canonical(name), out path)) return null;
            try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16384); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }
    }
}
