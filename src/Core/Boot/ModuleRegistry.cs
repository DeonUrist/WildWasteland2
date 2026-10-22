using BepInEx.Configuration;

namespace WildWasteland.Core
{
    /// <summary>
    /// The only Core file that names module types (README 3.2 rule 2). Order = init order = config display order = Harmony subscriber
    /// order. Debug, Compatibility and Saves initialize before their consumers; remaining legacy entries stay until their target packages finish.
    /// </summary>
    internal static class ModuleRegistry
    {
        /// <summary>Adds every module to <see cref="ModuleHost"/>. Providers before consumers.</summary>
        internal static void RegisterAll()
        {
            SessionIntegrations.Register("Compatibility.Apocasaver", Modules.Compatibility.Apocasaver.ApocasaverSession.Install);
            ModuleHost.Add(new Modules.Debug.DebugModule());
            ModuleHost.Add(new Modules.Compatibility.CompatibilityModule());
            ModuleHost.Add(new Modules.Saves.SavesModule());
            ModuleHost.Add(new Modules.PlayerBody.PlayerBodyModule());
            ModuleHost.Add(new Modules.NPCAI.NPCAIModule());
            ModuleHost.Add(new Modules.Presentation.PresentationModule());
            ModuleHost.Add(new Modules.NPC.NpcModule());
            ModuleHost.Add(new Modules.Guns.GunsModule());
            ModuleHost.Add(new Modules.Gunplay.GunplayModule());
            ModuleHost.Add(new Modules.HUD.HudModule());
            ModuleHost.Add(new Modules.Patrols.PatrolsModule());
            ModuleHost.Add(new Modules.POI.PoiModule());
            ModuleHost.Add(new Modules.Quests.QuestsModule());
            ModuleHost.Add(new Modules.Survival.SurvivalModule());
            // Remaining target module entries are completed by their Wave B owners.
            ModuleHost.Add(new Modules.Vehicles.VehiclesModule());
            ModuleHost.Add(new Modules.Loot.LootModule());
            ModuleHost.Add(new Modules.Tools.ToolsModule());
            ModuleHost.Add(new Modules.Developer.DeveloperModule());
        }

        /// <summary>The Core-owned startup real-voice budget, or null before Core settings bind.</summary>
        internal static ConfigEntry<int> AudioVoicesSetting() { return CoreSettings.AudioVoices; }
    }
}
