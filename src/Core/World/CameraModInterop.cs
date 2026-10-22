using System;
using BepInEx.Configuration;
namespace WildWasteland.Core {
    /// <summary>Actual camera types and tuning deliberately exported for third-party reflection interoperability.</summary>
    public interface ICameraModInterop { Type ResolveType(string sourceName);ConfigEntry<float> CarHeight {get;} ConfigEntry<bool> Occlusion {get;} ConfigEntry<bool> VehicleOcclusion {get;} }
}
