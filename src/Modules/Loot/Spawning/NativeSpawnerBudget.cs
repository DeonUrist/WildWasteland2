namespace WildWasteland.Modules.Loot
{
    /// <summary>
    /// Native count budget of one ItemSpawner group (<see cref="NativeSpawners.Capture"/>): authored numberEnd roll range and the
    /// group kind. Kind: 0 critter/crate, 1 enemies, 2 common loot, 3 vehicles/trailers, 4 other (shopkeepers, sale stock),
    /// 5 unique loot (authored flow), 6 boss (disabled on town clones).
    /// </summary>
    internal struct NativeSpawnerBudget
    {
        /// <summary>Inclusive numberEnd range of the native roll.</summary>
        internal int Min, Max;
        internal int[] Counts;
        /// <summary>Group kind (see the type summary).</summary>
        internal int Kind;
    }
}
