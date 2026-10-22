using System;
using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Working legacy settings accessors retained until Patrols consumers move to Core settings.</summary>
    public sealed partial class PatrolsModule
    {
        /// <summary>Core owns the startup audio voice setting.</summary>
        [Obsolete("refactor: use CoreSettings.AudioVoices")]
        internal static ConfigEntry<int> AudioVoices { get { return CoreSettings.AudioVoices; } }
        /// <summary>Core logging policy owns native warning visibility; there is no Patrols setting to bind.</summary>
        partial void BindCoreDebug(ModuleConfig cfg) { }
        /// <summary>Audio settings are bound by Core before module configuration.</summary>
        partial void BindCoreAudio(ModuleConfig cfg) { }
        /// <summary>Native warning visibility reads the current Debug level directly.</summary>
        partial void HookCoreSettings() { }
    }
}
