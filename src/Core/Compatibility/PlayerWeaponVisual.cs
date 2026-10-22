using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Presentation-owned carrier and visibility queries for optional held-model shadow adapters.</summary>
    public interface IPlayerWeaponVisual
    {
        bool TryGet(object body,out GameObject root,out GameObject carrier);
        bool Visible(object body);
    }
}
