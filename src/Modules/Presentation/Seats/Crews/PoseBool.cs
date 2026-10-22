using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>One boolean seat-pose setting backed by an F7 tools.cfg entry.</summary>
    internal class PoseBool
    {
        internal readonly ConfigEntry<bool> Entry;
        /// <summary>Current value of the entry.</summary>
        internal bool Value { get { return Entry.Value; } }
        /// <summary>Wraps a bound entry.</summary>
        private PoseBool(ConfigEntry<bool> entry) { Entry = entry; }
        /// <summary>Binds <paramref name="key"/> in <c>[Presentation / sub]</c> (tools.cfg), its default taken from the captured old section when present.</summary>
        internal static PoseBool Create(ModuleConfig config, string sub, string oldSection, string key, bool value, string description)
        {
            return new PoseBool(PresentationModule.WorldTool(config, sub, oldSection, key, value, description));
        }
    }
}
