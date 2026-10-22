using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using MonoMod.Utils;
using NWH.VehiclePhysics2;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.HUD
{
    /// <summary>
    /// ChaseCamera (local.apocalypter.chasecamera) part serving the HUD: observes the mod's own driving snapshot (transpiler on
    /// DrivingReadings.Capture, no boxing) so the WW driving HUD shows its speed/RPM/gear, hides the mod's native HUD while the WW one is
    /// shown, and supplies the rendered compass heading. Readings are also published as <see cref="IDrivingTelemetry"/>
    /// (<see cref="ChaseCameraTelemetry"/>). Records only while the HUD (CleanHUD) runs.
    /// </summary>
    internal static class ChaseCameraHud
    {
        private delegate bool Heading(out float yaw);
        private static Heading _chaseHeading, _bodyHeading;
        private static Func<VehicleController, int, string> _gear;
        private static Action _hide;
        private static Func<bool> _thirdPerson, _specialView;
        private static Func<bool> _resolve;
        private static Func<Transform> _car;
        private static Func<VehicleController, bool> _capture;
        private static Transform _owner;
        private static VehicleController _drivingVehicle;
        private static float _nextVehicleSearch;
        private static object _enabled, _hudEnabled, _size, _gap;
        private static float _nextHiddenLog;
        private static string _state = "not sampled", _lastTraceState;
        private static bool _lastHudVisible;
        private static float _nextStateLog;
        private static IModule _owner_;
        private static FieldInfo[] _fields;
        private static bool _attempted, _ready;
        private static int _frame = -1, _selected;
        private static VehicleController _vehicle;
        /// <summary>True once the mod's plugin was found and resolution was attempted (ready or failed).</summary>
        internal static bool Attempted { get { return _attempted; } }
        /// <summary>Last recorded speed (km/h), rpm and rpm fraction of the redline.</summary>
        internal static float Speed, Rpm, Fraction;
        /// <summary>True when a snapshot was recorded this frame.</summary>
        internal static bool Live { get { return _ready && _frame == Time.frameCount; } }
        /// <summary>The mod's own HUD switch (shown on the WW HUD settings), or null while the bridge is not ready.</summary>
        internal static ConfigEntry<bool> HudEnabled { get { return _ready ? (ConfigEntry<bool>)_hudEnabled : null; } }
        /// <summary>The mod's HUD scale setting, clamped.</summary>
        internal static float Scale { get { return Mathf.Clamp(((ConfigEntry<float>)_size).Value, .65f, 1.75f); } }
        /// <summary>The mod's HUD gap setting, clamped.</summary>
        internal static float Gap { get { return Mathf.Clamp(((ConfigEntry<float>)_gap).Value, 0, 160); } }
        /// <summary>Gear label of the last snapshot.</summary>
        internal static string Gear { get { return _gear(_vehicle, _selected); } }

        /// <summary>Resolves the mod's readings/HUD/view API and installs the snapshot transpiler under <paramref name="owner"/> (one attempt per start).</summary>
        internal static void Install(IModule owner)
        {
            if (_attempted) return;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("local.apocalypter.chasecamera", out info) || ReferenceEquals(info.Instance, null)) return;
            _attempted = true; _owner_ = owner;
            try
            {
                if (info.Metadata.Version != new Version(0, 2, 5)) Log.Warn(owner, "ChaseCamera " + info.Metadata.Version + " differs from the tested 0.2.5; attempting HUD compatibility with this version");
                var assembly = info.Instance.GetType().Assembly;
                var readings = assembly.GetType("ApocaChaseCamera.DrivingReadings", true);
                var snapshot = assembly.GetType("ApocaChaseCamera.DrivingSnapshot", true);
                var capture = AccessTools.Method(readings, "Capture", new[] { typeof(VehicleController), snapshot.MakeByRefType() });
                if (capture == null || !capture.IsStatic || capture.ReturnType != typeof(bool)) throw new NotSupportedException("Unknown readings interface");
                _fields = new FieldInfo[4];
                string[] names = { "SpeedKph", "Rpm", "RpmFraction", "SelectedGear" };
                for (int i = 0; i < names.Length; i++)
                {
                    var field = AccessTools.Field(snapshot, names[i]);
                    if (field == null || field.IsStatic || field.FieldType != (i == 3 ? typeof(int) : typeof(float))) throw new NotSupportedException("Unknown " + names[i] + " interface");
                    _fields[i] = field;
                }
                _gear = (Func<VehicleController, int, string>)Delegate.CreateDelegate(typeof(Func<VehicleController, int, string>), AccessTools.Method(readings, "Gear"));
                _hide = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(assembly.GetType("ApocaChaseCamera.DrivingHud", true), "Hide"));
                _chaseHeading = (Heading)Delegate.CreateDelegate(typeof(Heading), AccessTools.Method(assembly.GetType("ApocaChaseCamera.ChaseView", true), "CompassHeading"));
                _bodyHeading = (Heading)Delegate.CreateDelegate(typeof(Heading), AccessTools.Method(assembly.GetType("ApocaChaseCamera.ApocaplayerBridge", true), "CompassHeading"));
                var binding = assembly.GetType("ApocaChaseCamera.CameraBinding", true);
                _resolve = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), AccessTools.Method(binding, "Resolve"));
                var car = Access.Read<Transform>(AccessTools.Field(binding, "Car"));
                _car = () => car(null);
                _thirdPerson = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), AccessTools.Method(binding, "ThirdPersonVisible"));
                _specialView = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), AccessTools.Method(binding, "SpecialView"));
                _enabled = AccessTools.Field(info.Instance.GetType(), "Enabled").GetValue(null);
                _hudEnabled = AccessTools.Field(info.Instance.GetType(), "HudEnabled").GetValue(null);
                _size = AccessTools.Field(info.Instance.GetType(), "HudScale").GetValue(null);
                _gap = AccessTools.Field(info.Instance.GetType(), "HudGap").GetValue(null);
                if (!(_enabled is ConfigEntry<bool>) || !(_hudEnabled is ConfigEntry<bool>) || !(_size is ConfigEntry<float>) || !(_gap is ConfigEntry<float>)) throw new NotSupportedException("Unknown HUD settings");
                if (!Core.Patches.Patch(owner, capture, transpiler: AccessTools.Method(typeof(ChaseCameraHud), "CaptureReadings"))) throw new InvalidOperationException("Readings hook unavailable");
                _capture = CaptureCall(capture, snapshot);
                _ready = true;
                Log.Main(owner, "ChaseCamera " + info.Metadata.Version + " HUD bridge ready: first/third-person source speed/RPM/gear, driving-only visibility, text above weapon UI and rendered compass heading.");
            }
            catch (Exception e) { _ready = false; Log.Warn(owner, "ChaseCamera HUD bridge unavailable; retaining its original HUD: " + e.Message); }
        }

        /// <summary>Generated delegate calling Capture(vehicle, out snapshot) with a local snapshot (replaces an interpreted Expression.Compile).</summary>
        private static Func<VehicleController, bool> CaptureCall(MethodInfo capture, Type snapshot)
        {
            using (var dm = new DynamicMethodDefinition("WW_ChaseCameraCapture", typeof(bool), new[] { typeof(VehicleController) }))
            {
                var il = dm.GetILGenerator(); var local = il.DeclareLocal(snapshot);
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldloca, local); il.Emit(OpCodes.Call, capture); il.Emit(OpCodes.Ret);
                return (Func<VehicleController, bool>)Delegate.CreateDelegate(typeof(Func<VehicleController, bool>), dm.Generate());
            }
        }

        /// <summary>Transpiler of DrivingReadings.Capture: before each return of a successful capture, records the snapshot's four fields
        /// (no boxing of the private struct, the source telemetry is unchanged). The WW HUD also captures in first person.</summary>
        private static IEnumerable<CodeInstruction> CaptureReadings(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var record = AccessTools.Method(typeof(ChaseCameraHud), "Record");
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ret)
                {
                    var skip = generator.DefineLabel();
                    var first = new CodeInstruction(OpCodes.Dup);
                    first.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    yield return first;
                    yield return new CodeInstruction(OpCodes.Brfalse, skip);
                    for (int i = 0; i < _fields.Length; i++)
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Ldfld, _fields[i]);
                    }
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, record);
                    var end = new CodeInstruction(OpCodes.Nop); end.labels.Add(skip); yield return end;
                }
                yield return instruction;
            }
        }

        /// <summary>Called by the transpiled Capture (per successful capture, every frame while driving): stores the snapshot.</summary>
        private static void Record(float speed, float rpm, float fraction, int selected, VehicleController vehicle)
        {
            if (!_ready || !CompatibilityHost.CleanHud.Running) return;
            Speed = speed; Rpm = rpm; Fraction = fraction; _selected = selected; _vehicle = vehicle; _frame = Time.frameCount;
        }
        /// <summary>HUD driving-panel update (every frame while the HUD runs): applies the visibility gates and captures this frame's snapshot.</summary>
        internal static bool Refresh(bool thirdPersonOnly = false)
        {
            if (!_ready) return Unavailable("bridge unavailable");
            if (!((ConfigEntry<bool>)_enabled).Value || !((ConfigEntry<bool>)_hudEnabled).Value) return Unavailable("disabled");
            if (!Core.GameRefs.InGame) return Unavailable("game gate");
            if (Core.ApocasetterBridge.InputBlocked || !Application.isFocused) return Unavailable("input/focus gate");
            if (!_resolve()) { _owner = null; _drivingVehicle = null; return Unavailable("driving binding"); }
            if (thirdPersonOnly && !_thirdPerson()) return Unavailable("first person view"); // refs-ok: REFACTOR-REQUEST B17-7
            // Camera scope/binocular state can remain latched until ESC; it does not own vehicle instrument visibility.
            var car = _car();
            if (_owner != car) { _owner = car; _drivingVehicle = null; _nextVehicleSearch = 0; }
            if (car == null) return Unavailable("no car");
            if (_drivingVehicle == null && Time.unscaledTime >= _nextVehicleSearch)
            {
                _nextVehicleSearch = Time.unscaledTime + .5f;
                _drivingVehicle = car.GetComponent<VehicleController>();
                if (_drivingVehicle == null) _drivingVehicle = car.GetComponentInChildren<VehicleController>(true);
            }
            if (_drivingVehicle == null) return Unavailable("no vehicle controller");
            if (!_capture(_drivingVehicle)) return Unavailable("capture failed");
            if (!Live) return Unavailable("snapshot missing");
            _state = "live"; return true;
        }
        /// <summary>Records why the readout is hidden (diagnostics) and returns false.</summary>
        private static bool Unavailable(string state) { _state = state; return false; }
        /// <summary>Level-2 trace of the readout state (throttled to state changes or 5 s).</summary>
        internal static void TraceState(UnityEngine.UI.Text rpm, bool hudVisible)
        {
            if (!Log.L2 || !_ready) return;
            if (_lastTraceState == _state && _lastHudVisible == hudVisible && Time.unscaledTime < _nextStateLog) return;
            _lastTraceState = _state; _lastHudVisible = hudVisible; _nextStateLog = Time.unscaledTime + 5f;
            var menu = Core.GameRefs.Menu;
            Log.Deep(_owner_, "ChaseCamera readout state: " + _state + ", HUD visible=" + hudVisible
                + ", menu=" + (menu == null ? "missing" : menu.ActiveStateName) + ", save=" + Core.GameRefs.SaveLoadState + ", time scale=" + Time.timeScale
                + ", snapshot age=" + (Time.frameCount - _frame) + ", car=" + (_owner == null ? "none" : _owner.name)
                + ", controller=" + (_drivingVehicle == null ? "none" : _drivingVehicle.name)
                + ", special view=" + _specialView()
                + ", RPM active=" + rpm.gameObject.activeInHierarchy + ", enabled=" + rpm.enabled + ", alpha=" + rpm.canvasRenderer.GetAlpha()
                + ", culled=" + rpm.canvasRenderer.cull + ", text=" + rpm.text + ", position=" + rpm.rectTransform.position);
        }
        /// <summary>Hides the mod's own driving HUD while the WW readout shows this frame's snapshot.</summary>
        internal static void HideNative() { if (Live) _hide(); }
        /// <summary>Level-2 trace of the hidden readout (throttled to 1 s).</summary>
        internal static void TraceHidden()
        {
            if (!Log.L2 || !_ready || Time.unscaledTime < _nextHiddenLog) return;
            _nextHiddenLog = Time.unscaledTime + 1f;
            Log.Deep(_owner_, "ChaseCamera readout hidden: snapshot age=" + (Time.frameCount - _frame)
                + ", camera=" + ((ConfigEntry<bool>)_enabled).Value + ", HUD=" + ((ConfigEntry<bool>)_hudEnabled).Value
                + ", third person=" + _thirdPerson() + ", special view=" + _specialView()
                + ", in game=" + Core.GameRefs.InGame + ", input blocked=" + Core.ApocasetterBridge.InputBlocked + ", focused=" + Application.isFocused);
        }
        /// <summary>The compass heading rendered by the mod's chase view (or its body bridge) while its camera is enabled.</summary>
        internal static bool TryHeading(out float yaw)
        {
            yaw = 0;
            if (!_ready || !((ConfigEntry<bool>)_enabled).Value || !Core.GameRefs.InGame || Core.ApocasetterBridge.InputBlocked || !Application.isFocused) return false;
            return _chaseHeading(out yaw) || _bodyHeading(out yaw);
        }
        /// <summary>Forgets every delegate and setting (the scope removes the transpiler); the next Install resolves again.</summary>
        internal static void Clear()
        {
            _ready = _attempted = false; _owner_ = null; _frame = -1; _vehicle = null;
            _gear = null; _hide = null; _chaseHeading = _bodyHeading = null;
            _thirdPerson = _specialView = null; _nextHiddenLog = 0;
            _state = "not sampled"; _lastTraceState = null; _lastHudVisible = false; _nextStateLog = 0;
            _resolve = null; _car = null; _capture = null; _owner = null; _drivingVehicle = null; _nextVehicleSearch = 0;
            _enabled = _hudEnabled = _size = _gap = null; _fields = null;
        }
    }
}
