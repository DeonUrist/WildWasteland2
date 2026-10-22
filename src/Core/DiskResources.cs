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
        internal static string FilePath(string name)
        {
            string path;
            if (!Paths.TryGetValue(name, out path)) throw new FileNotFoundException("Runtime asset is not listed in asset-index.tsv: " + name);
            return path;
        }
        internal static Stream Open(string name)
        {
            string path; if (!Paths.TryGetValue(name, out path)) return null;
            try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16384); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }
    }
}
