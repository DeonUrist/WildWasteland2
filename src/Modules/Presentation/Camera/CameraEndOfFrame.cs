using System;
using System.Collections;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// The end-of-frame coroutine (on Core's runner) that gives the camera its real matrices back after the third-person view was drawn, so the
    /// game's next-frame mouse picks cast from the first-person eye. Started by the Presentation World part; restarted when Core recreated its runner.
    /// </summary>
    internal static class CameraEndOfFrame
    {
        static Coroutine _loop;
        static Runner _host;

        /// <summary>Starts the loop on the current Core runner (no-op when it already runs there).</summary>
        internal static void Start()
        {
            if (_loop != null && _host != null && _host == Runner.Instance) return;
            _host = Runner.Instance;
            _loop = Scheduler.StartCoroutine(Loop());
        }

        /// <summary>Stops the loop if it still runs on the current runner.</summary>
        internal static void Stop()
        {
            if (_loop != null && _host != null && _host == Runner.Instance) Scheduler.StopCoroutine(_loop);
            _loop = null; _host = null;
        }

        /// <summary>After every drawn frame (WaitForEndOfFrame): restores the third-person camera override; ends when the World part stops.</summary>
        static IEnumerator Loop()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (!PresentationModule.WorldRunning) yield break;
                try { ThirdPerson.EndOfFrame(); } catch (Exception) { }
            }
        }
    }
}
