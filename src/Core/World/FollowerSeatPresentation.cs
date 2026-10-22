using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Presentation-owned follower seat/body operations for the actual native actor.</summary>
    public interface IFollowerSeatPresentation
    {
        Component Apply(GameObject actor,Transform seat,string humanType,string armProfile);
        void Release(Component pose);
        void Animated(GameObject actor,bool enabled);
    }
}
