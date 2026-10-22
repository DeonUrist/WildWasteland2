using System;
using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;
namespace WildWasteland.Modules.Vehicles
{
    /// <summary>Packs resolved authored structures without Patrols crew, cargo or gameplay policy.</summary>
    internal static class VehicleAssembler
    {
        private const string DefaultHeadlight = "Headlight";
        private static VehicleStructure _structure;
        /// <summary>Releases the last resolved prefab snapshot when the mechanical cache is reset.</summary>
        internal static void Clear() {_structure=null;}
        /// <summary>Writes assembly details only when deep logging is enabled.</summary>
        private static void Deep(string message) { if (Log.L2) Log.Deep(VehiclesModule.Instance, message); }
        /// <summary>Finds descendants in native direct-child-first order.</summary>
        private static Transform FindChild(Transform root,string name) { for(int i=0;i<root.childCount;i++)if(root.GetChild(i).name==name)return root.GetChild(i); for(int i=0;i<root.childCount;i++){var hit=FindChild(root.GetChild(i),name);if(hit!=null)return hit;}return null; }
        internal static IEnumerator PackStructure(GameObject car, VehicleStructure tpl, string bumper, bool trailer)
        {
            _structure = tpl;
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
            var prefab = _structure.Resolve(DefaultHeadlight, "headlight");
            if (prefab == null) { Log.Warn(VehiclesModule.Instance, "Headlight prefab not found: " + DefaultHeadlight); return 0; }
            foreach (var h in empty) Attach(prefab, h, precache);
            if (Log.L2) Deep("Headlights: " + empty.Count + " fitted on " + car.name);
            return empty.Count;
        }

        private static int AttachAll(GameObject car, string[] hingeNames, string partQuery, string kind, bool precache = false)
        {
            if (string.IsNullOrEmpty(partQuery)) return 0;
            // bumpers carry no ID FSM, so they are not in the item catalog: look them up as plain asset roots
            var prefab = kind == "bumper" ? _structure.Resolve(partQuery, "bumper") : _structure.Resolve(partQuery, kind);
            if (prefab == null) { Log.Warn(VehiclesModule.Instance, kind + " prefab not found: " + partQuery); return 0; }
            int n = 0;
            foreach (var hn in hingeNames)
            {
                var hinge = FindChild(car.transform, hn);
                if (hinge == null) { Log.Warn(VehiclesModule.Instance, "Hinge " + hn + " not found on " + car.name); continue; }
                Attach(prefab, hinge, precache);
                n++;
            }
            return n;
        }

        private static GameObject Attach(GameObject prefab, Transform hinge, bool precache = false)
        {
            var part = precache ? VehicleBuildCache.ClonePart(prefab, hinge) : UnityEngine.Object.Instantiate(prefab, hinge.position, hinge.rotation);
            part.SetActive(true);
            var rb = part.GetComponent<Rigidbody>();
            if (rb != null) UnityEngine.Object.DestroyImmediate(rb);
            if (!VehicleAttachments.Prepare(part))
            {
                try { part.tag = "vehPart"; } catch (Exception e) { Log.Warn(VehiclesModule.Instance, "tag vehPart: " + e.Message); }
                part.layer = 8;
            }
            part.transform.SetParent(hinge, true);
            part.transform.localPosition = Vector3.zero;
            part.transform.localRotation = Quaternion.identity;
            HingeOffset(hinge, part.transform);
            if (precache) { part.name = prefab.name; VehicleBuildCache.Record(part, prefab.name); }
            else { ItemSpawner.Name(part, prefab.name); ItemSpawner.AddExisting(part, false); }
            if (Log.L2) Deep("  " + part.name + " -> " + hinge.name);
            return part;
        }

        internal static GameObject AttachTemplatePart(GameObject car, VehicleStructure tpl, int index, GameObject[] placed, bool precache = false)
        {
            var p = tpl.Parts[index];
            if (string.IsNullOrEmpty(p.prefab)) return null;
            Transform root = car.transform;
            if (p.parent >= 0)
            {
                var host = placed[p.parent];
                if (host == null) { Log.Warn(VehiclesModule.Instance, tpl.Name + ": part [" + index + "] " + p.prefab + " skipped, its parent part [" + p.parent + "] is missing"); return null; }
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
            if (hinge == null) { Log.Warn(VehiclesModule.Instance, tpl.Name + ": hinge " + p.hinge + " not found on " + root.name + " for " + p.prefab); return null; }
            // only a real hinge holds one part; colliders and parts carry any number of attachables (plates, spikes)
            var prefab = tpl.Resolve(p.prefab);
            if (prefab == null) { Log.Warn(VehiclesModule.Instance, tpl.Name + ": part prefab not found: " + p.prefab); return null; }
            bool realHinge = hinge.name.StartsWith("hinge", StringComparison.OrdinalIgnoreCase) && !VehicleAttachments.IsFreeAttachment(prefab);
            var existing = realHinge ? PartOn(hinge) : null;
            if (existing != null) { if (Log.L2) Deep("  " + hinge.name + " already holds " + existing.name + ", " + p.prefab + " not added"); return existing; }
            if (p.hingePos != null && p.hingePos.Length == 3) hinge.localPosition = new Vector3(p.hingePos[0], p.hingePos[1], p.hingePos[2]);
            if (p.hingeRot != null && p.hingeRot.Length == 3) hinge.localEulerAngles = new Vector3(p.hingeRot[0], p.hingeRot[1], p.hingeRot[2]);
            var part = Attach(prefab, hinge, precache);
            if (p.pos != null && p.pos.Length == 3) part.transform.localPosition = new Vector3(p.pos[0], p.pos[1], p.pos[2]);
            if (p.rot != null && p.rot.Length == 3) part.transform.localEulerAngles = new Vector3(p.rot[0], p.rot[1], p.rot[2]);
            return part;
        }

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
                if (moved) if (Log.L2) Deep("  " + part.name + " placed like the game's attach on " + hinge.name + ": local " + part.localPosition.ToString("F3") + " / " + part.localEulerAngles.ToString("F1"));
            }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "Hinge offset on " + hinge.name + ": " + e.Message); }
        }

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
    }
}
