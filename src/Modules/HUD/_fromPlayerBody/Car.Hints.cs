using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Car part (split from PlayerBody/Vehicles/Car.cs in Wave A): the driving hints (vehicle hotkeys, cassette volume) drawn with the
    /// game's hint font while seated. Target: HUD (B10), reading the vehicle controls state from Vehicles in Wave B.
    /// </summary>
    internal static partial class Car
    {
        // (WW) formatted only when the volume changes (was a new string on every OnGUI event)
        private static float _volShown = -1f; private static string _volText = "0.0";
        private static string VolumeText
        {
            get
            {
                float v = RadioVolume;
                if (v != _volShown) { _volShown = v; _volText = v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }
                return _volText;
            }
        }
        private static void AddHint(StringBuilder hints, KeyCode key, string text)
        {
            if (key == KeyCode.None) return;
            if (hints.Length > 0) hints.Append('\n');
            hints.Append(KeyName(key)).Append(" - ").Append(text);
        }
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
        private static void BindingSig(int i, string action, KeyCode fallback)
        {
            var a = GameBindings.Find(action);
            _sigNow[i] = a != null ? (int)a.Key : -1 - (int)fallback;
            _sigNow[i + 1] = a != null ? (int)a.AlternativeKey : -1;
        }
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
            if (!PlayerBodyModule.VehicleHotkeyHint.Value) { _hint = ""; return; }
            bool ignition = _start != null && PlayerBodyModule.IgnitionControlOn.Value;
            bool playing = _radioAudio != null && _radioOnOff != null && _radioOnOff.ActiveStateName == "on";
            string ignitionAction = ignition ? GameBindings.IgnitionAction : null;
            _sigNow[0] = (ignition ? 1 : 0) | (_running ? 2 : 0) | (_lightOn != null || _lightOff != null ? 4 : 0) | (_lightsOn ? 8 : 0)
                       | (_handbrake != null ? 16 : 0) | (_brakeOn ? 32 : 0) | (_radioAudio != null ? 64 : 0) | (playing ? 128 : 0) | (ignitionStep << 8);
            if (ignition) BindingSig(1, ignitionAction, PlayerBodyModule.IgnitionKeyCode); else _sigNow[1] = _sigNow[2] = 0;
            BindingSig(3, "Headlight", KeyCode.None);
            BindingSig(5, "Handbrake", KeyCode.None);
            _sigNow[7] = (int)PlayerBodyModule.CassetteKeyCode; _sigNow[8] = (int)PlayerBodyModule.VolumeDownKeyCode; _sigNow[9] = (int)PlayerBodyModule.VolumeUpKeyCode;
            bool same = _sigValid;
            for (int i = 0; same && i < _sigNow.Length; i++) same = _sigNow[i] == _sigLast[i];
            if (same) { _hint = _hintText; return; }
            var hints = new StringBuilder();
            if (ignition) AddActionHint(hints, ignitionAction, PlayerBodyModule.IgnitionKeyCode, ignitionStep == 4 ? "Ignition On / Hold to Start" : ignitionStep == 5 ? "Ignition Off / Hold to Start" : ignitionStep == 6 ? "Cranking (release to stop)" : ignitionStep == 1 ? "Ignition On" : ignitionStep == 2 ? "Start Engine" : ignitionStep == 3 ? "Stop Engine" : "Ignition (wait)");
            if (_lightOn != null || _lightOff != null) AddActionHint(hints, "Headlight", KeyCode.None, _lightsOn ? "Headlights Off" : "Headlights On");
            if (_handbrake != null) AddActionHint(hints, "Handbrake", KeyCode.None, _brakeOn ? "Handbrake Release" : "Handbrake Apply");
            if (_radioAudio != null)
            {
                AddHint(hints, PlayerBodyModule.CassetteKeyCode, playing ? "Cassette Stop" : "Cassette Start");
                AddHint(hints, PlayerBodyModule.VolumeDownKeyCode, "Volume Down");
                AddHint(hints, PlayerBodyModule.VolumeUpKeyCode, "Volume Up");
            }
            _hintText = hints.ToString();
            Array.Copy(_sigNow, _sigLast, _sigNow.Length); _sigValid = true;
            _hint = _hintText;
        }
        // the Text the START FSM writes its hint into (UiTextSetText actions): the game's hint font
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
                        if (t != null && t.font != null) { _gameText = t; _font = t.font; PlayerBodyModule.Verbose("Ignition hint font: " + t.font.name + " (" + Game.PathOf(t.transform) + ")"); return; }
                    }
            }
            catch (Exception) { }
        }

        private static string KeyName(KeyCode k)
        {
            if (k == KeyCode.Equals || k == KeyCode.Plus) return "+";
            if (k == KeyCode.Minus) return "-";
            string n = k.ToString();
            return n.StartsWith("Alpha") ? n.Substring(5) : n;
        }

        private static GUIStyle _hintStyle;
        internal static bool GuiWanted { get { return PlayerBodyModule.Running && Game.Ready && Game.InCar && !Game.Dead && !Game.Paused; } }
        public static void OnGUI()
        {
            if (!PlayerBodyModule.Running || !Game.Ready || !Game.InCar || Game.Dead || Game.Paused) return;
            if (PlayerBodyModule.VehicleStatusHint.Value) VehicleHud.Draw(_start != null && !_running, _brakeOn, _musicPlaying, VolumeText);
            if (!PlayerBodyModule.VehicleHotkeyHint.Value || string.IsNullOrEmpty(_hint)) return;
            int size = Mathf.RoundToInt(Screen.height / 42f);
            if (_gameText != null && _gameText.canvas != null)
                size = Mathf.Max(10, Mathf.RoundToInt(_gameText.fontSize * _gameText.canvas.scaleFactor));
            if (_hintStyle == null) _hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, wordWrap = false };   // (2.2.6) reused
            var st = _hintStyle; st.fontSize = size;
            st.font = _font; st.fontStyle = _font != null && _gameText != null ? _gameText.fontStyle : FontStyle.Normal;
            var r = new Rect(Screen.width * 0.025f, Screen.height * 0.62f, Screen.width * 0.5f, size * 8f);
            st.normal.textColor = new Color(0f, 0f, 0f, 0.8f); GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), _hint, st);
            st.normal.textColor = _gameText != null ? new Color(_gameText.color.r, _gameText.color.g, _gameText.color.b, 1f) : Color.white;
            GUI.Label(r, _hint, st);
        }
    }
}
