namespace WildWasteland.Modules.Survival
{
    /// <summary>
    /// Old Loot part of the Survival entry (Wave A split). Empty since B15: the natural Stew Pot substitution is owned by the Loot
    /// module (LootSubstitutions, subscribed by LootModule.Init). Wave C deletes this file and the partial declaration.
    /// </summary>
    public sealed partial class SurvivalModule
    {
        /// <summary>Empty (moved to Loot); kept so the partial declaration in SurvivalModule.cs stays implemented until Wave C.</summary>
        partial void InitLoot() { }
    }
}
