using System.Collections.Generic;
using ES3Internal;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    /// <summary>War Rig shelter query of the storm protection (the player inside a rig; no rig loaded = nothing sheltered); Vehicles part, a Survival IShelterRegistry registration in Wave B (Wave A split of Patrols WarRig/NativeStormProtection.cs).</summary>
    internal static partial class NativeStormProtection
    {
        internal static bool Protected(GameObject target)
        {
            var actor = Player;
            return actor != null && target != null && (target == actor || target.transform.IsChildOf(actor.transform)) && DustStormBridge.Inside(actor);
        }

        private static bool NoRigs { get { return WarRigUnit.All.Count == 0; } }
    }
}
