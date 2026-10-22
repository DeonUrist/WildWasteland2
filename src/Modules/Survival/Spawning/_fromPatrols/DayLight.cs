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
    // =============================================================== daylight
    // Is it dark outside? The game's sky is Azure[Sky] Dynamic Skybox (AzureTimeController; __GameManager__ [DayTime] reads its timeline
    // from there): dark while the sun is below the horizon (GetSunElevation < 0), light again once it is clearly up (> 0.05) - a small
    // hysteresis so the lights do not flicker at dawn. Without Azure: __GameManager__ [DayTime].Time, dark before 6:00 and from 19:30.
    internal static class DayLight
    {
        private static UnityEngine.AzureSky.AzureTimeController _azure;
        private static PlayMakerFSM _dayTime;
        private static float _nextFind, _nextEval;
        private static bool _dark, _known;

        internal static void Reset() { _azure = null; _dayTime = null; _nextFind = 0f; _nextEval = 0f; _known = false; }

        internal static bool Dark
        {
            get
            {
                if (_known && Time.unscaledTime < _nextEval) return _dark;
                _nextEval = Time.unscaledTime + 2f;
                try { Evaluate(); }
                catch (Exception e) { Core.Log.Warn("[Patrols] " + "DayLight: " + e.Message); _azure = null; _nextFind = Time.unscaledTime + 30f; }
                return _dark;
            }
        }

        private static void Evaluate()
        {
            if (_azure == null && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 10f;
                var go = GameObject.Find("Azure[Sky] Dynamic Skybox");
                _azure = go != null ? go.GetComponent<UnityEngine.AzureSky.AzureTimeController>() : null;
                if (_azure == null) _azure = UnityEngine.Object.FindObjectOfType<UnityEngine.AzureSky.AzureTimeController>();
                if (_azure == null && (_dayTime == null || _dayTime.gameObject == null))
                {
                    _dayTime = null;
                    var gm = Core.GameRefs.GameManager;
                    if (gm != null) foreach (var f in gm.GetComponents<PlayMakerFSM>()) if (f.FsmName == "DayTime") { _dayTime = f; break; }
                }
            }
            bool was = _dark;
            if (_azure != null)
            {
                float elevation = _azure.GetSunElevation();
                _dark = _known && _dark ? elevation < 0.05f : elevation < 0f;
                _known = true;
            }
            else if (_dayTime != null && _dayTime.Fsm.Initialized)
            {
                var t = _dayTime.FsmVariables.GetFsmFloat("Time");
                if (t == null) return;
                _dark = t.Value < 6f || t.Value >= 19.5f;
                _known = true;
            }
            if (_known && was != _dark) PatrolsModule.Verbose("DayLight: " + (_dark ? "dark" : "light") + (_azure != null ? " (sun elevation " + _azure.GetSunElevation().ToString("0.00") + ")" : " (DayTime fallback)"));
        }
    }
}
