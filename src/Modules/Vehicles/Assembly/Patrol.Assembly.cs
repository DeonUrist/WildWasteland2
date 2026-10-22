using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ES3Internal;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Template-driven vehicle assembly (structure packing, headlights, parts on hinges, hinge offsets, fluids, handbrake, start-up, part conditions); Vehicles part of Patrol (Wave A split of Patrols Spawning/Patrol.cs, inventory 1a).</summary>
    internal partial class Patrol
    {
        // Build only dormant structure. No registration, rolled state, occupants or native Awake/Start in the cache.
        internal static IEnumerator PackStructure(GameObject car, CarTemplate tpl, string bumper, bool trailer)
        {
            if (tpl.Parts != null)
            {
                var placed = new GameObject[tpl.Parts.Length];
                for (int i = 0; i < placed.Length; i++)
                { placed[i] = AttachTemplatePart(car, tpl, i, placed, true); yield return null; }
            }
            else
            {
                string rear = tpl.RearWheel.Length > 0 ? tpl.RearWheel : tpl.Wheel;
                string[] hinges = { "hinge_wheel_FL", "hinge_wheel_FR", "hinge_wheel_RL", "hinge_wheel_RR", "hinge_engine", "hinge_radiator", "hinge_steeringwheel", "hinge_exhaust", "hinge_seat_driver", "hinge_seat_passenger" };
                string[] parts = { tpl.Wheel, tpl.Wheel, rear, rear, tpl.Engine, tpl.Radiator, tpl.SteeringWheel, tpl.Exhaust, tpl.Seat, tpl.PassengerSeat };
                string[] kinds = { "wheel", "wheel", "wheel", "wheel", "engine", "radiator", "steeringwheel", "exhaust", "seat", "seat" };
                for (int i = 0; i < hinges.Length; i++)
                { AttachAll(car, new[] { hinges[i] }, parts[i], kinds[i], true); yield return null; }
            }
            if (trailer) yield break;
            var front = FindChild(car.transform, "hinge_bumper_front");
            if (front != null && !HasPart(front) && bumper.Length > 0) AttachAll(car, new[] { "hinge_bumper_front" }, bumper, "bumper", true);
            yield return null;
            AttachMissingHeadlights(car, true);
        }

        // Every raider car has its headlights: each empty hinge_headlight on the frame (two same-named ones on the Junker, the one on the
        // Halfbreed's fork) gets the game's basic "Headlight" unless the template already fitted one there.
        private const string DefaultHeadlight = "Headlight";
        private static int AttachMissingHeadlights(GameObject car, bool precache = false)
        {
            var empty = new List<Transform>();
            foreach (var t in car.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "hinge_headlight") continue;
                bool onPart = false, full = false;
                for (var a = t.parent; a != null && a != car.transform; a = a.parent) if (a.CompareTag("vehPart")) { onPart = true; break; }
                for (int i = 0; i < t.childCount; i++) if (t.GetChild(i).CompareTag("vehPart")) { full = true; break; }
                if (!onPart && !full) empty.Add(t);
            }
            if (empty.Count == 0) return 0;
            var prefab = Prefabs.Find(DefaultHeadlight, "headlight");
            if (prefab == null) { Core.Log.Warn("[Patrols] " + "Headlight prefab not found: " + DefaultHeadlight); return 0; }
            foreach (var h in empty) Attach(prefab, h, precache);
            PatrolsModule.Verbose("Headlights: " + empty.Count + " fitted on " + car.name);
            return empty.Count;
        }

        // Headlights: the car's light switch (switch_panel/switch_lights, LightOff FSM "useDoor" = the player's click: click sound,
        // switch turned, HeadlightsON sent to the car) when it is off, and HeadlightsON / OFF straight to every Headlight FSM (the
        // headlight items and the dashboard gauges' backlight: global events HeadlightsON / HeadlightsOFF) for cars without a switch.
        internal static void Headlights(GameObject car, bool on, PlayMakerFSM[] snapshot = null)
        {
            if (car == null) return;
            int n = 0;
            foreach (var f in snapshot ?? car.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f == null || f.Fsm == null || !f.Fsm.Initialized) continue;
                if (f.FsmName == (on ? "LightOff" : "LightOn") && f.enabled && f.gameObject.name == "switch_lights" && f.ActiveStateName == "idle")
                    f.SendEvent("useDoor");
                else if (f.FsmName == "Headlight") { f.SendEvent(on ? "HeadlightsON" : "HeadlightsOFF"); n++; }
            }
            PatrolsModule.Verbose("Lights " + (on ? "on" : "off") + ": " + car.name + " (" + n + " light FSM(s))");
        }

        internal static Vector3 DriverSeatPos(GameObject car)
        {
            var sit = FindChild(car.transform, "sitPos") ?? FindChild(car.transform, "hinge_seat_driver");
            return sit != null ? sit.position : car.transform.position;
        }

        // Opens the fitted car door nearest to a seat (same side of the car, within 2.2 m): the door item's DoorOpen FSM, "useDoor"
        // = the player's click (it swings the hinge open, then hands over to DoorClose). Only door items (vehPart) count - a truck's
        // container doors are part of the frame. Returns true when a closed door started to open; false = no door there or already open.
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
            PatrolsModule.Verbose("Door: " + best.gameObject.name + " opened on " + car.name);
            return true;
        }

        // The game's own ignition: START [Start] FSM Ignition -> Start (engine sound, RPM, IsRunning), NWH StartEngine() as a fallback.
        // Used by the build and when a saved car is revived after a load.
        internal static IEnumerator StartUp(GameObject car, bool loading = false)
        {
            if (Explode.IsWreck(car) || car.GetComponent<Crew>()?.MerchantWaiting==true) yield break;
            if (Core.Services.VehicleIgnition != null && Core.Services.VehicleIgnition.TryStart(car)) { if (!loading) yield return new WaitForSeconds(1.5f); yield break; }
            var start = FindFsm(car, "START", "Start");
            if (start == null) { Core.Log.Warn("[Patrols] " + "No START/Start FSM on " + (car != null ? car.name : "?")); yield break; }
            if (loading && !start.enabled) yield break; // an optional ignition provider may still be discovering this loaded car
            PatrolsModule.Verbose("START state before: " + start.ActiveStateName);
            start.Fsm.SetState("Ignition");
            yield return null;
            yield return null;
            if (Explode.IsWreck(car) || car.GetComponent<Crew>()?.MerchantWaiting==true) yield break;   // blew up mid-start: no more ignition
            start.Fsm.SetState("Start");
            if (loading) { Nwh.StartEngine(car); yield break; } // native physics will spin it up at handoff; no scaled-time wait while loading
            yield return new WaitForSeconds(1.5f);
            if (Explode.IsWreck(car) || car.GetComponent<Crew>()?.MerchantWaiting==true) yield break;
            bool running = Nwh.EngineRunning(car);
            PatrolsModule.Verbose("START state: " + start.ActiveStateName + "  engine running: " + running);
            if (!running)
            {
                Nwh.StartEngine(car);
                yield return new WaitForSeconds(1f);
                if (Explode.IsWreck(car)) yield break;
                PatrolsModule.Verbose("After NWH StartEngine(): running=" + Nwh.EngineRunning(car) + "  START state: " + start.ActiveStateName);
            }
        }

        private static int AttachAll(GameObject car, string[] hingeNames, string partQuery, string kind, bool precache = false)
        {
            if (string.IsNullOrEmpty(partQuery)) return 0;
            // bumpers carry no ID FSM, so they are not in the item catalog: look them up as plain asset roots
            var prefab = kind == "bumper" ? Prefabs.FindAny(partQuery) : Prefabs.Find(partQuery, kind);
            if (prefab == null) { Core.Log.Warn("[Patrols] " + kind + " prefab not found: " + partQuery); return 0; }
            int n = 0;
            foreach (var hn in hingeNames)
            {
                var hinge = FindChild(car.transform, hn);
                if (hinge == null) { Core.Log.Warn("[Patrols] " + "Hinge " + hn + " not found on " + car.name); continue; }
                Attach(prefab, hinge, precache);
                n++;
            }
            return n;
        }

        // The game's vehPart_Attach "Attach" state: destroy the item's Rigidbody, tag vehPart, layer 8,
        // parent to the hinge with local position/rotation reset. Done before the item's own FSMs Start,
        // so CheckTag/LockPhysics see an already-attached part (same as a part loaded from a save).
        private static GameObject Attach(GameObject prefab, Transform hinge, bool precache = false)
        {
            var part = precache ? VehicleBuildCache.ClonePart(prefab, hinge) : UnityEngine.Object.Instantiate(prefab, hinge.position, hinge.rotation);
            part.SetActive(true);
            var rb = part.GetComponent<Rigidbody>();
            if (rb != null) UnityEngine.Object.DestroyImmediate(rb);
            if (!VehicleAttachments.Prepare(part))
            {
                try { part.tag = "vehPart"; } catch (Exception e) { Core.Log.Warn("[Patrols] " + "tag vehPart: " + e.Message); }
                part.layer = 8;
            }
            part.transform.SetParent(hinge, true);
            part.transform.localPosition = Vector3.zero;
            part.transform.localRotation = Quaternion.identity;
            HingeOffset(hinge, part.transform);
            if (precache) { part.name = prefab.name; VehicleBuildCache.Record(part, prefab.name); }
            else { Register.Name(part, prefab.name); Register.Add(part, false); }
            PatrolsModule.Verbose("  " + part.name + " -> " + hinge.name);
            return part;
        }

        // One part of a JSON template: the hinge is found by its path under the frame or under the part it sits on (by name
        // anywhere below as a fallback); a hinge that already holds a part (the frame came with one) keeps it, and that part
        // stands in as the parent for the parts listed on it.
        internal static GameObject AttachTemplatePart(GameObject car, CarTemplate tpl, int index, GameObject[] placed, bool precache = false)
        {
            var p = tpl.Parts[index];
            if (string.IsNullOrEmpty(p.prefab)) return null;
            Transform root = car.transform;
            if (p.parent >= 0)
            {
                var host = placed[p.parent];
                if (host == null) { Core.Log.Warn("[Patrols] " + tpl.Name + ": part [" + index + "] " + p.prefab + " skipped, its parent part [" + p.parent + "] is missing"); return null; }
                root = host.transform;
            }
            var hinge = FindPath(root, p.hinge);
            if (hinge == null && p.hinge.Length > 0)
            {
                int slash = p.hinge.LastIndexOf('/');
                string last = slash >= 0 ? p.hinge.Substring(slash + 1) : p.hinge;
                int hash = last.LastIndexOf('#');
                hinge = FindChild(root, hash > 0 ? last.Substring(0, hash) : last);
            }
            if (hinge == null) { Core.Log.Warn("[Patrols] " + tpl.Name + ": hinge " + p.hinge + " not found on " + root.name + " for " + p.prefab); return null; }
            // only a real hinge holds one part; colliders and parts carry any number of attachables (plates, spikes)
            var prefab = Prefabs.Exact(p.prefab);
            if (prefab == null) { Core.Log.Warn("[Patrols] " + tpl.Name + ": part prefab not found: " + p.prefab); return null; }
            bool realHinge = hinge.name.StartsWith("hinge", StringComparison.OrdinalIgnoreCase) && !VehicleAttachments.IsFreeAttachment(prefab);
            var existing = realHinge ? PartOn(hinge) : null;
            if (existing != null) { PatrolsModule.Verbose("  " + hinge.name + " already holds " + existing.name + ", " + p.prefab + " not added"); return existing; }
            if (p.hingePos != null && p.hingePos.Length == 3) hinge.localPosition = new Vector3(p.hingePos[0], p.hingePos[1], p.hingePos[2]);
            if (p.hingeRot != null && p.hingeRot.Length == 3) hinge.localEulerAngles = new Vector3(p.hingeRot[0], p.hingeRot[1], p.hingeRot[2]);
            var part = Attach(prefab, hinge, precache);
            if (p.pos != null && p.pos.Length == 3) part.transform.localPosition = new Vector3(p.pos[0], p.pos[1], p.pos[2]);
            if (p.rot != null && p.rot.Length == 3) part.transform.localEulerAngles = new Vector3(p.rot[0], p.rot[1], p.rot[2]);
            return part;
        }

        // The hinge's own attach (vehPart_Attach FSM, state "Attach") does more than parent + reset for some parts: a door, hood or trunk
        // hinge follows its SetParent with a SetPosition on the item (Self space, e.g. z = -0.513 on the Poloska's hinge_door_R), and may
        // set a rotation. Those actions are replayed on the part, in order, so it sits exactly where the player's click would put it.
        // Only actions aimed at the item (not at the hinge itself) count.
        private static void HingeOffset(Transform hinge, Transform part)
        {
            try
            {
                PlayMakerFSM fsm = null;
                foreach (var f in hinge.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "vehPart_Attach") { fsm = f; break; }
                if (fsm == null || fsm.Fsm == null) return;
                FsmState state = null;
                foreach (var st in fsm.Fsm.States) if (st.Name == "Attach") { state = st; break; }
                if (state == null) return;
                var actions = state.Actions;
                if (actions == null || actions.Length == 0) { state.LoadActions(); actions = state.Actions; }
                if (actions == null) return;
                bool moved = false;
                foreach (var a in actions)
                {
                    var sp = a as HutongGames.PlayMaker.Actions.SetPosition;
                    if (sp != null && OnItem(sp.gameObject))
                    {
                        bool local = sp.space == Space.Self;
                        Vector3 v = sp.vector != null && !sp.vector.IsNone ? sp.vector.Value : (local ? part.localPosition : part.position);
                        if (sp.x != null && !sp.x.IsNone) v.x = sp.x.Value;
                        if (sp.y != null && !sp.y.IsNone) v.y = sp.y.Value;
                        if (sp.z != null && !sp.z.IsNone) v.z = sp.z.Value;
                        if (local) part.localPosition = v; else part.position = v;
                        moved = true;
                        continue;
                    }
                    var sr = a as HutongGames.PlayMaker.Actions.SetRotation;
                    if (sr != null && OnItem(sr.gameObject))
                    {
                        bool local = sr.space == Space.Self;
                        Vector3 e;
                        if (sr.quaternion != null && !sr.quaternion.IsNone) e = sr.quaternion.Value.eulerAngles;
                        else if (sr.vector != null && !sr.vector.IsNone) e = sr.vector.Value;
                        else e = local ? part.localEulerAngles : part.eulerAngles;
                        if (sr.xAngle != null && !sr.xAngle.IsNone) e.x = sr.xAngle.Value;
                        if (sr.yAngle != null && !sr.yAngle.IsNone) e.y = sr.yAngle.Value;
                        if (sr.zAngle != null && !sr.zAngle.IsNone) e.z = sr.zAngle.Value;
                        if (local) part.localEulerAngles = e; else part.eulerAngles = e;
                        moved = true;
                    }
                }
                if (moved) PatrolsModule.Verbose("  " + part.name + " placed like the game's attach on " + hinge.name + ": local " + part.localPosition.ToString("F3") + " / " + part.localEulerAngles.ToString("F1"));
            }
            catch (Exception e) { Core.Log.Warn("[Patrols] " + "Hinge offset on " + hinge.name + ": " + e.Message); }
        }

        // the action targets the attached item (a GameObject variable such as {Item}), not the hinge that owns the FSM
        private static bool OnItem(FsmOwnerDefault go)
        {
            return go != null && go.OwnerOption != OwnerDefaultOption.UseOwner;
        }

        private static bool HasPart(Transform hinge) { return PartOn(hinge) != null; }

        private static GameObject PartOn(Transform hinge)
        {
            for (int i = 0; i < hinge.childCount; i++)
            {
                var c = hinge.GetChild(i);
                bool tagged;
                try { tagged = c.CompareTag("vehPart"); } catch (Exception) { tagged = false; }
                if (tagged) return c.gameObject;
            }
            return null;
        }

        internal static Transform FindPath(Transform root, string path)
        {
            if (root == null) return null;
            if (string.IsNullOrEmpty(path)) return root;
            var t = root;
            foreach (var raw in path.Split('/'))
            {
                string seg = raw; int want = 0;   // "name#k" = the k-th (0-based) of several same-named siblings (Apocatemplater writes it)
                int hash = raw.LastIndexOf('#');
                if (hash > 0 && int.TryParse(raw.Substring(hash + 1), out want)) seg = raw.Substring(0, hash); else want = 0;
                Transform next = null; int seen = 0;
                for (int i = 0; i < t.childCount; i++)
                    if (t.GetChild(i).name == seg) { if (seen == want) { next = t.GetChild(i); break; } seen++; }
                if (next == null) return null;
                t = next;
            }
            return t;
        }

        // The tank: <car>/Fuel [LiquidAmount] Liquid / LiquidCapacity, filled to pct % of the capacity.
        private static void Fuel(GameObject car, float pct)
        {
            var fuel = FindChild(car.transform, "Fuel");
            if (fuel == null) { Core.Log.Warn("[Patrols] " + "No Fuel child"); return; }
            if (!SetLiquid(fuel.gameObject, pct, "Fuel")) Core.Log.Warn("[Patrols] " + "Fuel/LiquidAmount FSM not found");
        }

        // Engine oil and radiator water live on the part's "cap" child ([LiquidAmount] Liquid / LiquidCapacity, 2 l oil, 3 l water);
        // filled to a rolled % once the parts sit on their hinges.
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
                    SetLiquid(cap.gameObject, PatrolsModule.RollPartFill(), part.name);
                }
            }
        }

        private static bool SetLiquid(GameObject holder, float pct, string label)
        {
            foreach (var f in holder.GetComponents<PlayMakerFSM>())
            {
                if (f.FsmName != "LiquidAmount") continue;
                var liquid = f.FsmVariables.GetFsmFloat("Liquid");
                var cap = f.FsmVariables.GetFsmFloat("LiquidCapacity");
                if (liquid == null || cap == null) { Core.Log.Warn("[Patrols] " + label + "/LiquidAmount has no Liquid/LiquidCapacity"); return true; }
                float capacity = cap.Value > 0f ? cap.Value : 20f;
                liquid.Value = capacity * Mathf.Clamp01(pct / 100f);
                PatrolsModule.Verbose(label + ": " + liquid.Value.ToString("0.0") + " / " + capacity + " (" + pct.ToString("0") + " %)");
                return true;
            }
            return false;
        }

        // The handbrake lever FSM: HandbrakeOn -> (click) over -> Sound -> HandbrakeOff (SetProperty input.Handbrake + lever rotation).
        // Entering "Sound" plays the click and flows into HandbrakeOff by itself; "Sound 2" goes back to HandbrakeOn.
        internal static void Handbrake(GameObject car, bool on) { Handbrake(car, on, FindFsm(car, "handbrake", "Handbrake")); }
        // f = the car's handbrake/Handbrake FSM, cached by the caller (null = none)
        internal static void Handbrake(GameObject car, bool on, PlayMakerFSM f)
        {
            if (f == null) { Core.Log.Warn("[Patrols] " + "No handbrake/Handbrake FSM; setting NWH input.Handbrake only"); Nwh.SetHandbrake(car, on ? 1f : 0f); return; }
            string cur = f.Fsm.Initialized ? f.ActiveStateName : "";
            bool isOn = cur == "HandbrakeOn" || cur == "over" || cur == "Sound 2" || cur == "";
            if (on == isOn) { if (Core.Log.Verbose) PatrolsModule.Verbose("Handbrake already " + (on ? "on" : "off") + " (" + cur + ")"); return; }
            f.Fsm.SetState(on ? "Sound 2" : "Sound");
            if (Core.Log.Verbose) PatrolsModule.Verbose("Handbrake " + (on ? "on" : "released") + " (was " + cur + ")");
        }

        // Parts with a Condition FSM (engine, radiator, wheels) get a rolled condition; the FSM's tiers react to the variable.
        private static void SetPartConditions(GameObject car, PlayMakerFSM[] snapshot = null)
        {
            int n = 0;
            foreach (var f in snapshot ?? car.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f.FsmName != "Condition" || !f.Fsm.Initialized) continue;
                var v = f.FsmVariables.GetFsmFloat("Condition");
                if (v == null) continue;
                float c = PatrolsModule.RollPartHealth();
                v.Value = c;
                var repair = f.gameObject.GetComponents<PlayMakerFSM>();
                foreach (var r in repair)
                    if (r.FsmName == "Repair" && r.Fsm.Initialized) { var rv = r.FsmVariables.GetFsmFloat("Condition"); if (rv != null) rv.Value = c; }
                if (Core.Log.Verbose) PatrolsModule.Verbose("  " + f.gameObject.name + " condition " + c.ToString("0"));
                n++;
            }
            PatrolsModule.Verbose("Part conditions set on " + n + " part(s) (" + PatrolsModule.MinPartHealth.Value + ".." + PatrolsModule.MaxPartHealth.Value + " %)");
        }
    }
}
