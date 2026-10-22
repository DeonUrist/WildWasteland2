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

        /// <summary>Sets the plugin paths from the DLL location and initializes disk resources (Plugin.Awake).</summary>
        public static void Init(string dllPath)
        {
            DllPath = dllPath;
            Root = Path.GetDirectoryName(dllPath);
            DiskResources.Initialize();
        }

        /// <summary>Asset folder of a module: plugins/WildWasteland/&lt;id&gt;/ (D13: the new module id).</summary>
        public static string ModuleDir(string id) { return Path.Combine(Root ?? ".", id); }
        /// <summary>config/WildWasteland/.</summary>
        public static string ConfigDir { get { return Path.Combine(Paths.ConfigPath, "WildWasteland"); } }
        /// <summary>Data folder of a module, config/WildWasteland/&lt;id&gt;/ (created on demand).</summary>
        public static string ModuleConfigDir(string id)
        {
            var d = Path.Combine(ConfigDir, id);
            try { Directory.CreateDirectory(d); } catch { }
            return d;
        }
    }
}
