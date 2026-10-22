using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    // (2.3.3) Smooth first-person weapon / item sway. The game's [Sway] FSMs under the PlayerCamera (WeaponsArm/Parent, ItemAnim,
    // Flashlight, GodHands_Model) run PlayMaker's SmoothLookAt toward an object "LookAt" at speed 40:
    //   lastRotation = Slerp(lastRotation, desired, speed * Time.deltaTime)
    // That step is frame-rate dependent (a share of speed*dt, clamped at 1): with uneven frame times the weapon's lag behind the view changes
    // from frame to frame, and the LookAt target can move in physics steps while the camera turns every frame - both show as jerky arms /
    // weapon when turning. Harmony prefix on SmoothLookAt.DoSmoothLookAt for those FSMs: the same look-at, but
    //  - the target is followed in the camera's space with a short exponential filter (a target that jumps at physics rate glides),
    //  - the rotation eases exponentially (1 - e^(-rate dt), rate = speed x 1.65 = the game's feel at 60 fps) - the same lag at every frame rate.
    // A handful of vector operations per active sway object and frame; anything else (other FSMs, Enabled off) runs the game's own code.
    internal static class FirstPersonSway
    {
        private sealed class State { public Quaternion Rot; public Vector3 LocalTarget; public bool Ready; public GameObject Go; public int Frame; }
        private static readonly Dictionary<SmoothLookAt, State> _state = new Dictionary<SmoothLookAt, State>();
        // (WW) every SmoothLookAt action is classified once: NotSway marks the ones of other FSMs (one dictionary lookup, no string compare per frame)
        private static readonly State NotSway = new State();
        // (WW) the action's private fields through compiled setters (was FieldInfo.SetValue = boxing per object and frame)
        private static readonly Action<SmoothLookAt, Quaternion> _lastRotation = Setter<Quaternion>("lastRotation"), _desiredRotation = Setter<Quaternion>("desiredRotation");
        private static readonly Action<SmoothLookAt, GameObject> _previousGo = Setter<GameObject>("previousGo");
        private const float RateScale = 1.65f, TargetRate = 30f;

        private static Action<SmoothLookAt, T> Setter<T>(string name)
        {
            var f = HarmonyLib.AccessTools.Field(typeof(SmoothLookAt), name);
            if (f == null || f.FieldType != typeof(T)) return null;
            try
            {
                var a = System.Linq.Expressions.Expression.Parameter(typeof(SmoothLookAt), "a");
                var v = System.Linq.Expressions.Expression.Parameter(typeof(T), "v");
                return System.Linq.Expressions.Expression.Lambda<Action<SmoothLookAt, T>>(
                    System.Linq.Expressions.Expression.Assign(System.Linq.Expressions.Expression.Field(a, f), v), a, v).Compile();
            }
            catch (Exception) { return (x, val) => f.SetValue(x, val); }
        }

        internal static void Clear() { _state.Clear(); }
        // scene load: drop the actions whose FSM is gone
        private static readonly List<SmoothLookAt> _dead = new List<SmoothLookAt>();
        internal static void Sweep()
        {
            _dead.Clear();
            foreach (var kv in _state) if (kv.Key == null || kv.Key.Fsm == null || kv.Key.Fsm.Owner == null) _dead.Add(kv.Key);
            foreach (var k in _dead) _state.Remove(k);
            _dead.Clear();
        }

        // Core.Hooks.SmoothLookAtDo prefix (subscribed in PlayerBodyModule.Init)
        public static bool BeforeSmoothLookAt(SmoothLookAt __instance)
        {
            State s;
            if (_state.TryGetValue(__instance, out s))
            {
                if (ReferenceEquals(s, NotSway)) return true;
            }
            else
            {
                var f = __instance.Fsm;
                if (f == null) return true;
                if (f.Name != "Sway") { _state[__instance] = NotSway; return true; }
                s = new State(); _state[__instance] = s;
            }
            if (!PlayerBodyModule.FirstPersonSwayOn.Value) { s.Ready = false; return true; }
            try
            {
                var cam = Game.PlayerCamera;
                var go = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
                var target = __instance.targetObject != null ? __instance.targetObject.Value : null;
                if (cam == null || go == null || target == null || !go.transform.IsChildOf(cam)) return true;

                var tr = go.transform;
                Vector3 worldTarget = __instance.targetPosition != null && !__instance.targetPosition.IsNone
                    ? target.transform.TransformPoint(__instance.targetPosition.Value) : target.transform.position;
                Vector3 local = cam.InverseTransformPoint(worldTarget);
                float dt = Time.deltaTime;
                if (!s.Ready || s.Go != go || Time.frameCount - s.Frame > 2)   // first use, another object, or the sway was off (weapon swap): start where it is
                {
                    s.Rot = tr.rotation; s.LocalTarget = local; s.Go = go; s.Ready = true;
                }
                else s.LocalTarget = Vector3.Lerp(s.LocalTarget, local, 1f - Mathf.Exp(-TargetRate * dt));
                s.Frame = Time.frameCount;
                Vector3 lookAt = cam.TransformPoint(s.LocalTarget);
                if (__instance.keepVertical != null && __instance.keepVertical.Value) lookAt.y = tr.position.y;
                Vector3 diff = lookAt - tr.position;
                Quaternion desired = s.Rot;
                if (diff.sqrMagnitude > 1e-10f)
                {
                    Vector3 up = __instance.upVector == null || __instance.upVector.IsNone ? Vector3.up : __instance.upVector.Value;
                    desired = Quaternion.LookRotation(diff, up);
                }
                float speed = __instance.speed != null ? __instance.speed.Value : 40f;
                s.Rot = Quaternion.Slerp(s.Rot, desired, 1f - Mathf.Exp(-speed * RateScale * dt));
                tr.rotation = s.Rot;
                // keep the game's own fields in step (Enabled off continues from here)
                if (_lastRotation != null) _lastRotation(__instance, s.Rot);
                if (_desiredRotation != null) _desiredRotation(__instance, desired);
                if (_previousGo != null) _previousGo(__instance, go);
                if (__instance.finishEvent != null && __instance.finishTolerance != null && Quaternion.Angle(s.Rot, desired) <= __instance.finishTolerance.Value)
                    __instance.Fsm.Event(__instance.finishEvent);
                return false;
            }
            catch (Exception e) { PlayerBodyModule.Verbose("First-person sway: " + e.Message); return true; }
        }
    }
}
