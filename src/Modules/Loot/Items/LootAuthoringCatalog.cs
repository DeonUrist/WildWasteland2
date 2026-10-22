using System.Collections.Generic;
using WildWasteland.Core;
using WildWasteland.Modules.Loot;
namespace WildWasteland.Modules.Loot
{
    /// <summary>Copies the actual Loot-owned pickup catalog into detached authoring rows.</summary>
    internal sealed class LootAuthoringCatalog : ILootAuthoringCatalog
    {
        public void Items(List<LootAuthoringItem> into) { into.Clear(); foreach(var item in PickupCatalog.All())into.Add(new LootAuthoringItem{Id=item.Id,Name=item.Name}); }
        public string Name(string id) { return PickupCatalog.Name(id); }
    }
}
