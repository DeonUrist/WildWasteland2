using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// Template vehicle assembly queue: one build at a time, stepped on one scheduler slice under Core WorldWork's Vehicles owner; native
    /// frame/physics waits and ignition timing keep coroutine semantics. Requests come from Patrols (Patrol.Build); exactly one driver steps
    /// the queue per frame: the Vehicles job while the Vehicles module runs, else the old Patrols job (so Patrols spawns never stall).
    /// </summary>
    internal static class BuildQueue
    {
        internal static readonly object Continue = new object(), Assembled = new object();

        /// <summary>One queued build: its owner, template, pose, callbacks and the nested iterator stack.</summary>
        private sealed class Request
        {
            internal Patrol Owner; internal CarTemplate Template; internal Vector3? World; internal Quaternion Rotation;   // refs-ok: REFACTOR-REQUEST B12-1 (template data model stays Patrols')
            internal bool Menu, Hold, Waiting; internal int Generation, Frame;
            internal Action<GameObject> Done; internal Coroutine Wait;
            internal IEnumerator Root; internal readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
        }
        private static readonly Queue<Request> Pending = new Queue<Request>();
        private static readonly List<Request> Finishing = new List<Request>();
        private static Request _active;
        /// <summary>Number of builds waiting.</summary>
        internal static int Queued { get { return Pending.Count; } }
        /// <summary>True while a build is being stepped.</summary>
        internal static bool Building { get { return _active != null; } }

        /// <summary>Queues a build of <paramref name="template"/> (pose kept in stable world space until it starts).</summary>
        internal static void Add(Patrol owner, CarTemplate template, Vector3? at, Quaternion rotation, bool menu, bool hold, Action<GameObject> done)   // refs-ok: REFACTOR-REQUEST B12-1
        {
            Pending.Enqueue(new Request { Owner = owner, Template = template, World = at.HasValue ? (Vector3?)Origin.World(at.Value) : null,
                Rotation = rotation, Menu = menu, Hold = hold, Done = done, Generation = Patrol.SceneGen });
        }

        /// <summary>Old Patrols job step ("Patrols.Build"): drives the queue only while the Vehicles module is off (one driver per frame).</summary>
        [Obsolete("refactor: the Vehicles module drives the build queue (BuildQueue.DriveFromVehicles)")]
        internal static bool Step() { return !VehiclesModule.Running && StepCore(); }

        /// <summary>Sliced job "Vehicles.Build queue" (1 ms): steps the queue while the Vehicles module runs.</summary>
        internal static bool DriveFromVehicles() { return VehiclesModule.Running && StepCore(); }

        /// <summary>
        /// One step of the active build (starting the next queued one first): nested iterators are pushed, null waits a frame, other yields wait
        /// in a coroutine, <see cref="Assembled"/> hands the rest of the build to a finishing coroutine. Early-out: frame budget, WorldWork owner.
        /// </summary>
        private static bool StepCore()
        {
            if (Scheduler.FrameMs >= Scheduler.FrameBudgetMs) return false;
            if (_active == null)
            {
                if (Pending.Count == 0) return false;
                var next = Pending.Dequeue();
                if (next.Owner == null || next.Generation != Patrol.SceneGen) { Cancel(next); return Pending.Count > 0; }
                _active = next;
                Vector3? at = next.World.HasValue ? (Vector3?)Origin.Scene(next.World.Value) : null;
                next.Root = next.Owner.Build(next.Template, at, next.Rotation, next.Menu, next.Hold, next.Done);
                next.Stack.Push(next.Root);
            }
            var r = _active;
            if (r.Waiting || Time.frameCount < r.Frame) return false;
            if (!WorldWork.Try(WorldWork.Vehicles)) return false;
            try
            {
                var iterator = r.Stack.Peek();
                if (!iterator.MoveNext())
                {
                    r.Stack.Pop(); var disposable = iterator as IDisposable; if (disposable != null) disposable.Dispose();
                    if (r.Stack.Count == 0) _active = null;
                    return true;
                }
                var yielded = iterator.Current;
                if (ReferenceEquals(yielded, Continue)) return true;
                if (ReferenceEquals(yielded, Assembled))
                {
                    _active = null;
                    Finishing.Add(r); r.Wait = Scheduler.StartCoroutine(Finish(r));
                    return false;
                }
                var nested = yielded as IEnumerator;
                if (nested != null) { r.Stack.Push(nested); return true; }
                if (yielded == null) r.Frame = Time.frameCount + 1;
                else { r.Waiting = true; r.Wait = Scheduler.StartCoroutine(Await(r, yielded)); }
                return false;
            }
            catch { _active = null; Cancel(r); throw; }
        }

        /// <summary>Coroutine: waits for a yielded instruction (WaitForSeconds, WaitForFixedUpdate...) then lets the step continue.</summary>
        private static IEnumerator Await(Request r, object wait)
        {
            yield return wait;
            r.Waiting = false; r.Wait = null;
        }

        /// <summary>Coroutine: runs the rest of an assembled build (start-up, crew) as a plain coroutine.</summary>
        private static IEnumerator Finish(Request r)
        {
            try { yield return r.Root; }
            finally { Finishing.Remove(r); }
        }

        /// <summary>Cancels a request: stops its wait, tells the owner (or the callback) when it never started, disposes its iterators.</summary>
        private static void Cancel(Request r)
        {
            if (r.Wait != null) Scheduler.StopCoroutine(r.Wait);
            if (r.Root == null) { if (r.Owner != null) r.Owner.CancelBuild(r.Menu, r.Done); else if (r.Done != null) r.Done(null); return; }
            while (r.Stack.Count > 0)
            {
                var disposable = r.Stack.Pop() as IDisposable;
                if (disposable != null) { try { disposable.Dispose(); } catch (Exception e) { Log.Error(VehiclesModule.Instance, "Build cancellation", e); } }
            }
        }

        /// <summary>Cancels every queued, active and finishing build (scene change, Patrols shutdown).</summary>
        internal static void Reset()
        {
            var active = _active; _active = null;
            var pending = Pending.ToArray(); Pending.Clear();
            var finishing = Finishing.ToArray(); Finishing.Clear();
            if (active != null) Cancel(active);
            foreach (var r in pending) Cancel(r);
            foreach (var r in finishing) Cancel(r);
        }
    }
}
