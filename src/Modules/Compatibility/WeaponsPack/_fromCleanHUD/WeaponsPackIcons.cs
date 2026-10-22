using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class WeaponsPackIcons
    {
        private static bool checkedPack;
        private static Type chainsaw, flame;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Ensure(CleanHudModule module)
        {
            if (checkedPack) return;
            checkedPack = true;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("apocalypter.weaponspack", out info) || ReferenceEquals(info.Instance, null)) return;
            try
            {
                var type = info.Instance.GetType();
                chainsaw = type.Assembly.GetType("ChainsawPrototype.ChainsawItem", true);
                flame = type.Assembly.GetType("ChainsawPrototype.FlamethrowerItem", true);
                var icon = type.GetMethod("PackHudIcon", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(GameObject), typeof(Texture2D).MakeByRefType() }, null);
                if (icon == null || icon.ReturnType != typeof(bool)) throw new MissingMemberException("unsupported icon contract");
                if (Core.Patches.Patch(module, icon, prefix: AccessTools.Method(typeof(WeaponsPackIcons), nameof(BeforePackIcon))))
                    Core.Log.Always("[CleanHUD] Weapons Pack: shared white weapon silhouettes enabled.");
            }
            catch (Exception e) { chainsaw = flame = null; Core.Log.Warn("[CleanHUD] Weapons Pack icons unavailable: " + e.GetBaseException().Message); }
        }

        private static bool BeforePackIcon(ref bool __result)
        {
            if (!CleanHudModule.Running) return true;
            __result = true; return false; // let Assets.WeaponIcon render the same silhouette as other weapons
        }

        internal static GameObject Model(GameObject item, out string key)
        {
            key = null;
            if (item == null || chainsaw == null) return null;
            if (!item.name.StartsWith("Chainsaw", StringComparison.Ordinal) && !item.name.StartsWith("Flamethrower", StringComparison.Ordinal)) return null;
            for (var node = item.transform; node != null; node = node.parent)
            {
                // Pack item identities retain these prefixes after the native save suffix.
                if (node.name.StartsWith("Chainsaw", StringComparison.Ordinal) && node.GetComponent(chainsaw) != null)
                { key = "ww_weaponspack_chainsaw"; var model = node.Find("ChainsawModel"); return model != null ? model.gameObject : null; }
                if (node.name.StartsWith("Flamethrower", StringComparison.Ordinal) && node.GetComponent(flame) != null)
                { key = "ww_weaponspack_flamethrower"; var model = node.Find("FlamethrowerModel"); return model != null ? model.gameObject : null; }
            }
            return null;
        }

        internal static void Shutdown() { checkedPack = false; chainsaw = flame = null; }
    }
}
