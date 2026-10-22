using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    /// <summary>Owns native far-cleanup protection for patrol/War Rig objects and trailer detachment.</summary>
    internal static class PatrolRetention
    {
        private static readonly HashSet<GetDistance> CleanupChecks = new HashSet<GetDistance>();
        /// <summary>True once native far-cleanup distance checks have been identified for this world.</summary>
        internal static bool CleanupProtectionArmed { get { return CleanupChecks.Count > 0; } }
        /// <summary>Captures native far-cleanup checks for this world without coupling to a POI encounter lifecycle.</summary>
        internal static void Prepare() {CleanupChecks.Clear();var cleanup=Core.FsmUtil.Find(Core.GameRefs.GameManager,"DeleteFarAwayItems");if(cleanup!=null)foreach(var state in cleanup.FsmStates)foreach(var action in state.Actions)if(action is GetDistance distance)CleanupChecks.Add(distance);}
        /// <summary>Releases native action identities at scene or module teardown.</summary>
        internal static void Clear() {CleanupChecks.Clear();}
        /// <summary>Installs the patrol-owned native far-distance interception.</summary>
        internal static void InstallRetention(Core.IModule owner)
        { Core.Patches.Patch(owner, AccessTools.Method(typeof(GetDistance), "OnEnter"), prefix: AccessTools.Method(typeof(PatrolRetention), nameof(BeforeCleanupDistance))); }
        /// <summary>Recognizes War Rig objects whose native cleanup is suppressed.</summary>
        internal static bool Protected(GameObject go)
        {
            if (go == null) return false;
            if (Core.ModApi.Get<Core.IVehicleAttachments>()?.IsWarRig(go)==true) return true;
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
            Core.ModApi.Get<Core.IVehicleAttachments>()?.DetachTankers(truck);
        }
    }
}
