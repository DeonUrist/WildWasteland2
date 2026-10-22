using UnityEngine;
using WildWasteland.Core;
using WarRig = WildWasteland.Modules.Patrols.WarRig;   // refs-ok: Vehicles-owned namespace until the Wave C rename

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// The War Rig tanker interior as a storm shelter (README 6.6/6.8): registered with Survival's IShelterRegistry by VehiclesModule, and the
    /// geometry behind the storm protection and the ApocaDustStorm bridge. A point is inside when it lies in the tank box of any active rig.
    /// </summary>
    internal sealed class WarRigShelter : IShelterProvider
    {
        private static readonly Bounds Tank = new Bounds(new Vector3(0, .95f, -1.25f), new Vector3(4.4f, 2, 17.7f));

        /// <summary>True when the actor (or, without one, the scene point) is inside a War Rig tank.</summary>
        public bool Shelters(GameObject actor, Vector3 world)
        {
            return actor != null ? Inside(actor) : InsidePoint(Origin.Scene(world));
        }

        /// <summary>True when the actor's position lies inside the tank of any active War Rig (no rig loaded = false).</summary>
        internal static bool Inside(GameObject actor)
        {
            return actor != null && InsidePoint(actor.transform.position);
        }

        /// <summary>True when a scene position lies inside the tank box of any active War Rig (rig model space, layout offset/rotation/scale).</summary>
        internal static bool InsidePoint(Vector3 scene)
        {
            var all = WarRig.WarRigUnit.All;
            for (int i = 0; i < all.Count; i++)
            {
                var rig = all[i];
                if (rig == null || !rig.gameObject.activeInHierarchy) continue;
                Vector3 p = Quaternion.Inverse(WarRig.Layout.Rot) * (rig.transform.InverseTransformPoint(scene) - WarRig.Layout.Off) / WarRig.S.Scale;
                if (Tank.Contains(p)) return true;
            }
            return false;
        }
    }
}
