namespace WildWasteland.Core
{
    /// <summary>One item row of a car/truck loot profile in PatrolsAndLoot.json: native prefab name and spawn chance in percent
    /// (above 100 = guaranteed copies plus the remainder as a chance, rolled by the Loot table provider).</summary>
    public sealed class LootItem
    {
        /// <summary>Native item prefab name.</summary>
        public string Id = "";
        /// <summary>Chance in percent (0..100000).</summary>
        public float Chance;
    }
}
