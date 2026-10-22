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
    // Ignition hotkey: one press sends the panel's MousePickEvent click event through the FSM.
    // The input action also handles the key while entering hover states, without a physical mouse-over check.
    // Running = the engine says so (NWH VehicleController.powertrain.engine.IsRunning, by reflection) or the FSM is in a running state.
    internal static partial class Car
    {
        private static bool _ignitionPress;
        private static PlayMakerFSM _start;
        private static Transform _startCar;
        private static Transform _viewCar;
        private static string _hint = "";
        private static Text _gameText;       // the game's own hint text (what the START FSM writes "Start" into): its font
        private static Font _font;
        private static object _engine;
        private static PropertyInfo _isRunning;
        private static PlayMakerFSM _lightOn, _lightOff, _handbrake, _radioOnOff, _play, _stop, _volume;
        private static AudioSource _radioAudio;
        private static float _nextControls, _startRetry;
        private static bool _running, _lightsOn, _brakeOn, _musicPlaying;
        private static bool _simIgnition, _simKeyOn, _simRunning, _simCranking;

        private static readonly string[] RunningStates = { "Start", "wait", "off3", "over3" };

        public static void Tick()
        {
            _hint = "";
            if (!PlayerBodyModule.Running || !Game.Ready || !Game.InCar || Game.Dead) { _viewCar = null; return; }
            var car = Game.CarRoot;
            if (car == null) return;
            if (_viewCar != car && Game.CameraHolder != null && Game.PlayerCamera != null)
            {
                var look = Core.FsmUtil.Find(Game.Player, "Look");
                if (look != null && look.Fsm.Initialized)
                {
                    Game.CameraHolder.localRotation = Quaternion.identity;
                    Game.PlayerCamera.localRotation = Quaternion.identity;
                    // MouseLook.OnEnter recaptures its private yaw/pitch from the reset holder.
                    look.Fsm.ReEnterState(); _viewCar = car;
                    PlayerBodyModule.Verbose("Cockpit entry: facing forward in " + car.name);
                }
            }
            // (2.2.6) searched once per car (again every 5 s while nothing was found - parts can arrive late), not on every frame
            if (_startCar != car || (_start == null && Time.unscaledTime >= _startRetry))
            {
                bool newCar = _startCar != car;
                _startCar = car; _startRetry = Time.unscaledTime + 5f; _start = null; _engine = null; _isRunning = null;
                _nextControls = 0f;
                _lightOn = _lightOff = _handbrake = _radioOnOff = _play = _stop = _volume = null;
                _radioAudio = null;
                foreach (var f in car.GetComponentsInChildren<PlayMakerFSM>(true))
                    if (f.FsmName == "Start" && f.name == "START" && HasState(f, "Ignition")) { _start = f; break; }
                FindEngine(car);
                if (_start != null) FindGameText();
                if (_start != null) PlayerBodyModule.Verbose("Ignition: " + Game.PathOf(_start.transform) + (_engine != null ? ", engine state from " + _engine.GetType().Name : ", engine state from the FSM only"));
                if (newCar) PlayerBodyModule.Info("Car: " + car.name + " - ignition " + (_start != null ? "found" : "not found (hotkey off)") + ", engine state " + (_engine != null ? "from the vehicle" : "from the FSM"));
            }
            if (PressedAction("Headlight", KeyCode.None) || Pressed(PlayerBodyModule.CassetteKeyCode) || Pressed(PlayerBodyModule.VolumeDownKeyCode) || Pressed(PlayerBodyModule.VolumeUpKeyCode))
                _nextControls = 0f;                           // a hotkey acts on the controls as they are now
            if (Time.unscaledTime >= _nextControls)
            {
                _nextControls = Time.unscaledTime + 2f;      // (2.2.6) was 0.5 s: three searches through the whole car
                FindControls(car);
            }
            if (Game.Paused) return;
            if (Pressed(PlayerBodyModule.CassetteKeyCode) && _radioAudio != null && _radioAudio.clip != null)
            {
                bool playing = _radioOnOff != null && _radioOnOff.ActiveStateName == "on";
                var control = playing ? _stop : _play;
                if (control != null && control.gameObject.activeInHierarchy && control.Fsm.Initialized)
                {
                    if (!playing && _radioAudio.volume == 0f) SetVolume(0.5f);
                    // The game's Play/Stop FSMs disable one another. Re-enter the action
                    // explicitly so repeated hotkey presses also work with a dormant FSM.
                    control.enabled = true;
                    control.Fsm.SetState(playing ? "stop" : "play");
                }
            }
            if (Pressed(PlayerBodyModule.VolumeDownKeyCode)) SetVolume(RadioVolume - 0.1f);
            if (Pressed(PlayerBodyModule.VolumeUpKeyCode)) SetVolume(RadioVolume + 0.1f);
            TickIgnition();
            UpdateHints();
        }

        internal static void Patch(PlayerBodyModule m)
        {
            Core.Patches.Patch(m, Core.Patches.Method(typeof(HutongGames.PlayMaker.Actions.MousePickEvent), "DoMousePickEvent"),
                prefix: Core.Patches.Method(typeof(Car), "BeforeMousePick"));
        }

        private static void TickIgnition()
        {
            _simIgnition = false;
            var ignition = Core.Services.VehicleIgnition;
            bool input = PlayerBodyModule.IgnitionControlOn.Value && !Core.ApocasetterBridge.InputBlocked && Application.isFocused;
            if (ignition != null && ignition.TryPlayer(_startCar.gameObject,
                input && PressedAction(GameBindings.IgnitionAction, PlayerBodyModule.IgnitionKeyCode),
                input && HeldAction(GameBindings.IgnitionAction, PlayerBodyModule.IgnitionKeyCode), out _simKeyOn, out _simRunning, out _simCranking))
            { _simIgnition = true; return; }
            if (_start == null || !PlayerBodyModule.IgnitionControlOn.Value || !Active(_start) || Core.ApocasetterBridge.InputBlocked) return;
            if (!PressedAction(GameBindings.IgnitionAction, PlayerBodyModule.IgnitionKeyCode)) return;
            try
            {
                PlayerBodyModule.Verbose("Ignition: key press in " + _start.ActiveStateName);
                _ignitionPress = true;
                foreach (var action in _start.Fsm.ActiveState.Actions)
                    if (action.Enabled && action is HutongGames.PlayMaker.Actions.MousePickEvent) { ((HutongGames.PlayMaker.Actions.MousePickEvent)action).OnUpdate(); break; }
            }
            catch (Exception e) { PlayerBodyModule.Warn("Ignition: " + e.Message); }
            finally { _ignitionPress = false; }
        }

        private static bool BeforeMousePick(HutongGames.PlayMaker.Actions.MousePickEvent __instance)
        {
            if (!_ignitionPress || _start == null || __instance.Fsm != _start.Fsm) return true;
            var ev = __instance.mouseDown;
            if (FsmEvent.IsNullOrEmpty(ev)) ev = __instance.mouseOver;
            else _ignitionPress = false;      // one click, even if entering another pick state
            if (!FsmEvent.IsNullOrEmpty(ev))
            {
                PlayerBodyModule.Verbose("Ignition: panel event " + ev.Name + " from " + __instance.Fsm.ActiveStateName);
                __instance.Fsm.Event(ev);
            }
            return false;
        }

        private static bool Active(PlayMakerFSM f) { return f != null && f.isActiveAndEnabled && f.Fsm != null && f.Fsm.Initialized; }
        private static bool Pressed(KeyCode key)
        {
            if (key == KeyCode.None) return false;
            if (Input.GetKeyDown(key)) return true;
            // Unity reports the physical = key for + on the main keyboard.
            if (key == KeyCode.Equals || key == KeyCode.Plus) return GameBindings.KeyDown(KeyCode.Equals) || GameBindings.KeyDown(KeyCode.Plus) || GameBindings.KeyDown(KeyCode.KeypadPlus);
            if (key == KeyCode.Minus) return GameBindings.KeyDown(KeyCode.KeypadMinus);
            return false;
        }
        private static bool PressedAction(string action, KeyCode fallback)
        { return GameBindings.Find(action) != null ? GameBindings.Down(action, KeyCode.None) : Pressed(fallback); }
        private static bool HeldAction(string action, KeyCode fallback)
        {
            var binding = GameBindings.Find(action);
            return binding != null ? GameBindings.KeyHeld(binding.Key) || GameBindings.KeyHeld(binding.AlternativeKey) : GameBindings.KeyHeld(fallback);
        }

        // (WW perf) the three part transforms are kept per car while they are still inside it (FindDeep read every transform's name of the
        // whole car three times every 2 s); FSM lists through one reused List
        private static Transform _ctlCar, _ctlLights, _ctlBrake, _ctlRadio;
        private static readonly List<PlayMakerFSM> _ctlFsms = new List<PlayMakerFSM>();
        private static Transform Part(Transform car, Transform cached, string name)
        {
            return cached != null && cached.IsChildOf(car) ? cached : Game.FindDeep(car, name);
        }
        private static void FindControls(Transform car)
        {
            _lightOn = _lightOff = _handbrake = _radioOnOff = _play = _stop = _volume = null;
            _radioAudio = null;
            if (!ReferenceEquals(_ctlCar, car)) { _ctlCar = car; _ctlLights = _ctlBrake = _ctlRadio = null; }
            var lights = _ctlLights = Part(car, _ctlLights, "switch_lights");
            if (lights != null) { lights.GetComponents(_ctlFsms); foreach (var f in _ctlFsms)
            {
                if (f.FsmName == "LightOn") _lightOn = f;
                if (f.FsmName == "LightOff") _lightOff = f;
            } }
            var brake = _ctlBrake = Part(car, _ctlBrake, "handbrake");
            if (brake != null) { brake.GetComponents(_ctlFsms); foreach (var f in _ctlFsms) if (f.FsmName == "Handbrake") _handbrake = f; }
            var radio = _ctlRadio = Part(car, _ctlRadio, "hinge_radio");
            _ctlFsms.Clear();
            if (radio == null) return;
            radio.GetComponentsInChildren(true, _ctlFsms);
            foreach (var f in _ctlFsms)
                if (f.FsmName == "OnOff" && f.gameObject.activeInHierarchy && f.GetComponent<AudioSource>() != null)
                { _radioOnOff = f; _radioAudio = f.GetComponent<AudioSource>(); break; }
            _ctlFsms.Clear();
            if (_radioOnOff == null) return;
            _radioOnOff.GetComponentsInChildren(true, _ctlFsms);
            foreach (var f in _ctlFsms)
            {
                if (f.FsmName == "Play" && f.name == "PlayStop") _play = f;
                if (f.FsmName == "Stop" && f.name == "PlayStop") _stop = f;
                if (f.FsmName == "Volume" && f.name == "Volume") _volume = f;
            }
            _ctlFsms.Clear();
        }

        private static float RadioVolume { get { return _radioAudio != null ? _radioAudio.volume : 0f; } }
        private static void SetVolume(float value)
        {
            if (_radioAudio == null) return;
            float v = Mathf.Clamp01(Mathf.Round(value * 10f) / 10f);
            _radioAudio.volume = v;
            var variable = _volume != null ? _volume.FsmVariables.FindFsmFloat("audioVolume") : null;
            if (variable != null) variable.Value = v;
        }


        private static bool HasState(PlayMakerFSM f, string name)
        {
            try { foreach (var st in f.FsmStates) if (st.Name == name) return true; } catch (Exception) { }
            return false;
        }

        private static bool EngineRunning(string state)
        {
            if (_simIgnition) return _simRunning;
            if (_engine != null && _isRunning != null)
            {
                try { return (bool)_isRunning.GetValue(_engine, null) || state == "Start"; } catch (Exception) { }
            }
            if (Array.IndexOf(RunningStates, state) >= 0) return true;
            try { var b = _start.FsmVariables.GetFsmBool("engineIsRunning"); if (b != null && b.Value) return true; } catch (Exception) { }
            return false;
        }

        // NWH Vehicle Physics 2: VehicleController.powertrain.engine.IsRunning
        private static void FindEngine(Transform car)
        {
            try
            {
                foreach (var c in car.GetComponents<Component>())
                {
                    if (c == null || c.GetType().Name != "VehicleController") continue;
                    var pt = Member(c, "powertrain");
                    var en = pt != null ? Member(pt, "engine") : null;
                    if (en == null) return;
                    var p = en.GetType().GetProperty("IsRunning", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null && p.PropertyType == typeof(bool)) { _engine = en; _isRunning = p; }
                    return;
                }
            }
            catch (Exception) { }
        }

        private static object Member(object o, string name)
        {
            var t = o.GetType();
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) return f.GetValue(o);
            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return p != null ? p.GetValue(o, null) : null;
        }


        public static void Reset()
        {
            _ignitionPress = false; _start = null; _startCar = _viewCar = null; _hint = ""; _gameText = null; _font = null; _engine = null; _isRunning = null;
            _lightOn = _lightOff = _handbrake = _radioOnOff = _play = _stop = _volume = null;
            _radioAudio = null; _nextControls = 0f;
            _running = _lightsOn = _brakeOn = _musicPlaying = false;
            _sigValid = false; _hintText = "";
        }
    }
}
