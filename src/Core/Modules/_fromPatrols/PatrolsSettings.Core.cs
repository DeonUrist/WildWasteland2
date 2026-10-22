using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrols settings read by Core (AudioVoices, Hide missing-FSM warnings / QuietFsm); Wave A split of PatrolsSettings.cs.</summary>
    public sealed partial class PatrolsModule
    {
        internal static ConfigEntry<int> AudioVoices;
        internal static ConfigEntry<bool> HideFsmWarnings;

        /// <summary>Binds the tools.cfg Debug entry Hide missing-FSM warnings (drives Core QuietFsm).</summary>
        partial void BindCoreDebug(ModuleConfig cfg)
        {
            HideFsmWarnings = cfg.Tool(TDebug, "Hide missing-FSM warnings", true, "The game's \"Could not find FSM: ID on GameObject: ...\" warnings are not printed (QuietFsm)");
        }

        /// <summary>Binds the tools.cfg Audio entry AudioVoices (read by Core at startup).</summary>
        partial void BindCoreAudio(ModuleConfig cfg)
        {
            // audio (startup)
            AudioVoices = cfg.Tool(TAudio, "AudioVoices", 64,
                "How many sounds the game can play at once (Unity's real voices; the game ships with 32). A firefight with several raider cars needs " +
                "more: every shot and every hit is a sound, and beyond the limit sounds - the player's own shots too - cut out. 64 is a good value; " +
                "0 leaves the game's own setting. Applied at game start (restart the game after a change)",
                new AcceptableValueList<int>(0, 32, 48, 64, 96, 128));
        }

        /// <summary>Wires the Core QuietFsm switch to the Hide missing-FSM warnings entry (initial value + live changes).</summary>
        partial void HookCoreSettings()
        {
            QuietFsm.On = HideFsmWarnings.Value;
            HideFsmWarnings.SettingChanged += (s, e) => QuietFsm.On = HideFsmWarnings.Value;
        }
    }
}
