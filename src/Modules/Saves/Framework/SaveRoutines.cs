using WildWasteland.Core;
using UI = WildWasteland.Core.UI;
using Events = WildWasteland.Core.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Saves
{
    /// <summary>Tracks Saves coroutines and disposes their iterators so shutdown always runs rollback/thaw finally blocks.</summary>
    internal static class SaveRoutines
    {
        sealed class Running
        {
            internal IEnumerator Routine;
            internal Coroutine Handle;
        }
        static readonly List<Running> Routines = new List<Running>();
        /// <summary>True while a save, exit or return-to-menu load routine is pending.</summary>
        internal static bool Active { get { return Routines.Count != 0; } }
        /// <summary>Starts an iterator under Saves lifetime tracking, including its synchronous first step.</summary>
        internal static void Start(IEnumerator routine)
        {
            var running = new Running { Routine = routine };
            Routines.Add(running);
            try
            {
                running.Handle = Scheduler.StartCoroutine(Run(running));
                if (running.Handle == null && running.Routine != null) throw new InvalidOperationException("Saves coroutine runner is unavailable");
            }
            catch { Dispose(running); throw; }
        }
        /// <summary>Relays yields and disposes the original iterator on success, exception or cancellation.</summary>
        static IEnumerator Run(Running running)
        {
            try { while (running.Routine != null && running.Routine.MoveNext()) yield return running.Routine.Current; }
            finally { Dispose(running); }
        }
        /// <summary>Disposes once; removes tracking first because iterator finally blocks may inspect framework state.</summary>
        static void Dispose(Running running)
        {
            var routine = running.Routine; running.Routine = null; Routines.Remove(running);
            var disposable = routine as IDisposable; if (disposable != null) disposable.Dispose();
        }
        /// <summary>Stops Unity execution and explicitly disposes every iterator even if Unity omits disposal on StopCoroutine.</summary>
        internal static void StopAll()
        {
            Exception failure = null;
            foreach (var running in Routines.ToArray())
            {
                try { Scheduler.StopCoroutine(running.Handle); }
                catch (Exception error) { failure = failure ?? error; }
                finally { try { Dispose(running); } catch (Exception error) { failure = failure ?? error; } }
            }
            if (failure != null) throw failure;
        }
    }
}
