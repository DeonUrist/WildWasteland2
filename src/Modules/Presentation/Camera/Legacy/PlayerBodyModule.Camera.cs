using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Old PlayerBody entry part (Wave A split) for the camera rig. Wave B (B06): binding, hooks, patches and the end-of-frame loop are owned by
    /// the Presentation module (World part, <see cref="CameraSettings"/>, <see cref="CameraEndOfFrame"/>); the partial methods below are empty
    /// and the old config statics forward to the new entries until Wave C deletes them (ChaseCameraCompat reads three of them, B17-8).
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        /// <summary>Forwarder to <see cref="CameraSettings.ThirdPersonOnFoot"/>.</summary>
        [Obsolete("refactor: use CameraSettings / ICameraRig")] internal static ConfigEntry<bool> ThirdPersonOnFoot { get { return CameraSettings.ThirdPersonOnFoot; } }
        /// <summary>Forwarder to <see cref="CameraSettings.ToggleMiddleMouse"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<bool> ToggleMiddleMouse { get { return CameraSettings.ToggleMiddleMouse; } }
        /// <summary>Forwarder to <see cref="CameraSettings.DynamicCrosshair"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<bool> DynamicCrosshair { get { return CameraSettings.DynamicCrosshair; } }
        /// <summary>Forwarder to <see cref="CameraSettings.PickAssistOn"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<bool> PickAssistOn { get { return CameraSettings.PickAssistOn; } }
        /// <summary>Forwarder to <see cref="CameraSettings.OcclusionPrototype"/> (read by ChaseCameraCompat, B17-8).</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<bool> OcclusionPrototype { get { return CameraSettings.OcclusionPrototype; } }
        /// <summary>Forwarder to <see cref="CameraSettings.OcclusionInVehicle"/> (read by ChaseCameraCompat, B17-8).</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<bool> OcclusionInVehicle { get { return CameraSettings.OcclusionInVehicle; } }
        /// <summary>Forwarder to <see cref="CameraSettings.ObserveKey"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<Key> ObserveKey { get { return CameraSettings.ObserveKey; } }
        /// <summary>Forwarder to <see cref="CameraSettings.ObserveKeyCode"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static KeyCode ObserveKeyCode { get { return CameraSettings.ObserveKeyCode; } }
        /// <summary>Forwarder to <see cref="CameraSettings.EnableMMB"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<bool> EnableMMB { get { return CameraSettings.EnableMMB; } }
        /// <summary>Forwarder to <see cref="CameraSettings.ThirdDistance"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<float> ThirdDistance { get { return CameraSettings.ThirdDistance; } }
        /// <summary>Forwarder to <see cref="CameraSettings.ThirdHeight"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<float> ThirdHeight { get { return CameraSettings.ThirdHeight; } }
        /// <summary>Forwarder to <see cref="CameraSettings.ThirdShoulder"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<float> ThirdShoulder { get { return CameraSettings.ThirdShoulder; } }
        /// <summary>Forwarder to <see cref="CameraSettings.ThirdCarDistance"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<float> ThirdCarDistance { get { return CameraSettings.ThirdCarDistance; } }
        /// <summary>Forwarder to <see cref="CameraSettings.ThirdCarHeight"/> (read by ChaseCameraCompat, B17-8).</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<float> ThirdCarHeight { get { return CameraSettings.ThirdCarHeight; } }
        /// <summary>Forwarder to <see cref="CameraSettings.OrbitSpeed"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<float> OrbitSpeed { get { return CameraSettings.OrbitSpeed; } }
        /// <summary>Forwarder to <see cref="CameraSettings.PickAssistRadius"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<float> PickAssistRadius { get { return CameraSettings.PickAssistRadius; } }
        /// <summary>Forwarder to <see cref="CameraSettings.AdsTime"/> (read by AimTransition, Presentation/Character).</summary>
        [Obsolete("refactor: use CameraSettings.AdsTime")] internal static ConfigEntry<float> AdsTime { get { return CameraSettings.AdsTime; } }
        /// <summary>Forwarder to <see cref="CameraSettings.OcclusionOpacity"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<float> OcclusionOpacity { get { return CameraSettings.OcclusionOpacity; } }
        /// <summary>Forwarder to <see cref="CameraSettings.OcclusionRadius"/>.</summary>
        [Obsolete("refactor: use CameraSettings")] internal static ConfigEntry<float> OcclusionRadius { get { return CameraSettings.OcclusionRadius; } }

        /// <summary>Old bind step ([PlayerBody] ThirdPerson...); empty, Presentation binds <see cref="CameraSettings"/>.</summary>
        partial void BindCameraView(ModuleConfig cfg) { }
        /// <summary>Old bind step ([PlayerBody] PickAssist); empty, Presentation binds <see cref="CameraSettings"/>.</summary>
        partial void BindCameraPickAssist(ModuleConfig cfg) { }
        /// <summary>Old bind step ([PlayerBody / Camera culling]); empty, Presentation binds <see cref="CameraSettings"/>.</summary>
        partial void BindCameraCulling(ModuleConfig cfg) { }
        /// <summary>Old bind step ([Keys] Observe); empty, Presentation binds <see cref="CameraSettings"/>.</summary>
        partial void BindCameraKeys(ModuleConfig cfg) { }
        /// <summary>Old bind step ([PlayerBody / Camera] tools); empty, Presentation binds <see cref="CameraSettings"/>.</summary>
        partial void BindCameraTools(ModuleConfig cfg) { }
        /// <summary>Old bind step ([PlayerBody / Camera culling] tools); empty, Presentation binds <see cref="CameraSettings"/>.</summary>
        partial void BindCameraCullingTools(ModuleConfig cfg) { }
        /// <summary>Old init step (camera hooks/patches); empty, Presentation installs them.</summary>
        partial void InitCamera() { }
        /// <summary>Old init step (end-of-frame loop); empty, Presentation starts <see cref="CameraEndOfFrame"/>.</summary>
        partial void InitCameraEndOfFrame() { }
        /// <summary>Old shutdown step; empty, Presentation stops the camera parts.</summary>
        partial void ShutdownCamera() { }
        /// <summary>Old scene step (end-of-frame restart); empty, Presentation handles it.</summary>
        partial void SceneCameraRestart() { }
        /// <summary>Old scene step (camera caches); empty, Presentation handles it.</summary>
        partial void SceneCamera() { }
    }
}
