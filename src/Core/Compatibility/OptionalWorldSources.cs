using System.Collections.Generic;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Optional inventory custody; false leaves the previous equipment snapshot intact during an external transaction.</summary>
    public interface ILogicalEquipmentInventory { bool Read(GameObject[] items, ref int drawn); }
    /// <summary>Optional authored item display names; each read returns detached data.</summary>
    public interface IExternalItemNames { Dictionary<string,string> Read(); }
    /// <summary>External vehicle factories discovered by Compatibility, separate from Vehicles' own registrations.</summary>
    public interface IExternalVehicleTemplates { int Generation { get; } GameObject Template(string name); }
    /// <summary>Optional native suspension API; false presence lets Vehicles use its own geometry.</summary>
    public interface IExternalSuspension { bool CanSet { get; } bool CanGet { get; } bool Set(GameObject car,float width,float height); bool Get(GameObject car,out float width,out float height); }
    /// <summary>Optional motorcycle template with the shared external prefab identity.</summary>
    public interface IExternalMotorcycle { GameObject Template(); }
    /// <summary>Vehicles prepares an externally built motorcycle without leaking its private rear-access component.</summary>
    public interface IMotorcyclePreparation { void Prepare(GameObject car); }
    /// <summary>Optional map ownership detected by Compatibility before HUD starts.</summary>
    public interface IExternalMapPolicy { bool FogOwned { get; } }
    /// <summary>Optional external storm exposure in scene coordinates, matching the third-party model API.</summary>
    public interface IExternalStormField { bool Available { get; } bool Active { get; } bool Inside(Vector3 scene); }
}
