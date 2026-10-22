using BepInEx.Configuration;

namespace WildWasteland.Core
{
    /// <summary>
    /// The only Core file that names module types (README 3.2 rule 2). Order = init order = config display order = Harmony subscriber
    /// order. W0: the existing modules keep their order; the new skeleton modules follow (final order in core-modapi.md 5 is set in
    /// Wave C when the old entries are retired).
    /// </summary>
    internal static class ModuleRegistry
    {
        /// <summary>Adds every module to <see cref="ModuleHost"/>. Providers before consumers.</summary>
        internal static void RegisterAll()
        {
            ModuleHost.Add(new Modules.PlayerBody.PlayerBodyModule());
            ModuleHost.Add(new Modules.NPC.NpcModule());
            ModuleHost.Add(new Modules.Guns.GunsModule());
            ModuleHost.Add(new Modules.HUD.HudModule());
            ModuleHost.Add(new Modules.CleanHUD.CleanHudModule());
            ModuleHost.Add(new Modules.Patrols.PatrolsModule());
            ModuleHost.Add(new Modules.POI.PoiModule());
            ModuleHost.Add(new Modules.Weather.WeatherModule());
            ModuleHost.Add(new Modules.Quests.QuestsModule());
            ModuleHost.Add(new Modules.EngineSimCompat.EngineSimCompatModule());
            ModuleHost.Add(new Modules.Survival.SurvivalModule());
            ModuleHost.Add(new Modules.Towns.TownsModule());
            // W0 skeletons (empty until Wave B)
            ModuleHost.Add(new Modules.Debug.DebugModule());
            ModuleHost.Add(new Modules.Compatibility.CompatibilityModule());
            ModuleHost.Add(new Modules.Saves.SavesModule());
            ModuleHost.Add(new Modules.Presentation.PresentationModule());
            ModuleHost.Add(new Modules.NPCAI.NPCAIModule());
            ModuleHost.Add(new Modules.Gunplay.GunplayModule());
            ModuleHost.Add(new Modules.Vehicles.VehiclesModule());
            ModuleHost.Add(new Modules.Loot.LootModule());
            ModuleHost.Add(new Modules.Tools.ToolsModule());
            ModuleHost.Add(new Modules.Developer.DeveloperModule());
        }

        /// <summary>The audio real-voice count setting (today bound by Patrols as [Patrols] AudioVoices; Core audio setting in Wave B), or null.</summary>
        internal static ConfigEntry<int> AudioVoicesSetting() { return Modules.Patrols.PatrolsModule.AudioVoices; }
    }
}
