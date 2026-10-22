using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Guns
{
    /// <summary>Gunplay compatibility entry points for canonical Core actor queries.</summary>
    internal static partial class Tracers
    {
        /// <summary>Delegates IsCorpse to the shared actor classifier.</summary>
        private static bool IsCorpse(GameObject go) { return ActorQuery.IsCorpse(go); }
        /// <summary>Delegates HasBodypart to the shared actor classifier.</summary>
        private static bool HasBodypart(GameObject go) { return ActorQuery.HasBodypart(go); }
        /// <summary>Delegates IsPlayerObj to the shared actor classifier.</summary>
        private static bool IsPlayerObj(GameObject go) { return ActorQuery.IsPlayerObj(go); }
        /// <summary>Delegates col_isHead to the shared actor classifier.</summary>
        private static bool col_isHead(GameObject go) { return ActorQuery.IsHead(go); }
        /// <summary>Delegates IsVehiclePart to the shared actor classifier.</summary>
        private static bool IsVehiclePart(GameObject go) { return ActorQuery.IsVehiclePart(go); }
        /// <summary>Delegates HealthOf to the shared actor classifier.</summary>
        private static float HealthOf(GameObject go) { return ActorQuery.HealthOf(go); }
        /// <summary>Delegates Creature to the shared actor classifier.</summary>
        private static Transform Creature(Transform transform) { return ActorQuery.Creature(transform); }
        /// <summary>Delegates RootOf to the shared actor classifier.</summary>
        private static Transform RootOf(Transform transform) { return ActorQuery.RootOf(transform); }
        /// <summary>Delegates OwningPart to the shared actor classifier.</summary>
        private static Transform OwningPart(Transform transform) { return ActorQuery.OwningPart(transform); }
    }
}
