using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.Survival
{
    /// <summary>
    /// Debug part of the Survival entry (Wave A split, owner B03): the per-module Stew Pot log switch, which Wave B retires
    /// into the Debug VerboseLevel (pot registered / natural substitution -> level 1, drink/pour/boil traces -> level 2).
    /// </summary>
    public sealed partial class SurvivalModule
    {
        internal static ConfigEntry<bool> Verbose;

        /// <summary>Binds tools.cfg [Survival / Debug] VerboseLog (read by Runtime, Stew and NaturalLoot before logging).</summary>
        partial void BindDebug(ModuleConfig cfg)
        {
            Verbose = cfg.Tool("Debug", "VerboseLog", false, "Log Stew Pot filling, drinking, pouring, cooking and spoilage.");
        }
    }
}
