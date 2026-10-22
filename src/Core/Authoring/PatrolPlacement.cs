using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Detached native merchant placement row; runtime merchant bookkeeping stays Patrols-owned.</summary>
    public sealed class MerchantPlacement { public string Id,Label,Kind;public Transform Transform; }
    /// <summary>Actual Patrols turret placement authoring operations.</summary>
    public interface ITurretPlacement
    {
        bool InGame {get;}
        GameObject[] Cars();
        Transform Anchor(GameObject car,int slot);
        GameObject Rider(GameObject car,int slot);
        string TemplateName(GameObject car);
        string SaveAnchor(GameObject car,int slot);
        int VehicleSlots {get;}
        int TotalSlots {get;}
    }
    /// <summary>Actual merchant layout authoring operations without private merchant components.</summary>
    public interface IMerchantPlacement
    {
        GameObject[] Cars();
        MerchantPlacement[] Objects(GameObject car);
        string Save(GameObject car);
        void Freeze(GameObject car);
    }
    /// <summary>Presentation owns the bus preset and the selected edit target.</summary>
    public interface IBusPassengerAuthoring
    {
        GameObject Nearest(Vector3 scenePosition,float radius);
        void Select(GameObject actor);
        bool Editing(GameObject actor);
        Vector3 Value(int joint,int side);
        void Adjust(int joint,int side,Vector3 delta);
        string Save();
        void Reset();
    }
}
