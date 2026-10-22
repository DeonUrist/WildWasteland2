using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using NWH.VehiclePhysics2;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;
using WildWasteland.Core.Events;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Player cockpit controls (Vehicles part of Car; the driving hints part is HUD's Car.Hints.cs): cockpit entry facing forward,
    /// ignition hotkey (one press sends the panel's MousePickEvent click through the FSM, or goes to the IVehicleIgnition provider),
    /// cassette start/stop and volume keys, the forced steering return-to-centre of the player's car, and the cockpit state that
    /// IVehicleControls reports and VehicleIgnition / VehicleControlsState publish.
    /// </summary>
    internal static partial class Car
    {
        private static bool _ignitionPress;
        private static PlayMakerFSM _start;
        private static Transform _startCar;
        private static Transform _viewCar;
        private static string _hint = "";
        private static Text _gameText;       // the game's own hint text (what the START FSM writes "Start" into): its font
        private static Font _font;
        private static VehicleController _engineCar;
        private static PlayMakerFSM _lightOn, _lightOff, _handbrake, _radioOnOff, _play, _stop, _volume;
        private static AudioSource _radioAudio;
        private static float _nextControls, _startRetry;
        private static bool _running, _lightsOn, _brakeOn, _musicPlaying;
        private static bool _simIgnition, _simKeyOn, _simRunning, _simCranking;
        private static bool _keyOn, _cranking, _published;
        private static float _publishedVolume = -1f;
        private static Steering _steering;
        private static bool _steeringWas;

        private static readonly string[] RunningStates = { "Start", "wait", "off3", "over3" };

        /// <summary>Old per-frame entry called from PlayerBody's BodyRunner; the Vehicles job "Vehicles.Cockpit" runs <see cref="TickCockpit"/> instead.</summary>
        [Obsolete("refactor: the Vehicles module ticks the cockpit itself (Car.TickCockpit)")]
        public static void Tick() { }

        /// <summary>The player's current cockpit root (null when not seated or the module is off).</summary>
        internal static Transform Current { get { return _startCar; } }

        /// <summary>
        /// Every-frame job "Vehicles.Cockpit" (Update): while the player is seated, faces the view forward on entry, finds the car's ignition,
        /// light, handbrake and radio FSMs (once per car, light/radio every 2 s), handles the cassette/volume keys and the ignition key,
        /// keeps the steering returning to centre and publishes cockpit changes. Early-out: module off, player not ready / not in a car / dead.
        /// </summary>
        internal static void TickCockpit()
        {
            _hint = "";
            if (!VehiclesModule.Running || !PlayerState.Ready || !PlayerState.InCar || PlayerState.Dead) { _viewCar = null; ReleaseSteering(); return; }
            var car = PlayerState.CarRoot;
            if (car == null) return;
            if (_viewCar != car && PlayerState.CameraHolder != null && PlayerState.PlayerCamera != null)
            {
                var look = FsmUtil.Find(PlayerState.Player, "Look");
                if (look != null && look.Fsm.Initialized)
                {
                    PlayerState.CameraHolder.localRotation = Quaternion.identity;
                    PlayerState.PlayerCamera.localRotation = Quaternion.identity;
                    // MouseLook.OnEnter recaptures its private yaw/pitch from the reset holder.
                    look.Fsm.ReEnterState(); _viewCar = car;
                    if (Log.L2) Log.Deep(VehiclesModule.Instance, "Cockpit entry: facing forward in " + car.name);
                }
            }
            // (2.2.6) searched once per car (again every 5 s while nothing was found - parts can arrive late), not on every frame
            if (_startCar != car || (_start == null && Time.unscaledTime >= _startRetry))
            {
                bool newCar = _startCar != car;
                if (newCar) ReleaseSteering();
                _startCar = car; _startRetry = Time.unscaledTime + 5f; _start = null; _engineCar = null;
                _nextControls = 0f;
                _lightOn = _lightOff = _handbrake = _radioOnOff = _play = _stop = _volume = null;
                _radioAudio = null;
                foreach (var f in car.GetComponentsInChildren<PlayMakerFSM>(true))
                    if (f.FsmName == "Start" && f.name == "START" && HasState(f, "Ignition")) { _start = f; break; }
                _engineCar = car.GetComponent<VehicleController>();
                if (_start != null) FindGameText();
                if (_start != null && Log.L2) Log.Deep(VehiclesModule.Instance, "Ignition: " + PathOf(_start.transform) + (_engineCar != null ? ", engine state from the vehicle controller" : ", engine state from the FSM only"));
                if (newCar && Log.L1) Log.Main(VehiclesModule.Instance, "Car: " + car.name + " - ignition " + (_start != null ? "found" : "not found (hotkey off)") + ", engine state " + (_engineCar != null ? "from the vehicle" : "from the FSM"));
            }
            HoldSteering(car);
            if (PressedAction("Headlight", KeyCode.None) || Pressed(VehiclesSettings.CassetteKeyCode) || Pressed(VehiclesSettings.VolumeDownKeyCode) || Pressed(VehiclesSettings.VolumeUpKeyCode))
                _nextControls = 0f;                           // a hotkey acts on the controls as they are now
            if (Time.unscaledTime >= _nextControls)
            {
                _nextControls = Time.unscaledTime + 2f;      // (2.2.6) was 0.5 s: three searches through the whole car
                FindControls(car);
            }
            if (PlayerState.Paused) return;
            if (Pressed(VehiclesSettings.CassetteKeyCode) && _radioAudio != null && _radioAudio.clip != null)
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
            if (Pressed(VehiclesSettings.VolumeDownKeyCode)) SetVolume(RadioVolume - 0.1f);
            if (Pressed(VehiclesSettings.VolumeUpKeyCode)) SetVolume(RadioVolume + 0.1f);
            TickIgnition();
            ReadCockpit();
            UpdateHints();
        }

        /// <summary>Forces NWH Steering.returnToCenter on the player's car while seated (native smoothing, road-wheel angles and visual return together); the original value is restored on leaving.</summary>
        private static void HoldSteering(Transform car)
        {
            if (_steering == null)
            {
                var vc = car.GetComponent<VehicleController>();
                if (vc == null || vc.steering == null) return;
                _steering = vc.steering; _steeringWas = _steering.returnToCenter;
            }
            if (!_steering.returnToCenter) _steering.returnToCenter = true;
        }

        /// <summary>Puts the remembered returnToCenter value back on the last car (player left it, car changed, module off).</summary>
        private static void ReleaseSteering()
        {
            if (_steering != null) { try { _steering.returnToCenter = _steeringWas; } catch (Exception) { } }
            _steering = null;
        }

        /// <summary>Installs the MousePickEvent.DoMousePickEvent prefix that turns one ignition key press into one panel click.</summary>
        internal static void Patch(IModule owner)
        {
            Patches.Patch(owner, Patches.Method(typeof(HutongGames.PlayMaker.Actions.MousePickEvent), "DoMousePickEvent"),
                prefix: Patches.Method(typeof(Car), "BeforeMousePick"));
        }

        /// <summary>Ignition key: the IVehicleIgnition provider (Engine Sim) first, otherwise one click of the START FSM's active MousePickEvent.</summary>
        private static void TickIgnition()
        {
            _simIgnition = false;
            var ignition = ModApi.Get<IVehicleIgnition>();
            bool input = VehiclesSettings.IgnitionKey.Value && !ApocasetterBridge.InputBlocked && Application.isFocused;
            if (ignition != null && ignition.TryPlayer(_startCar.gameObject,
                input && PressedAction(GameBindings.IgnitionAction, IgnitionControl.KeyCode),
                input && HeldAction(GameBindings.IgnitionAction, IgnitionControl.KeyCode), out _simKeyOn, out _simRunning, out _simCranking))
            { _simIgnition = true; return; }
            if (_start == null || !VehiclesSettings.IgnitionKey.Value || !Active(_start) || ApocasetterBridge.InputBlocked) return;
            if (!PressedAction(GameBindings.IgnitionAction, IgnitionControl.KeyCode)) return;
            try
            {
                if (Log.L2) Log.Deep(VehiclesModule.Instance, "Ignition: key press in " + _start.ActiveStateName);
                _ignitionPress = true;
                foreach (var action in _start.Fsm.ActiveState.Actions)
                    if (action.Enabled && action is HutongGames.PlayMaker.Actions.MousePickEvent) { ((HutongGames.PlayMaker.Actions.MousePickEvent)action).OnUpdate(); break; }
            }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "Ignition: " + e.Message); }
            finally { _ignitionPress = false; }
        }

        /// <summary>
        /// Prefix of MousePickEvent.DoMousePickEvent (every MousePickEvent update of every FSM): while an ignition key press is being replayed on
        /// the START FSM, sends its mouse-down (or mouse-over) event instead of the physical mouse check. Early-out: no press pending / other FSM.
        /// </summary>
        private static bool BeforeMousePick(HutongGames.PlayMaker.Actions.MousePickEvent __instance)
        {
            if (!_ignitionPress || _start == null || __instance.Fsm != _start.Fsm) return true;
            var ev = __instance.mouseDown;
            if (FsmEvent.IsNullOrEmpty(ev)) ev = __instance.mouseOver;
            else _ignitionPress = false;      // one click, even if entering another pick state
            if (!FsmEvent.IsNullOrEmpty(ev))
            {
                if (Log.L2) Log.Deep(VehiclesModule.Instance, "Ignition: panel event " + ev.Name + " from " + __instance.Fsm.ActiveStateName);
                __instance.Fsm.Event(ev);
            }
            return false;
        }

        /// <summary>
        /// Reads the cockpit state once per frame (engine, key, lights, handbrake, music) for IVehicleControls, and publishes VehicleIgnition /
        /// VehicleControlsState when a value changed (only when somebody listens).
        /// </summary>
        private static void ReadCockpit()
        {
            string state = _start != null ? _start.ActiveStateName ?? "" : "";
            _running = _start != null && EngineRunning(state);
            _keyOn = _simIgnition ? _simKeyOn : _start != null && state != "off" && state != "over" && state.Length > 0;
            _cranking = _simIgnition && _simCranking;
            // LightOn is the action enabled when lights are ON (its useDoor switches them off).
            _lightsOn = Active(_lightOn);
            string brake = _handbrake != null ? _handbrake.ActiveStateName : "";
            _brakeOn = brake == "HandbrakeOn" || brake == "over" || brake == "Sound 2";
            _musicPlaying = _radioAudio != null && _radioAudio.isPlaying && _radioAudio.pitch > 0f;
            Publish();
        }

        private static bool _pubRunning, _pubKey, _pubCrank, _pubLights, _pubBrake, _pubMusic;
        /// <summary>Publishes the changed parts of the cockpit state as VehicleIgnition / VehicleControlsState events.</summary>
        private static void Publish()
        {
            bool first = !_published; _published = true;
            if (first || _pubRunning != _running || _pubKey != _keyOn || _pubCrank != _cranking)
            {
                _pubRunning = _running; _pubKey = _keyOn; _pubCrank = _cranking;
                if (ModEvents.HasListeners<VehicleIgnition>())
                {
                    var ev = new VehicleIgnition { Vehicle = Entities.Of(_startCar.gameObject), On = _keyOn, Running = _running, Cranking = _cranking, Source = _simIgnition ? IgnitionSource.EngineSim : IgnitionSource.Player };
                    ModEvents.Publish(ref ev);
                }
            }
            float volume = RadioVolume;
            if (first || _pubLights != _lightsOn || _pubBrake != _brakeOn || _pubMusic != _musicPlaying || _publishedVolume != volume)
            {
                _pubLights = _lightsOn; _pubBrake = _brakeOn; _pubMusic = _musicPlaying; _publishedVolume = volume;
                if (ModEvents.HasListeners<VehicleControlsState>())
                {
                    var ev = new VehicleControlsState { Vehicle = Entities.Of(_startCar.gameObject), CassettePlaying = _musicPlaying, Volume = VolumeLabel(volume), Handbrake = _brakeOn, Headlights = _lightsOn };
                    ModEvents.Publish(ref ev);
                }
            }
        }

        /// <summary>Volume as the one-decimal label the HUD shows ("0.5").</summary>
        internal static string VolumeLabel(float v) { return v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }

        /// <summary>Fills the cockpit state of the player's current car; false when <paramref name="vehicle"/> is not that car.</summary>
        internal static bool TryCockpit(GameObject vehicle, out CockpitState state)
        {
            state = default(CockpitState);
            if (vehicle == null || _startCar == null || vehicle.transform != _startCar) return false;
            state.KeyOn = _keyOn; state.Running = _running; state.Cranking = _cranking; state.Handbrake = _brakeOn;
            state.Headlights = _lightsOn; state.CassettePlaying = _musicPlaying; state.Volume = VolumeLabel(RadioVolume);
            return true;
        }

        /// <summary>True for an enabled, active and initialised FSM.</summary>
        private static bool Active(PlayMakerFSM f) { return f != null && f.isActiveAndEnabled && f.Fsm != null && f.Fsm.Initialized; }
        /// <summary>Key went down this frame (legacy input); + / - also accept the keypad and the physical = key.</summary>
        private static bool Pressed(KeyCode key)
        {
            if (key == KeyCode.None) return false;
            if (Input.GetKeyDown(key)) return true;
            // Unity reports the physical = key for + on the main keyboard.
            if (key == KeyCode.Equals || key == KeyCode.Plus) return GameBindings.KeyDown(KeyCode.Equals) || GameBindings.KeyDown(KeyCode.Plus) || GameBindings.KeyDown(KeyCode.KeypadPlus);
            if (key == KeyCode.Minus) return GameBindings.KeyDown(KeyCode.KeypadMinus);
            return false;
        }
        /// <summary>The game's Controls action went down this frame, or the fallback key when the action does not exist.</summary>
        private static bool PressedAction(string action, KeyCode fallback)
        { return GameBindings.Find(action) != null ? GameBindings.Down(action, KeyCode.None) : Pressed(fallback); }
        /// <summary>The game's Controls action is held, or the fallback key when the action does not exist.</summary>
        private static bool HeldAction(string action, KeyCode fallback)
        {
            var binding = GameBindings.Find(action);
            return binding != null ? GameBindings.KeyHeld(binding.Key) || GameBindings.KeyHeld(binding.AlternativeKey) : GameBindings.KeyHeld(fallback);
        }

        // (WW perf) the three part transforms are kept per car while they are still inside it (FindDeep read every transform's name of the
        // whole car three times every 2 s); FSM lists through one reused List
        private static Transform _ctlCar, _ctlLights, _ctlBrake, _ctlRadio;
        private static readonly List<PlayMakerFSM> _ctlFsms = new List<PlayMakerFSM>();
        /// <summary>The cached part transform while it is still inside the car, otherwise a fresh deep search by name.</summary>
        private static Transform Part(Transform car, Transform cached, string name)
        {
            return cached != null && cached.IsChildOf(car) ? cached : FindDeep(car, name);
        }
        /// <summary>Finds the light switch, handbrake and radio FSMs of the car (every 2 s and on a control hotkey).</summary>
        private static void FindControls(Transform car)
        {
            _lightOn = _lightOff = _handbrake = _radioOnOff = _play = _stop = _volume = null;
            _radioAudio = null;
            if (!ReferenceEquals(_ctlCar, car)) { _ctlCar = car; _ctlLights = _ctlBrake = _ctlRadio = null; }
            var lights = _ctlLights = Part(car, _ctlLights, "switch_lights");
            if (lights != null)
            {
                lights.GetComponents(_ctlFsms);
                foreach (var f in _ctlFsms)
                {
                    if (f.FsmName == "LightOn") _lightOn = f;
                    if (f.FsmName == "LightOff") _lightOff = f;
                }
            }
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

        /// <summary>Current radio volume (0 without a radio).</summary>
        private static float RadioVolume { get { return _radioAudio != null ? _radioAudio.volume : 0f; } }
        /// <summary>Sets the radio volume in 0.1 steps (clamped 0..1) on the AudioSource and the Volume FSM's audioVolume.</summary>
        private static void SetVolume(float value)
        {
            if (_radioAudio == null) return;
            float v = Mathf.Clamp01(Mathf.Round(value * 10f) / 10f);
            _radioAudio.volume = v;
            var variable = _volume != null ? _volume.FsmVariables.FindFsmFloat("audioVolume") : null;
            if (variable != null) variable.Value = v;
        }

        /// <summary>True when the FSM has a state of that name.</summary>
        private static bool HasState(PlayMakerFSM f, string name)
        {
            try { foreach (var st in f.FsmStates) if (st.Name == name) return true; } catch (Exception) { }
            return false;
        }

        /// <summary>
        /// Engine running: the ignition provider's state, otherwise the typed NWH engine (README 8 #7: was a PropertyInfo read with boxing every
        /// frame) or the START state "Start", then the running FSM states and the FSM's engineIsRunning bool.
        /// </summary>
        private static bool EngineRunning(string state)
        {
            if (_simIgnition) return _simRunning;
            if (_engineCar != null)
            {
                try
                {
                    var engine = _engineCar.powertrain != null ? _engineCar.powertrain.engine : null;
                    if (engine != null) return engine.IsRunning || state == "Start";
                }
                catch (Exception) { }
            }
            if (Array.IndexOf(RunningStates, state) >= 0) return true;
            try { var b = _start.FsmVariables.GetFsmBool("engineIsRunning"); if (b != null && b.Value) return true; } catch (Exception) { }
            return false;
        }

        /// <summary>Slash-separated hierarchy path of a transform (log text).</summary>
        private static string PathOf(Transform t)
        {
            if (t == null) return "";
            string p = t.name;
            for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }

        /// <summary>Finds a named transform recursively (self first, then children in order).</summary>
        private static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>Clears every cached cockpit reference (scene change, PlayerBody off/on); keeps the remembered steering value.</summary>
        public static void Reset()
        {
            _ignitionPress = false; _start = null; _startCar = _viewCar = null; _hint = ""; _gameText = null; _font = null; _engineCar = null;
            _lightOn = _lightOff = _handbrake = _radioOnOff = _play = _stop = _volume = null;
            _radioAudio = null; _nextControls = 0f;
            _running = _lightsOn = _brakeOn = _musicPlaying = _keyOn = _cranking = _published = false; _publishedVolume = -1f;
            _sigValid = false; _hintText = "";
        }

        /// <summary>Vehicles shutdown: the steering centring goes back to the car's own value and the cockpit state is cleared.</summary>
        internal static void Release() { ReleaseSteering(); Reset(); }
    }
}
