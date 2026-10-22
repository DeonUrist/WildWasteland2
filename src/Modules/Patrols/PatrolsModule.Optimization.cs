using System;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Publishes patrol-owned protection through Core contracts instead of optimization reflection.</summary>
    public sealed partial class PatrolsModule
    {
        /// <summary>Provides cleanup protection and registers actual patrol vehicle ownership.</summary>
        public override void ProvideApis()
        {
            ModApi.Provide<ICullingProtection>(this, new PatrolCullingProtection());
            var ownership = ModApi.Get<IVehicleOwnership>();
            if (ownership != null) ownership.Register(this, new PatrolVehicleOwnership());
        }

        /// <summary>Preserves the native far-cleanup eligibility used by the former optimization bridge.</summary>
        private sealed class PatrolCullingProtection : ICullingProtection
        {
            /// <summary>Protects a rig only while the patrol module and its native cleanup interception are active.</summary>
            public bool Protected(GameObject go)
            {
                return Running && WarRig.PoiEncounter.CleanupProtectionArmed && WarRig.PoiEncounter.Protected(go);
            }

            /// <summary>Detaches a far truck's tankers only under the same native eligibility condition.</summary>
            public void BeforeFarDelete(GameObject go)
            {
                if (!Running || go == null || !WarRig.PoiEncounter.CleanupProtectionArmed) return;
                try { WarRig.PoiEncounter.DetachTankers(go); }
                catch (Exception error) { Log.Error(Instance, "Far-delete trailer cleanup", error); }
            }
        }

        /// <summary>Recognizes actual patrol-controlled roots without class-name/namespace inspection.</summary>
        private sealed class PatrolVehicleOwnership : IVehicleOwnerQuery
        {
            /// <summary>True for a root carrying a patrol marker or active pilot component.</summary>
            public bool Owns(GameObject vehicle)
            {
                return vehicle != null && (vehicle.GetComponent<PatrolMarker>() != null || vehicle.GetComponent<Pilot>() != null);
            }
        }
    }
}
