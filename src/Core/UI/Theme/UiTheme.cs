using System;
using BepInEx.Configuration;
using UnityEngine;

namespace WildWasteland.Core.UI
{
    /// <summary>Core-owned shared atlas/theme lifecycle; every UI consumer uses the same resources and settings.</summary>
    internal sealed class UiTheme : IUiTheme
    {
        private static readonly UiTheme Instance = new UiTheme();
        internal static ConfigEntry<bool> CleanFont;
        private static bool _started;
        /// <summary>Accent/font notifications for consumers of the shared theme contract.</summary>
        public event Action Changed;
        /// <summary>The active shared accent color.</summary>
        public Color Accent { get { return AccentTheme.Resolve(ThemePalette.Gold); } }
        /// <summary>The configured accent name.</summary>
        public string AccentName { get { return AccentTheme.Setting.Value; } }
        /// <summary>The native menu font, if already observed.</summary>
        public Font Font { get { return Skin.GameFont; } }
        /// <summary>The shared settings panel texture.</summary>
        public Texture2D Panel { get { return ThemeAssets.Tex("panel_menu"); } }
        /// <summary>Muted text color used by Core's settings skin.</summary>
        public Color Muted { get { return Ledger.Muted; } }
        /// <summary>Binds one copy of each shared UI setting before module settings are contributed.</summary>
        internal static void Bind(ConfigFile file)
        {
            CleanFont = TunedSettings.Bind(file, "Core / UI", "Clean font", false, "Use the forcedSquare atlas instead of the game's Helveticrap font.");
            AccentTheme.Setting = TunedSettings.Bind(file, "Core / UI", "Accent", "Yellow", new ConfigDescription("Shared text and border accent.", new AcceptableValueList<string>(AccentTheme.Names)));
        }
        /// <summary>Starts shared presentation independently of the HUD module; resource loading remains lazy.</summary>
        internal static void Init()
        {
            if (_started) return; _started = true;
            AccentTheme.Init();
            CleanFont.SettingChanged += FontChanged;
            AccentTheme.Setting.SettingChanged += Notify;
            Scheduler.Sliced(null, "Core.Accent panels", 500, AccentTheme.Step);
            ModApi.Provide<IUiTheme>(null, Instance);
        }
        /// <summary>Invalidates atlas meshes and notifies consumers after a font choice changes.</summary>
        private static void FontChanged(object sender, EventArgs args) { GlyphText.RefreshFont(); Notify(sender, args); }
        /// <summary>Notifies theme consumers after a shared style change.</summary>
        private static void Notify(object sender, EventArgs args)
        {
            var changed = Instance.Changed;
            if (changed != null) changed();
        }
        /// <summary>Releases Core theme textures and callbacks once at plugin shutdown.</summary>
        internal static void Shutdown()
        {
            if (!_started) return; _started = false;
            CleanFont.SettingChanged -= FontChanged;
            AccentTheme.Setting.SettingChanged -= Notify;
            AccentTheme.Clear(); GlyphFont.Reset(); ThemeAssets.Dispose();
            ModApi.Withdraw<IUiTheme>(null);
        }
    }
}
