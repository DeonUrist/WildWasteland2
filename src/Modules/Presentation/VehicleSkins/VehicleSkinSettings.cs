using System.IO;
using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// Config and folder of the faction vehicle skins (custom paintjobs and cargo container pictures). Bound by <see cref="PresentationModule"/>
    /// (World part) in settings.cfg <c>[Presentation / Vehicle skins]</c>; textures live in <c>plugins/WildWasteland/Presentation/VehicleSkins/</c> (D13).
    /// </summary>
    internal static class VehicleSkinSettings
    {
        internal const string Section = "Vehicle skins", Folder = "VehicleSkins";
        internal static ConfigEntry<bool> CustomPaintjobs;

        /// <summary>Binds the paintjob switch (default from the captured <c>[Patrols] Custom paintjobs</c>).</summary>
        internal static void Bind(ModuleConfig cfg)
        {
            CustomPaintjobs = PresentationModule.WorldPlayer(cfg, Section, "Patrols", "Custom paintjobs", true,
                "Faction paintjobs on the mod's cars and pictures of the load on cargo containers (PNG files in plugins/WildWasteland/Presentation/VehicleSkins). Off = the game's textures; nothing is preloaded.");
        }

        /// <summary>
        /// Texture folder: <c>plugins/WildWasteland/Presentation/VehicleSkins</c>. While the release still installs the textures in the old
        /// <c>Patrols/Textures</c> folder (and the new one does not exist) that folder is used with a warning.
        /// </summary>
        internal static string TextureDir()
        {
            string dir = Path.Combine(Assets.ModuleDir("Presentation"), Folder);
            if (Directory.Exists(dir)) return dir;
            string old = Path.Combine(Assets.ModuleDir("Patrols"), "Textures");
            if (!Directory.Exists(old)) return dir;
            Log.Warn(PresentationModule.WorldOwner, "Vehicle skins: textures found only in the old folder " + old + "; move them to " + dir);
            return old;
        }
    }
}
