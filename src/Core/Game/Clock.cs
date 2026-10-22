using System;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Core
{
    /// <summary>
    /// The game clock from __GameManager__ [DayTime] (int Day, float Time = hour of day) and day/night from Azure[Sky], replacing
    /// Quests Game.GameHours, Patrols TrafficReinforcements.CallHour and Patrols DayLight (CleanHUD MessageLog and Performance
    /// HudFsms only filter/step the DayTime FSM and keep doing so). The FSM and its two variables are cached (re-resolved when
    /// the FSM object dies, at most once per second while missing) and cleared on scene load; reads do not allocate.
    /// Note (Performance CT7): with "HUD FSMs" on, Time may be up to 0.2 s stale; unchanged from today.
    /// </summary>
    public static class Clock
    {
        static PlayMakerFSM _fsm;
        static FsmInt _day;
        static FsmFloat _time;
        static float _nextFind;
        static bool _installed;
        static bool _fallbackTracked;

        // day/night (Patrols DayLight)
        static UnityEngine.AzureSky.AzureTimeController _azure;
        static float _nextAzure, _nextEval;
        static bool _dark, _known;

        /// <summary>The DayTime FSM, or null while it cannot be found.</summary>
        public static PlayMakerFSM Fsm { get { Resolve(); return _fsm; } }

        /// <summary>True when the DayTime FSM exists and is initialised (TrafficReinforcements' precondition).</summary>
        public static bool Ready { get { Resolve(); return _fsm != null && _fsm.Fsm != null && _fsm.Fsm.Initialized && _day != null && _time != null; } }

        /// <summary>
        /// Hours since day 0 (Day * 24 + Time; sleeping and time skips count, pausing does not); -1 when the clock is not found
        /// (Quests Game.GameHours semantics: no initialisation check - use <see cref="Ready"/> for TrafficReinforcements').
        /// </summary>
        public static double GameHours
        {
            get
            {
                Resolve();
                if (_day == null || _time == null) return -1.0;
                return _day.Value * 24.0 + _time.Value;
            }
        }

        /// <summary>Current day number, -1 when unknown.</summary>
        public static int Day { get { Resolve(); return _day != null ? _day.Value : -1; } }

        /// <summary>Hour of day 0..24, -1 when unknown.</summary>
        public static float HourOfDay { get { Resolve(); return _time != null ? _time.Value : -1f; } }

        /// <summary>
        /// Dark outside (Patrols DayLight, verbatim rules): Azure sun elevation below 0, light again above 0.05 (hysteresis);
        /// without Azure, DayTime.Time before 6:00 or from 19:30. Re-evaluated at most every 2 s; Azure looked up every 10 s while
        /// missing (GameObject.Find by name, then FindObjectOfType - throttled, never per frame).
        /// </summary>
        public static bool Dark
        {
            get
            {
                if (_known && Time.unscaledTime < _nextEval) return _dark;
                _nextEval = Time.unscaledTime + 2f;
                try { EvaluateDark(); }
                catch (Exception e) { Log.Warn("[Core] DayLight: " + e.Message); _azure = null; _nextAzure = Time.unscaledTime + 30f; }
                return _dark;
            }
        }

        /// <summary>Forgets every cached reference (scene load).</summary>
        public static void Reset()
        {
            _fsm = null; _day = null; _time = null; _nextFind = 0f;
            _azure = null; _nextAzure = 0f; _nextEval = 0f; _known = false;
        }

        /// <summary>Finds and caches the DayTime FSM and its Day/Time variables (throttled while missing).</summary>
        static void Resolve()
        {
            if (!_installed) { _installed = true; SceneManager.sceneLoaded += SceneLoaded; }
            if (_fsm != null && _day != null && _time != null) return;
            if (Time.unscaledTime < _nextFind) return;
            _nextFind = Time.unscaledTime + 1f;
            if (_fsm == null) _fsm = FsmUtil.Find(GameRefs.GameManager, "DayTime");
            if (_fsm == null)
            {
                // Quests also accepts a DayTime FSM outside the game-manager root. Reuse the shared sliced index.
                if (!_fallbackTracked) { FsmIndex.Track(null, "DayTime"); _fallbackTracked = true; }
                var all = FsmIndex.ByName("DayTime");
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null) { _fsm = all[i]; break; }
            }
            if (_fsm == null) return;
            _day = FsmIndex.Int(_fsm, "Day");
            _time = FsmIndex.Float(_fsm, "Time");
        }

        /// <summary>Scene load: drop the cached FSM and sky.</summary>
        static void SceneLoaded(Scene scene, LoadSceneMode mode) { Reset(); }

        /// <summary>Patrols DayLight.Evaluate: Azure first, DayTime fallback.</summary>
        static void EvaluateDark()
        {
            if (_azure == null && Time.unscaledTime >= _nextAzure)
            {
                _nextAzure = Time.unscaledTime + 10f;
                var go = GameObject.Find("Azure[Sky] Dynamic Skybox");
                _azure = go != null ? go.GetComponent<UnityEngine.AzureSky.AzureTimeController>() : null;
                if (_azure == null) _azure = UnityEngine.Object.FindObjectOfType<UnityEngine.AzureSky.AzureTimeController>();
            }
            bool was = _dark;
            if (_azure != null)
            {
                float elevation = _azure.GetSunElevation();
                _dark = _known && _dark ? elevation < 0.05f : elevation < 0f;
                _known = true;
            }
            else
            {
                Resolve();
                if (_fsm == null || _fsm.Fsm == null || !_fsm.Fsm.Initialized || _time == null) return;
                _dark = _time.Value < 6f || _time.Value >= 19.5f;
                _known = true;
            }
            if (_known && was != _dark && Log.Verbose) Log.Info("[Core] DayLight: " + (_dark ? "dark" : "light") + (_azure != null ? " (sun elevation)" : " (DayTime fallback)"));
        }
    }
}
