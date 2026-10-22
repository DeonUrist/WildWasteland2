using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;
using NWH.VehiclePhysics2;
using NWH.VehiclePhysics2.Powertrain;
using SkrilStudio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Engine Sim bridge owned by Compatibility: runs the
    /// Engine Sim manager from WW jobs, caches its scans/listener/camera, bridges keyboard ignition and AI start requests and
    /// provides <see cref="IVehicleIgnition"/>. Delegates come from Core <see cref="Access"/> (no interpreted Expression.Compile).
    /// </summary>
    internal sealed class EngineSimBridge : ModuleBase, IVehicleIgnition
    {
        /// <summary>Distinct child owner keeps bridge cleanup independent of other Compatibility integrations.</summary>
        public override string Id { get { return "Compatibility.EngineSim"; } }
        /// <summary>Bridge display name used in diagnostics.</summary>
        public override string DisplayName { get { return "Engine Sim"; } }
        /// <summary>The containing Compatibility module owns the player setting.</summary>
        public override void BindConfig(ModuleConfig cfg) { }
        private static EngineSimBridge _owner;
        private static object _manager, _playerSwitch;
        private static IDictionary _active;
        private static Action<object> _update, _fixed;
        private static Func<object, object> _switch;
        private static Func<object, bool> _partGone, _keyOn, _on, _cranking;
        private static Func<object, Component> _itemText;
        private static Action<object, bool> _turn, _setCranking, _setPressed;
        private static Action<object, string> _shown;
        private static bool _ready, _dispatch, _attempted, _down, _held;
        private static int _playerFrame = -1;
        private static readonly Dictionary<object, StartRequest> Starts = new Dictionary<object, StartRequest>();
        /// <summary>Retains the native engine and bounded crank deadline for one requested switch.</summary>
        private sealed class StartRequest { internal EngineComponent Engine; internal float Until; }

        /// <summary>Init part: resets the bridge state, tries to install now and keeps trying once a second until Engine Sim is found.</summary>
        public override void Init()
        {
            _owner = this; _ready = _dispatch = _attempted = false; _playerFrame = -1;
            TryInstall();
            Scheduler.Interval(this, "Engine Sim discovery", 1f, TryInstall);
            if (!_attempted) Log.Always("[EngineSimCompat] Engine Sim not loaded; compatibility idle");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        /// <summary>Detects Engine Sim and resolves the reflective native surface before installing owned callbacks.</summary>
        private static void TryInstall()
        {
            if (_attempted) return;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.burner.apocalypter.enginesim", out info) || ReferenceEquals(info.Instance, null)) return;
            _attempted = true;
            try
            {
                if (info.Metadata.Version != new Version(1, 7, 0)) Log.Warn("[EngineSimCompat] Engine Sim " + info.Metadata.Version + " differs from the tested 1.7.0; attempting compatibility with this version");
                var plugin = info.Instance.GetType(); var asm = plugin.Assembly;
                var manager = asm.GetType("EngineSim.EngineManager", true);
                var runner = AccessTools.Field(plugin, "_runner").GetValue(null) as GameObject;
                if (runner == null) { Log.Warn("[EngineSimCompat] Engine Sim has no runner; check its native-library loading error. Standalone behavior retained."); return; }
                _manager = runner.GetComponent(manager);
                if (_manager == null) throw new InvalidOperationException("Engine Sim manager missing");
                var coupling = asm.GetType("EngineSim.DriveCoupling", true);
                var ignition = asm.GetType("EngineSim.IgnitionSwitch", true);
                _active = (IDictionary)AccessTools.Field(coupling, "Active").GetValue(null);
                _update = Call(manager, "Update"); _fixed = Call(manager, "FixedUpdate");
                _switch = Get<object>(coupling, "_switch"); _partGone = Get<bool>(coupling, "PartGone"); _keyOn = Get<bool>(coupling, "KeyOn");
                _on = Get<bool>(ignition, "On"); _cranking = Get<bool>(ignition, "Cranking");
                _turn = Call<bool>(ignition, "Turn"); _setCranking = Set<bool>(ignition, "Cranking"); _setPressed = Set<bool>(ignition, "_pressed");
                _itemText = Get<Component>(ignition, "_itemText"); _shown = Set<string>(ignition, "_shown");
                EngineDiscovery.Init(_owner);
                Patch(manager, "Update", "ScheduledOnly"); Patch(manager, "FixedUpdate", "ScheduledOnly");
                Patch(manager, "Scan", transpiler: "CachedScan"); Patch(manager, "FindListener", "FindListener");
                Patch(asm.GetType("EngineSim.Listener", true), "get_Current", "FindListener");
                Patch(ignition, "Update", "BeforeSwitch", "AfterSwitch", "SwitchInput");
                Patch(ignition, "Pointed", transpiler: "CachedCamera"); Patch(ignition, "Pointed", postfix: "KeyboardPointed");
                Patch(ignition, "SetText", "SetText"); Patch(coupling, "Release", postfix: "Released");
                Patch(typeof(VehicleController), "OnEnable", postfix: "VehicleEnabled");
                Patch(typeof(RealisticEngineSound), "OnEnable", postfix: "SoundEnabled");
                Scheduler.Interval(_owner, "Engine Sim prune", .5f, EngineDiscovery.Prune);
                Scheduler.EveryFrame(_owner, "Engine Sim frame", Frame);
                Scheduler.EveryFrame(_owner, "Engine Sim physics", PhysicsFrame, Phase.FixedUpdate);
                ModApi.Provide<IVehicleIgnition>(_owner, _owner); _ready = true;
                Log.Always("[EngineSimCompat] Engine Sim " + info.Metadata.Version + " scheduled; shared Core world discovery, player/crew ignition bridge ready; native simulation and audio threads retained");
            }
            catch
            {
                _ready = false;
                ClearStarts();
                Core.Patches.Unpatch(_owner);
                Scheduler.RemoveAll(_owner); WorldIndex.RemoveAll(_owner);
                EngineDiscovery.Clear();
                throw;
            }
        }
        /// <summary>Installs one required bridge hook and fails the bridge if its native target changed.</summary>
        private static void Patch(Type type, string name, string prefix = null, string postfix = null, string transpiler = null)
        {
            var own = typeof(EngineSimBridge);
            if (!Core.Patches.Patch(_owner, AccessTools.Method(type, name),
                prefix: prefix == null ? null : AccessTools.Method(own, prefix),
                postfix: postfix == null ? null : AccessTools.Method(own, postfix),
                transpiler: transpiler == null ? null : AccessTools.Method(own, transpiler)))
                throw new InvalidOperationException("Engine Sim hook failed: " + type.Name + "." + name);
        }
        /// <summary>Suppresses the native manager callback only while this bridge is dispatching it elsewhere.</summary>
        private static bool ScheduledOnly(object __instance) { return !_ready || !ReferenceEquals(__instance, _manager) || _dispatch; }
        /// <summary>Invokes the captured native callback and releases suppression if it fails.</summary>
        private static void Run(Action<object> tick)
        {
            if (!_ready || (_manager as Component) == null) return;
            _dispatch = true;
            try { tick(_manager); }
            catch
            {
                _ready = false;
                ClearStarts();
                ModApi.Withdraw<IVehicleIgnition>(_owner);
                Log.Warn("[EngineSimCompat] Manager callback failed; original Engine Sim callbacks restored");
                throw;
            }
            finally { _dispatch = false; }
        }
        /// <summary>Invokes the captured native callback and releases suppression if it fails.</summary>
        private static void Frame() { Run(_update); }
        /// <summary>Invokes the captured native callback and releases suppression if it fails.</summary>
        private static void PhysicsFrame() { Run(_fixed); }
        /// <summary>Returns cached sound components while healthy, otherwise restores native discovery.</summary>
        private static RealisticEngineSound[] Sounds() { return _ready ? EngineDiscovery.Snapshot : Resources.FindObjectsOfTypeAll<RealisticEngineSound>(); }
        /// <summary>Uses the cached active listener only while the bridge remains healthy.</summary>
        private static bool FindListener(ref AudioListener __result) { if (!_ready) return true; __result = EngineDiscovery.Listener(); return false; }
        /// <summary>Returns Core’s current camera reference for the native pointed query.</summary>
        private static Camera CachedMainCamera() { return GameRefs.MainCamera; }
        /// <summary>Replaces exactly the known native engine sound enumeration with the retained snapshot.</summary>
        private static IEnumerable<CodeInstruction> CachedScan(IEnumerable<CodeInstruction> instructions)
        {
            int count = 0;
            foreach (var i in instructions)
            {
                var m = i.operand as MethodInfo;
                if (m != null && m.DeclaringType == typeof(Resources) && m.Name == "FindObjectsOfTypeAll" && m.ReturnType == typeof(RealisticEngineSound[]))
                { i.opcode = OpCodes.Call; i.operand = AccessTools.Method(typeof(EngineSimBridge), "Sounds"); count++; }
                yield return i;
            }
            if (count != 1) throw new NotSupportedException("Engine Sim scan interface changed");
        }
        /// <summary>Replaces exactly the known native main-camera lookup with the shared camera reference.</summary>
        private static IEnumerable<CodeInstruction> CachedCamera(IEnumerable<CodeInstruction> instructions)
        {
            int count = 0;
            foreach (var i in instructions)
            {
                var m = i.operand as MethodInfo;
                if (m != null && m.DeclaringType == typeof(UnityEngine.Camera) && m.Name == "get_main")
                { i.opcode = OpCodes.Call; i.operand = AccessTools.Method(typeof(EngineSimBridge), "CachedMainCamera"); count++; }
                yield return i;
            }
            if (count != 1) throw new NotSupportedException("Engine Sim camera interface changed");
        }
        /// <summary>Allows ignition input only during focused unblocked gameplay.</summary>
        private static bool InputAllowed { get { return GameRefs.InGame && !ApocasetterBridge.InputBlocked && Application.isFocused; } }
        /// <summary>Matches the current frame’s player switch claim without retaining stale input.</summary>
        private static bool PlayerKey(object sw) { return _ready && _playerFrame == Time.frameCount && ReferenceEquals(sw, _playerSwitch); }
        /// <summary>Matches the current frame’s player switch claim without retaining stale input.</summary>
        private static bool MouseDown(int button, object sw) { return !_ready ? Input.GetMouseButtonDown(button) : InputAllowed && (Input.GetMouseButtonDown(button) || (PlayerKey(sw) && _down)); }
        /// <summary>Matches the current frame’s player switch claim without retaining stale input.</summary>
        private static bool MouseHeld(int button, object sw) { return !_ready ? Input.GetMouseButton(button) : InputAllowed && (Input.GetMouseButton(button) || (PlayerKey(sw) && _held)); }
        /// <summary>Rewrites the two known native mouse calls, retaining labels and exception boundaries.</summary>
        private static IEnumerable<CodeInstruction> SwitchInput(IEnumerable<CodeInstruction> instructions)
        {
            int count = 0;
            foreach (var i in instructions)
            {
                var m = i.operand as MethodInfo;
                if (m != null && m.DeclaringType == typeof(Input) && (m.Name == "GetMouseButtonDown" || m.Name == "GetMouseButton"))
                {
                    var load = new CodeInstruction(OpCodes.Ldarg_0); load.labels.AddRange(i.labels); i.labels.Clear();
                    load.blocks.AddRange(i.blocks); i.blocks.Clear(); yield return load;
                    i.opcode = OpCodes.Call; i.operand = AccessTools.Method(typeof(EngineSimBridge), m.Name == "GetMouseButtonDown" ? "MouseDown" : "MouseHeld"); count++;
                }
                yield return i;
            }
            if (count != 2) throw new NotSupportedException("Engine Sim switch input interface changed");
        }
        /// <summary>Lets the current keyboard-operated switch pass the native pointing guard.</summary>
        private static void KeyboardPointed(object __instance, ref bool __result)
        { if (PlayerKey(__instance) && InputAllowed && (_down || _held)) __result = true; }
        /// <summary>Updates the captured native UI text directly while the bridge owns it.</summary>
        private static bool SetText(object __instance, string __0)
        {
            if (!_ready) return true;
            _shown(__instance, __0);
            var text = _itemText(__instance) as Text;
            if (text != null) text.text = __0;
            return false;
        }
        /// <summary>Lets explicit player ignition control supersede an AI crank request.</summary>
        private static bool BeforeSwitch(object __instance)
        {
            if (!_ready) return true;
            if (PlayerKey(__instance)) Starts.Remove(__instance);
            return true;
        }
        /// <summary>Maintains an AI crank request until the engine turns or its deadline expires.</summary>
        private static void AfterSwitch(object __instance)
        {
            if (!_ready) return;
            StartRequest request;
            if (!Starts.TryGetValue(__instance, out request)) return;
            bool crank = request.Engine != null && _on(__instance) && !Turning(request.Engine) && Time.time < request.Until;
            _setCranking(__instance, crank);
            if (!crank) Starts.Remove(__instance);
        }
        /// <summary>Forgets requests and player claims when a native coupling is released.</summary>
        private static void Released(object __instance)
        {
            if (!_ready) return;
            var sw = _switch(__instance); if (sw == null) return;
            Starts.Remove(sw);
            if (ReferenceEquals(sw, _playerSwitch)) { _playerSwitch = null; _playerFrame = -1; }
        }
        /// <summary>Queues a newly enabled vehicle into shared component discovery.</summary>
        private static void VehicleEnabled(VehicleController __instance) { if (_ready) EngineDiscovery.Queue(__instance.transform); }
        /// <summary>Adds newly enabled native sounds to the retained snapshot.</summary>
        private static void SoundEnabled(RealisticEngineSound __instance) { if (_ready) EngineDiscovery.Observe(__instance); }
        /// <summary>Uses the native idle-RPM threshold to distinguish running from cranking.</summary>
        private static bool Turning(EngineComponent engine) { return engine.OutputRPM > .5f * engine.idleRPM; }
        /// <summary>Resolves the native coupling of a valid vehicle using Engine Sim’s active registry.</summary>
        private static object Coupling(GameObject car, out EngineComponent engine)
        {
            engine = null; if (!_ready || car == null) return null;
            var vc = car.GetComponent<VehicleController>();
            if (vc == null || vc.powertrain == null || vc.powertrain.engine == null) return null;
            engine = vc.powertrain.engine;
            var result = _active[engine];
            return result != null && !_partGone(result) ? result : null;
        }
        /// <summary>Claims keyboard input for this frame and reports native ignition and crank state.</summary>
        public bool TryPlayer(GameObject car, bool down, bool held, out bool keyOn, out bool running, out bool cranking)
        {
            keyOn = running = cranking = false;
            EngineComponent engine; var coupling = Coupling(car, out engine); if (coupling == null) return false;
            var sw = _switch(coupling); if (sw == null) return false;
            _playerSwitch = sw; _playerFrame = Time.frameCount; _down = down; _held = held;
            keyOn = _keyOn(coupling); running = keyOn && Turning(engine); cranking = _cranking(sw); return true;
        }
        /// <summary>Reports native key and engine rotation state when this vehicle has a supported coupling.</summary>
        public bool TryRunning(GameObject car, out bool running)
        {
            running = false; EngineComponent engine;
            var coupling = Coupling(car, out engine); if (coupling == null) return false;
            running = _keyOn(coupling) && Turning(engine); return true;
        }
        /// <summary>Turns the native key on and admits a bounded crank request for crew ignition.</summary>
        public bool TryStart(GameObject car)
        {
            EngineComponent engine; var coupling = Coupling(car, out engine); if (coupling == null) return false;
            var sw = _switch(coupling); if (sw == null) return false;
            if (!_on(sw)) _turn(sw, true);
            if (!Turning(engine) && !Starts.ContainsKey(sw)) Starts.Add(sw, new StartRequest { Engine = engine, Until = Time.time + 8f });
            return true;
        }
        /// <summary>Clears crank state and turns the native key off for a supported vehicle.</summary>
        public bool TryStop(GameObject car)
        {
            EngineComponent engine; var coupling = Coupling(car, out engine); if (coupling == null) return false;
            var sw = _switch(coupling); if (sw == null) return false;
            Starts.Remove(sw); _setPressed(sw, false); _setCranking(sw, false);
            if (_on(sw)) _turn(sw, false);
            return true;
        }
        /// <summary>Releases every pending native crank and pressed flag before forgetting requests.</summary>
        private static void ClearStarts()
        {
            foreach (var request in Starts) { _setCranking(request.Key, false); _setPressed(request.Key, false); }
            Starts.Clear();
        }
        /// <summary>Scene part: reseeds the discovery cache and forgets start requests and the player's switch.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (_ready) EngineDiscovery.Init(_owner); ClearStarts(); _playerSwitch = null; _playerFrame = -1; }
        /// <summary>Shutdown part: withdraws the ignition provider and clears every cached Engine Sim reference.</summary>
        public override void Shutdown()
        {
            _ready = false;
            Core.Patches.Unpatch(this); Scheduler.RemoveAll(this); WorldIndex.RemoveAll(this);
            ModApi.Withdraw<IVehicleIgnition>(this);
            ClearStarts(); EngineDiscovery.Clear(); _manager = _playerSwitch = null; _active = null; _owner = null; _dispatch = false; _playerFrame = -1;
        }

        /// <summary>Compiled getter of an Engine Sim field or property (Core Access; throws when missing, like the old Expression helper).</summary>
        private static Func<object, T> Get<T>(Type type, string name) { return Access.Get<T>(type, name); }
        /// <summary>Compiled setter of an Engine Sim field or (private) property setter of exactly type T; throws when it cannot be built.</summary>
        private static Action<object, T> Set<T>(Type type, string name)
        {
            MemberInfo member = AccessTools.Field(type, name);
            if (member == null) member = AccessTools.Property(type, name);
            var setter = member != null ? Access.TypedSetter<T>(member) : null;
            if (setter == null) throw new MissingMemberException(type.FullName, name);
            return setter;
        }
        /// <summary>Compiled call of a parameterless instance method (any overload lookup as AccessTools.Method(type, name)).</summary>
        private static Action<object> Call(Type type, string name) { return Access.Call<Action<object>>(type, name, (Type[])null); }
        /// <summary>Compiled call of a one-argument instance method.</summary>
        private static Action<object, T> Call<T>(Type type, string name) { return Access.Call<Action<object, T>>(type, name, (Type[])null); }
    }
}
