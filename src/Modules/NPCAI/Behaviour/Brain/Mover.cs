using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// The one body mover of the NPC drivers (README 6.2): the brain (combat: drives the game's Movement pedal through the SetVelocity hook)
    /// and Idle (ghost walks, homes, travel orders: drives the Rigidbody itself) both turn, slow down while turning, follow a POI route and
    /// write the planar velocity through these primitives, so the body rules (TurnRate, the turn-speed curve, POI carrying, gravity kept)
    /// are defined once.
    /// </summary>
    internal static class Mover
    {
        /// <summary>Degrees the body may turn this frame ([NPCAI / Brain] TurnRate, at least 10 deg/s).</summary>
        internal static float TurnStep(float dt) { return Mathf.Max(10f, NpcaiSettings.TurnRate.Value) * dt; }

        /// <summary>Speed while turning: 1 within FullSpeedAngle of the wanted heading, down to SlowestSpeed at SlowestAngle and beyond.</summary>
        internal static float SpeedFactor(float offDeg)
        {
            float a0 = NpcaiSettings.MoveFullSpeedAngle.Value, a1 = Mathf.Max(a0 + 1f, NpcaiSettings.MoveSlowestAngle.Value);
            if (offDeg <= a0) return 1f;
            return Mathf.Lerp(1f, Mathf.Clamp01(NpcaiSettings.MoveSlowestSpeed.Value), Mathf.Clamp01((offDeg - a0) / (a1 - a0)));
        }

        /// <summary>Turns the body toward a world yaw at the turn rate (yaw only) and returns the new yaw.</summary>
        internal static float TurnToward(Transform t, float wantYaw, float dt)
        {
            float yaw = Mathf.MoveTowardsAngle(t.eulerAngles.y, wantYaw, TurnStep(dt));
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            return yaw;
        }

        /// <summary>Direction of the NPC's POI route at its position (false when no POI route guides it; zero = wait).</summary>
        internal static bool PoiDirection(GameObject owner, Vector3 pos, out Vector3 dir) { return PoiNav.SteerDir(owner, pos, out dir); }

        /// <summary>Carries the body along its POI route at speed (true when the route moved it: no body physics this frame).</summary>
        internal static bool CarryOnPoiRoute(GameObject owner, float speed, Vector3 dir) { return PoiNav.Walk(owner, speed, dir); }

        /// <summary>Sets the planar velocity of the body, keeping its vertical speed (gravity, falls, hops).</summary>
        internal static void Push(Rigidbody rb, Vector3 planar) { planar.y = rb.velocity.y; rb.velocity = planar; }

        /// <summary>Stops the planar motion of the body, keeping its vertical speed.</summary>
        internal static void Halt(Rigidbody rb) { var v = rb.velocity; rb.velocity = new Vector3(0f, v.y, 0f); }
    }
}
