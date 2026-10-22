using System;
using System.IO;
using UnityEngine;

namespace WildWasteland.Modules.HUD
{
    /// <summary>
    /// CharacterPortraits part (split from PlayerBody/Body/CharacterPortraits.cs in Wave A): the portrait PNGs (plugins/.../Portraits,
    /// extracted when missing, reloaded when edited) shown on the settings page and the TAB screen. Target: HUD (B10).
    /// </summary>
    internal sealed class CharacterPortraitBank : WildWasteland.Core.ICharacterPortraits
    {
        private static readonly string[] Names = { "Female", "Male", "Merchant", "Bodyguard", "Sprokka", "Pistoleer", "Mechanic", "OrganicMechanic", "Gungirl", "Shoota" };
        private static readonly int[] Order = { 1, 2, 3, 4, 7, 0, 8, 9 };
        private static readonly Texture2D[] Pictures = new Texture2D[Names.Length];
        private static readonly DateTime[] Modified = new DateTime[Names.Length];
        /// <summary>Releases editable portrait textures when the HUD owner stops.</summary>
        internal void Dispose() { for (int i = 0; i < Pictures.Length; i++) { if (Pictures[i] != null) UnityEngine.Object.Destroy(Pictures[i]); Pictures[i] = null; Modified[i] = default(DateTime); } }
        /// <summary>Returns the loaded portrait for a valid authored character choice.</summary>
        public Texture Picture(int choice) { return choice >= 0 && choice < Pictures.Length ? Pictures[choice] : null; }

        /// <summary>Reloads editable character PNGs only when their file timestamps change.</summary>
        public void Prepare()
        {
            string directory = Path.Combine(WildWasteland.Core.Assets.ModuleDir("HUD"), "Portraits");
            Directory.CreateDirectory(directory);
            for (int entry = 0; entry < Order.Length; entry++)
            {
                int i = Order[entry];
                string path = Path.Combine(directory, Names[i] + ".png");
                Texture2D picture = null;
                try
                {
                    if (!File.Exists(path))
                        using (var source = WildWasteland.Core.DiskResources.Open("HUD.Portraits." + Names[i] + ".png"))
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
                    HudModule.Warn("Character portrait " + path + ": " + e.Message);
                }
            }
            InventoryModel.RefreshPortrait();
        }
    }
}
