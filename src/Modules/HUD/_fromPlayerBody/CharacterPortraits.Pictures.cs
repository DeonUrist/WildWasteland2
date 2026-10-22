using System;
using System.IO;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// CharacterPortraits part (split from PlayerBody/Body/CharacterPortraits.cs in Wave A): the portrait PNGs (plugins/.../Portraits,
    /// extracted when missing, reloaded when edited) shown on the settings page and the TAB screen. Target: HUD (B10).
    /// </summary>
    internal static partial class CharacterPortraits
    {
        internal static Texture Picture(int choice) { return Pictures[Canonical(choice)]; }

        // Only on opening settings: external Photoshop replacements reload without restarting the game.
        internal static void Prepare()
        {
            string directory = PlayerBodyModule.ModPath("Portraits");
            Directory.CreateDirectory(directory);
            for (int entry = 0; entry < Order.Length; entry++)
            {
                int i = Order[entry];
                string path = Path.Combine(directory, Names[i] + ".png");
                Texture2D picture = null;
                try
                {
                    if (!File.Exists(path))
                        using (var source = WildWasteland.Core.DiskResources.Open("PlayerBody.CharacterPortraits." + Names[i] + ".png"))
                            if (source != null) using (var output = File.Create(path)) source.CopyTo(output);
                    if (!File.Exists(path)) continue;
                    var modified = File.GetLastWriteTimeUtc(path);
                    if (Pictures[i] != null && Modified[i] == modified) continue;
                    picture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "WW portrait " + Names[i], hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                    if (!ImageConversion.LoadImage(picture, File.ReadAllBytes(path), true)) throw new InvalidDataException("not a PNG");
                    if (Pictures[i] != null) UnityEngine.Object.Destroy(Pictures[i]);
                    Pictures[i] = picture; Modified[i] = modified;
                }
                catch (Exception e)
                {
                    if (picture != null) UnityEngine.Object.Destroy(picture);
                    PlayerBodyModule.Warn("Character portrait " + path + ": " + e.Message);
                }
            }
            InventoryModel.RefreshPortrait();
        }
    }
}
