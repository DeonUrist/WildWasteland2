using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// Car part (split from PlayerBody/Vehicles/Car.cs in Wave A): the driving hints (vehicle hotkeys, cassette volume) drawn with the
    /// game's hint font while seated. Target: HUD (B10), reading the vehicle controls state from Vehicles in Wave B.
    /// </summary>
    internal sealed class VehicleHudData : IVehicleHudData
    {
        /// <summary>Native cockpit validity, excluding dead and paused frames.</summary>
        public bool Active { get { return VehiclesModule.Running && PlayerState.Ready && PlayerState.InCar && !PlayerState.Dead && !PlayerState.Paused; } }
        /// <summary>Reports an attached ignition whose engine is stopped.</summary>
        public bool IgnitionOff { get { return Car.HudIgnitionOff; } }
        /// <summary>Reports the current cockpit handbrake state.</summary>
        public bool HandbrakeOn { get { return Car.HudHandbrakeOn; } }
        /// <summary>Reports active cassette playback.</summary>
        public bool Playing { get { return Car.HudRadioPlaying; } }
        /// <summary>Formats cassette volume only when its value changes.</summary>
        public string Volume { get { return Car.VolumeText; } }
        /// <summary>Returns cached controls labels generated from current cockpit bindings.</summary>
        public string Hint { get { return Car.HudHint; } }
        /// <summary>Returns the native ignition hint font.</summary>
        public Font Font { get { return Car.HudFont; } }
        /// <summary>Returns the style of the native ignition hint.</summary>
        public FontStyle FontStyle { get { return Car.HudFontStyle; } }
        /// <summary>Returns the opaque native ignition hint tint.</summary>
        public Color HintColor { get { return Car.HudHintColor; } }
        /// <summary>Computes the native hint size at the current canvas scale.</summary>
        public int FontSize { get { return Car.HudFontSize; } }
    }
    internal static partial class Car
    {
        /// <summary>Exposes the cached handbrake state.</summary>
        internal static bool HudHandbrakeOn { get { return _brakeOn; } }
        /// <summary>Exposes the cached cassette state.</summary>
        internal static bool HudRadioPlaying { get { return _musicPlaying; } }
        internal static bool HudIgnitionOff { get { return _start != null && !_running; } }
        /// <summary>Exposes the cached controls hint string.</summary>
        internal static string HudHint { get { return _hint; } }
        /// <summary>Exposes the captured native hint font.</summary>
        internal static Font HudFont { get { return _font; } }
        internal static FontStyle HudFontStyle { get { return _font != null && _gameText != null ? _gameText.fontStyle : FontStyle.Normal; } }
        internal static Color HudHintColor { get { return _gameText != null ? new Color(_gameText.color.r, _gameText.color.g, _gameText.color.b, 1f) : Color.white; } }
        internal static int HudFontSize { get { return _gameText != null && _gameText.canvas != null ? Mathf.Max(10, Mathf.RoundToInt(_gameText.fontSize * _gameText.canvas.scaleFactor)) : Mathf.RoundToInt(Screen.height / 42f); } }
        // (WW) formatted only when the volume changes (was a new string on every OnGUI event)
        private static float _volShown = -1f; private static string _volText = "0.0";
        internal static string VolumeText
        {
            get
            {
                float v = RadioVolume;
                if (v != _volShown) { _volShown = v; _volText = v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }
                return _volText;
            }
        }
        /// <summary>Appends one explicit cockpit key label to the hint builder.</summary>
        private static void AddHint(StringBuilder hints, KeyCode key, string text)
        {
            if (key == KeyCode.None) return;
            if (hints.Length > 0) hints.Append('\n');
            hints.Append(KeyName(key)).Append(" - ").Append(text);
        }
        /// <summary>Appends a native rebindable action label to the hint builder.</summary>
        private static void AddActionHint(StringBuilder hints, string action, KeyCode fallback, string text)
        {
            var label = GameBindings.Label(action, fallback);
            if (string.IsNullOrEmpty(label)) return;
            if (hints.Length > 0) hints.Append('\n');
            hints.Append(label).Append(" - ").Append(text);
        }
        // (WW) the hint text is rebuilt only when something it shows changed (was a new string every frame in a car): the controls found,
        // their states and every key it names (the game's Controls bindings and ours) are compared as a few ints
        private static readonly int[] _sigNow = new int[10], _sigLast = new int[10];
        private static bool _sigValid;
        private static string _hintText = "";
        /// <summary>Captures both bindings of a native action for hint cache invalidation.</summary>
        private static void BindingSig(int i, string action, KeyCode fallback)
        {
            var a = GameBindings.Find(action);
            _sigNow[i] = a != null ? (int)a.Key : -1 - (int)fallback;
            _sigNow[i + 1] = a != null ? (int)a.AlternativeKey : -1;
        }
        /// <summary>Refreshes cockpit readings and rebuilds hint text only when state or bindings change.</summary>
        private static void UpdateHints()
        {
            string ignitionState = _start != null ? _start.ActiveStateName ?? "" : "";
            _running = _start != null && EngineRunning(ignitionState);
            int ignitionStep = ignitionState == "off" || ignitionState == "over" ? 1 : ignitionState == "off2" || ignitionState == "over2" ? 2 : ignitionState == "off3" || ignitionState == "over3" ? 3 : 0;
            if (_simIgnition) ignitionStep = _simCranking ? 6 : _simKeyOn ? 5 : 4;
            // LightOn is the action enabled when lights are ON (its useDoor switches them off).
            _lightsOn = Active(_lightOn);
            string brake = _handbrake != null ? _handbrake.ActiveStateName : "";
            _brakeOn = brake == "HandbrakeOn" || brake == "over" || brake == "Sound 2";
            _musicPlaying = _radioAudio != null && _radioAudio.isPlaying && _radioAudio.pitch > 0f;
            bool ignition = _start != null && VehiclesSettings.IgnitionKey.Value;
            bool playing = _radioAudio != null && _radioOnOff != null && _radioOnOff.ActiveStateName == "on";
            string ignitionAction = ignition ? GameBindings.IgnitionAction : null;
            _sigNow[0] = (ignition ? 1 : 0) | (_running ? 2 : 0) | (_lightOn != null || _lightOff != null ? 4 : 0) | (_lightsOn ? 8 : 0)
                       | (_handbrake != null ? 16 : 0) | (_brakeOn ? 32 : 0) | (_radioAudio != null ? 64 : 0) | (playing ? 128 : 0) | (ignitionStep << 8);
            if (ignition) BindingSig(1, ignitionAction, IgnitionControl.KeyCode); else _sigNow[1] = _sigNow[2] = 0;
            BindingSig(3, "Headlight", KeyCode.None);
            BindingSig(5, "Handbrake", KeyCode.None);
            _sigNow[7] = (int)VehiclesSettings.CassetteKeyCode; _sigNow[8] = (int)VehiclesSettings.VolumeDownKeyCode; _sigNow[9] = (int)VehiclesSettings.VolumeUpKeyCode;
            bool same = _sigValid;
            for (int i = 0; same && i < _sigNow.Length; i++) same = _sigNow[i] == _sigLast[i];
            if (same) { _hint = _hintText; return; }
            var hints = new StringBuilder();
            if (ignition) AddActionHint(hints, ignitionAction, IgnitionControl.KeyCode, ignitionStep == 4 ? "Ignition On / Hold to Start" : ignitionStep == 5 ? "Ignition Off / Hold to Start" : ignitionStep == 6 ? "Cranking (release to stop)" : ignitionStep == 1 ? "Ignition On" : ignitionStep == 2 ? "Start Engine" : ignitionStep == 3 ? "Stop Engine" : "Ignition (wait)");
            if (_lightOn != null || _lightOff != null) AddActionHint(hints, "Headlight", KeyCode.None, _lightsOn ? "Headlights Off" : "Headlights On");
            if (_handbrake != null) AddActionHint(hints, "Handbrake", KeyCode.None, _brakeOn ? "Handbrake Release" : "Handbrake Apply");
            if (_radioAudio != null)
            {
                AddHint(hints, VehiclesSettings.CassetteKeyCode, playing ? "Cassette Stop" : "Cassette Start");
                AddHint(hints, VehiclesSettings.VolumeDownKeyCode, "Volume Down");
                AddHint(hints, VehiclesSettings.VolumeUpKeyCode, "Volume Up");
            }
            _hintText = hints.ToString();
            Array.Copy(_sigNow, _sigLast, _sigNow.Length); _sigValid = true;
            _hint = _hintText;
        }
        // the Text the START FSM writes its hint into (UiTextSetText actions): the game's hint font
        /// <summary>Captures the native ignition hint label and its font from the START FSM.</summary>
        private static void FindGameText()
        {
            if (_gameText != null) return;
            try
            {
                foreach (var st in _start.FsmStates)
                    foreach (var a in st.Actions)
                    {
                        if (a == null || a.GetType().Name != "UiTextSetText") continue;
                        var f = a.GetType().GetField("gameObject", BindingFlags.Instance | BindingFlags.Public);
                        var od = f != null ? f.GetValue(a) as FsmOwnerDefault : null;
                        var go = od != null ? _start.Fsm.GetOwnerDefaultTarget(od) : null;
                        var t = go != null ? go.GetComponent<Text>() : null;
                        if (t != null && t.font != null) { _gameText = t; _font = t.font; Core.Log.Deep(VehiclesModule.Instance,"Ignition hint font: " + t.font.name + " (" + Core.Names.PathOf(t.transform) + ")"); return; }
                    }
            }
            catch (Exception) { }
        }

        /// <summary>Formats numeric and plus/minus cockpit keys for the hint display.</summary>
        private static string KeyName(KeyCode k)
        {
            if (k == KeyCode.Equals || k == KeyCode.Plus) return "+";
            if (k == KeyCode.Minus) return "-";
            string n = k.ToString();
            return n.StartsWith("Alpha") ? n.Substring(5) : n;
        }

    }
}
