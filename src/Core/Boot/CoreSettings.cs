using BepInEx.Configuration;

namespace WildWasteland.Core
{
    /// <summary>Settings whose behavior belongs to the kernel rather than a gameplay module.</summary>
    public static class CoreSettings
    {
        public static ConfigEntry<int> AudioVoices { get; private set; }
        /// <summary>Binds the startup real-voice budget before modules bind their own settings.</summary>
        internal static void Bind(ConfigFile tools)
        {
            AudioVoices = TunedSettings.Bind(tools, "Core / Audio", "AudioVoices", 64,
                new ConfigDescription("Unity real sound voices. 0 preserves the native value; applied on game start.",
                    new AcceptableValueList<int>(0, 32, 48, 64, 96, 128)));
        }
    }
}
