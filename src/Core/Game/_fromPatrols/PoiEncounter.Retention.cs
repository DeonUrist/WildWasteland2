using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    /// <summary>Far-object cleanup retention (the native DeleteFarAwayItems GetDistance checks skip War Rigs, POI rigs and retained town traffic; far tankers are unhitched); Core far-cleanup retention service in Wave B (Wave A split of Patrols WarRig/PoiEncounter.cs). Names are read by reflection from Performance PatrolsBridge.</summary>
    internal static partial class PoiEncounter
    {
        private static readonly HashSet<GetDistance> CleanupChecks = new HashSet<GetDistance>();
        internal static void InstallRetention(Core.IModule owner)
        { Core.Patches.Patch(owner, AccessTools.Method(typeof(GetDistance), "OnEnter"), prefix: AccessTools.Method(typeof(PoiEncounter), nameof(BeforeCleanupDistance))); }
        internal static bool Protected(GameObject go)
        {
            if (go == null) return false;
            if (go.GetComponentInParent<WarRigUnit>() != null) return true;
            var marker = go.GetComponentInParent<PatrolMarker>();
            return marker != null && marker.TemplateName == "Abandoned War Rig" && marker.SetupOnly;
        }
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
