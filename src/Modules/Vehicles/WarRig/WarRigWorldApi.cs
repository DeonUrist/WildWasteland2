using WildWasteland.Core;
namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Publishes the existing vehicle feature's natural-spawn switch.</summary>
    internal sealed class WarRigWorldApi:IWarRigWorld { public bool SpawnEnabled=>Running&&Patrols.WarRig.S.SpawnEnabled!=null&&Patrols.WarRig.S.SpawnEnabled.Value; private static bool Running=>VehiclesModule.Running; }
}
