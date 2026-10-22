using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Weapons Pack part serving melee: a chainsaw contact plays the machete slash clip of Gunplay melee instead of the pack's standard
    /// melee sound (prefix of PlayStandardMeleeHitSound). Acts only while the Guns module runs.
    /// </summary>
    internal static class WeaponsPackSound
    {
        private static bool installed;
        private static Func<object, AudioSource> source;

        /// <summary>Resolves the pack's hit audio source and patches PlayStandardMeleeHitSound under <paramref name="scope"/>.</summary>
        internal static void Install(IModule scope, BepInEx.PluginInfo info)
        {
            try
            {
                var type = info.Instance.GetType();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var field = type.GetField("sawHit", flags);
                var sound = type.GetMethod("PlayStandardMeleeHitSound", flags);
                if (field == null || field.FieldType != typeof(AudioSource) || sound == null || sound.ReturnType != typeof(void)
                    || sound.GetParameters().Length != 2 || sound.GetParameters()[0].ParameterType != typeof(Vector3)) throw new MissingMemberException("unsupported chainsaw sound contract");
                source = Access.Read<AudioSource>(field);
                installed = Core.Patches.Patch(scope, sound, prefix: AccessTools.Method(typeof(WeaponsPackSound), nameof(BeforeHitSound)));
                if (installed) Log.Main(scope, "Weapons Pack: chainsaw contacts use the machete slash sound.");
            }
            catch (Exception e) { installed = false; Log.Warn(scope, "Weapons Pack slash sound unavailable: " + e.GetBaseException().Message); }
        }

        /// <summary>Prefix of PlayStandardMeleeHitSound (per chainsaw contact): plays the slash clip at the hit point and skips the original.</summary>
        private static bool BeforeHitSound(object __instance, Vector3 __0)
        {
            if (!installed || !CompatibilityHost.Guns.Running) return true;
            var clip = ModApi.Get<IWeaponEffects>()?.StabClip(); // refs-ok: REFACTOR-REQUEST B17-3
            var audio = source(__instance);
            if (clip == null || audio == null) return true;
            audio.transform.position = __0; audio.pitch = UnityEngine.Random.Range(.9f, 1.1f); audio.volume = 1f;
            audio.PlayOneShot(clip, .9f); return false;
        }

        /// <summary>Forgets the audio source; the scope removes the patch.</summary>
        internal static void Shutdown() { installed = false; source = null; }
    }
}
