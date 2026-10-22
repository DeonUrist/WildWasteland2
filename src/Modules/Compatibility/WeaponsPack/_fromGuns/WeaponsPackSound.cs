using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    internal static class WeaponsPackSound
    {
        private static bool checkedPack, installed;
        private static FieldInfo source;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Ensure()
        {
            if (checkedPack) return;
            checkedPack = true;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("apocalypter.weaponspack", out info) || ReferenceEquals(info.Instance, null)) return;
            try
            {
                var type = info.Instance.GetType();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                source = type.GetField("sawHit", flags);
                var sound = type.GetMethod("PlayStandardMeleeHitSound", flags);
                if (source == null || source.FieldType != typeof(AudioSource) || sound == null || sound.ReturnType != typeof(void)
                    || sound.GetParameters().Length != 2 || sound.GetParameters()[0].ParameterType != typeof(Vector3)) throw new MissingMemberException("unsupported chainsaw sound contract");
                installed = Core.Patches.Patch(GunsModule.Instance, sound, prefix: AccessTools.Method(typeof(WeaponsPackSound), nameof(BeforeHitSound)));
                if (installed) Core.Log.Always("[Guns] Weapons Pack: chainsaw contacts use the machete slash sound.");
            }
            catch (Exception e) { Core.Log.Warn("[Guns] Weapons Pack slash sound unavailable: " + e.GetBaseException().Message); }
        }

        private static bool BeforeHitSound(object __instance, Vector3 __0)
        {
            if (!installed) return true;
            var clip = Tracers.StabClip();
            var audio = source.GetValue(__instance) as AudioSource;
            if (clip == null || audio == null) return true;
            audio.transform.position = __0; audio.pitch = UnityEngine.Random.Range(.9f, 1.1f); audio.volume = 1f;
            audio.PlayOneShot(clip, .9f); return false;
        }

        internal static void Shutdown() { checkedPack = installed = false; source = null; }
    }
}
