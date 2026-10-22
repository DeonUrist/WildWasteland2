using System;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Existing template editor entry points forwarding to shared authoring file operations.</summary>
    internal static partial class EditorStore
    {
        /// <summary>Publishes a complete text file through the Core atomic writer.</summary>
        [Obsolete("refactor: use Core.AuthoringFiles.AtomicWrite")]
        internal static void AtomicWrite(string path, string text) { Core.AuthoringFiles.AtomicWrite(path, text); }
        /// <summary>Finds an unused sanitized filename inside an authoring folder.</summary>
        [Obsolete("refactor: use Core.AuthoringFiles.UniquePath")]
        internal static string UniquePath(string dir, string stem, string extension) { return Core.AuthoringFiles.UniquePath(dir, stem, extension); }
        /// <summary>Replaces non-letter/digit filename characters with underscores.</summary>
        [Obsolete("refactor: use Core.AuthoringFiles.SafeName")]
        internal static string SafeName(string name) { return Core.AuthoringFiles.SafeName(name); }
        /// <summary>Rejects filenames escaping their authoring directory.</summary>
        [Obsolete("refactor: use Core.AuthoringFiles.SafePath")]
        internal static string SafePath(string dir, string name) { return Core.AuthoringFiles.SafePath(dir, name); }
    }
}
