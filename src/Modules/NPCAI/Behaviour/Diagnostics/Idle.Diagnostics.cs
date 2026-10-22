using WildWasteland.Core;
using System;
using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Idle part owned by Debug: the world overlay of idle controllers (DrawDebug).</summary>
    internal static partial class Idle
    {
        // [Debug] ShowNavigation: home (green; grey = forgotten), patrol points (lime), search points (violet) and a label above the head
        /// <summary>ShowNavigation overlay: homes, patrol / search points and a label above each idle controller.</summary>
        internal static void CollectDebug(List<DiagnosticMarker> markers)
        {
            if (!WildWasteland.Core.NavigationDiagnosticState.Overlay || _ctl.Count == 0) return;
            var cam = Core.GameRefs.MainCamera; if (cam == null) return;
            float now = Time.time;
            Vector3 cp = cam.transform.position;
            foreach (var c in _ctl.Values)
            {
                var a = c.A;
                if (c.Excluded || a.Owner == null || a.T.parent != null || (a.T.position - cp).sqrMagnitude > 200f * 200f) continue;
                if (c.HasHome)
                {
                    DiagnosticMarkers.Mark(markers, c.Home + Vector3.up * 0.2f, c.Forgotten ? Color.gray : Color.green, 10f);
                    if (c.Points != null) foreach (var p in c.Points) DiagnosticMarkers.Mark(markers, p + Vector3.up * 0.2f, new Color(0.6f, 1f, 0.2f), 7f);
                }
                if (c.InSearch && c.SPts != null) foreach (var p in c.SPts) DiagnosticMarkers.Mark(markers, p + Vector3.up * 0.2f, new Color(0.75f, 0.6f, 1f), 7f);
                string s = null; Color col = new Color(0.6f, 1f, 0.6f);
                if (c.InSearch)
                {
                    s = c.Leg == Leg.SearchOut ? "search round: out to point " + c.SLast : c.Leg == Leg.SearchBack ? "search round: back" : c.SPts == null || c.SPts.Count == 0 ? "search: no clear round, stands" : "search round: looks";
                    col = new Color(0.75f, 0.6f, 1f);
                }
                else if (a.State == Senses.State.Idle)
                {
                    if (c.Leg == Leg.Home) { s = "RETURNING " + Flat(a.T.position - c.Home).ToString("0") + " m (try " + (c.Tries + 1) + "/" + NpcModule.IdleReturnTries.Value + ")"; col = Color.yellow; }
                    else if (c.Forgotten) { s = "IDLE - home forgotten"; col = Color.gray; }
                    else if (!c.HasHome) s = "IDLE - no home";
                    else if (c.Leg == Leg.ToPoint) s = "IDLE walk -> point " + c.PointIdx;
                    else if (c.Leg == Leg.Back) s = "IDLE walk -> home";
                    else if (c.PendingBack) s = "IDLE at point " + c.PointIdx + ", looks";
                    else if (c.Tries > 0 && now < c.WaitUntil) { s = "WAITS to return (" + c.Tries + "/" + NpcModule.IdleReturnTries.Value + " failed)"; col = new Color(1f, 0.7f, 0.2f); }
                    else if (!c.LastWasSearch && now - c.BusyAt < NpcModule.IdleReturnDelay.Value && Flat(a.T.position - c.Home) > 2.5f) { s = "IDLE - returns in " + (NpcModule.IdleReturnDelay.Value - (now - c.BusyAt)).ToString("0") + " s"; col = new Color(1f, 0.9f, 0.4f); }
                    else s = "IDLE at home" + (c.PointsState == 1 ? " (" + (c.Points != null ? c.Points.Count : 0) + " patrol pts)" : c.PointsState == -1 ? " (no map yet)" : "");
                }
                if (c.Leg == Leg.Travel) { s = "TRAVEL " + Flat(a.T.position - c.Goal).ToString("0") + " m" + (c.Ord != null && c.Ord.Tries > 0 ? " (try " + (c.Ord.Tries + 1) + ")" : ""); col = Color.cyan; }
                if (s == null) continue;
                Vector3 top = a.Col != null ? new Vector3(a.Col.bounds.center.x, a.Col.bounds.max.y, a.Col.bounds.center.z) : a.T.position + Vector3.up * 2f;
                DiagnosticMarkers.Label(markers, top + Vector3.up * (c.InSearch ? 0.65f : 0.35f), s, col);   // the senses' own label (SEARCH ...) sits at +0.35
            }
        }
    }
}
