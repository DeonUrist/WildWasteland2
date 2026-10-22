using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// <see cref="IVehicleSkins"/> provider of the Presentation World part (README D10): faction paintjobs and the cargo container pictures
    /// chosen by the Loot cargo key. The vehicle body name is the car root's prefab name. Remembers the faction painted per vehicle
    /// (instance id) for <see cref="PaintOf"/>.
    /// </summary>
    internal sealed class VehicleSkinsApi : IVehicleSkins
    {
        readonly Dictionary<int, string> _painted = new Dictionary<int, string>();

        /// <summary>Paints <paramref name="vehicle"/> for <paramref name="faction"/> and lays the cargo picture for <paramref name="lootKey"/> (null = no cargo panels).</summary>
        bool IVehicleSkins.Paint(GameObject vehicle, string faction, string lootKey)
        {
            if (vehicle == null) return false;
            string body = Names.Prefab(vehicle.name);
            Paint.Apply(vehicle, body, faction);
            if (lootKey != null) Paint.ApplyCargo(vehicle, lootKey);
            if (_painted.Count > 512) Sweep();
            _painted[vehicle.GetInstanceID()] = faction ?? "Scrapyard";
            return true;
        }

        /// <summary>Restores the native look and forgets the paint key.</summary>
        public void Clear(GameObject vehicle)
        {
            if (vehicle == null) return;
            Paint.Clear(vehicle);
            _painted.Remove(vehicle.GetInstanceID());
        }

        /// <summary>True when a skin exists for the faction.</summary>
        public bool HasSkin(string faction) { return Paint.HasSkin(faction); }

        /// <summary>Faction painted on the vehicle through this API, or null (native / painted by the old Patrols path).</summary>
        public string PaintOf(GameObject vehicle)
        {
            string f;
            return vehicle != null && _painted.TryGetValue(vehicle.GetInstanceID(), out f) ? f : null;
        }

        /// <summary>Drops entries of destroyed vehicles (instance ids no longer resolvable are kept only up to the cap).</summary>
        void Sweep() { _painted.Clear(); }
    }
}
