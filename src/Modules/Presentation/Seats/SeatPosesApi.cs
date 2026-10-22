using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// <see cref="ISeatPoses"/> provider of the Presentation World part: one entry point for the crew seat pose (<see cref="Pose"/>: drivers,
    /// passengers, bus passengers, turret gunners, bike riders, quest followers) plus the player's steering-wheel hands. Publishes
    /// <see cref="SeatPoseChanged"/>. The existing Patrols / Quests callers keep their direct Pose calls until their owners switch.
    /// </summary>
    internal sealed class SeatPosesApi : ISeatPoses
    {
        /// <summary>Seats <paramref name="character"/> on <paramref name="seat"/> with the pose of human type <paramref name="pose"/> (null = generic) and the kind's configuration.</summary>
        public bool Seat(GameObject character, Transform seat, SeatKind kind, string pose)
        {
            if (character == null || seat == null) return false;
            var p = Pose.Apply(character, seat, pose);
            switch (kind)
            {
                case SeatKind.Passenger: p.ConfigurePassengerAim(); break;
                case SeatKind.BusPassenger: p.ConfigureBusPassenger(); break;
                case SeatKind.Turret: p.ConfigureTurret(false, false); break;
                case SeatKind.Rider: p.ConfigureTurret(false, true); break;
                case SeatKind.Follower: p.AnimatedFollower = true; break;
            }
            Publish(character, seat, kind, true);
            return true;
        }

        /// <summary>Ends the seat pose: the animation body is released and the pose component removed.</summary>
        public void Release(GameObject character)
        {
            var p = character != null ? character.GetComponent<Pose>() : null;
            if (p == null) return;
            Transform seat; SeatKind kind;
            p.TryGetSeat(out seat, out kind);
            p.ReleaseBody();
            Object.Destroy(p);
            Publish(character, seat, kind, false);
        }

        /// <summary>True while the character carries an active seat pose.</summary>
        public bool IsSeated(GameObject character)
        {
            var p = character != null ? character.GetComponent<Pose>() : null;
            Transform seat; SeatKind kind;
            return p != null && p.TryGetSeat(out seat, out kind);
        }

        /// <summary>Seat anchor and kind of a seated character.</summary>
        public bool TryGetSeat(GameObject character, out Transform seat, out SeatKind kind)
        {
            var p = character != null ? character.GetComponent<Pose>() : null;
            if (p != null) return p.TryGetSeat(out seat, out kind);
            seat = null; kind = SeatKind.Passenger;
            return false;
        }

        /// <summary>Seated animation on/off: an animated follower keeps its own arms (the pose only places legs and hips).</summary>
        public void Animate(GameObject character, bool enabled)
        {
            var p = character != null ? character.GetComponent<Pose>() : null;
            if (p != null) p.AnimatedFollower = enabled;
        }

        /// <summary>True while the steering-wheel / handlebar hand IK of the player's seat is active.</summary>
        public bool PlayerHandsOnWheel { get { return WheelArms.HandsOnWheel; } }

        /// <summary>Publishes <see cref="SeatPoseChanged"/> for a character on the vehicle that owns the seat.</summary>
        static void Publish(GameObject character, Transform seat, SeatKind kind, bool seated)
        {
            var vehicle = seat != null ? Entities.Of(seat.root.gameObject) : EntityId.None;
            var e = new SeatPoseChanged { Character = Entities.Of(character), Vehicle = seated ? vehicle : EntityId.None, Seat = kind, SeatIndex = -1, Seated = seated };
            ModEvents.Publish(ref e);
        }
    }
}
