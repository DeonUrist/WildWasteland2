using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Defers new materialization around the actual WW autosave transaction rather than a retired standalone plugin.</summary>
    internal partial class Convoy
    {
        /// <summary>True during a save or while a positive autosave deadline falls inside the requested spawn-free interval.</summary>
        internal static bool AutosaveSoon(float within)
        {
            var saves = ModApi.Get<ISaves>();
            if (saves == null) return SaveBus.Busy;
            float remaining = saves.AutosaveDueIn;
            return saves.Busy || remaining > 0f && remaining < within;
        }
    }
}
