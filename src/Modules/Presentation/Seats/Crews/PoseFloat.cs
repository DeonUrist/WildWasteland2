using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>One float seat-pose setting, always backed by an F7 tools.cfg entry (the old in-memory "PoseConfiguration = false" path is gone).</summary>
    internal class PoseFloat
    {
        internal readonly ConfigEntry<float> Entry;
        /// <summary>Current value of the entry.</summary>
        internal float Value { get { return Entry.Value; } }
        /// <summary>Wraps a bound entry.</summary>
        private PoseFloat(ConfigEntry<float> entry) { Entry = entry; }
        /// <summary>Binds <paramref name="key"/> in <c>[Presentation / sub]</c> (tools.cfg), its default taken from the captured old section when present.</summary>
        internal static PoseFloat Create(ModuleConfig config, string sub, string oldSection, string key, float value, string description, AcceptableValueRange<float> range)
        {
            return new PoseFloat(PresentationModule.WorldTool(config, sub, oldSection, key, value, description, range));
        }
    }
}
