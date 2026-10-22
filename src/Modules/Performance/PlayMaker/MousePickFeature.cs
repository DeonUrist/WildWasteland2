using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// PM7 (plan §4, research B: 22 camera MousePick pairs with 13 masks, ~40 raycasts/frame). Vanilla
    /// ActionHelpers.DoMousePick(distance, mask) (Assembly-CSharp/HutongGames.PlayMaker/ActionHelpers.cs:200-209):
    ///   if (Camera.main != null) { Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out mousePickInfo,
    ///   distance, mask); mousePickLayerMaskUsed = mask; mousePickDistanceUsed = distance; mousePickRaycastTime = frameCount; }
    /// Its own one-entry cache (MousePick/MouseOver: same frame, same mask, cached distance >= requested) is kept untouched;
    /// only DoMousePick's raycast is replaced: one RaycastNonAlloc per frame along the same ray with the union of the masks
    /// and the largest distance seen, then each query takes the nearest hit whose collider layer is in its mask and whose
    /// distance is <= its distance (Physics.Raycast = nearest hit within maxDistance; the multi-hit query reports each
    /// collider's nearest hit, so the chosen RaycastHit is the same). The cast is reused only while frame, physics step
    /// (FixedUpdate vs Update), camera and ray are identical, the buffer was not full, the union mask/distance cover the
    /// query, and the chosen collider is still enabled and active; otherwise it is cast again. The three private statics
    /// and mousePickInfo are written exactly like vanilla, so PlayerBody's PickAssist postfix (runs after) is unaffected.
    /// Known limit (Shadow mode measures it): a collider that is added/moved/enabled between two picks of the same frame
    /// with Physics.autoSyncTransforms on is seen by vanilla's second raycast, not by the shared cast.
    /// </summary>
    public sealed class MousePickFeature : Feature
    {
        public override string PlanId { get { return "PM7"; } }
        public override string Key { get { return "Shared camera pick"; } }
        public override string Title { get { return "One camera pick raycast per frame"; } }
        public override string Category { get { return Categories.PlayMaker; } }
        public override int Order { get { return 7; } }
        public override bool SupportsShadow { get { return true; } }
        public override string Help { get { return "The ~40 cursor raycasts the game's FSMs make every frame (one per layer mask) share one raycast. Same hits."; } }

        internal static bool On, Shadow;
        static MousePickFeature _inst;

        static readonly Action<object, float> SetTime = FastReflect.Setter<object, float>(typeof(ActionHelpers), "mousePickRaycastTime");
        static readonly Action<object, float> SetDist = FastReflect.Setter<object, float>(typeof(ActionHelpers), "mousePickDistanceUsed");
        static readonly Action<object, int> SetMask = FastReflect.Setter<object, int>(typeof(ActionHelpers), "mousePickLayerMaskUsed");

        // the shared cast
        static RaycastHit[] _hits = new RaycastHit[64];
        static int[] _layers = new int[64];
        static int _n, _frame = -1, _castMask;
        static float _phase, _castDist;
        static Camera _cam;
        static Vector3 _origin, _dir;
        static bool _valid;
        // learned over time: masks and distances the game asks for
        static int _unionMask;
        static float _maxDist;

        protected override void OnEnable(FeatureMode mode)
        {
            if (!EnsurePatched(typeof(Patch))) throw new Exception("ActionHelpers.DoMousePick patch failed");
            _inst = this; _valid = false; _unionMask = 0; _maxDist = 0;
            Shadow = mode == FeatureMode.Shadow; On = mode == FeatureMode.On;
        }
        protected override void OnDisable() { On = Shadow = false; _valid = false; _cam = null; Array.Clear(_hits, 0, _hits.Length); }
        public override void OnSceneLoaded() { _valid = false; _cam = null; _unionMask = 0; _maxDist = 0; Array.Clear(_hits, 0, _hits.Length); }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("Union mask / distance", "0x" + _unionMask.ToString("X8") + " / " + _maxDist.ToString("0.#"));
        }

        static float PhaseKey() { return Time.inFixedTimeStep ? Time.fixedTime : -1f; }

        static void Cast(Camera cam, Ray ray)
        {
            for (;;)
            {
                _n = Physics.RaycastNonAlloc(ray, _hits, _maxDist, _unionMask, QueryTriggerInteraction.UseGlobal);
                Telemetry.PickCasts++;
                if (_n < _hits.Length) break;
                _hits = new RaycastHit[_hits.Length * 2];   // full buffer = results truncated arbitrarily: cast again
            }
            if (_layers.Length < _hits.Length) _layers = new int[_hits.Length];
            for (int i = 0; i < _n; i++) { var c = _hits[i].collider; _layers[i] = c != null ? c.gameObject.layer : -1; }
            _frame = Time.frameCount; _phase = PhaseKey(); _cam = cam; _origin = ray.origin; _dir = ray.direction;
            _castMask = _unionMask; _castDist = _maxDist; _valid = true;
        }

        /// <summary>Index of the nearest hit for (mask, distance), -1 = none, -2 = cache must be refreshed.</summary>
        static int Select(int mask, float distance)
        {
            int best = -1; float bd = float.PositiveInfinity;
            for (int i = 0; i < _n; i++)
            {
                int layer = _layers[i];
                if (layer < 0 || ((1 << layer) & mask) == 0) continue;
                float d = _hits[i].distance;
                if (d <= distance && d < bd) { bd = d; best = i; }
            }
            if (best >= 0)
            {
                var c = _hits[best].collider;   // a collider disabled/destroyed since the cast: vanilla's new raycast would not see it
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) return -2;
            }
            return best;
        }

        static bool Answer(Camera cam, Ray ray, float distance, int layerMask, out RaycastHit hit)
        {
            Telemetry.PickQueries++;
            hit = default(RaycastHit);
            bool fresh = _valid && _frame == Time.frameCount && _phase == PhaseKey() && ReferenceEquals(_cam, cam)
                && (layerMask & ~_castMask) == 0 && distance <= _castDist;
            // exact float compare of the ray (Vector3 == is approximate)
            if (fresh && (_origin.x != ray.origin.x || _origin.y != ray.origin.y || _origin.z != ray.origin.z
                || _dir.x != ray.direction.x || _dir.y != ray.direction.y || _dir.z != ray.direction.z)) fresh = false;
            if (!fresh)
            {
                _unionMask |= layerMask;
                if (distance > _maxDist) _maxDist = distance;
                Cast(cam, ray);
            }
            int i = Select(layerMask, distance);
            if (i == -2)
            {
                Cast(cam, ray);
                i = Select(layerMask, distance);
                if (i == -2) { _valid = false; return false; }   // still odd: let vanilla raycast
            }
            if (i >= 0) hit = _hits[i];
            return true;
        }

        static bool SameHit(RaycastHit a, RaycastHit b)
        {
            // PhysX's single-hit and multi-hit terrain queries can differ in the last float bits. Require the same
            // surface and allow at most 0.1 mm of position/distance noise; this changes only Shadow diagnostics.
            return a.collider == b.collider && a.triangleIndex == b.triangleIndex
                && Mathf.Abs(a.distance - b.distance) <= .0001f && (a.point - b.point).sqrMagnitude <= .00000001f
                && (a.normal - b.normal).sqrMagnitude <= .00000001f;
        }

        [HarmonyPatch]
        static class Patch
        {
            [HarmonyPatch(typeof(ActionHelpers), "DoMousePick"), HarmonyPrefix]
            static bool DoMousePickPre(float distance, int layerMask)
            {
                if (!On && !Shadow) return true;
                var cam = Camera.main;                                // vanilla's camera (not GameRefs.MainCamera): exactness
                if (cam == null) return !On;                          // vanilla: nothing happens
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                RaycastHit mine;
                if (Shadow)
                {
                    if (Answer(cam, ray, distance, layerMask, out mine))
                    {
                        RaycastHit v;
                        Physics.Raycast(ray, out v, distance, layerMask);
                        if (!SameHit(v, mine) && _inst != null)
                            _inst.Mismatch("DoMousePick mask 0x" + layerMask.ToString("X") + " dist " + distance + ": vanilla " + (v.collider != null ? v.collider.name + " @" + v.distance : "none") + ", shared " + (mine.collider != null ? mine.collider.name + " @" + mine.distance : "none"));
                    }
                    return true;
                }
                if (!Answer(cam, ray, distance, layerMask, out mine)) return true;
                ActionHelpers.mousePickInfo = mine;
                SetMask(null, layerMask);
                SetDist(null, distance);
                SetTime(null, Time.frameCount);
                return false;
            }
        }
    }
}
