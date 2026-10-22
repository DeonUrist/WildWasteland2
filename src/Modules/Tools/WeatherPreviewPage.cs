using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.Tools
{
    /// <summary>Weather preview authoring surface over the active Survival provider.</summary>
    internal sealed class WeatherPreviewPage : ToolPage
    {
        readonly List<ConfigEntryBase> _entries = new List<ConfigEntryBase>();
        /// <summary>Registers the preview as a Tools-owned authoring page.</summary>
        internal WeatherPreviewPage(IModule owner) { Owner = owner; Group = ToolGroups.Debug; Title = "Weather visual preview"; IsTool = true; }
        /// <summary>Draws live palette controls and explicit unsaved preview actions.</summary>
        public override void Draw(ToolLayout l)
        {
            var api = ModApi.Get<IWeatherPreview>();
            if (api == null) { l.Text("Enable Survival weather to author previews."); return; }
            api.Settings(_entries);
            foreach (var entry in _entries)
            {
                var color = entry as ConfigEntry<Color>;
                if (color == null) { l.Entry(entry); continue; }
                Color value = color.Value; string key = color.Definition.Key;
                l.Text(key + "  #" + ColorUtility.ToHtmlStringRGB(value));
                Color next = new Color(l.Slider(key + " / Red", key + ".R", value.r, 0, 1), l.Slider(key + " / Green", key + ".G", value.g, 0, 1), l.Slider(key + " / Blue", key + ".B", value.b, 0, 1), 1);
                if (next != value) color.Value = next;
            }
            if (l.Button("Reset storm colors")) foreach (var entry in _entries) { var color = entry as ConfigEntry<Color>; if (color != null) color.Value = (Color)color.DefaultValue; }
            l.Text("Manual previews are not saved. Natural storms retain native encounter and save bookkeeping.");
            l.Value("Active storms", api.Count + " / 4"); l.Value("Hurricanes per critical storm", api.Columns.ToString());
            if (l.Button("Dust storm ahead", api.Available)) ToolWindow.CloseThen(Dust);
            if (l.Button("Critical dust storm preview ahead", api.Available)) ToolWindow.CloseThen(Critical);
            if (l.Button("Dust storm around player", api.Available)) ToolWindow.CloseThen(Around);
            if (l.Button("Clear weather previews", api.Count > 0)) api.Clear();
        }
        /// <summary>Resolves the current provider before spawning an ordinary ahead preview.</summary>
        static void Dust() { var api = ModApi.Get<IWeatherPreview>(); if (api != null) api.Spawn(false, false); }
        /// <summary>Resolves the current provider before spawning a critical ahead preview.</summary>
        static void Critical() { var api = ModApi.Get<IWeatherPreview>(); if (api != null) api.Spawn(true, false); }
        /// <summary>Resolves the current provider before spawning a local ordinary preview.</summary>
        static void Around() { var api = ModApi.Get<IWeatherPreview>(); if (api != null) api.Spawn(false, true); }
    }
}
