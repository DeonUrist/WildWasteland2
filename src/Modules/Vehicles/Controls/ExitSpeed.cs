using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ES3Internal;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // =============================================================== exit speed
    // The game lets the player leave a car only below 6 m/s: PlayerCamera [DriveUse] compares the car's speed against a literal 6 in
    // its inCar / over 2 / TooFast states (FloatCompare float2). A truck shoving the player's car keeps it above that for good, so the
    // literal is replaced by [Debug] ExitSpeedKmh (converted to m/s); re-checked every few seconds (live config edits, new Player object).
    internal static class ExitSpeed
    {
        private static PlayMakerFSM _fsm;
        private static float _next, _applied = -1f;
        private static readonly List<FsmFloat> _limits = new List<FsmFloat>();

        // WW: module shutdown puts the game's own 6 m/s literal back
        internal static void Restore()
        {
            try { foreach (var v in _limits) if (v != null) v.Value = 6f; } catch (Exception) { }
            _limits.Clear(); _fsm = null; _applied = -1f; _next = 0f;
        }

        internal static void Tick()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 5f;
            float want = Mathf.Max(1f, PatrolsModule.ExitSpeedKmh.Value) / 3.6f;
            if (_fsm == null || _fsm.gameObject == null)
            {
                _fsm = null; _limits.Clear(); _applied = -1f;
                // PlayerCameraHolder/PlayerCamera (a scene root, fsm_dump 2026-10-01); under the Player in older builds
                var holder = Core.GameRefs.PlayerCameraHolder;
                var cam = holder != null ? holder.transform.Find("PlayerCamera") : null;
                var player = PlayerRef.Player;
                if (cam == null && player != null) cam = Patrol.FindChild(player, "PlayerCamera");
                if (cam == null) return;
                foreach (var f in cam.GetComponents<PlayMakerFSM>()) if (f.FsmName == "DriveUse") { _fsm = f; break; }
                if (_fsm == null) return;
                try
                {
                    foreach (var st in _fsm.FsmStates)
                        foreach (var a in st.Actions)
                        {
                            if (a == null || a.GetType().Name != "FloatCompare") continue;
                            var fld = a.GetType().GetField("float2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            var v = fld != null ? fld.GetValue(a) as FsmFloat : null;
                            if (v != null && !v.UseVariable && Mathf.Abs(v.Value - 6f) < 0.01f) _limits.Add(v);   // the vanilla literal
                        }
                }
                catch (Exception e) { Core.Log.Warn("[Patrols] " + "ExitSpeed: " + e.Message); }
                if (_limits.Count == 0) { Core.Log.Warn("[Patrols] " + "ExitSpeed: no 6 m/s compare found in PlayerCamera [DriveUse]"); return; }
            }
            if (Mathf.Abs(_applied - want) < 0.001f) return;
            foreach (var v in _limits) v.Value = want;
            _applied = want;
            PatrolsModule.Verbose("Exit speed limit set to " + PatrolsModule.ExitSpeedKmh.Value.ToString("0") + " km/h (" + _limits.Count + " compare(s) in PlayerCamera [DriveUse])");
        }
    }
}
