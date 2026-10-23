using System;
using System.Reflection;
using System.Linq.Expressions;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Weapons Pack part serving the player body: in first person the pack's held model casts its shadow through a shadow-only copy
    /// parented to the body, and the body's own native prop renderers are hidden meanwhile. Observes the pack's UpdateThirdPerson
    /// (prefix restores, postfix copies). Acts only while the player-body module runs. Reads the body's private held-prop carrier and
    /// its first-person settings directly until Presentation exposes them (REFACTOR-REQUEST B17-1).
    /// </summary>
    internal static class WeaponsPackShadows
    {
        private delegate bool HeldSource(out Transform model, out Transform rig, out Transform view);
        private static HeldSource source; private static Func<bool> third;
        private static Func<bool> flame; private static Func<GameObject> flameVisual;
        private static GameObject shadow, carrier; private static Transform model; private static Renderer[] native;
        private static bool[] enabled; private static object owner; private static bool suppressed; // refs-ok: REFACTOR-REQUEST B17-1

        /// <summary>Resolves the pack's held-model source and patches UpdateThirdPerson under <paramref name="scope"/>.</summary>
        internal static void Install(IModule scope, BepInEx.PluginInfo info)
        {
            try
            {
                var type = info.Instance.GetType(); const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                if (!Core.Patches.Patch(scope, AccessTools.Method(type, "BodyType"), prefix: AccessTools.Method(typeof(WeaponsPackShadows), nameof(BeforeBodyType))))
                    throw new InvalidOperationException("Body type adapter hook unavailable");
                source = (HeldSource)Delegate.CreateDelegate(typeof(HeldSource), info.Instance, type.GetMethod("ThirdPersonSource", flags));
                third = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), info.Instance, type.GetMethod("ActualThirdPersonActive", flags));
                flame = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), info.Instance, type.GetMethod("OwnsThirdPersonFlame", flags));
                flameVisual = Expression.Lambda<Func<GameObject>>(Expression.Field(Expression.Constant(info.Instance, type), type.GetField("thirdModel", flags))).Compile();
                if (!Core.Patches.Patch(scope, AccessTools.Method(type, "AfterThirdPersonDrawnWeapon"), postfix: AccessTools.Method(typeof(WeaponsPackShadows), nameof(AfterWeaponKey))))
                    throw new InvalidOperationException("Flamethrower pose key hook unavailable");
                Log.Main(scope, "Weapons Pack: flamethrower uses Presentation weapon poses with its own key.");
                if (Core.Patches.Patch(scope, type.GetMethod("UpdateThirdPerson", flags), prefix: AccessTools.Method(typeof(WeaponsPackShadows), nameof(BeforePose)), postfix: AccessTools.Method(typeof(WeaponsPackShadows), nameof(AfterPose))))
                    Log.Main(scope, "Weapons Pack: first-person weapon shadows use pack models.");
            }
            catch (Exception e) { source = null; Log.Warn(scope, "Weapons Pack shadow integration unavailable: " + e.GetBaseException().Message); }
        }

        /// <summary>Resolves the pack's original player-body contract through Presentation's exported types.</summary>
        private static bool BeforeBodyType(string __0, ref Type __result)
        {
            if (!CompatibilityHost.PlayerBody.Running) return true;
            __result = ModApi.Get<ICameraModInterop>()?.ResolveType("Apocaplayer." + __0);
            return __result == null;
        }

        /// <summary>Prefix of the pack's UpdateThirdPerson (every frame while it poses): restores native renderers and hides the copy.</summary>
        private static void BeforePose() { Restore(); if (shadow != null) shadow.SetActive(false); }

        /// <summary>Keeps flamethrower authoring separate from the pack's native M16 carrier.</summary>
        private static void AfterWeaponKey(ref string __0)
        {
            if (CompatibilityHost.PlayerBody.Running && flame != null && flame()) __0 = "flamethrower";
        }

        /// <summary>Postfix of UpdateThirdPerson: in first person, mirrors the held model into a shadow-only copy and hides the body's prop.</summary>
        private static void AfterPose(object __0)
        {
            if (CompatibilityHost.PlayerBody.Running && flame != null && flame() && third())
            {
                Transform posed; var characters = ModApi.Get<ICharacterPresentation>(); var visible = flameVisual();
                if (characters != null && visible != null && characters.TryGetHeldProp(Entities.Player, out posed))
                    visible.transform.SetPositionAndRotation(posed.position, posed.rotation);
                return;
            }
            var api=ModApi.Get<IPlayerWeaponVisual>();GameObject root,currentCarrier;Transform held,rig,view;var body=__0;
            if(source==null||api==null||!GameRefs.InGame||third()||!api.TryGet(body,out root,out currentCarrier)||!source(out held,out rig,out view))return;
            if (shadow == null || model != held || shadow.transform.parent != root.transform) {
                if (shadow != null) UnityEngine.Object.Destroy(shadow); model = held; shadow = new GameObject("WW.WeaponsPack.Shadow") { hideFlags = HideFlags.DontSave }; shadow.transform.SetParent(root.transform, false);
                foreach (var filter in held.GetComponentsInChildren<MeshFilter>(true)) { if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue; var part = new GameObject("shadow"); part.transform.SetParent(shadow.transform, false); var matrix = held.worldToLocalMatrix * filter.transform.localToWorldMatrix; part.transform.localPosition = matrix.GetColumn(3); part.transform.localRotation = matrix.rotation; part.transform.localScale = matrix.lossyScale; part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh; var renderer = part.AddComponent<MeshRenderer>(); renderer.sharedMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials; renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly; renderer.receiveShadows = false; }
            }
            if (shadow.transform.childCount == 0) return;
            shadow.transform.SetPositionAndRotation(held.position, held.rotation); var scale = held.lossyScale; var parent = shadow.transform.parent.lossyScale; shadow.transform.localScale = new Vector3(scale.x / parent.x, scale.y / parent.y, scale.z / parent.z); shadow.SetActive(true);
            var current = currentCarrier; if (current == null) return;
            if (carrier != current) { carrier = current; native = carrier.GetComponentsInChildren<Renderer>(true); enabled = new bool[native.Length]; }
            owner = body; suppressed = true;
            for (int i = 0; i < native.Length; i++) { enabled[i] = native[i].enabled; native[i].enabled = false; }
        }

        /// <summary>Re-enables the native prop renderers that the body still wants visible.</summary>
        private static void Restore() { if (!suppressed) return; suppressed = false; if (native != null) for (int i = 0; i < native.Length; i++) if (native[i] != null && !native[i].enabled) native[i].enabled = enabled[i] && owner != null && ModApi.Get<IPlayerWeaponVisual>()?.Visible(owner)==true; }

        /// <summary>Restores the native renderers, destroys the shadow copy and drops every reference.</summary>
        internal static void Shutdown() { Restore(); if (shadow != null) UnityEngine.Object.Destroy(shadow); shadow = null; model = null; source = null; third = null; flame = null; flameVisual = null; native = null; carrier = null; enabled = null; owner = null; }
    }
}
