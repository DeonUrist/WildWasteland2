using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Actual native merchant stock query supplied by Loot for an authored station prefab.</summary>
    public interface IMerchantGoods { GameObject[] Goods(string stationName); }
}
