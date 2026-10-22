using UnityEngine;
using UnityEngine.Rendering;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>Owns mesh shadows and native carrier visibility for external local-player held-prop poses.</summary>
    internal static class WeaponsPackShadows
    {
        private static string _sourceId;
        private static IModule _module;
        private static GameObject shadow, carrier; private static Transform model; private static Renderer[] native;
        private static bool[] enabled; private static Body owner; private static bool suppressed;
        /// <summary>Subscribes owned presentation policy to external pose and save/load boundary events.</summary>
        internal static void Install(PlayerBodyModule module)
        {
            _module = module;
            ModEvents.Subscribe<ExternalPlayerPropPoseBegin>(module, BeforePose);
            ModEvents.Subscribe<ExternalPlayerPropPoseReady>(module, AfterPose);
            ModEvents.Subscribe<ExternalPlayerPropReleased>(module, Released);
            ModEvents.Subscribe<Core.Events.SaveBegin>(module, SaveBegin);
            ModEvents.Subscribe<Core.Events.LoadBegin>(module, LoadBegin);
        }
        /// <summary>Restores the previous source before admitting a new native update; latest Begin owns the local prop.</summary>
        private static void BeforePose(ref ExternalPlayerPropPoseBegin value) { _sourceId = value.SourceId; Restore(); if (shadow != null) shadow.SetActive(false); }
        /// <summary>Restores and removes visuals only when the currently active external source is released.</summary>
        private static void Released(ref ExternalPlayerPropReleased value) { if (_sourceId == value.SourceId) ClearVisuals(); }
        /// <summary>Clears temporary render state before serialization of native scene objects.</summary>
        private static void SaveBegin(ref Core.Events.SaveBegin value) { ClearVisuals(); }
        /// <summary>Releases all cached scene renderers before another world loads.</summary>
        private static void LoadBegin(ref Core.Events.LoadBegin value) { ClearVisuals(); }
        /// <summary>Copies the actual held model to presentation-owned shadow meshes while preserving native visibility intent.</summary>
        private static void AfterPose(ref ExternalPlayerPropPoseReady value)
        {
            Restore(); if (shadow != null) shadow.SetActive(false);
            var body = BodyRunner.PlayerBody; var held = value.Model;
            if (_sourceId != value.SourceId || held == null || body == null || !Core.GameRefs.InGame || SaveBus.Busy || value.ThirdPerson || !PlayerBodyModule.BodyFirstPerson.Value || Game.InCar && !PlayerBodyModule.BodyInCar || body.Root == null || !body.Root.activeInHierarchy) return;
            if (shadow == null || model != held || shadow.transform.parent != body.Root.transform) {
                if (shadow != null) UnityEngine.Object.Destroy(shadow); model = held; shadow = new GameObject("WW.WeaponsPack.Shadow") { hideFlags = HideFlags.DontSave }; shadow.transform.SetParent(body.Root.transform, false);
                foreach (var filter in held.GetComponentsInChildren<MeshFilter>(true)) { if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue; var part = new GameObject("shadow"); part.transform.SetParent(shadow.transform, false); var matrix = held.worldToLocalMatrix * filter.transform.localToWorldMatrix; part.transform.localPosition = matrix.GetColumn(3); part.transform.localRotation = matrix.rotation; part.transform.localScale = matrix.lossyScale; part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh; var renderer = part.AddComponent<MeshRenderer>(); renderer.sharedMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials; renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly; renderer.receiveShadows = false; }
            }
            if (shadow.transform.childCount == 0) return;
            shadow.transform.SetPositionAndRotation(held.position, held.rotation); var scale = held.lossyScale; var parent = shadow.transform.parent.lossyScale; shadow.transform.localScale = new Vector3(scale.x / parent.x, scale.y / parent.y, scale.z / parent.z); shadow.SetActive(true);
            var current = body.ExternalWeaponCarrier; if (current == null) return;
            if (carrier != current) { carrier = current; native = carrier.GetComponentsInChildren<Renderer>(true); enabled = new bool[native.Length]; }
            owner = body; suppressed = true;
            for (int i = 0; i < native.Length; i++) { enabled[i] = native[i].enabled; native[i].enabled = false; }
        }
        /// <summary>Restores only suppressed native renderers that remain intended to be visible by their body owner.</summary>
        private static void Restore() { if (!suppressed) return; suppressed = false; if (native != null) for (int i = 0; i < native.Length; i++) if (native[i] != null && !native[i].enabled) native[i].enabled = enabled[i] && owner != null && owner.WeaponPropVisible; }
        /// <summary>Destroys temporary shadow visuals after restoring their native carrier.</summary>
        private static void ClearVisuals() { Restore(); if (shadow != null) Object.Destroy(shadow); shadow = null; model = null; native = null; carrier = null; enabled = null; owner = null; _sourceId = null; }
        /// <summary>Unsubscribes presentation and restores temporary renderer state on feature shutdown.</summary>
        internal static void Shutdown()
        {
            if (_module != null)
            {
                ModEvents.Unsubscribe<ExternalPlayerPropPoseBegin>(_module, BeforePose); ModEvents.Unsubscribe<ExternalPlayerPropPoseReady>(_module, AfterPose); ModEvents.Unsubscribe<ExternalPlayerPropReleased>(_module, Released);
                ModEvents.Unsubscribe<Core.Events.SaveBegin>(_module, SaveBegin); ModEvents.Unsubscribe<Core.Events.LoadBegin>(_module, LoadBegin);
            }
            _module = null; ClearVisuals();
        }
    }
    internal sealed partial class Body
    {
        /// <summary>Presentation-owned native held carrier; compatibility code never reads this implementation detail.</summary>
        internal GameObject ExternalWeaponCarrier { get { return _prop; } }
    }
}
