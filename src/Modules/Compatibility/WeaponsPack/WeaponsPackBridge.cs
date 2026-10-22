using System;
using BepInEx.Bootstrap;
using WildWasteland.Core;
using WildWasteland.Core.Events;
using WildWasteland.Modules.CleanHUD;
using WildWasteland.Modules.Guns;
using WildWasteland.Modules.NPC;
using WildWasteland.Modules.PlayerBody;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// The single Weapons Pack (apocalypter.weaponspack) bridge (README 6.12): one detection on the first runner tick after chainloading,
    /// then four observation parts, each serving one consumer and acting only while that consumer's module runs:
    /// <see cref="WeaponsPackFear"/> (NPC burning fear, chainsaw kill witnesses, chainsaw noise), <see cref="WeaponsPackShadows"/>
    /// (first-person held-model shadows), <see cref="WeaponsPackIcons"/> (HUD weapon silhouettes) and <see cref="WeaponsPackSound"/>
    /// (machete slash sound on chainsaw contact). Every patch, job and hook belongs to the Compatibility.WeaponsPack scope.
    /// </summary>
    internal static class WeaponsPackBridge
    {
        /// <summary>BepInEx GUID of the pack.</summary>
        internal const string Guid = "apocalypter.weaponspack";
        private static IModule _scope;
        private static Job _discovery;
        private static bool _attempted;

        /// <summary>Registers discovery, the chainsaw-cut sweep, the corpse hook and the load reset under <paramref name="scope"/>.</summary>
        internal static void Start(IModule scope)
        {
            _scope = scope; _attempted = false;
            _discovery = Scheduler.Interval(scope, "Compatibility.WeaponsPack.Discovery", .5f, Discover);
            Scheduler.Interval(scope, "Compatibility.WeaponsPack.Cuts", 2f, WeaponsPackFear.Tick, Phase.Update, true);
            Hooks.CreateObjectEnter.Postfix(scope, WeaponsPackFear.AfterCorpse);
            ModEvents.Subscribe<LoadBegin>(scope, OnLoadBegin);
        }

        /// <summary>Resolves the pack once (first tick after every plugin's Awake) and installs the four parts independently.</summary>
        private static void Discover()
        {
            if (_attempted) return;
            _attempted = true;
            if (_discovery != null) { _discovery.Remove(); _discovery = null; }
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out info) || ReferenceEquals(info.Instance, null)) { if (Log.L2) Log.Deep(_scope, "Weapons Pack not installed"); return; }
            WeaponsPackFear.Install(_scope, info);
            WeaponsPackShadows.Install(_scope, info);
            WeaponsPackIcons.Install(_scope, info);
            WeaponsPackSound.Install(_scope, info);
        }

        /// <summary>A save starts loading: chainsaw cuts and burning fear of the old world are forgotten (was the NPC LoadBegin step).</summary>
        private static void OnLoadBegin(ref LoadBegin e) { WeaponsPackFear.Reset(); }

        /// <summary>Scene change: same reset as the NPC entry's ResetAll did.</summary>
        internal static void OnSceneLoaded() { if (_scope != null) WeaponsPackFear.Reset(); }

        /// <summary>Releases every part's state; the scope then removes the patches, jobs and hooks.</summary>
        internal static void Shutdown()
        {
            try { WeaponsPackShadows.Shutdown(); } catch (Exception e) { Log.Warn(_scope, "Weapons Pack shadows shutdown: " + e.Message); }
            WeaponsPackFear.Shutdown(); WeaponsPackIcons.Shutdown(); WeaponsPackSound.Shutdown();
            _discovery = null; _attempted = false; _scope = null;
        }
    }
}
