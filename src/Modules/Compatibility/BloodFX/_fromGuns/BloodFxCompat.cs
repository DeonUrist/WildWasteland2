using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    internal static class BloodFxCompat
    {
        private static bool checkedPlugin, resumed;
        private static object owner; private static Action update; private static Action<GameObject> collision;
        private static FieldInfo relayOwner; private static Job job;
        private static ConfigEntry<KeyCode> testKey;
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Ensure()
        {
            if (checkedPlugin) return; checkedPlugin = true;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.apoca.bloodfx", out info) || ReferenceEquals(info.Instance, null)) return;
            try {
                object plugin = info.Instance; var type = plugin.GetType(); const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var tick = type.GetMethod("Update", flags, null, Type.EmptyTypes, null); var hit = type.GetMethod("OnSprayCollision", flags, null, new[] { typeof(GameObject) }, null);
                var relay = type.Assembly.GetType("BloodFX.BloodRelay", true); relayOwner = relay.GetField("Owner", flags); var enter = relay.GetMethod("OnParticleCollision", flags, null, new[] { typeof(GameObject) }, null);
                var enabled = type.GetField("cfgEnabled", flags);
                var test = type.GetMethod("TestHit", flags, null, new[] { typeof(bool) }, null); var key = type.GetField("keyTestHit", flags);
                if (type.FullName != "BloodFX.Plugin" || tick == null || tick.ReturnType != typeof(void) || hit == null || hit.ReturnType != typeof(void) || enter == null || enter.ReturnType != typeof(void) || relayOwner == null || relayOwner.FieldType != type || enabled == null || enabled.FieldType != typeof(ConfigEntry<bool>) || enabled.GetValue(plugin) == null) throw new MissingMemberException("unsupported BloodFX lifecycle contract");
                update = (Action)Delegate.CreateDelegate(typeof(Action), plugin, tick); collision = (Action<GameObject>)Delegate.CreateDelegate(typeof(Action<GameObject>), plugin, hit);
                if (!Core.Patches.Patch(GunsModule.Instance, enter, prefix: AccessTools.Method(typeof(BloodFxCompat), nameof(BeforeCollision)))) { update = null; collision = null; return; }
                if (test != null && test.ReturnType == typeof(void) && key != null && key.FieldType == typeof(ConfigEntry<KeyCode>)) {
                    testKey = key.GetValue(plugin) as ConfigEntry<KeyCode>;
                    if (testKey != null && Core.Patches.Patch(GunsModule.Instance, test, prefix: AccessTools.Method(typeof(BloodFxCompat), nameof(BeforeTestHit)))) Core.Log.Always("[Guns] BloodFX: F8 debug hit suppressed while WW compatibility is active; saved binding preserved.");
                }
                owner = plugin; job = Scheduler.EveryFrame(GunsModule.Instance, "BloodFX lifecycle", Tick);
                Core.Log.Always("[Guns] BloodFX " + info.Metadata.Version + ": scene-load lifecycle recovery connected.");
            } catch (Exception e) { owner = null; update = null; collision = null; Core.Log.Warn("[Guns] BloodFX compatibility unavailable: " + e.GetBaseException().Message); }
        }
        private static void Tick()
        {
            if (ReferenceEquals(owner, null) || update == null || (owner as UnityEngine.Object) != null || !GameRefs.InGame) return;
            if (!resumed) { resumed = true; Core.Log.Always("[Guns] BloodFX: native plugin object destroyed; continuing its managed updater through WW."); }
            update();
        }
        private static bool BeforeCollision(object __instance, GameObject __0)
        {
            if (ReferenceEquals(owner, null) || collision == null || (owner as UnityEngine.Object) != null || !ReferenceEquals(relayOwner.GetValue(__instance), owner)) return true;
            if (GameRefs.InGame) collision(__0); return false;
        }
        private static bool BeforeTestHit(bool __0) { return ReferenceEquals(owner, null) || __0 || testKey == null || testKey.Value != KeyCode.F8; }
        internal static void Shutdown() { if (job != null) job.Remove(); job = null; owner = null; update = null; collision = null; relayOwner = null; testKey = null; checkedPlugin = resumed = false; }
    }
}
