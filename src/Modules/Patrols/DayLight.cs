using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Temporary caller facade over Survival's shared daylight query.</summary>
    internal static class DayLight
    {
        /// <summary>Uses the active environment provider or the native game clock.</summary>
        internal static bool Dark { get { var day = ModApi.Get<IDayLight>(); return day != null ? day.Night : Clock.Ready && (Clock.GameHours % 24 < 6 || Clock.GameHours % 24 >= 19.5); } }
        /// <summary>Retained caller reset; the provider owns native scene reference invalidation.</summary>
        internal static void Reset() { }
    }
}
