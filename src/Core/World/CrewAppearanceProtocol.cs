using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Presentation-owned crew aliases and appearance transfer, independent of patrol orchestration.</summary>
    public interface ICrewAppearance
    {
        string BasePrefab(string name);
        string Identity(GameObject actor,string fallback);
        void Dress(GameObject actor,string requested,bool randomize=true);
        void Transfer(GameObject source,GameObject destination);
    }
}
