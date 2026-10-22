using System;
using System.IO;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    // First-run data migration (PORTING.md 1): user data that lived next to the standalone DLLs is copied into
    // BepInEx\plugins\WildWasteland\Patrols\ when it is missing there. Nothing is ever overwritten or deleted; the old folders stay as
    // they are (the standalone mods must be removed for the module to run, their data is only read).
    //   plugins\Apocapatrol\  BaseTemplates\ PlayerTemplates\ OldTemplatesFolder\ CarTemplates\ Textures\ Sounds\ PatrolsAndLoot.json
    //                         MerchantLayouts.json BusPassengerPose.json(.bak) + the editor's one-time upgrade markers
    //   plugins\WarRig\       WarRig.glb WarRig_colliders.glb Defaults\  -> Patrols\WarRig\
    //   plugins\Apocapatrol\theme\ -> WildWasteland\theme\ (the shared editor theme, only files that are missing)
    // Save sidecars and the road cache are not copied: they are read from the old BepInEx\config\Apocapatrol locations as a fallback.
    public sealed partial class PatrolsModule
    {
        static readonly string[] LegacyFolders = { "BaseTemplates", "PlayerTemplates", "OldTemplatesFolder", "CarTemplates", "Textures", "Sounds" };
        static readonly string[] LegacyFiles = { "PatrolsAndLoot.json", "MerchantLayouts.json", "BusPassengerPose.json", "BusPassengerPose.json.bak",
            "legacy-templates-imported.txt", ".coyote-traffic-v1", ".merchant-traffic-v1" };
        static readonly string[] WarRigFiles = { "WarRig.glb", "WarRig_colliders.glb" };

        void MigrateData()
        {
            int copied = 0;
            try
            {
                string old = Assets.LegacyPluginDir("Apocapatrol");
                if (Directory.Exists(old))
                {
                    foreach (var f in LegacyFolders) copied += CopyMissing(Path.Combine(old, f), Path.Combine(DataDir, f));
                    foreach (var f in LegacyFiles) copied += CopyFile(Path.Combine(old, f), Path.Combine(DataDir, f));
                    if (Assets.Root != null) copied += CopyMissing(Path.Combine(old, "theme"), Path.Combine(Assets.Root, "theme"));
                }
                string rig = Assets.LegacyPluginDir("WarRig"), rigDir = Path.Combine(DataDir, "WarRig");
                if (Directory.Exists(rig))
                {
                    foreach (var f in WarRigFiles) copied += CopyFile(Path.Combine(rig, f), Path.Combine(rigDir, f));
                    copied += CopyMissing(Path.Combine(rig, "Defaults"), Path.Combine(rigDir, "Defaults"));
                }
            }
            catch (Exception e) { Log.Warn("[Patrols] data migration: " + e.Message); }
            if (copied > 0) Log.Always("[Patrols] copied " + copied + " file(s) of the standalone Apocapatrol / War Rig data into " + DataDir);
        }

        static int CopyMissing(string from, string to)
        {
            if (!Directory.Exists(from)) return 0;
            int n = 0;
            foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                n += CopyFile(file, Path.Combine(to, file.Substring(from.Length).TrimStart('\\', '/')));
            return n;
        }

        static int CopyFile(string from, string to)
        {
            try
            {
                if (!File.Exists(from) || File.Exists(to)) return 0;
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(from, to, false);
                return 1;
            }
            catch (Exception e) { Log.Warn("[Patrols] could not copy " + from + ": " + e.Message); return 0; }
        }
    }
}
