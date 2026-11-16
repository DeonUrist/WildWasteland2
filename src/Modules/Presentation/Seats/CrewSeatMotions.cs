using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Presentation
{
    internal sealed class CrewSeatMotions : ICrewSeatExit
    {
        private sealed class Transition
        {
            internal Animator Animator;
            internal Transform Actor, Seat, Car;
            internal Vector3? From;
            internal Vector3 GroundHips;
            internal Quaternion Facing;
            internal bool Enter, Passenger, Roll, StepOut;
            internal float LaunchFixedTime, Rate;
            internal Rigidbody Body;
            internal Collider Collider;
            internal SeatMotion Motion;
        }

        public System.Func<bool> StepOut(GameObject actor, Transform car, Vector3 localGround, bool passenger)
        {
            if (car == null || Pose.IsBike(car) || !CarSeatPose(actor, out var seat)) return null;
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return null;
            var transition = new Transition { Animator = animator, Actor = actor.transform, Seat = seat, Car = car,
                StepOut = true, Passenger = passenger, GroundHips = localGround };
            Active.Add(transition);
            return () => Active.Contains(transition);
        }

        internal static bool BlockMovement(GameObject actor)
        {
            foreach (var transition in Active)
                if (transition.Roll && transition.Actor != null && transition.Actor.gameObject == actor) return true;
            return false;
        }

        private static readonly List<Transition> Active = new List<Transition>();

        private static bool CarSeatPose(GameObject actor, out Transform seat)
        {
            seat = null;
            if (actor == null) return false;
            var pose = actor.GetComponent<Pose>();
            SeatKind kind;
            return pose != null && pose.TryGetSeat(out seat, out kind)
                && (kind == SeatKind.Driver || kind == SeatKind.Passenger || kind == SeatKind.BusPassenger);
        }

        internal static void OnSeated(ref CrewSeated evt)
        {
            var actor = Entities.Find(evt.Npc);
            var car = Entities.Find(evt.Vehicle);
            Transform seat;
            if (car == null || Pose.IsBike(car.transform) || !CarSeatPose(actor, out seat)) return;
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;
            Active.Add(new Transition { Animator = animator, Actor = actor.transform, Seat = seat, Car = car.transform,
                Enter = true, Passenger = evt.Seat != 0 });
        }

        internal static void OnBailedOut(ref CrewBailedOut evt)
        {
            if (evt.Reason != CrewLeaveReason.Jump || !VehicleExitRoll.Required(evt.Velocity)) return;
            var actor = Entities.Find(evt.Npc);
            var car = Entities.Find(evt.Vehicle);
            var previous = Entities.Find(evt.Passenger);
            Transform seat;
            if (actor == null || car == null || Pose.IsBike(car.transform) || !CarSeatPose(previous, out seat)) return;
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman || !actor.TryGetComponent<Rigidbody>(out var body) || body.isKinematic) return;
            Active.Add(new Transition { Animator = animator, Actor = actor.transform, Car = car.transform, Roll = true,
                Body = body, Collider = actor.GetComponent<Collider>(), LaunchFixedTime = Time.fixedTime,
                Rate = VehicleExitRoll.Rate(evt.Velocity), Facing = Quaternion.LookRotation(evt.Velocity, Vector3.up) });
        }

        internal static void LateTick()
        {
            bool active = Active.Count > 0 && GameRefs.WorldActive;
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var transition = Active[i];
                try { if (active && Step(transition)) continue; }
                catch (System.Exception e) { Log.Warn(PresentationModule.Instance, "Crew seat animation: " + e.Message); }
                transition.Motion?.Dispose();
                Active.RemoveAt(i);
            }
        }

        private static bool Step(Transition transition)
        {
            var animator = transition.Animator;
            if (animator == null || !animator.gameObject.activeInHierarchy || transition.Actor == null) return false;
            if ((transition.Enter || transition.StepOut) && (transition.Seat == null || transition.Car == null
                || transition.Actor.parent != transition.Seat)) return false;
            if (transition.Motion == null)
            {
                if (transition.Roll)
                {
                    if (transition.Body == null || transition.Collider == null) return false;
                    var bounds = transition.Collider.bounds;
                    if (Time.fixedTime <= transition.LaunchFixedTime || transition.Body.velocity.y > .5f
                        || !Body.FindExitGround(bounds.center, bounds.extents.y + .08f, transition.Car, transition.Actor, out _)) return true;
                }
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips == null) return false;
                if (transition.StepOut) transition.From = transition.Car.InverseTransformPoint(hips.position);
                else transition.GroundHips = transition.Actor.InverseTransformPoint(hips.position);
                if (transition.Roll) transition.From = Origin.World(hips.position);
                var driver = ModAPI.Find(animator);
                transition.Motion = SeatMotion.Start(animator, transition.Enter, transition.Passenger, transition.Roll,
                    driver != null ? driver.Graph : default, transition.Rate);
                if (transition.Motion == null) return false;
            }
            if (transition.StepOut)
            {
                var ground = transition.Car.TransformPoint(transition.GroundHips);
                if (Body.FindExitGround(ground + Vector3.up * .6f, 2f, transition.Car, transition.Actor, out var hit)) ground = hit;
                return transition.Motion.Step(transition.Car.TransformPoint(transition.From.Value), ground + Vector3.up * animator.humanScale,
                    SeatMotion.Facing(transition.Car, Vector3.zero), false);
            }
            if (transition.Enter)
            {
                var target = transition.Seat.TransformPoint(new Vector3(SeatPoseSettings.DriverOffsetX.Value,
                    SeatPoseSettings.DriverOffsetY.Value, SeatPoseSettings.DriverOffsetZ.Value));
                return transition.Motion.Step(null, target, SeatMotion.Facing(transition.Car, Vector3.zero));
            }
            return transition.Motion.Step(Origin.Scene(transition.From.Value),
                transition.Actor.TransformPoint(transition.GroundHips), transition.Facing);
        }

        internal static void Clear()
        {
            foreach (var transition in Active) transition.Motion?.Dispose();
            Active.Clear();
        }
    }
}
