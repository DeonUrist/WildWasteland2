using System;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    internal static class ChaseCameraCompat
    {
        // The source bridge expects the former Plugin's static config fields.
        internal static ConfigEntry<bool> Enabled, OcclusionPrototype, OcclusionInVehicle;
        internal static ConfigEntry<float> ThirdCarHeight;
        private static Action _refresh, _shutdown;
        private static bool _attempted;

        internal static void Init(PlayerBodyModule owner)
        {
            _attempted = false;
            Enabled = owner.Config.Enabled; ThirdCarHeight = PlayerBodyModule.ThirdCarHeight;
            OcclusionPrototype = PlayerBodyModule.OcclusionPrototype; OcclusionInVehicle = PlayerBodyModule.OcclusionInVehicle;
            TryInstall();
            Scheduler.Interval(owner, "ChaseCamera discovery", 1f, TryInstall);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TryInstall()
        {
            if (_attempted) return;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("local.apocalypter.chasecamera", out info) || ReferenceEquals(info.Instance, null)) return;
            _attempted = true;
            try
            {
                if (info.Metadata.Version != new Version(0, 2, 5)) Core.Log.Warn("[PlayerBody] ChaseCamera " + info.Metadata.Version + " differs from the tested 0.2.5; attempting compatibility with this version");
                var bridge = info.Instance.GetType().Assembly.GetType("ApocaChaseCamera.ApocaplayerBridge", true);
                var refresh = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(bridge, "Refresh"));
                var shutdown = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(bridge, "Shutdown"));
                var discover = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(bridge, "Discover"));
                if (!Core.Patches.Patch(PlayerBodyModule.Instance, AccessTools.Method(bridge, "FindType"), prefix: AccessTools.Method(typeof(ChaseCameraCompat), "BeforeFindType")))
                    throw new InvalidOperationException("Type adapter hook unavailable");
                _refresh = refresh; _shutdown = shutdown;
                refresh(); shutdown(); discover();
                if (!(bool)AccessTools.Field(bridge, "compatible").GetValue(null)) throw new NotSupportedException("Source bridge rejected the PlayerBody interface; see its preceding warning");
                Core.Log.Always("[PlayerBody] ChaseCamera " + info.Metadata.Version + " type adapter ready; source bridge retains driving camera, aim, cutaway and origin handling.");
            }
            catch (Exception e) { PlayerBodyModule.Warn("ChaseCamera compatibility unavailable: " + e.Message); }
        }

        private static bool BeforeFindType(string __0, ref Type __result)
        {
            if (!PlayerBodyModule.Running) return true;
            switch (__0)
            {
                case "Apocaplayer.ThirdPerson": __result = typeof(ThirdPerson); break;
                case "Apocaplayer.Game": __result = typeof(Game); break;
                case "Apocaplayer.Plugin": __result = typeof(ChaseCameraCompat); break;
                case "Apocaplayer.OcclusionCutaway": __result = typeof(OcclusionCutaway); break;
                case "Apocaplayer.Cave": __result = typeof(Cave); break;
                default: return true;
            }
            return false;
        }

        internal static void Clear()
        {
            try { if (_refresh != null) _refresh(); if (_shutdown != null) _shutdown(); }
            catch (Exception e) { PlayerBodyModule.Warn("ChaseCamera shutdown: " + e.Message); }
            _refresh = _shutdown = null; _attempted = false;
            Enabled = OcclusionPrototype = OcclusionInVehicle = null; ThirdCarHeight = null;
        }
    }
}
