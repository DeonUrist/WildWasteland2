using System;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>NpcModule part owned by Presentation: Women of Wasteland config (female population, models, voices, spawn key).</summary>
    public sealed partial class NpcModule
    {
        internal static ConfigEntry<bool> EnableFemaleNpc, FemaleCrew, GungirlVoiceMatch;
        internal static ConfigEntry<int> FemalePopulation;
        internal static ConfigEntry<float> GungirlVoiceVolume, GungirlVoiceIntervalMin, GungirlVoiceIntervalMax;
        internal static ConfigEntry<string> GungirlModel, GungirlTexture, GungirlVoice, GungirlHideParts, ShootaModel, ShootaTexture, ShootaHideParts;
        internal static ConfigEntry<Key> FemaleSpawnKey;

        /// <summary>Binds one Women of Wasteland player setting; the old "General" section maps to [NPC / Women of Wasteland].</summary>
        private static ConfigEntry<T> Woman<T>(ModuleConfig cfg, string section, string key, T value, string description, AcceptableValueBase range = null)
        {
            string sub = section == "General" ? "Women of Wasteland" : section;
            return range == null ? cfg.Player(sub, key, value, description) : cfg.Player(sub, key, value, description, range);
        }

        /// <summary>Presentation part of NpcModule.BindConfig (Women of Wasteland entries and the debug spawn key); called first.</summary>
        static partial void BindWomen(ModuleConfig cfg)
        {
            EnableFemaleNpc = Woman(cfg, "General", "EnableFemaleNpc", true, "Convert a share of newly spawned Flexa and Sprokka raiders to Gungirl and Shoota. Existing females keep their identity when off.");
            FemalePopulation = Woman(cfg, "General", "FemalePopulation", 33, "Percentage of eligible new raiders and crew converted to female variants.", new AcceptableValueRange<int>(0, 100));
            FemaleCrew = Woman(cfg, "General", "FemaleCrew", true, "Apply the female population chance to new Flexa/Sprokka drivers, passengers and turret gunners. Explicit female template choices still apply when off.");
            GungirlModel = Woman(cfg, "Gungirl", "Model", "Models/Flexa_female.glb", "Gungirl model, relative to the NPC asset folder. Restart to reload.");
            GungirlTexture = Woman(cfg, "Gungirl", "Texture", "Models/flexa_female.png", "Gungirl texture, relative to the NPC asset folder. Restart to reload.");
            GungirlHideParts = Woman(cfg, "Gungirl", "HideParts", "beard, headband, armband", "Comma-separated attachment name prefixes hidden on newly dressed Gungirls.");
            ShootaModel = Woman(cfg, "Shoota", "Model", "Models/Sprokka_female.glb", "Shoota model, relative to the NPC asset folder. Restart to reload.");
            ShootaTexture = Woman(cfg, "Shoota", "Texture", "Models/sprokka_female.png", "Shoota texture, relative to the NPC asset folder. Restart to reload.");
            ShootaHideParts = Woman(cfg, "Shoota", "HideParts", "armor2, armband", "Comma-separated attachment name prefixes hidden on newly dressed Shootas.");
            GungirlVoice = Woman(cfg, "Gungirl", "Voice", "Sounds/Gungirl", "Female voice folder, relative to the NPC asset folder. WAV and OGG; OGG wins. Restart to reload.");
            GungirlVoiceMatch = Woman(cfg, "Gungirl", "VoiceMatchLoudness", true, "Match female voice loudness to the native clips.");
            GungirlVoiceVolume = Woman(cfg, "Gungirl", "VoiceVolume", 1f, "Female voice volume multiplier.", new AcceptableValueRange<float>(0f, 4f));
            GungirlVoiceIntervalMin = Woman(cfg, "Gungirl", "VoiceIntervalMin", 2f, "Shortest combat shout pause for newly dressed females, seconds.", new AcceptableValueRange<float>(0f, 60f));
            GungirlVoiceIntervalMax = Woman(cfg, "Gungirl", "VoiceIntervalMax", 8f, "Longest combat shout pause for newly dressed females, seconds.", new AcceptableValueRange<float>(0f, 60f));
            FemaleSpawnKey = cfg.Hotkey("Spawn female raider", Key.None, "Debug: spawn a Gungirl six metres ahead.");
            WomenFeature.BindPlayerAssets();
        }
    }
}
