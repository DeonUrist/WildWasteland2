using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Weather
{
    internal static class WeatherPalette
    {
        private static ConfigEntry<Color> _wall, _main, _hurricane, _sand;
        private static ConfigEntry<float> _darkening;
        internal static int Revision;
        internal static Color Wall { get { return _wall.Value; } }
        internal static Color Main { get { return _main.Value; } }
        internal static Color Hurricane { get { return _hurricane.Value; } }
        internal static Color Sand { get { return _sand.Value; } }
        internal static float Darkening { get { return _darkening.Value; } }
        internal static void Bind(ModuleConfig cfg)
        {
            _wall = cfg.Player("Colors","External wall color",(Color)new Color32(184,154,125,255),"Color of the incoming front and its cloud bank. Live RGB controls are in Developer > Weather > Weather visual preview.");
            _main = cfg.Player("Colors","Main tint color",(Color)new Color32(128,89,54,255),"Interior haze and sky/lighting tint. Applies live; does not recolor UI or the independent vanilla sand effect.");
            _hurricane = cfg.Player("Colors","Hurricane color",(Color)new Color32(163,144,127,255),"Independent color of the dense rotating hurricane shell. Does not fill its central eye.");
            _sand = cfg.Player("Colors","Vanilla sand effect color",(Color)new Color32(87,47,12,255),"Independent tint of the game's local sand/cloud particles, with live RGB controls.");
            _darkening = cfg.Player("Storms","Storm darkening",.75f,"Extra world dimming with depth inside a storm. 0 = none; 1 = complete darkness in the deep interior. HUD stays readable.",new AcceptableValueRange<float>(0,1));
            _wall.SettingChanged += Changed; _main.SettingChanged += Changed; _hurricane.SettingChanged += Changed; _sand.SettingChanged += Changed;
        }
        private static void Changed(object sender, System.EventArgs args) { Revision++; }
        internal static void Draw(ToolLayout l)
        {
            l.Heading("Storm colors");
            DrawColor(l,_wall); DrawColor(l,_main); DrawColor(l,_hurricane); DrawColor(l,_sand);
            if (l.Button("Reset storm colors")) { _wall.Value = (Color)_wall.DefaultValue; _main.Value = (Color)_main.DefaultValue; _hurricane.Value = (Color)_hurricane.DefaultValue; _sand.Value = (Color)_sand.DefaultValue; }
            l.Entry(_darkening);
        }
        private static void DrawColor(ToolLayout l, ConfigEntry<Color> entry)
        {
            Color value = entry.Value;
            l.Text(entry.Definition.Key+"  #"+ColorUtility.ToHtmlStringRGB(value));
            Color next = new Color(l.Slider(entry.Definition.Key+" / Red",entry.Definition.Key+".R",value.r,0,1),
                l.Slider(entry.Definition.Key+" / Green",entry.Definition.Key+".G",value.g,0,1),
                l.Slider(entry.Definition.Key+" / Blue",entry.Definition.Key+".B",value.b,0,1),1);
            if (next != value) entry.Value = next;
        }
    }
}
