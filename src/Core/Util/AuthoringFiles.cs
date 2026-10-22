using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WildWasteland.Core
{
    /// <summary>Patrols editor file helpers (atomic text write, safe/unique paths); generic file utilities for Core (B01, Wave B).</summary>
    public static class AuthoringFiles
    {
        public static void AtomicWrite(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + ".tmp";
            File.WriteAllText(temp, text);
            WildWasteland.Core.AtomicFile.Replace(temp, path);   // D17: no Mono File.Replace (Win32 error 1175)
        }
        public static string UniquePath(string dir, string stem, string extension)
        {
            stem = SafeName(stem);
            if (stem.Length == 0) throw new ArgumentException("Empty filename");
            string path = SafePath(dir, stem + extension);
            for (int i = 1; File.Exists(path); i++) path = SafePath(dir, stem + "_" + i + extension);
            return path;
        }
        public static string SafeName(string name) { return new string((name ?? "").Trim().Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray()); }
        public static string SafePath(string dir, string name)
        {
            string root = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, name));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(name) != name) throw new ArgumentException("Path outside template folder");
            return path;
        }
    }
}
