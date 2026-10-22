using System.Collections.Generic;
using BepInEx.Configuration;
using WildWasteland.Core;
namespace WildWasteland.Modules.Survival
{
    internal sealed partial class WeatherRuntime : IWeatherPreview
    {
        /// <summary>Preview creation requires the active weather feature and a loaded player.</summary>
        internal bool Presents(UnityEngine.GameObject root)
        { for (int i = 0; i < _count; i++) if (_anchors[i] != null && _anchors[i].Root == root) return true; return false; }
        /// <summary>Preview creation requires the active weather feature and a loaded player.</summary>
        public bool Available { get { return _running && GameRefs.InPlay; } }
        /// <summary>Refreshes the existing deterministic layout before reporting column count.</summary>
        public int Columns { get { UpdateLayout(); return _columnCount; } }
        /// <summary>Copies palette then generation controls in their established authoring order.</summary>
        public void Settings(List<ConfigEntryBase> into)
        { into.Clear(); WeatherPalette.Entries(into); into.Add(_mapAreas); into.Add(_density); into.Add(_width); into.Add(_depth); }
        /// <summary>Authoring interface forwards to the existing unsaved native preview path.</summary>
        void IWeatherPreview.Spawn(bool critical, bool aroundPlayer) { Spawn(critical, aroundPlayer); }
        /// <summary>Authoring clear releases runtime previews and anchors.</summary>
        void IWeatherPreview.Clear() { Clear(); }
    }
}
