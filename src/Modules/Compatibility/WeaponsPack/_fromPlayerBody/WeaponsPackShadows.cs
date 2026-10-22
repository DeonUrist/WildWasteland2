using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace WildWasteland.Modules.PlayerBody
{
    internal static class WeaponsPackShadows
    {
        private delegate bool HeldSource(out Transform model, out Transform rig, out Transform view);
        private static HeldSource source; private static Func<bool> third; private static FieldInfo prop;
        private static GameObject shadow, carrier; private static Transform model; private static Renderer[] native;
        private static bool[] enabled; private static Body owner; private static bool suppressed;
        internal static void Install(PlayerBodyModule module)
        {
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("apocalypter.weaponspack", out info) || ReferenceEquals(info.Instance, null)) return;
            try {
                var type = info.Instance.GetType(); const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                source = (HeldSource)Delegate.CreateDelegate(typeof(HeldSource), info.Instance, type.GetMethod("ThirdPersonSource", flags));
                third = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), info.Instance, type.GetMethod("ActualThirdPersonActive", flags));
                prop = typeof(Body).GetField("_prop", flags);
                if (prop == null) throw new MissingFieldException("Body._prop");
                if (Core.Patches.Patch(module, type.GetMethod("UpdateThirdPerson", flags), prefix: AccessTools.Method(typeof(WeaponsPackShadows), nameof(BeforePose)), postfix: AccessTools.Method(typeof(WeaponsPackShadows), nameof(AfterPose)))) Core.Log.Always("[PlayerBody] Weapons Pack: first-person weapon shadows use pack models.");
            } catch (Exception e) { source = null; Core.Log.Warn("[PlayerBody] Weapons Pack shadow integration unavailable: " + e.GetBaseException().Message); }
        }
        private static void BeforePose() { Restore(); if (shadow != null) shadow.SetActive(false); }
        private static void AfterPose(object __0)
        {
            var body = __0 as Body; Transform held, rig, view;
            if (source == null || body == null || !Core.GameRefs.InGame || third() || !PlayerBodyModule.BodyFirstPerson.Value || Game.InCar && !PlayerBodyModule.BodyInCar || body.Root == null || !body.Root.activeInHierarchy || !source(out held, out rig, out view)) return;
            if (shadow == null || model != held || shadow.transform.parent != body.Root.transform) {
                if (shadow != null) UnityEngine.Object.Destroy(shadow); model = held; shadow = new GameObject("WW.WeaponsPack.Shadow") { hideFlags = HideFlags.DontSave }; shadow.transform.SetParent(body.Root.transform, false);
                foreach (var filter in held.GetComponentsInChildren<MeshFilter>(true)) { if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue; var part = new GameObject("shadow"); part.transform.SetParent(shadow.transform, false); var matrix = held.worldToLocalMatrix * filter.transform.localToWorldMatrix; part.transform.localPosition = matrix.GetColumn(3); part.transform.localRotation = matrix.rotation; part.transform.localScale = matrix.lossyScale; part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh; var renderer = part.AddComponent<MeshRenderer>(); renderer.sharedMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials; renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly; renderer.receiveShadows = false; }
            }
            if (shadow.transform.childCount == 0) return;
            shadow.transform.SetPositionAndRotation(held.position, held.rotation); var scale = held.lossyScale; var parent = shadow.transform.parent.lossyScale; shadow.transform.localScale = new Vector3(scale.x / parent.x, scale.y / parent.y, scale.z / parent.z); shadow.SetActive(true);
            var current = prop.GetValue(body) as GameObject; if (current == null) return;
            if (carrier != current) { carrier = current; native = carrier.GetComponentsInChildren<Renderer>(true); enabled = new bool[native.Length]; }
            owner = body; suppressed = true;
            for (int i = 0; i < native.Length; i++) { enabled[i] = native[i].enabled; native[i].enabled = false; }
        }
        private static void Restore() { if (!suppressed) return; suppressed = false; if (native != null) for (int i = 0; i < native.Length; i++) if (native[i] != null && !native[i].enabled) native[i].enabled = enabled[i] && owner != null && owner.WeaponPropVisible; }
        internal static void Shutdown() { Restore(); if (shadow != null) UnityEngine.Object.Destroy(shadow); shadow = null; model = null; source = null; third = null; prop = null; native = null; carrier = null; enabled = null; owner = null; }
    }
}
