using System.IO;
using BepInEx;

namespace WildWasteland.Core
{
    /// <summary>
    /// File locations. Install layout (AGENTS.md 4):
    ///   BepInEx\plugins\WildWasteland\WildWasteland.dll
    ///   BepInEx\plugins\WildWasteland\theme\...            shared UI theme (F7 window, editors)
    ///   BepInEx\plugins\WildWasteland\&lt;ModuleId&gt;\...       module assets (models, sounds, textures, templates)
    ///   BepInEx\config\com.denis.apocalypter.wildwasteland.cfg   F6 (player-facing) settings
    ///   BepInEx\config\WildWasteland\tools.cfg                    F7 (technical) settings
    ///   BepInEx\config\WildWasteland\&lt;ModuleId&gt;\...          module data / sidecars
    /// </summary>
    public static class Assets
    {
        public static string Root;   // plugin folder (directory of WildWasteland.dll)
        public static string DllPath;

        public static void Init(string dllPath)
        {
            DllPath = dllPath;
            Root = Path.GetDirectoryName(dllPath);
            DiskResources.Initialize();
        }

        public static string ModuleDir(string id) { return Path.Combine(Root ?? ".", id); }
        public static string ConfigDir { get { return Path.Combine(Paths.ConfigPath, "WildWasteland"); } }
        public static string ModuleConfigDir(string id)
        {
            var d = Path.Combine(ConfigDir, id);
            try { Directory.CreateDirectory(d); } catch { }
            return d;
        }
        /// <summary>Folder of a standalone plugin this module replaces (BepInEx\plugins\&lt;name&gt;), used for one-time data migration.</summary>
        public static string LegacyPluginDir(string folder) { return Path.Combine(Paths.PluginPath, folder); }
    }
}
