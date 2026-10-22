using System;
using BepInEx.Configuration;
using WildWasteland.Core;
namespace WildWasteland.Modules.Presentation {
    /// <summary>Exports only the existing camera interoperability surface; optional mods never import private WW namespaces.</summary>
    internal sealed class CameraModInterop : ICameraModInterop {
        public ConfigEntry<float> CarHeight=>CameraSettings.ThirdCarHeight;
        public ConfigEntry<bool> Occlusion=>CameraSettings.OcclusionPrototype;
        public ConfigEntry<bool> VehicleOcclusion=>CameraSettings.OcclusionInVehicle;
        public Type ResolveType(string name) { switch(name) {case "Apocaplayer.ThirdPerson":return typeof(ThirdPerson);case "Apocaplayer.OcclusionCutaway":return typeof(OcclusionCutaway);case "Apocaplayer.Cave":return typeof(Cave);default:return null;} }
    }
}
