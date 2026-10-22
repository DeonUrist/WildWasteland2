using System;
using System.IO;
using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// Installs the packaged default pose profiles (weapon-poses.txt, aim-lift.txt; source src/Modules/Presentation/Embedded/DefaultProfiles)
    /// into config/WildWasteland/Presentation/ when an installation does not have them yet. Existing (authored) files always win.
    /// Replaces the Presentation part of Core TunedSettings.InstallProfiles("PlayerBody") (D13).
    /// </summary>
    internal static class DefaultProfiles
    {
        const string Prefix = "Presentation.DefaultProfiles.";

        /// <summary>Copies every missing packaged profile into <paramref name="dataDir"/> (missing-only; a failure is logged, never thrown).</summary>
        internal static void Install(string dataDir)
        {
            var names = DiskResources.Names;
            if (names == null) return;
            foreach (string resource in names)
            {
                if (!resource.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                string path = Path.Combine(dataDir, resource.Substring(Prefix.Length));
                if (File.Exists(path)) continue;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var input = DiskResources.Open(resource))
                        if (input != null) using (var output = File.Create(path)) input.CopyTo(output);
                }
                catch (Exception e) { Log.Warn(PresentationModule.Instance, "default profile " + path + ": " + e.Message); }
            }
        }
    }
}
