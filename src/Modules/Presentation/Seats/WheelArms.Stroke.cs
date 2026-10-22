using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// WheelArms part (split from PlayerBody/Vehicles/WheelGripEditor.cs in Wave A): runtime helpers of the steering-wheel arm solver -
    /// per-pose value offsets, left/right mirroring and the hand-over-hand steering stroke. Used by the solver at runtime, so they stay with
    /// the seat pose (B06) while the editor itself moves to Developer.
    /// </summary>
    internal static partial class WheelArms
    {
        private static readonly float[] _turn = new float[2], _release = new float[2];
        private static int _releaseHand = -1, _spinDirection;
        private static float _releaseTime, _releaseDuration, _releaseStart, _releaseEnd;
        /// <summary>Start index of one pose / arm block in the grip setup values.</summary>
        private static int PoseOffset(int pose, int arm) { return 12 + pose * 24 + arm * 12; }
        /// <summary>Blend between the side and top grip vectors of a pose block.</summary>
        private static Vector3 PoseVector(float[] v, int side, int top, float blend)
        { return Vector3.Lerp(new Vector3(v[side], v[side + 1], v[side + 2]), new Vector3(v[top], v[top + 1], v[top + 2]), blend); }
        /// <summary>Mirrors a vector across the wheel plane frame.</summary>
        private static Vector3 MirrorVector(Vector3 v, Quaternion frame)
        { v = Quaternion.Inverse(frame) * v; v.x = -v.x; return frame * v; }
        /// <summary>Mirrors a rotation delta across the wheel plane frame.</summary>
        private static Quaternion MirrorRotation(Quaternion q, Quaternion frame)
        { q = Quaternion.Inverse(frame) * q * frame; return frame * new Quaternion(q.x, -q.y, -q.z, q.w) * Quaternion.Inverse(frame); }
        /// <summary>Mirrors an orientation across the wheel plane frame.</summary>
        private static Quaternion MirrorOrientation(Quaternion q, Quaternion frame)
        { q = Quaternion.Inverse(frame) * q; return frame * new Quaternion(q.x, -q.y, -q.z, q.w); }
        /// <summary>Copies one arm pose block to the other arm, mirrored.</summary>
        private static void MirrorPose(float[] v, int pose, int sourceArm)
        {
            int source = PoseOffset(pose, sourceArm), target = PoseOffset(pose, 1 - sourceArm);
            for (int k = 0; k < 12; k++) v[target + k] = v[source + k] * (k == 0 || k == 4 || k == 5 || k == 7 || k == 8 || k == 9 ? -1f : 1f);
        }

        /// <summary>Resets the hand-over-hand stroke state at a steering angle.</summary>
        private static void ResetStroke(float spin)
        {
            spin = -Mathf.Abs(spin);
            _turn[0] = _turn[1] = _release[0] = _release[1] = _top[0] = _top[1] = 0f;
            _lastSpin = spin; _returning = false; _releaseHand = -1; _spinDirection = 0;
        }

        /// <summary>Advances the hand-over-hand steering stroke (release and regrip phases) for the wheel spin.</summary>
        private static void TickStroke(float spin, float stroke)
        {
            spin = -Mathf.Abs(spin); // One working right-turn sequence; Solve reflects its arm roles for left steering.
            const float back = 40f; // Both hands follow small corrections before the trailing hand releases.
            if (_editor && _preview < 2) { ResetStroke(spin); return; }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (dt <= 0f) return;
            float delta = spin - _lastSpin, distance = Mathf.Abs(spin);
            if (Mathf.Abs(_vehicle.input.Steering) < .04f || distance < Mathf.Abs(_lastSpin) - .01f) _returning = true;
            else if (distance > Mathf.Abs(_lastSpin) + .01f) _returning = false;
            _lastSpin = spin;
            if (Mathf.Abs(delta) > .001f) _spinDirection = delta > 0f ? 1 : -1;
            if (_returning)
            {
                // Let the rim unwind underneath relaxed hands; no further pulls or regrips on the return.
                _releaseHand = -1;
                float k = 1f - Mathf.Exp(-6f * dt);
                for (int i = 0; i < 2; i++)
                {
                    _turn[i] = Mathf.Lerp(_turn[i], 0f, k); _release[i] = Mathf.Lerp(_release[i], 0f, k);
                    if (Mathf.Abs(_turn[i]) < .1f) _turn[i] = _release[i] = 0f;
                    float side = i == 0 ? 180f - _setup.V[3] : _setup.V[3];
                    _top[i] = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_turn[i] / ((i == 0 ? 110f : 70f) - side)));
                }
                return;
            }

            // A closed grip follows the actual wheel delta. Only one hand releases at a time;
            // its reach finishes in time even when the driver stops turning mid-stroke.
            for (int i = 0; i < 2; i++)
            {
                if (i == _releaseHand) continue;
                _turn[i] = Mathf.Clamp(_turn[i] + delta, i == 0 ? -stroke : -back, i == 0 ? back : stroke);
                _release[i] = 0f;
            }
            if (_releaseHand >= 0)
            {
                int hand = _releaseHand;
                _releaseTime = Mathf.Min(1f, _releaseTime + dt / _releaseDuration);
                _turn[hand] = Mathf.Lerp(_releaseStart, _releaseEnd, Mathf.SmoothStep(0f, 1f, _releaseTime));
                float lift = Mathf.Sin(_releaseTime * Mathf.PI);
                _release[hand] = lift * lift;
                if (_releaseTime >= 1f) { _release[hand] = 0f; _releaseHand = -1; }
            }
            if (_releaseHand < 0)
            {
                bool centered = distance < .5f;
                for (int i = 0; i < 2; i++)
                {
                    float lo = i == 0 ? -stroke : -back, hi = i == 0 ? back : stroke;
                    bool atEnd = _spinDirection > 0 ? _turn[i] >= hi - .01f : _spinDirection < 0 && _turn[i] <= lo + .01f;
                    if (centered ? Mathf.Abs(_turn[i]) < .1f : !atEnd) continue;
                    _releaseHand = i; _releaseStart = _turn[i];
                    _releaseEnd = centered ? 0f : _spinDirection > 0 ? lo : hi;
                    _releaseTime = 0f;
                    _releaseDuration = Mathf.Clamp(Mathf.Abs(_releaseEnd - _releaseStart) / Mathf.Max(240f, Mathf.Abs(delta) / dt), .35f, .65f);
                    break;
                }
            }
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 180f - _setup.V[3] : _setup.V[3];
                float top = i == 0 ? 110f : 70f;
                _top[i] = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_turn[i] / (top - side)));
            }
        }
    }
}
