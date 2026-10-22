using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using NWH.VehiclePhysics2;
using UnityEngine;

namespace WildWasteland.Modules.CleanHUD
{
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
        private static FieldInfo[] _fields;
        private static bool _attempted, _ready;
        private static int _frame = -1, _selected;
        private static VehicleController _vehicle;
        internal static float Speed, Rpm, Fraction;
        internal static bool Live { get { return _ready && _frame == Time.frameCount; } }
        internal static ConfigEntry<bool> HudEnabled { get { return _ready ? (ConfigEntry<bool>)_hudEnabled : null; } }
        internal static float Scale { get { return Mathf.Clamp(((ConfigEntry<float>)_size).Value, .65f, 1.75f); } }
        internal static float Gap { get { return Mathf.Clamp(((ConfigEntry<float>)_gap).Value, 0, 160); } }
        internal static string Gear { get { return _gear(_vehicle, _selected); } }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(CleanHudModule owner)
        {
            if (_attempted) return;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("local.apocalypter.chasecamera", out info) || ReferenceEquals(info.Instance, null)) return;
            _attempted = true;
            try
            {
                if (info.Metadata.Version != new Version(0, 2, 5)) CleanHudModule.Warn("ChaseCamera " + info.Metadata.Version + " differs from the tested 0.2.5; attempting HUD compatibility with this version");
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
                _car = Expression.Lambda<Func<Transform>>(Expression.Field(null, AccessTools.Field(binding, "Car"))).Compile();
                _thirdPerson = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), AccessTools.Method(binding, "ThirdPersonVisible"));
                _specialView = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), AccessTools.Method(binding, "SpecialView"));
                _enabled = AccessTools.Field(info.Instance.GetType(), "Enabled").GetValue(null);
                _hudEnabled = AccessTools.Field(info.Instance.GetType(), "HudEnabled").GetValue(null);
                _size = AccessTools.Field(info.Instance.GetType(), "HudScale").GetValue(null);
                _gap = AccessTools.Field(info.Instance.GetType(), "HudGap").GetValue(null);
                if (!(_enabled is ConfigEntry<bool>) || !(_hudEnabled is ConfigEntry<bool>) || !(_size is ConfigEntry<float>) || !(_gap is ConfigEntry<float>)) throw new NotSupportedException("Unknown HUD settings");
                if (!Core.Patches.Patch(owner, capture, transpiler: AccessTools.Method(typeof(ChaseCameraHud), "CaptureReadings"))) throw new InvalidOperationException("Readings hook unavailable");
                var vehicle = Expression.Parameter(typeof(VehicleController), "vehicle");
                var reading = Expression.Variable(snapshot, "reading");
                _capture = Expression.Lambda<Func<VehicleController, bool>>(Expression.Block(new[] { reading }, Expression.Call(capture, vehicle, reading)), vehicle).Compile();
                _ready = true;
                CleanHudModule.Log("ChaseCamera " + info.Metadata.Version + " HUD bridge ready: first/third-person source speed/RPM/gear, driving-only visibility, text above weapon UI and rendered compass heading.");
            }
            catch (Exception e) { _ready = false; CleanHudModule.Warn("ChaseCamera HUD bridge unavailable; retaining its original HUD: " + e.Message); }
        }

        // Observe the successful source snapshot without boxing its private struct
        // or replacing its telemetry. CleanHUD also captures in first person.
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

        private static void Record(float speed, float rpm, float fraction, int selected, VehicleController vehicle)
        {
            if (!_ready || !CleanHudModule.Running) return;
            Speed = speed; Rpm = rpm; Fraction = fraction; _selected = selected; _vehicle = vehicle; _frame = Time.frameCount;
        }
        internal static bool Refresh()
        {
            if (!_ready) return Unavailable("bridge unavailable");
            if (!((ConfigEntry<bool>)_enabled).Value || !((ConfigEntry<bool>)_hudEnabled).Value) return Unavailable("disabled");
            if (!Core.GameRefs.InGame) return Unavailable("game gate");
            if (Core.ApocasetterBridge.InputBlocked || !Application.isFocused) return Unavailable("input/focus gate");
            if (!_resolve()) { _owner = null; _drivingVehicle = null; return Unavailable("driving binding"); }
            if (CleanHudModule.DrivingHudOnlyThirdPerson.Value && !_thirdPerson()) return Unavailable("first person view");
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
        private static bool Unavailable(string state) { _state = state; return false; }
        internal static void TraceState(UnityEngine.UI.Text rpm, bool hudVisible)
        {
            if (!Core.Log.Verbose || !_ready) return;
            if (_lastTraceState == _state && _lastHudVisible == hudVisible && Time.unscaledTime < _nextStateLog) return;
            _lastTraceState = _state; _lastHudVisible = hudVisible; _nextStateLog = Time.unscaledTime + 5f;
            var menu = Core.GameRefs.Menu;
            Core.Log.Info("[CleanHUD] ChaseCamera readout state: " + _state + ", HUD visible=" + hudVisible
                + ", menu=" + (menu == null ? "missing" : menu.ActiveStateName) + ", save=" + Core.GameRefs.SaveLoadState + ", time scale=" + Time.timeScale
                + ", snapshot age=" + (Time.frameCount - _frame) + ", car=" + (_owner == null ? "none" : _owner.name)
                + ", controller=" + (_drivingVehicle == null ? "none" : _drivingVehicle.name)
                + ", special view=" + _specialView()
                + ", RPM active=" + rpm.gameObject.activeInHierarchy + ", enabled=" + rpm.enabled + ", alpha=" + rpm.canvasRenderer.GetAlpha()
                + ", culled=" + rpm.canvasRenderer.cull + ", text=" + rpm.text + ", position=" + rpm.rectTransform.position);
        }
        internal static void HideNative() { if (Live) _hide(); }
        internal static void TraceHidden()
        {
            if (!Core.Log.Verbose || !_ready || Time.unscaledTime < _nextHiddenLog) return;
            _nextHiddenLog = Time.unscaledTime + 1f;
            Core.Log.Info("[CleanHUD] ChaseCamera readout hidden: snapshot age=" + (Time.frameCount - _frame)
                + ", camera=" + ((ConfigEntry<bool>)_enabled).Value + ", HUD=" + ((ConfigEntry<bool>)_hudEnabled).Value
                + ", third person=" + _thirdPerson() + ", special view=" + _specialView()
                + ", in game=" + Core.GameRefs.InGame + ", input blocked=" + Core.ApocasetterBridge.InputBlocked + ", focused=" + Application.isFocused);
        }
        internal static bool TryHeading(out float yaw)
        {
            yaw = 0;
            if (!_ready || !((ConfigEntry<bool>)_enabled).Value || !Core.GameRefs.InGame || Core.ApocasetterBridge.InputBlocked || !Application.isFocused) return false;
            return _chaseHeading(out yaw) || _bodyHeading(out yaw);
        }
        internal static void Clear()
        {
            _ready = _attempted = false; _frame = -1; _vehicle = null;
            _gear = null; _hide = null; _chaseHeading = _bodyHeading = null;
            _thirdPerson = _specialView = null; _nextHiddenLog = 0;
            _state = "not sampled"; _lastTraceState = null; _lastHudVisible = false; _nextStateLog = 0;
            _resolve = null; _car = null; _capture = null; _owner = null; _drivingVehicle = null; _nextVehicleSearch = 0;
            _enabled = _hudEnabled = _size = _gap = null; _fields = null;
        }
    }
}
