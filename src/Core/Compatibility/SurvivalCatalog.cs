using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Survival-owned prefab access for optional catalog integrations, available only while Survival runs.</summary>
    public interface ISurvivalCatalog
    {
        /// <summary>Prepared native-save-compatible stew pot, constructed lazily when a catalog needs it.</summary>
        GameObject StewPot { get; }
    }
}
