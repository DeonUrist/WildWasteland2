using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    internal static class EmptyGunSound
    {
        internal static void Init(GunsModule owner)
        {
            Core.Patches.Patch(owner, AccessTools.Method(typeof(PlaySound), "DoPlaySound"),
                prefix: AccessTools.Method(typeof(EmptyGunSound), nameof(Before)));
        }

        internal static bool Before(PlaySound __instance)
        {
            var fsm = __instance.Fsm;
            if (fsm == null) return true;
            bool empty = fsm.Name == "Attack" && fsm.ActiveStateName == "empty";
            if (!empty && fsm.Name != "AnimSound" && fsm.Name != "Reload") return true;
            var weapon = __instance.Owner;
            if (weapon == null || weapon.transform.parent != PlayerRig.Parent) return true;
            var animation = Services.Animation;
            if (!GameRefs.InGame || animation != null && animation.PlayerThirdPerson) return true;
            var camera = PlayerRig.Camera;
            var clip = __instance.clip != null ? __instance.clip.Value as AudioClip : null;
            if (camera == null || clip == null || empty && clip.name != "empty_fire") return true;
            // Weapon roots are offset from the camera; first-person handling sounds belong at the listener.
            AudioSource.PlayClipAtPoint(clip, camera.position, __instance.volume.Value);
            if (Log.Verbose) Log.Info("[Guns] " + weapon.name + " " + (empty ? "empty click" : "reload sound") + ": source corrected to first-person camera.");
            return false;       // OnEnter still calls Finish() after DoPlaySound.
        }
    }
}
