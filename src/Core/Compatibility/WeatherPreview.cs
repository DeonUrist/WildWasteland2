using System.Collections.Generic;
using BepInEx.Configuration;
namespace WildWasteland.Core
{
    /// <summary>Explicit weather authoring controls; gameplay weather ownership remains in Survival.</summary>
    public interface IWeatherPreview
    {
        /// <summary>True when a preview can be spawned in the loaded world.</summary>
        bool Available { get; }
        /// <summary>Number of currently presented storms.</summary>
        int Count { get; }
        /// <summary>Number of hurricane columns in the current authored layout.</summary>
        int Columns { get; }
        /// <summary>Copies live palette and generation settings for the authoring page.</summary>
        void Settings(List<ConfigEntryBase> into);
        /// <summary>Spawns an unsaved ordinary or critical preview ahead of or around the player.</summary>
        void Spawn(bool critical, bool aroundPlayer);
        /// <summary>Releases currently presented storms and preview anchors.</summary>
        void Clear();
    }
}
