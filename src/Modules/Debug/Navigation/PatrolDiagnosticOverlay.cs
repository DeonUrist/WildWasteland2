using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Debug
{
    /// <summary>Draws the original pilot labels from copied lines, retaining slot order and convoy fallback.</summary>
    internal static class PatrolDiagnosticOverlay
    {
        private static readonly List<string> Lines = new List<string>();
        /// <summary>Renders only while the requested native-time overlay is visible.</summary>
        internal static void Draw()
        {
            if (!RuntimeDiagnosticState.AiOverlay || Time.timeScale <= 0) return;
            var source = ModApi.Get<IPatrolDiagnosticSource>(); if (source == null) return;
            Lines.Clear(); source.CollectOverlayLines(Lines);
            for (int i = 0; i < Lines.Count; i++) if (Lines[i] != null) GUI.Label(new Rect(10, 10 + 18 * i, 1400, 22), Lines[i]);
        }
    }
}
