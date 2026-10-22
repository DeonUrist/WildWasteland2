using System;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Resolves the actual Loot provider at each operation, including module toggles.</summary>
    internal static class SpawnerControl
    {
        internal static INativeSpawners Required { get { var api=ModApi.Get<INativeSpawners>(); if(api==null) throw new InvalidOperationException("POI spawning requires the enabled Loot module."); return api; } }
    }
}
