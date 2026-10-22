using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Observes the optional pack's completed local player prop pose without knowing any WW body or renderer type.</summary>
    internal static class WeaponsPackBridge
    {
        private const string SourceId = "WeaponsPack";
        private delegate bool HeldSource(out Transform model, out Transform rig, out Transform view);
        private static HeldSource _source;
        private static Func<bool> _third;
        private static bool _attempted;
        /// <summary>Resolves the actual native pose and third-person flag delegates, then installs scoped observation hooks.</summary>
        internal static void Ensure(IModule owner)
        {
            if (_attempted) return; _attempted = true;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("apocalypter.weaponspack", out info) || ReferenceEquals(info.Instance, null)) return;
            try
            {
                var type = info.Instance.GetType(); const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                _source = (HeldSource)Delegate.CreateDelegate(typeof(HeldSource), info.Instance, type.GetMethod("ThirdPersonSource", flags));
                _third = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), info.Instance, type.GetMethod("ActualThirdPersonActive", flags));
                if (!Patches.Patch(owner, type.GetMethod("UpdateThirdPerson", flags), prefix: AccessTools.Method(typeof(WeaponsPackBridge), "BeforePose"), postfix: AccessTools.Method(typeof(WeaponsPackBridge), "AfterPose"), finalizer: AccessTools.Method(typeof(WeaponsPackBridge), "PoseFinished"))) throw new MissingMethodException("Pack pose observation unavailable");
                Log.Always("[Compatibility] Weapons Pack external held-prop pose connected");
            }
            catch (Exception error) { Patches.Unpatch(owner); Release(); _source = null; _third = null; Log.Warn("[Compatibility] Weapons Pack prop pose unavailable: " + error.GetBaseException().Message); }
        }
        /// <summary>Lets Presentation restore native carrier visibility before the source mutates its pose.</summary>
        private static void BeforePose() { var value = new ExternalPlayerPropPoseBegin { SourceId = SourceId }; ModEvents.Publish(ref value); }
        /// <summary>Publishes the actual native held model only after its source pose is complete.</summary>
        private static void AfterPose()
        {
            if (_source == null || _third == null || !ModEvents.HasListeners<ExternalPlayerPropPoseReady>()) return;
            try
            {
                Transform held, rig, view;
                if (!_source(out held, out rig, out view) || held == null) { Release(); return; }
                var value = new ExternalPlayerPropPoseReady { SourceId = SourceId, Model = held, ThirdPerson = _third() }; ModEvents.Publish(ref value);
            }
            catch (Exception error) { Release(); Log.Warn("[Compatibility] Weapons Pack prop snapshot failed: " + error.GetBaseException().Message); }
        }
        /// <summary>Releases presentation even when the original callback throws before its postfix can publish a pose.</summary>
        private static Exception PoseFinished(Exception __exception) { if (__exception != null) Release(); return __exception; }
        /// <summary>Signals source release without deciding how Presentation restores its owned visuals.</summary>
        private static void Release() { var value = new ExternalPlayerPropReleased { SourceId = SourceId }; ModEvents.Publish(ref value); }
        /// <summary>Releases all source delegates and notifies any still-running presentation consumer.</summary>
        internal static void Shutdown() { Release(); _source = null; _third = null; _attempted = false; }
    }
}
