using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Weapons Pack part serving the HUD: the pack's own HUD icon is replaced by the shared white weapon silhouette (prefix of its
    /// PackHudIcon), and <see cref="Model"/> tells the HUD icon renderer which pack model to draw. Acts only while the HUD (CleanHUD) runs.
    /// </summary>
    internal static class WeaponsPackIcons
    {
        private static Type chainsaw, flame;

        /// <summary>Resolves the pack's item types and patches PackHudIcon under <paramref name="scope"/>.</summary>
        internal static void Install(IModule scope, BepInEx.PluginInfo info)
        {
            try
            {
                var type = info.Instance.GetType();
                chainsaw = type.Assembly.GetType("ChainsawPrototype.ChainsawItem", true);
                flame = type.Assembly.GetType("ChainsawPrototype.FlamethrowerItem", true);
                var icon = type.GetMethod("PackHudIcon", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(GameObject), typeof(Texture2D).MakeByRefType() }, null);
                if (icon == null || icon.ReturnType != typeof(bool)) throw new MissingMemberException("unsupported icon contract");
                if (Core.Patches.Patch(scope, icon, prefix: AccessTools.Method(typeof(WeaponsPackIcons), nameof(BeforePackIcon))))
                    Log.Main(scope, "Weapons Pack: shared white weapon silhouettes enabled.");
            }
            catch (Exception e) { chainsaw = flame = null; Log.Warn(scope, "Weapons Pack icons unavailable: " + e.GetBaseException().Message); }
        }

        /// <summary>Prefix of the pack's PackHudIcon (HUD refresh): while the HUD runs the pack draws no icon of its own.</summary>
        private static bool BeforePackIcon(ref bool __result)
        {
            if (!CompatibilityHost.CleanHud.Running) return true;
            __result = true; return false; // let Assets.WeaponIcon render the same silhouette as other weapons
        }

        /// <summary>The pack model under a chainsaw/flamethrower item (and its icon cache key), or null for any other item.</summary>
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

        /// <summary>Forgets the pack types (no model lookups until the next install).</summary>
        internal static void Shutdown() { chainsaw = flame = null; }
    }
}
