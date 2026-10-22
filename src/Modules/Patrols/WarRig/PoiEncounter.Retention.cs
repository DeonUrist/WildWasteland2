using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    /// <summary>Owns native far-cleanup protection for patrol/War Rig objects and trailer detachment.</summary>
    internal static partial class PoiEncounter
    {
        private static readonly HashSet<GetDistance> CleanupChecks = new HashSet<GetDistance>();
        /// <summary>True once native far-cleanup distance checks have been identified for this world.</summary>
        internal static bool CleanupProtectionArmed { get { return CleanupChecks.Count > 0; } }
        /// <summary>Installs the patrol-owned native far-distance interception.</summary>
        internal static void InstallRetention(Core.IModule owner)
        { Core.Patches.Patch(owner, AccessTools.Method(typeof(GetDistance), "OnEnter"), prefix: AccessTools.Method(typeof(PoiEncounter), nameof(BeforeCleanupDistance))); }
        /// <summary>Recognizes War Rig objects whose native cleanup is suppressed.</summary>
        internal static bool Protected(GameObject go)
        {
            if (go == null) return false;
            if (go.GetComponentInParent<WarRigUnit>() != null) return true;
            var marker = go.GetComponentInParent<PatrolMarker>();
            return marker != null && marker.TemplateName == "Abandoned War Rig" && marker.SetupOnly;
        }
        /// <summary>Retains eligible rigs/town traffic and releases far tanker joints before native deletion.</summary>
        private static bool BeforeCleanupDistance(GetDistance __instance)
        {
            if (!PatrolsModule.Running || !CleanupChecks.Contains(__instance)) return true;
            var target = __instance.target != null ? __instance.target.Value : null;
            if (Protected(target) || WorldTraffic.RetainsTown(target))
            { __instance.Fsm.Event("FINISHED"); __instance.Finish(); return false; }
            var player = Core.GameRefs.Player;
            if (target != null && player != null && (target.transform.position - player.transform.position).sqrMagnitude > 5000f * 5000f)
                DetachTankers(target);
            return true;
        }
        /// <summary>Unhitches tankers connected to a truck that is about to be removed.</summary>
        internal static void DetachTankers(GameObject truck)
        {
            var body = truck.GetComponent<Rigidbody>(); if (body == null) return;
            foreach (var rig in WarRigUnit.All)
                if (rig != null) foreach (var joint in rig.GetComponents<ConfigurableJoint>())
                    if (joint.connectedBody == body)
                    {
                        UnityEngine.Object.Destroy(joint);
                        var leg = rig.transform.Find("leg"); if (leg != null) leg.gameObject.SetActive(true);
                    }
        }
    }
}
