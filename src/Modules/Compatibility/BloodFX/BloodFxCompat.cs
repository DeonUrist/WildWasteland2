using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Recovers the optional managed BloodFX updater only after its native Unity component has been destroyed.</summary>
    internal static class BloodFxCompat
    {
        private static IModule resourceOwner;
        private static bool checkedPlugin, resumed;
        private static object owner; private static Action update; private static Action<GameObject> collision;
        private static Func<object, object> relayOwner; private static Job job, discovery;
        private static ConfigEntry<KeyCode> testKey;
        /// <summary>Registers discovery on a runner tick after every plugin's Awake (job "Compatibility.BloodFX.Discovery").</summary>
        internal static void Start(IModule scope) { resourceOwner = scope; checkedPlugin = false; discovery = Scheduler.Interval(scope, "Compatibility.BloodFX.Discovery", .5f, Discover); }
        /// <summary>Discovery job body: one attempt, then the job removes itself.</summary>
        private static void Discover() { if (discovery != null) { discovery.Remove(); discovery = null; } Ensure(resourceOwner); }
        /// <summary>Resolves native callbacks, installs scoped relay guards and admits exactly one recovery updater.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Ensure(IModule scope)
        {
            if (checkedPlugin || scope == null) return; checkedPlugin = true; resourceOwner = scope;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.apoca.bloodfx", out info) || ReferenceEquals(info.Instance, null)) return;
            try {
                object plugin = info.Instance; var type = plugin.GetType(); const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var tick = type.GetMethod("Update", flags, null, Type.EmptyTypes, null); var hit = type.GetMethod("OnSprayCollision", flags, null, new[] { typeof(GameObject) }, null);
                var relay = type.Assembly.GetType("BloodFX.BloodRelay", true); var relayField = relay.GetField("Owner", flags); var enter = relay.GetMethod("OnParticleCollision", flags, null, new[] { typeof(GameObject) }, null);
                var enabled = type.GetField("cfgEnabled", flags);
                var test = type.GetMethod("TestHit", flags, null, new[] { typeof(bool) }, null); var key = type.GetField("keyTestHit", flags);
                if (type.FullName != "BloodFX.Plugin" || tick == null || tick.ReturnType != typeof(void) || hit == null || hit.ReturnType != typeof(void) || enter == null || enter.ReturnType != typeof(void) || relayField == null || relayField.FieldType != type || enabled == null || enabled.FieldType != typeof(ConfigEntry<bool>) || enabled.GetValue(plugin) == null) throw new MissingMemberException("unsupported BloodFX lifecycle contract");
                relayOwner = Access.Read<object>(relayField); update = (Action)Delegate.CreateDelegate(typeof(Action), plugin, tick); collision = (Action<GameObject>)Delegate.CreateDelegate(typeof(Action<GameObject>), plugin, hit);
                if (!Core.Patches.Patch(scope, enter, prefix: AccessTools.Method(typeof(BloodFxCompat), nameof(BeforeCollision)))) { Shutdown(); checkedPlugin = true; return; }
                if (test != null && test.ReturnType == typeof(void) && key != null && key.FieldType == typeof(ConfigEntry<KeyCode>)) {
                    testKey = key.GetValue(plugin) as ConfigEntry<KeyCode>;
                    if (testKey != null && Core.Patches.Patch(scope, test, prefix: AccessTools.Method(typeof(BloodFxCompat), nameof(BeforeTestHit)))) Log.Main(scope, "BloodFX: F8 debug hit suppressed while WW compatibility is active; saved binding preserved.");
                }
                owner = plugin; job = Scheduler.EveryFrame(scope, "Compatibility.BloodFX.Lifecycle", Tick);
                Log.Main(scope, "BloodFX " + info.Metadata.Version + ": scene-load lifecycle recovery connected.");
            } catch (Exception e) { Shutdown(); checkedPlugin = true; Log.Warn(scope, "BloodFX compatibility unavailable: " + e.GetBaseException().Message); }
        }
        /// <summary>Runs the original updater only during gameplay and only after native component destruction.</summary>
        private static void Tick()
        {
            if (ReferenceEquals(owner, null) || update == null || (owner as UnityEngine.Object) != null || !GameRefs.InGame) return;
            if (!resumed) { resumed = true; Log.Main(resourceOwner, "BloodFX: native plugin object destroyed; continuing its managed updater through WW."); }
            update();
        }
        /// <summary>Forwards relay collision only when its captured native owner was destroyed and this integration owns recovery.</summary>
        private static bool BeforeCollision(object __instance, GameObject __0)
        {
            if (ReferenceEquals(owner, null) || collision == null || (owner as UnityEngine.Object) != null || !ReferenceEquals(relayOwner(__instance), owner)) return true;
            if (GameRefs.InGame) collision(__0); return false;
        }
        /// <summary>Suppresses only the non-player F8 test action while preserving the saved key and other test paths.</summary>
        private static bool BeforeTestHit(bool __0) { return ReferenceEquals(owner, null) || __0 || testKey == null || testKey.Value != KeyCode.F8; }
        /// <summary>Removes scoped native patches and recovery job, then releases every captured optional reference.</summary>
        internal static void Shutdown() { if (resourceOwner != null) Core.Patches.Unpatch(resourceOwner); resourceOwner = null; if (job != null) job.Remove(); job = null; if (discovery != null) discovery.Remove(); discovery = null; owner = null; update = null; collision = null; relayOwner = null; testKey = null; checkedPlugin = resumed = false; }
    }
}
