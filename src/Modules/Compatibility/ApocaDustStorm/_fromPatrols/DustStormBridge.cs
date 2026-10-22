using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    // Independent compatibility patch. No DustStorms code or binary is bundled.
    internal static class DustStormBridge
    {
        private static FieldInfo checkingShelter;
        private static GameObject lastPlayer;
        private static bool lastInside;
        private static readonly Bounds Tank = new Bounds(new Vector3(0, .95f, -1.25f), new Vector3(4.4f, 2, 17.7f));

        internal static bool Inside(GameObject actor)
        {
            if (actor == null) return false;
            foreach (var rig in WarRigUnit.All)
            {
                if (rig == null || !rig.gameObject.activeInHierarchy) continue;
                Vector3 p = Quaternion.Inverse(Layout.Rot) * (rig.transform.InverseTransformPoint(actor.transform.position) - Layout.Off) / S.Scale;
                if (Tank.Contains(p)) return true;
            }
            return false;
        }

        private static bool broken;   // WW: one of the two patches failed -> both prefixes stand down (was: unpatch both)
        internal static void Install(Core.IModule owner)
        {
            broken = false; checkingShelter = null; lastPlayer = null; lastInside = false;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("local.apocalypter.duststorm", out info) || info.Instance == null) return;
            var assembly = info.Instance.GetType().Assembly;
            var shelter = assembly.GetType("ApocaDustStorm.StormShelter");
            var hazards = assembly.GetType("ApocaDustStorm.PlayerStormHazards");
            var contains = shelter != null ? AccessTools.Method(shelter, "ContainsActor", new[] { typeof(GameObject) }) : null;
            var tick = hazards != null ? AccessTools.Method(hazards, "Tick", new[] { typeof(GameObject), typeof(float), typeof(float), typeof(bool) }) : null;
            checkingShelter = hazards != null ? AccessTools.Field(hazards, "checkingShelter") : null;
            if (contains == null || contains.ReturnType != typeof(bool) || !contains.IsStatic || tick == null || !tick.IsStatic || tick.ReturnType != typeof(void) ||
                checkingShelter == null || checkingShelter.FieldType != typeof(bool) || !checkingShelter.IsStatic)
            { Core.Log.Warn("[Patrols] " + "DustStorms shelter API differs; War Rig storm compatibility was not installed."); return; }
            bool a = Core.Patches.Patch(owner, contains, prefix: AccessTools.Method(typeof(DustStormBridge), "Shelter"));
            bool b = Core.Patches.Patch(owner, tick, prefix: AccessTools.Method(typeof(DustStormBridge), "RefreshOnCrossing"));
            if (a && b) Core.Log.Info("[Patrols] " + "DustStorms " + info.Metadata.Version + ": War Rig interior provides complete storm shelter.");
            else { broken = true; Core.Log.Warn("[Patrols] " + "DustStorms bridge unavailable (patch failed)"); }
        }

        private static bool Shelter(GameObject __0, ref bool __result)
        { if (broken || !Inside(__0)) return true; __result = true; return false; }

        private static void RefreshOnCrossing(GameObject __0)
        {
            if (broken) return;
            bool inside = Inside(__0);
            if (lastPlayer != __0 || inside != lastInside) checkingShelter.SetValue(null, false);
            lastPlayer = __0; lastInside = inside;
        }
    }
}
