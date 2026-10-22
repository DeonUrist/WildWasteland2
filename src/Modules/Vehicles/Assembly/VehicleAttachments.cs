using System.Linq;
using UnityEngine;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Free attachables (plates, spikes, blast lances): items whose CheckBool FSM carries an "Attached" flag instead of the vehPart tag.</summary>
    internal static class VehicleAttachments
    {
        /// <summary>The item's CheckBool FSM with an Attached bool, or null.</summary>
        private static PlayMakerFSM Flag(GameObject go)
        {
            return go.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "CheckBool" && f.FsmVariables.GetFsmBool("Attached") != null);
        }
        /// <summary>True for a fitted part: vehPart tag, or an attachable with its Attached flag set.</summary>
        internal static bool IsPart(Transform t)
        {
            if (t.CompareTag("vehPart")) return true;
            var flag = Flag(t.gameObject); return flag != null && flag.FsmVariables.GetFsmBool("Attached").Value;
        }
        /// <summary>True for an attachable that may sit anywhere (not only on a hinge).</summary>
        internal static bool IsFreeAttachment(GameObject go) { return Flag(go) != null; }
        /// <summary>Marks an attachable as attached (CheckBool and saveItemVar); false for a normal part (caller tags it vehPart).</summary>
        internal static bool Prepare(GameObject part)
        {
            var flag = Flag(part); if (flag == null) return false;
            flag.FsmVariables.GetFsmBool("Attached").Value = true;
            var save = part.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "saveItemVar");
            var attached = save != null ? save.FsmVariables.GetFsmBool("Attached") : null;
            if (attached != null) attached.Value = true;
            // Blastlances keep their native weapon tag/layer, explosion triggers and pickup behavior.
            return true;
        }
    }
}
