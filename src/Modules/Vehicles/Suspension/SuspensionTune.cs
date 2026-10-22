using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using NWH.WheelController3D;
using UnityEngine;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// The geometry of a suspension adjustment without the Part Adjustment mod (its own SuspensionAdjustment is the reference; same rules:
    /// model scale X / position Y, hinge Y, mounts = vanilla baseline + axle-end width offset + height). Added at runtime; Patrols' own save
    /// data carries width / height per raider car.
    /// </summary>
    internal sealed class SuspensionTune : MonoBehaviour
    {
        internal float Width = 1f, Height;
        private Transform _model, _hinge;
        private Vector3 _modelPos, _modelScale, _hingePos;
        private readonly List<Mount> _mounts = new List<Mount>();
        private readonly List<Axle> _axles = new List<Axle>();
        private Rigidbody _body;
        private long _comSig; private float _nextCom; private bool _comShifted;
        private static readonly List<Collider> _cols = new List<Collider>();

        // WW: live count, the first-line filter of the shared SetPosition.DoSetPosition postfix (Suspension.AfterSetPosition)
        internal static int Live;
        /// <summary>Counts live tunes (first-line filter of the SetPosition postfix).</summary>
        private void Awake() { Live++; }
        /// <summary>Uncounts a destroyed tune.</summary>
        private void OnDestroy() { Live--; }
        /// <summary>One wheel mount: transform, vanilla baseline, axle centre line, lifted branch.</summary>
        private sealed class Mount { internal Transform T; internal Vector3 Baseline; internal float Center; internal bool Lifted; }
        /// <summary>One axle mesh branch with its ends in model space.</summary>
        private sealed class Axle { internal Transform Branch; internal bool Lifted; internal float Left, Right; internal Vector3 Center; }

        /// <summary>The car's tune, added when the car is an adjustable chassis (model, kit hinge, four Suspension-FSM wheel mounts); null otherwise.</summary>
        internal static SuspensionTune Ensure(GameObject car)
        {
            var t = car.GetComponent<SuspensionTune>();
            if (t != null) return t;
            var model = car.transform.Find("suspension_model");
            var hinge = Suspension.KitHinge(car);
            if (model == null || hinge == null) return null;
            var wheels = car.GetComponentsInChildren<WheelController>(true).Where(w => w.transform.parent == car.transform && w.name.StartsWith("hinge_wheel_", StringComparison.Ordinal)).ToArray();
            if (wheels.Length < 4) return null;
            foreach (var w in wheels) if (w.GetComponents<PlayMakerFSM>().All(f => f.FsmName != "Suspension")) return null;   // Rustliner / Rustcargo / Rustchief: dormant assemblies
            t = car.AddComponent<SuspensionTune>();
            t.Init(model, hinge, wheels);
            return t;
        }

        /// <summary>Captures the model/hinge baselines, the vanilla mount positions and the axle mesh ends in model space.</summary>
        private void Init(Transform model, Transform hinge, WheelController[] wheels)
        {
            _model = model; _hinge = hinge; _body = GetComponent<Rigidbody>();
            _modelPos = model.localPosition; _modelScale = model.localScale; _hingePos = hinge.localPosition;
            foreach (var w in wheels)
            {
                var fsm = w.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Suspension" && f.enabled);
                _mounts.Add(new Mount { T = w.transform, Baseline = VanillaPosition(w.transform, fsm), Lifted = fsm != null && fsm.Fsm.Initialized && fsm.ActiveStateName == "lifted" });
            }
            RefreshCenters();
            // the axle meshes' ends in the model's own coordinates (bounds work on the game's non-readable meshes)
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.name.ToLowerInvariant();
                if (mf.sharedMesh == null || !n.Contains("suspension") || (!n.Contains("front") && !n.Contains("rear"))) continue;
                var branch = mf.transform;
                while (branch.parent != null && branch.parent != model) branch = branch.parent;
                bool lifted = branch.name == "lifted" || branch.name == "buggy";
                if (branch.parent != model || (!lifted && branch.name != "stock")) continue;
                var b = mf.sharedMesh.bounds; float left = float.PositiveInfinity, right = float.NegativeInfinity;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                {
                    float v = model.InverseTransformPoint(mf.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)))).x;
                    left = Mathf.Min(left, v); right = Mathf.Max(right, v);
                }
                _axles.Add(new Axle { Branch = branch, Lifted = lifted, Left = left, Right = right, Center = model.InverseTransformPoint(mf.transform.TransformPoint(b.center)) });
            }
        }

        // the mount position the vanilla FSM's active state writes (stock / lifted SetPosition) - authoritative even after a load
        /// <summary>The mount position the vanilla FSM's active state writes (stock / lifted SetPosition) - authoritative even after a load.</summary>
        private static Vector3 VanillaPosition(Transform wheel, PlayMakerFSM fsm)
        {
            if (fsm == null) return wheel.localPosition;
            try
            {
                string want = fsm.Fsm.Initialized ? fsm.ActiveStateName : "stock";
                var state = fsm.FsmStates.FirstOrDefault(s => s.Name == want) ?? fsm.FsmStates.FirstOrDefault(s => s.Name == "stock");
                if (state == null) return wheel.localPosition;
                var acts = state.Actions; if ((acts == null || acts.Length == 0)) { state.LoadActions(); acts = state.Actions; }
                var sp = acts != null ? acts.OfType<SetPosition>().FirstOrDefault(a => a.Enabled && a.space == Space.Self) : null;
                if (sp == null) return wheel.localPosition;
                var p = sp.vector != null && !sp.vector.IsNone ? sp.vector.Value : wheel.localPosition;
                if (sp.x != null && !sp.x.IsNone) p.x = sp.x.Value;
                if (sp.y != null && !sp.y.IsNone) p.y = sp.y.Value;
                if (sp.z != null && !sp.z.IsNone) p.z = sp.z.Value;
                return p;
            }
            catch (Exception) { return wheel.localPosition; }
        }

        /// <summary>Per mount: the centre line between it and the opposite mount of the same axle.</summary>
        private void RefreshCenters()
        {
            foreach (var m in _mounts)
            {
                Mount opp = null; float nearest = float.PositiveInfinity;
                foreach (var c in _mounts)
                {
                    if (c == m || Mathf.Sign(c.Baseline.x) == Mathf.Sign(m.Baseline.x)) continue;
                    float d = Mathf.Abs(c.Baseline.z - m.Baseline.z);
                    if (d < nearest) { opp = c; nearest = d; }
                }
                m.Center = opp == null ? 0f : (m.Baseline.x + opp.Baseline.x) * 0.5f;
            }
        }

        /// <summary>Sets width (1..1.5) and height (-1..1) and applies them.</summary>
        internal void Set(float width, float height) { Width = Mathf.Clamp(width, 1f, 1.5f); Height = Mathf.Clamp(height, -1f, 1f); Apply(); }

        /// <summary>Applies model scale/position, kit hinge, mounts and centre of mass; resets to standard when the kit came off.</summary>
        private void Apply()
        {
            if (_model == null || _hinge == null) return;
            if (!Suspension.KitFitted(gameObject) && (Width != 1f || Height != 0f)) { Width = 1f; Height = 0f; }
            _model.localScale = new Vector3(_modelScale.x * Width, _modelScale.y, _modelScale.z);
            _model.localPosition = _modelPos + Vector3.up * Height;
            _hinge.localPosition = _hingePos + _hinge.parent.InverseTransformVector(transform.TransformVector(Vector3.up * Height));
            foreach (var m in _mounts) ApplyMount(m);
            ApplyCenterOfMass(true);
            if (_body != null) _body.WakeUp();
        }

        // 2.5.0: a car with a lift kit carries its weight lower. The Rigidbody's automatic centre of mass (nothing in the game or NWH sets one)
        // is lowered by KitDrop as soon as a kit sits on hinge_suspension, plus LiftFactor x any extra lift of the body (Height < 0), so a
        // tall car on big wheels does not flip in turns. The automatic value is re-read first (parts going on or off still move it) and
        // re-checked once a second. Same constants and rule as Part Adjustment 1.4.0; without a kit the centre is the automatic one.
        internal const float KitDrop = 0.3f, LiftFactor = 1.5f;

        /// <summary>Lowers the rigidbody centre of mass by the kit drop (+ lift factor) when [Vehicles / Suspension] LiftKitCenterOfMass is on and a kit is fitted; skipped when nothing relevant changed.</summary>
        private void ApplyCenterOfMass(bool force)
        {
            if (_body == null) return;
            bool kit = Suspension.KitFitted(gameObject);
            long sig = kit ? 19 : 17;
            sig = sig * 31 + (long)(Height * 1000f);
            _cols.Clear(); GetComponentsInChildren(false, _cols);   // Perf: reused buffer (1 Hz per tuned car, from FixedUpdate)
            foreach (var c in _cols) if (c != null && c.enabled && !c.isTrigger) sig = sig * 31 + c.GetInstanceID();
            _cols.Clear();
            if (!force && sig == _comSig) return;
            _comSig = sig;
            float drop = VehiclesSettings.LiftKitCenterOfMass.Value && kit ? KitDrop + LiftFactor * Mathf.Max(0f, -Height) : 0f;
            if (drop <= 0f) { if (_comShifted) { _body.ResetCenterOfMass(); _comShifted = false; } return; }
            _body.ResetCenterOfMass();
            _body.centerOfMass = _body.centerOfMass - Vector3.up * drop;
            _comShifted = true;
        }

        /// <summary>Per physics step, at most once a second (module running): re-checks the centre of mass (parts going on or off move it).</summary>
        private void FixedUpdate()
        {
            if (!VehiclesModule.Running) return;
            if (Time.time < _nextCom) return;
            _nextCom = Time.time + 1f;
            ApplyCenterOfMass(false);
        }

        /// <summary>Moves one mount to baseline + axle-end width offset (or scaled about the centre line) + height.</summary>
        private void ApplyMount(Mount m)
        {
            if (m.T == null) return;
            var p = m.Baseline;
            Vector3 off;
            if (AxleOffset(m, out off)) p += off;
            else p.x = m.Center + (p.x - m.Center) * Width;
            p.y += Height;
            if (!m.T.localPosition.Equals(p)) m.T.localPosition = p;
        }

        // the mount follows the end of the matching (stock / lifted) axle mesh as the model stretches: factory hub clearance kept
        /// <summary>The mount offset that follows the end of the matching (stock / lifted) axle mesh as the model stretches.</summary>
        private bool AxleOffset(Mount m, out Vector3 offset)
        {
            offset = Vector3.zero;
            Axle best = null; float dist = float.PositiveInfinity;
            foreach (var a in _axles)
            {
                if (a.Branch == null || a.Lifted != m.Lifted) continue;
                float d = Mathf.Abs(m.T.parent.InverseTransformPoint(_model.TransformPoint(a.Center)).z - m.Baseline.z);
                if (d < dist) { dist = d; best = a; }
            }
            if (best == null) return false;
            bool left = m.Baseline.x < m.T.parent.InverseTransformPoint(_model.position).x;
            float end = (left ? best.Left : best.Right) * _modelScale.x;
            var local = new Vector3(end * (Width - 1f), 0f, 0f);
            offset = m.T.parent.InverseTransformVector(_model.parent.TransformVector(_model.localRotation * local));
            return true;
        }

        // the vanilla FSM just wrote this mount (kit on / off, a load): new baseline, our offsets on top
        /// <summary>The vanilla FSM just wrote this mount (kit on / off, a load): new baseline, our offsets on top.</summary>
        internal void VanillaMountMoved(Transform wheel, string state)
        {
            var m = _mounts.FirstOrDefault(x => x.T == wheel);
            if (m == null) return;
            m.Baseline = wheel.localPosition; m.Lifted = state == "lifted";
            RefreshCenters();
            if (!Suspension.KitFitted(gameObject) && (Width != 1f || Height != 0f)) { Apply(); return; }
            foreach (var x in _mounts) ApplyMount(x);
        }
    }
}
