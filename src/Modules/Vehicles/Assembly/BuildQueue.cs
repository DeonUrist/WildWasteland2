using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // Assembly runs on one scheduler slice; native frame/physics waits and ignition timing retain coroutine semantics.
    internal static class BuildQueue
    {
        internal static readonly object Continue = new object(), Assembled = new object();
        private sealed class Request
        {
            internal Patrol Owner; internal CarTemplate Template; internal Vector3? World; internal Quaternion Rotation;
            internal bool Menu, Hold, Waiting; internal int Generation, Frame;
            internal Action<GameObject> Done; internal Coroutine Wait;
            internal IEnumerator Root; internal readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
        }
        private static readonly Queue<Request> Pending = new Queue<Request>();
        private static readonly List<Request> Finishing = new List<Request>();
        private static Request _active;
        internal static int Queued { get { return Pending.Count; } }
        internal static bool Building { get { return _active != null; } }

        internal static void Add(Patrol owner, CarTemplate template, Vector3? at, Quaternion rotation, bool menu, bool hold, Action<GameObject> done)
        {
            Pending.Enqueue(new Request { Owner = owner, Template = template, World = at.HasValue ? (Vector3?)TrafficSpace.World(at.Value) : null,
                Rotation = rotation, Menu = menu, Hold = hold, Done = done, Generation = Patrol.SceneGen });
        }
        internal static bool Step()
        {
            if (!PatrolsModule.Running || Core.Scheduler.FrameMs >= Core.Scheduler.FrameBudgetMs) return false;
            if (_active == null)
            {
                if (Pending.Count == 0) return false;
                var next = Pending.Dequeue();
                if (next.Owner == null || next.Generation != Patrol.SceneGen) { Cancel(next); return Pending.Count > 0; }
                _active = next;
                Vector3? at = next.World.HasValue ? (Vector3?)TrafficSpace.Scene(next.World.Value) : null;
                next.Root = next.Owner.Build(next.Template, at, next.Rotation, next.Menu, next.Hold, next.Done);
                next.Stack.Push(next.Root);
            }
            var r = _active;
            if (r.Waiting || Time.frameCount < r.Frame) return false;
            if (!Core.WorldWork.Try(Core.WorldWork.Vehicles)) return false;
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
                    Finishing.Add(r); r.Wait = Core.Scheduler.StartCoroutine(Finish(r));
                    return false;
                }
                var nested = yielded as IEnumerator;
                if (nested != null) { r.Stack.Push(nested); return true; }
                if (yielded == null) r.Frame = Time.frameCount + 1;
                else { r.Waiting = true; r.Wait = Core.Scheduler.StartCoroutine(Await(r, yielded)); }
                return false;
            }
            catch { _active = null; Cancel(r); throw; }
        }
        private static IEnumerator Await(Request r, object wait)
        {
            yield return wait;
            r.Waiting = false; r.Wait = null;
        }
        private static IEnumerator Finish(Request r)
        {
            try { yield return r.Root; }
            finally { Finishing.Remove(r); }
        }
        private static void Cancel(Request r)
        {
            if (r.Wait != null) Core.Scheduler.StopCoroutine(r.Wait);
            if (r.Root == null) { if (r.Owner != null) r.Owner.CancelBuild(r.Menu, r.Done); else if (r.Done != null) r.Done(null); return; }
            while (r.Stack.Count > 0)
            {
                var disposable = r.Stack.Pop() as IDisposable;
                if (disposable != null) { try { disposable.Dispose(); } catch (Exception e) { Core.Log.Error("[Patrols] Build cancellation: " + e); } }
            }
        }
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
