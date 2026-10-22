using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace WildWasteland.Core
{
    /// <summary>
    /// Per-module config helper (AGENTS.md 5.7).
    /// Main  = WildWasteland/settings.cfg: gameplay settings, module switches and hotkeys, edited in the F7 menu.
    /// Tools = BepInEx\config\WildWasteland\tools.cfg (invisible to Apocasetter, edited from the F7 tool window): tuning,
    ///         offsets, debug switches, developer settings.
    /// Sections are prefixed with the module id so five modules can share one file: "Patrols", "Patrols / Combat", ...
    /// Bind order = display order in Apocasetter, so bind Enabled first (Core does), then the important settings.
    /// </summary>
    public sealed class ModuleConfig
    {
        public readonly string Prefix;
        public readonly ConfigFile Main;
        public readonly ConfigFile Tools;
        public ConfigEntry<bool> Enabled { get; internal set; }

        /// <summary>Created by ModuleHost.BindAll for each module.</summary>
        internal ModuleConfig(string prefix, ConfigFile main, ConfigFile tools) { Prefix = prefix; Main = main; Tools = tools; }

        /// <summary>Section name "&lt;Id&gt;" (sub null/"General") or "&lt;Id&gt; / &lt;sub&gt;".</summary>
        public string Section(string sub)
        {
            return string.IsNullOrEmpty(sub) || sub == "General" ? Prefix : Prefix + " / " + sub;
        }

        /// <summary>Player-facing entry in settings.cfg.</summary>
        public ConfigEntry<T> Player<T>(string sub, string key, T def, string desc)
        {
            return TunedSettings.Bind(Main, Section(sub), key, def, desc);
        }
        /// <summary>Player-facing entry in settings.cfg with a range/list.</summary>
        public ConfigEntry<T> Player<T>(string sub, string key, T def, string desc, AcceptableValueBase range)
        {
            return TunedSettings.Bind(Main, Section(sub), key, def, new ConfigDescription(desc, range));
        }
        /// <summary>Tuning/debug entry in tools.cfg.</summary>
        public ConfigEntry<T> Tool<T>(string sub, string key, T def, string desc)
        {
            return TunedSettings.Bind(Tools, Section(sub), key, def, desc);
        }
        /// <summary>Tuning/debug entry in tools.cfg with a range/list.</summary>
        public ConfigEntry<T> Tool<T>(string sub, string key, T def, string desc, AcceptableValueBase range)
        {
            return TunedSettings.Bind(Tools, Section(sub), key, def, new ConfigDescription(desc, range));
        }
        /// <summary>Rebindable gameplay/editor hotkey, edited in Debug. The menu key lives in the plugin's F6 config.</summary>
        public ConfigEntry<Key> Hotkey(string key, Key def, string desc)
        {
            return TunedSettings.Bind(Main, "Keys", key, def, desc);
        }
    }
}
