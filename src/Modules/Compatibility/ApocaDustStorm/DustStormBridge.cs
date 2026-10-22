using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.Patrols.WarRig
{
    /// <summary>
    /// War Rig part of the ApocaDustStorm bridge (no DustStorms code or binary is bundled): StormShelter.ContainsActor answers true inside
    /// a War Rig's tank, and PlayerStormHazards re-checks shelter when the player crosses the tank boundary. The War Rig feature registers
    /// itself with <see cref="Register"/>; the patches belong to the Compatibility.ApocaDustStorm scope and act only while that host
    /// runs. <see cref="Inside"/> is also the War Rig shelter test of Survival/Vehicles storm protection.
    /// </summary>
    internal static class DustStormBridge
    {
        private static IModule _host;
        private static FieldInfo checkingShelter;
        private static GameObject lastPlayer;
        private static bool lastInside;
        private static readonly Bounds Tank = new Bounds(new Vector3(0, .95f, -1.25f), new Vector3(4.4f, 2, 17.7f));

        /// <summary>True when the actor stands inside the tank of an active War Rig.</summary>
        internal static bool Inside(GameObject actor)
        {
            if (actor == null) return false;
            foreach (var rig in WarRigUnit.All) // refs-ok: REFACTOR-REQUEST B17-14
            {
                if (rig == null || !rig.gameObject.activeInHierarchy) continue;
                Vector3 p = Quaternion.Inverse(Layout.Rot) * (rig.transform.InverseTransformPoint(actor.transform.position) - Layout.Off) / S.Scale; // refs-ok: REFACTOR-REQUEST B17-14
                if (Tank.Contains(p)) return true;
            }
            return false;
        }

        private static bool broken = true;   // WW: one of the two patches failed (or not installed) -> both prefixes stand down

        /// <summary>Registration by the War Rig feature (WarRigFeature.Install): remembers the host module; no patching here any more (B17).</summary>
        internal static void Install(IModule owner) { _host = owner; }

        /// <summary>True while the registering War Rig host runs.</summary>
        private static bool HostRunning { get { return _host != null && ModuleHost.IsRunning(_host); } }

        /// <summary>Resolves the shelter/hazard contract and patches it under <paramref name="scope"/> (no-op when the mod is absent).</summary>
        internal static void Install(IModule scope, BepInEx.PluginInfo info)
        {
            broken = true; checkingShelter = null; lastPlayer = null; lastInside = false;
            if (info == null) return;
            var assembly = info.Instance.GetType().Assembly;
            var shelter = assembly.GetType("ApocaDustStorm.StormShelter");
            var hazards = assembly.GetType("ApocaDustStorm.PlayerStormHazards");
            var contains = shelter != null ? AccessTools.Method(shelter, "ContainsActor", new[] { typeof(GameObject) }) : null;
            var tick = hazards != null ? AccessTools.Method(hazards, "Tick", new[] { typeof(GameObject), typeof(float), typeof(float), typeof(bool) }) : null;
            checkingShelter = hazards != null ? AccessTools.Field(hazards, "checkingShelter") : null;
            if (contains == null || contains.ReturnType != typeof(bool) || !contains.IsStatic || tick == null || !tick.IsStatic || tick.ReturnType != typeof(void) ||
                checkingShelter == null || checkingShelter.FieldType != typeof(bool) || !checkingShelter.IsStatic)
            { Log.Warn(scope, "DustStorms shelter API differs; War Rig storm compatibility was not installed."); return; }
            bool a = Core.Patches.Patch(scope, contains, prefix: AccessTools.Method(typeof(DustStormBridge), "Shelter"));
            bool b = Core.Patches.Patch(scope, tick, prefix: AccessTools.Method(typeof(DustStormBridge), "RefreshOnCrossing"));
            broken = !(a && b);
            if (!broken) Log.Main(scope, "DustStorms " + info.Metadata.Version + ": War Rig interior provides complete storm shelter.");
            else Log.Warn(scope, "DustStorms bridge unavailable (patch failed)");
        }

        /// <summary>Prefix of StormShelter.ContainsActor (per hazard check): true inside a War Rig tank.</summary>
        private static bool Shelter(GameObject __0, ref bool __result)
        { if (broken || !HostRunning || !Inside(__0)) return true; __result = true; return false; }

        /// <summary>Prefix of PlayerStormHazards.Tick: forces a shelter re-check when the player crosses the tank boundary.</summary>
        private static void RefreshOnCrossing(GameObject __0)
        {
            if (broken || !HostRunning) return;
            bool inside = Inside(__0);
            if (lastPlayer != __0 || inside != lastInside) checkingShelter.SetValue(null, false);
            lastPlayer = __0; lastInside = inside;
        }

        /// <summary>Stands both prefixes down and forgets the hazard field (the scope removes the patches).</summary>
        internal static void Shutdown() { broken = true; checkingShelter = null; lastPlayer = null; lastInside = false; }
    }
}
