using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>First-person handling sounds (empty click, reload) of the drawn gun play at the camera instead of the offset weapon root.</summary>
    internal static class EmptyGunSound
    {
        /// <summary>Patches PlaySound.DoPlaySound with <see cref="Before"/>.</summary>
        internal static void Init(IModule owner)
        {
            Core.Patches.Patch(owner, AccessTools.Method(typeof(PlaySound), "DoPlaySound"),
                prefix: AccessTools.Method(typeof(EmptyGunSound), nameof(Before)));
        }

        /// <summary>Harmony prefix on PlaySound.DoPlaySound (every PlaySound): first-person empty click / reload sounds of the drawn gun play at the camera instead of the offset weapon root; false = played here. First filter: FSM name length.</summary>
        internal static bool Before(PlaySound __instance)
        {
            var fsm = __instance.Fsm;
            if (fsm == null) return true;
            string n = fsm.Name;
            int len = n != null ? n.Length : 0;                      // (WW perf) Attack/Reload = 6, AnimSound = 9: one int compare rejects most sounds
            if (len != 6 && len != 9) return true;
            bool empty = n == "Attack" && fsm.ActiveStateName == "empty";
            if (!empty && n != "AnimSound" && n != "Reload") return true;
            var weapon = __instance.Owner;
            if (weapon == null || weapon.transform.parent != PlayerState.WeaponsParent) return true;
            var animation = Core.ModApi.Get<Core.IAnimationApi>();
            if (!GameRefs.InGame || animation != null && animation.PlayerThirdPerson) return true;
            var camera = PlayerState.PlayerCamera;
            var clip = __instance.clip != null ? __instance.clip.Value as AudioClip : null;
            if (camera == null || clip == null || empty && clip.name != "empty_fire") return true;
            // Weapon roots are offset from the camera; first-person handling sounds belong at the listener.
            AudioSource.PlayClipAtPoint(clip, camera.position, __instance.volume.Value);
            if (Log.L2) GunplayLog.Deep(weapon.name + " " + (empty ? "empty click" : "reload sound") + ": source corrected to first-person camera.");
            return false;       // OnEnter still calls Finish() after DoPlaySound.
        }
    }
}
