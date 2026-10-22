using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using NWH.WheelController3D;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Patrols
{
    // 2.3.0: a template's suspension adjustment (the Part Adjustment tool's wheel spacing x1..1.5 and assembly height) applied to a spawned
    // car - with or without the Part Adjustment mod. With it, the values go through its public SuspensionApi (reflection), so they live in
    // its per-car save data and the two mods never disagree. Without it, SuspensionTune below does the same geometry: the suspension
    // model is stretched on X and moved up, the lift-kit hinge moves with it, and each hinge_wheel_* mount (what NWH casts its suspension
    // from) follows the end of its axle (width) and the height. Only a car with a lift kit on hinge_suspension takes an adjustment: the
    // game's checkSuspension decides stock / lifted by that child, and the tool itself needs the kit (Denis: "no kit, no adjustment").
    /// <summary>Template suspension adjustment (Vehicles part of Suspension; the Part Adjustment API lookup is the Compatibility part).</summary>
    internal static class Suspension
    {

        /// <summary>The lift-kit hinge (hinge_suspension_parent/hinge_suspension) of a car, or null.</summary>
        internal static Transform KitHinge(GameObject car) { return car != null ? car.transform.Find("hinge_suspension_parent/hinge_suspension") : null; }

        /// <summary>True when a lift kit (vehPart) sits on the car's hinge_suspension.</summary>
        internal static bool KitFitted(GameObject car)
        {
            var h = KitHinge(car);
            if (h == null) return false;
            for (int i = 0; i < h.childCount; i++) if (h.GetChild(i).CompareTag("vehPart")) return true;
            return false;
        }

        /// <summary>Applies width / height to a car (standard 1 / 0 = nothing to do unless it was adjusted before); false without a kit / unsupported.</summary>
        internal static bool Apply(GameObject car, float width, float height, string why)
        {
            if (car == null) return false;
            width = Mathf.Clamp(width, 1f, 1.5f); height = Mathf.Clamp(height, -1f, 1f);
            bool standard = width == 1f && height == 0f;
            if (!KitFitted(car))
            {
                if (!standard) if (Log.L2) Deep("Suspension: " + car.name + " has no lift kit - its template's suspension " + width.ToString("0.00") + " / " + height.ToString("0.00") + " is ignored");
                return false;
            }
            var external=Core.ModApi.Get<Core.IExternalSuspension>();
            if (external?.CanSet==true)
            {
                try
                {
                    bool ok = external.Set(car,width,height);
                    if (Log.L2) Deep("Suspension: " + car.name + " width x" + width.ToString("0.00") + ", height " + height.ToString("+0.00;-0.00") + " m via Part Adjustment (" + why + ")" + (ok ? "" : " - refused"));
                    return ok;
                }
                catch (Exception e) { Log.Warn(VehiclesModule.Instance, "Suspension: Part Adjustment API: " + e.Message); }
            }
            var tune = SuspensionTune.Ensure(car);
            if (tune == null) { if (!standard) if (Log.L2) Deep("Suspension: " + car.name + " is not an adjustable chassis"); return false; }
            tune.Set(width, height);
            if (Log.L2) Deep("Suspension: " + car.name + " width x" + width.ToString("0.00") + ", height " + height.ToString("+0.00;-0.00") + " m (" + why + ")");
            return true;
        }

        /// <summary>The car's current adjustment (1 / 0 when standard or without a kit) - what the template exporter writes.</summary>
        internal static void Read(GameObject car, out float width, out float height)
        {
            width = 1f; height = 0f;
            if (car == null || !KitFitted(car)) return;
            var external=Core.ModApi.Get<Core.IExternalSuspension>();
            if (external?.CanGet==true)
            {
                try
                {
                    external.Get(car,out width,out height);
                    return;
                }
                catch (Exception e) { Log.Warn(VehiclesModule.Instance, "Suspension: Part Adjustment API: " + e.Message); }
            }
            var tune = car.GetComponent<SuspensionTune>();
            if (tune != null) { width = tune.Width; height = tune.Height; }
        }

        /// <summary>A one-line picture of the running gear for the log: what NWH really uses after the template's wheels went on.</summary>
        internal static string Describe(GameObject car)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var wc in car.GetComponentsInChildren<WheelController>(true))
            {
                if (wc.transform.parent != car.transform) continue;
                sb.Append(sb.Length > 0 ? ", " : "").Append(wc.name.Replace("hinge_wheel_", "")).Append(" r ").Append(wc.Radius.ToString("0.00")).Append(" w ").Append(wc.Width.ToString("0.00"))
                  .Append(" at ").Append(wc.transform.localPosition.ToString("F2")).Append(" spring ").Append(wc.SpringMaxLength.ToString("0.00"));
            }
            return sb.ToString();
        }

        // Harmony: the vanilla Suspension FSM moves a mount (stock / lifted SetPosition) when the kit goes on or comes off and after a
        // load - our own tune (no Part Adjustment) re-applies on top of the new vanilla baseline, like Part Adjustment does for itself
        // WW: Core.Hooks.SetPositionDo postfix. First line: a static counter of live SuspensionTune components (0 = no raider car
        // carries our own lift kit -> nothing to do, no string compare on the game's SetPosition actions).
        /// <summary>Old Patrols entry point; the Vehicles module installs the hook itself (<see cref="Hook"/>).</summary>
        [System.Obsolete("refactor: the Vehicles module installs the suspension hook (Suspension.Hook)")]

        /// <summary>Subscribes the SetPosition.DoSetPosition postfix (shared Core dispatcher) for the Vehicles module.</summary>
        internal static void Hook(IModule owner)
        {
            Hooks.SetPositionDo.Postfix(owner, AfterSetPosition);
        }

        /// <summary>
        /// Postfix of SetPosition.DoSetPosition (every SetPosition action in the game): when the vanilla Suspension FSM moved a wheel mount of a car
        /// carrying our SuspensionTune, the tune re-applies on the new baseline. Early-out: no live SuspensionTune (static counter), then FSM name.
        /// </summary>

        private static void AfterSetPosition(SetPosition __instance)
        {
            if (SuspensionTune.Live <= 0) return;
            try
            {
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Suspension") return;
                var owner = __instance.Fsm.Owner;
                if (owner == null || !owner.name.StartsWith("hinge_wheel_", StringComparison.Ordinal) || owner.transform.parent == null) return;
                var tune = owner.transform.parent.GetComponent<SuspensionTune>();
                if (tune != null) tune.VanillaMountMoved(owner.transform, __instance.State != null ? __instance.State.Name : "");
            }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "Suspension: after SetPosition: " + e.Message); }
        }

        /// <summary>Level-2 log line of the suspension rules.</summary>
        private static void Deep(string msg) { Log.Deep(VehiclesModule.Instance, msg); }
    }
}
