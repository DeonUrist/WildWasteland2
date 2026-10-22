using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ES3Internal;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Template-driven vehicle assembly (structure packing, headlights, parts on hinges, hinge offsets, fluids, handbrake, start-up, part conditions); Vehicles part of Patrol (Wave A split of Patrols Spawning/Patrol.cs, inventory 1a).</summary>
    internal partial class Patrol
    {
        /// <summary>Level-2 log line of the assembly.</summary>
        private static void Deep(string msg) { Log.Deep(PatrolsModule.Instance, msg); }

        /// <summary>Coroutine: packs only dormant structure for the build cache (parts, bumper, headlights); no registration, rolled state, occupants or native Awake/Start.</summary>


        // Every raider car has its headlights: each empty hinge_headlight on the frame (two same-named ones on the Junker, the one on the
        // Halfbreed's fork) gets the game's basic "Headlight" unless the template already fitted one there.
        private const string DefaultHeadlight = "Headlight";
        /// <summary>Fits the game's basic "Headlight" on every empty hinge_headlight of the frame; returns how many.</summary>


        // Headlights: the car's light switch (switch_panel/switch_lights, LightOff FSM "useDoor" = the player's click: click sound,
        // switch turned, HeadlightsON sent to the car) when it is off, and HeadlightsON / OFF straight to every Headlight FSM (the
        // headlight items and the dashboard gauges' backlight: global events HeadlightsON / HeadlightsOFF) for cars without a switch.
        /// <summary>Applies traffic light intent using the native vehicle control owner.</summary>
        internal static void Headlights(GameObject car,bool on,PlayMakerFSM[] snapshot=null) { Core.ModApi.Get<Core.IVehicleControls>()?.SetHeadlights(car,on); }

        /// <summary>World position of the driver seat (sitPos, else hinge_seat_driver, else the car).</summary>
        internal static Vector3 DriverSeatPos(GameObject car)
        {
            var sit = FindChild(car.transform, "sitPos") ?? FindChild(car.transform, "hinge_seat_driver");
            return sit != null ? sit.position : car.transform.position;
        }

        // Opens the fitted car door nearest to a seat (same side of the car, within 2.2 m): the door item's DoorOpen FSM, "useDoor"
        // = the player's click (it swings the hinge open, then hands over to DoorClose). Only door items (vehPart) count - a truck's
        // container doors are part of the frame. Returns true when a closed door started to open; false = no door there or already open.
        /// <summary>Opens the closed fitted door nearest to a seat (same side, within 2.2 m); true when one started to open.</summary>
        internal static bool OpenDoorAt(GameObject car, Vector3 seat)
        {
            if (car == null) return false;
            var ct = car.transform;
            float seatX = ct.InverseTransformPoint(seat).x;
            PlayMakerFSM best = null; float bd = 2.2f * 2.2f;
            foreach (var f in car.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f == null || f.FsmName != "DoorOpen" || !f.gameObject.CompareTag("vehPart")) continue;
                float x = ct.InverseTransformPoint(f.transform.position).x;
                if (Mathf.Abs(seatX) > 0.1f && Mathf.Abs(x) > 0.05f && Mathf.Sign(x) != Mathf.Sign(seatX)) continue;
                float d = (f.transform.position - seat).sqrMagnitude;
                if (d < bd) { bd = d; best = f; }
            }
            if (best == null || !best.enabled || best.Fsm == null || !best.Fsm.Initialized || best.ActiveStateName != "off") return false;
            best.SendEvent("useDoor");
            if (Log.L2) Deep("Door: " + best.gameObject.name + " opened on " + car.name);
            return true;
        }

        // The game's own ignition: START [Start] FSM Ignition -> Start (engine sound, RPM, IsRunning), NWH StartEngine() as a fallback.
        // Used by the build and when a saved car is revived after a load.
        /// <summary>Coroutine: the game's own ignition (START FSM Ignition -> Start), the ignition provider first, NWH StartEngine as a fallback; stops when the car became a wreck.</summary>
        internal static IEnumerator StartUp(GameObject car, bool loading = false)
        {
            if (Core.ModApi.Get<Core.IVehicleDrive>()==null) yield break;
            if (Explode.IsWreck(car) || car.GetComponent<Crew>()?.MerchantWaiting==true) yield break;
            var ignition = ModApi.Get<IVehicleIgnition>();
            if (ignition != null && ignition.TryStart(car)) { if (!loading) yield return new WaitForSeconds(1.5f); yield break; }
            var start = FindFsm(car, "START", "Start");
            if (start == null) { Log.Warn(PatrolsModule.Instance, "No START/Start FSM on " + (car != null ? car.name : "?")); yield break; }
            if (loading && !start.enabled) yield break; // an optional ignition provider may still be discovering this loaded car
            if (Log.L2) Deep("START state before: " + start.ActiveStateName);
            start.Fsm.SetState("Ignition");
            yield return null;
            yield return null;
            if (Explode.IsWreck(car) || car.GetComponent<Crew>()?.MerchantWaiting==true) yield break;   // blew up mid-start: no more ignition
            start.Fsm.SetState("Start");
            if (loading) { Core.ModApi.Get<Core.IVehicleDrive>()?.StartEngine(car); yield break; } // native physics will spin it up at handoff; no scaled-time wait while loading
            yield return new WaitForSeconds(1.5f);
            if (Explode.IsWreck(car) || car.GetComponent<Crew>()?.MerchantWaiting==true) yield break;
            bool running = Core.ModApi.Get<Core.IVehicleDrive>()?.EngineRunning(car)==true;
            if (Log.L2) Deep("START state: " + start.ActiveStateName + "  engine running: " + running);
            if (!running)
            {
                Core.ModApi.Get<Core.IVehicleDrive>()?.StartEngine(car);
                yield return new WaitForSeconds(1f);
                if (Explode.IsWreck(car)) yield break;
                if (Log.L2) Deep("After NWH StartEngine(): running=" + Core.ModApi.Get<Core.IVehicleDrive>().EngineRunning(car) + "  START state: " + start.ActiveStateName);
            }
        }

        /// <summary>Fits one part prefab (by name and kind) on each named hinge; returns how many.</summary>


        // The game's vehPart_Attach "Attach" state: destroy the item's Rigidbody, tag vehPart, layer 8,
        // parent to the hinge with local position/rotation reset. Done before the item's own FSMs Start,
        // so CheckTag/LockPhysics see an already-attached part (same as a part loaded from a save).
        /// <summary>The game's vehPart_Attach: part without rigidbody, tagged vehPart / layer 8, parented to the hinge, hinge offset replayed; registered (or recorded for the cache).</summary>


        // One part of a JSON template: the hinge is found by its path under the frame or under the part it sits on (by name
        // anywhere below as a fallback); a hinge that already holds a part (the frame came with one) keeps it, and that part
        // stands in as the parent for the parts listed on it.
        /// <summary>Fits part <paramref name="index"/> of a JSON template on its hinge (by path, name fallback) with the template's hinge/part pose.</summary>


        // The hinge's own attach (vehPart_Attach FSM, state "Attach") does more than parent + reset for some parts: a door, hood or trunk
        // hinge follows its SetParent with a SetPosition on the item (Self space, e.g. z = -0.513 on the Poloska's hinge_door_R), and may
        // set a rotation. Those actions are replayed on the part, in order, so it sits exactly where the player's click would put it.
        // Only actions aimed at the item (not at the hinge itself) count.
        /// <summary>Replays the hinge's vehPart_Attach "Attach" SetPosition/SetRotation actions aimed at the item, so the part sits where a click would put it.</summary>


        /// <summary>True when the action targets the attached item (a GameObject variable such as {Item}), not the hinge that owns the FSM.</summary>


        /// <summary>True when the hinge already holds a part.</summary>


        /// <summary>The vehPart child of a hinge, or null.</summary>


        /// <summary>Follows a slash path of child names ("name#k" = k-th same-named sibling); null when a segment is missing.</summary>


        // The tank: <car>/Fuel [LiquidAmount] Liquid / LiquidCapacity, filled to pct % of the capacity.
        /// <summary>Fills the car's Fuel tank (LiquidAmount) to <paramref name="pct"/> % of its capacity.</summary>
        private static void Fuel(GameObject car, float pct)
        {
            var fuel = FindChild(car.transform, "Fuel");
            if (fuel == null) { Log.Warn(PatrolsModule.Instance, "No Fuel child"); return; }
            if (!SetLiquid(fuel.gameObject, pct, "Fuel")) Log.Warn(PatrolsModule.Instance, "Fuel/LiquidAmount FSM not found");
        }

        // Engine oil and radiator water live on the part's "cap" child ([LiquidAmount] Liquid / LiquidCapacity, 2 l oil, 3 l water);
        // filled to a rolled % once the parts sit on their hinges.
        /// <summary>Fills engine oil and radiator water caps to a rolled percentage.</summary>
        private static void FillParts(GameObject car)
        {
            foreach (var hn in new[] { "hinge_engine", "hinge_radiator" })
            {
                var hinge = FindChild(car.transform, hn);
                if (hinge == null) continue;
                for (int i = 0; i < hinge.childCount; i++)
                {
                    var part = hinge.GetChild(i);
                    var cap = FindChild(part, "cap");
                    if (cap == null) continue;
                    SetLiquid(cap.gameObject, PatrolsModule.RollPartFill(), part.name);   // refs-ok: Patrols roll settings inside Patrols' Patrol class
                }
            }
        }

        /// <summary>Sets a LiquidAmount FSM to a percentage of its capacity; false when the holder has none.</summary>
        private static bool SetLiquid(GameObject holder, float pct, string label)
        {
            foreach (var f in holder.GetComponents<PlayMakerFSM>())
            {
                if (f.FsmName != "LiquidAmount") continue;
                var liquid = f.FsmVariables.GetFsmFloat("Liquid");
                var cap = f.FsmVariables.GetFsmFloat("LiquidCapacity");
                if (liquid == null || cap == null) { Log.Warn(PatrolsModule.Instance, label + "/LiquidAmount has no Liquid/LiquidCapacity"); return true; }
                float capacity = cap.Value > 0f ? cap.Value : 20f;
                liquid.Value = capacity * Mathf.Clamp01(pct / 100f);
                if (Log.L2) Deep(label + ": " + liquid.Value.ToString("0.0") + " / " + capacity + " (" + pct.ToString("0") + " %)");
                return true;
            }
            return false;
        }

        // The handbrake lever FSM: HandbrakeOn -> (click) over -> Sound -> HandbrakeOff (SetProperty input.Handbrake + lever rotation).
        // Entering "Sound" plays the click and flows into HandbrakeOff by itself; "Sound 2" goes back to HandbrakeOn.
        /// <summary>Applies traffic brake intent using the native vehicle control owner.</summary>
        internal static void Handbrake(GameObject car,bool on) { Core.ModApi.Get<Core.IVehicleControls>()?.SetHandbrake(car,on); }
        /// <summary>Preserves existing cached-caller shape while delegating actual native controls.</summary>
        internal static void Handbrake(GameObject car,bool on,PlayMakerFSM f) { Core.ModApi.Get<Core.IVehicleControls>()?.SetHandbrake(car,on); }

        // Parts with a Condition FSM (engine, radiator, wheels) get a rolled condition; the FSM's tiers react to the variable.
        /// <summary>Gives every Condition FSM part (engine, radiator, wheels) a rolled condition (Repair FSM kept in step).</summary>
        private static void SetPartConditions(GameObject car, PlayMakerFSM[] snapshot = null)
        {
            int n = 0;
            foreach (var f in snapshot ?? car.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f.FsmName != "Condition" || !f.Fsm.Initialized) continue;
                var v = f.FsmVariables.GetFsmFloat("Condition");
                if (v == null) continue;
                float c = PatrolsModule.RollPartHealth();   // refs-ok: Patrols roll settings inside Patrols' Patrol class
                v.Value = c;
                var repair = f.gameObject.GetComponents<PlayMakerFSM>();
                foreach (var r in repair)
                    if (r.FsmName == "Repair" && r.Fsm.Initialized) { var rv = r.FsmVariables.GetFsmFloat("Condition"); if (rv != null) rv.Value = c; }
                if (Log.L2) Deep("  " + f.gameObject.name + " condition " + c.ToString("0"));
                n++;
            }
            if (Log.L2) Deep("Part conditions set on " + n + " part(s) (" + PatrolsModule.MinPartHealth.Value + ".." + PatrolsModule.MaxPartHealth.Value + " %)");   // refs-ok: Patrols roll settings
        }
    }
}
