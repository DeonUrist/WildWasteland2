using WildWasteland.Core;

namespace WildWasteland.Modules.Survival
{
    /// <summary>
    /// Loot part of the Survival entry (Wave A split, owner B15): natural loot substitution (25% of natural gas stoves become
    /// Stew Pots). Wave B turns it into a Loot substitution rule registered by Survival through ModAPI.
    /// </summary>
    public sealed partial class SurvivalModule
    {
        /// <summary>Subscribes <see cref="NaturalLoot.BeforeCreate"/> to the shared CreateObject.OnEnter prefix dispatcher.</summary>
        partial void InitLoot()
        {
            Hooks.CreateObjectEnter.Prefix(this, NaturalLoot.BeforeCreate);
        }
    }
}
