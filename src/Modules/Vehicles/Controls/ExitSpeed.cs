using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// Exit speed: the game lets the player leave a car only below 6 m/s (PlayerCamera [DriveUse] compares the car's speed against a literal 6
    /// in its inCar / over 2 / TooFast states, FloatCompare float2). A truck shoving the player's car keeps it above that for good, so the
    /// literal is replaced by [Vehicles / Controls] ExitSpeedKmh (converted to m/s), re-checked every 5 s (live config edits, new Player object).
    /// </summary>
    internal static class ExitSpeed
    {
        private static PlayMakerFSM _fsm;
        private static float _next, _applied = -1f;
        private static readonly List<FsmFloat> _limits = new List<FsmFloat>();

        /// <summary>Puts the game's own 6 m/s literal back (module shutdown, scene change).</summary>
        internal static void Restore()
        {
            try { foreach (var v in _limits) if (v != null) v.Value = 6f; } catch (Exception) { }
            _limits.Clear(); _fsm = null; _applied = -1f; _next = 0f;
        }

        /// <summary>Old entry called from Patrols' Patrol.Tick; the Vehicles job "Vehicles.Exit speed" runs <see cref="Update"/> now.</summary>


        /// <summary>
        /// Interval job "Vehicles.Exit speed" (0.5 s, own 5 s throttle): finds the DriveUse FSM's 6 m/s compares once per player object and
        /// writes the configured limit when it changed. Early-out: module off, throttle, no PlayerCamera yet.
        /// </summary>
        internal static void Update()
        {
            if (!VehiclesModule.Running || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 5f;
            float want = Mathf.Max(1f, VehiclesSettings.ExitSpeedKmh.Value) / 3.6f;
            if (_fsm == null || _fsm.gameObject == null)
            {
                _fsm = null; _limits.Clear(); _applied = -1f;
                // PlayerCameraHolder/PlayerCamera (a scene root, fsm_dump 2026-10-01); under the Player in older builds
                var holder = GameRefs.PlayerCameraHolder;
                var cam = holder != null ? holder.transform.Find("PlayerCamera") : null;
                var player = PlayerState.Transform;
                if (cam == null && player != null) cam = FindChild(player, "PlayerCamera");
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
                catch (Exception e) { Log.Warn(VehiclesModule.Instance, "ExitSpeed: " + e.Message); }
                if (_limits.Count == 0) { Log.Warn(VehiclesModule.Instance, "ExitSpeed: no 6 m/s compare found in PlayerCamera [DriveUse]"); return; }
            }
            if (Mathf.Abs(_applied - want) < 0.001f) return;
            foreach (var v in _limits) v.Value = want;
            _applied = want;
            if (Log.L1) Log.Main(VehiclesModule.Instance, "Exit speed limit set to " + VehiclesSettings.ExitSpeedKmh.Value.ToString("0") + " km/h (" + _limits.Count + " compare(s) in PlayerCamera [DriveUse])");
        }

        /// <summary>Finds a named descendant, direct children first (same order as Patrols' helper).</summary>
        private static Transform FindChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++) { var c = root.GetChild(i); if (c.name == name) return c; }
            for (int i = 0; i < root.childCount; i++) { var r = FindChild(root.GetChild(i), name); if (r != null) return r; }
            return null;
        }
    }
}
