using System;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// BodyRunner part (split from PlayerBody/BodyRunner.cs in Wave A): the end-of-frame coroutine that gives the camera its real
    /// matrices back after the third-person view was drawn. Target: Presentation camera rig (B06).
    /// </summary>
    internal static partial class BodyRunner
    {
        // after the frame is drawn: the camera's real matrices back (the game's mouse picks next frame cast from the eye)
        internal static System.Collections.IEnumerator EndOfFrameLoop()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (!PlayerBodyModule.Running) yield break;
                try { ThirdPerson.EndOfFrame(); } catch (Exception) { }
            }
        }
    }
}
