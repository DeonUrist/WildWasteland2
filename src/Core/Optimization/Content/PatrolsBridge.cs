using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// The only place the Performance module touches the Patrols module (modules must not reference each other's types, AGENTS.md
    /// 5.1; there is no Core.Services hook for this yet - see the report): Patrols' War Rig POI protects its trucks from the vanilla
    /// 5 km DeleteFarAwayItems cleanup by prefixing GetDistance.OnEnter of that FSM (src/Modules/Patrols/WarRig/PoiEncounter.cs
    /// BeforeCleanupDistance): protected targets are skipped, and tankers are detached before a truck beyond 5 km is deleted.
    /// The CT1 culler replaces that FSM, so it asks the same two functions through reflection (delegates compiled once).
    /// Requested framework change: Core.Services.Culling { bool Protected(GameObject); void BeforeFarDelete(GameObject) }.
    /// </summary>
    internal static class PatrolsBridge
    {
        static bool _resolved;
        static Func<GameObject, bool> _protected;
        static Action<GameObject> _detach;
        static FieldInfo _running, _checks; static PropertyInfo _count;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Resolve()
        {
            _resolved = true;
            try
            {
                var poi = AccessTools.TypeByName("WildWasteland.Modules.Patrols.WarRig.PoiEncounter");
                var mod = AccessTools.TypeByName("WildWasteland.Modules.Patrols.PatrolsModule");
                if (poi == null || mod == null) return;
                var p = AccessTools.Method(poi, "Protected", new[] { typeof(GameObject) });
                var d = AccessTools.Method(poi, "DetachTankers", new[] { typeof(GameObject) });
                if (p != null) _protected = (Func<GameObject, bool>)Delegate.CreateDelegate(typeof(Func<GameObject, bool>), p);
                if (d != null) _detach = (Action<GameObject>)Delegate.CreateDelegate(typeof(Action<GameObject>), d);
                _running = AccessTools.Field(mod, "Running");
                _checks = AccessTools.Field(poi, "CleanupChecks");
                if (_checks != null) _count = _checks.FieldType.GetProperty("Count");
            }
            catch (Exception e) { Log.Warn("[Performance] CT1: Patrols War Rig cleanup protection not found (" + e.Message + ")"); }
        }

        /// <summary>True when Patrols would intercept the vanilla cleanup right now (module running and its GetDistance prefix
        /// armed for the DeleteFarAwayItems FSM). Read once per culling pass.</summary>
        internal static bool Active()
        {
            if (!_resolved) Resolve();
            if (_protected == null || _running == null) return false;
            try
            {
                if (!(bool)_running.GetValue(null)) return false;
                if (_checks == null || _count == null) return true;
                var set = _checks.GetValue(null);
                return set != null && (int)_count.GetValue(set, null) > 0;     // once per 10 s pass
            }
            catch { return false; }
        }

        internal static bool Protected(GameObject go) { try { return _protected != null && _protected(go); } catch { return false; } }
        internal static void BeforeFarDelete(GameObject go) { try { if (_detach != null) _detach(go); } catch (Exception e) { Log.Error("[Performance] CT1 Patrols DetachTankers", e); } }
    }
}
