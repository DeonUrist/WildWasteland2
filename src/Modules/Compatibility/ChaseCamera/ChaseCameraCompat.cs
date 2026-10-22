using System;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// ChaseCamera part serving the player body: the mod's ApocaplayerBridge looks up the old standalone "Apocaplayer.*" types by name;
    /// this type adapter (prefix of its FindType) answers with the WW player-body types, and exposes the static config fields the mod
    /// expects from "Apocaplayer.Plugin" (this class). Answers only while the player-body module runs. The mapped types are the mod's
    /// binding contract, so they are named here on purpose (REFACTOR-REQUEST B17-8).
    /// </summary>
    internal static class ChaseCameraCompat
    {
        /// <summary>The source bridge expects the former Plugin's static config fields (read by name).</summary>
        internal static ConfigEntry<bool> Enabled, OcclusionPrototype, OcclusionInVehicle;
        /// <summary>Former Plugin's third-person car camera height.</summary>
        internal static ConfigEntry<float> ThirdCarHeight;
        private static Action _refresh, _shutdown, _discover;
        private static bool _attempted, _hostWasRunning;
        private static IModule _owner;
        private static Job _discovery;

        /// <summary>Publishes the player-body settings the mod reads, tries the adapter now and once more on the first runner tick.</summary>
        internal static void Start(IModule owner)
        {
            _attempted = false; _owner = owner;
            var host = CompatibilityHost.PlayerBody.Config;
            Enabled = host != null ? host.Enabled : null;
            ThirdCarHeight = PlayerBodyModule.ThirdCarHeight; // refs-ok: REFACTOR-REQUEST B17-8
            OcclusionPrototype = PlayerBodyModule.OcclusionPrototype; OcclusionInVehicle = PlayerBodyModule.OcclusionInVehicle; // refs-ok: REFACTOR-REQUEST B17-8
            TryInstall();
            _discovery = Scheduler.Interval(owner, "Compatibility.ChaseCamera.Discovery", 1f, Discover);
        }

        /// <summary>Job "Compatibility.ChaseCamera.Discovery" (1 s): retries the install until the mod's plugin instance exists, and
        /// re-binds the mod when the player-body host starts or stops (the host used to install/clear the adapter in its Init/Shutdown).</summary>
        private static void Discover()
        {
            if (!_attempted) { TryInstall(); return; }
            bool running = CompatibilityHost.PlayerBody.Running;
            if (running == _hostWasRunning || _refresh == null) return;
            _hostWasRunning = running;
            try { _refresh(); _shutdown(); if (running) _discover(); }
            catch (Exception e) { Log.Warn(_owner, "ChaseCamera rebind: " + e.Message); }
        }

        /// <summary>Resolves the mod's bridge, installs the FindType prefix and lets the mod re-discover the WW types.</summary>
        private static void TryInstall()
        {
            if (_attempted) return;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("local.apocalypter.chasecamera", out info) || ReferenceEquals(info.Instance, null)) return;
            _attempted = true;
            try
            {
                if (info.Metadata.Version != new Version(0, 2, 5)) Log.Warn(_owner, "ChaseCamera " + info.Metadata.Version + " differs from the tested 0.2.5; attempting compatibility with this version");
                var bridge = info.Instance.GetType().Assembly.GetType("ApocaChaseCamera.ApocaplayerBridge", true);
                var refresh = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(bridge, "Refresh"));
                var shutdown = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(bridge, "Shutdown"));
                var discover = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(bridge, "Discover"));
                if (!Core.Patches.Patch(_owner, AccessTools.Method(bridge, "FindType"), prefix: AccessTools.Method(typeof(ChaseCameraCompat), "BeforeFindType")))
                    throw new InvalidOperationException("Type adapter hook unavailable");
                _refresh = refresh; _shutdown = shutdown; _discover = discover;
                _hostWasRunning = CompatibilityHost.PlayerBody.Running;
                refresh(); shutdown(); discover();
                if (_hostWasRunning && !(bool)AccessTools.Field(bridge, "compatible").GetValue(null)) throw new NotSupportedException("Source bridge rejected the PlayerBody interface; see its preceding warning");
                Log.Main(_owner, "ChaseCamera " + info.Metadata.Version + " type adapter ready; source bridge retains driving camera, aim, cutaway and origin handling.");
            }
            catch (Exception e) { Log.Warn(_owner, "ChaseCamera compatibility unavailable: " + e.Message); }
        }

        /// <summary>Prefix of ApocaplayerBridge.FindType(name) (only during the mod's discovery): maps the old Apocaplayer type names.</summary>
        private static bool BeforeFindType(string __0, ref Type __result)
        {
            if (!CompatibilityHost.PlayerBody.Running) return true;
            switch (__0)
            {
                case "Apocaplayer.ThirdPerson": __result = typeof(ThirdPerson); break; // refs-ok: REFACTOR-REQUEST B17-8
                case "Apocaplayer.Game": __result = typeof(Game); break; // refs-ok: REFACTOR-REQUEST B17-8
                case "Apocaplayer.Plugin": __result = typeof(ChaseCameraCompat); break;
                case "Apocaplayer.OcclusionCutaway": __result = typeof(OcclusionCutaway); break; // refs-ok: REFACTOR-REQUEST B17-8
                case "Apocaplayer.Cave": __result = typeof(Cave); break; // refs-ok: REFACTOR-REQUEST B17-8
                default: return true;
            }
            return false;
        }

        /// <summary>Lets the mod drop its WW bindings (Refresh + Shutdown), then forgets the adapter state.</summary>
        internal static void Clear()
        {
            try { if (_refresh != null) _refresh(); if (_shutdown != null) _shutdown(); }
            catch (Exception e) { Log.Warn(_owner, "ChaseCamera shutdown: " + e.Message); }
            _refresh = _shutdown = _discover = null; _attempted = _hostWasRunning = false; _discovery = null; _owner = null;
            Enabled = OcclusionPrototype = OcclusionInVehicle = null; ThirdCarHeight = null;
        }
    }
}
