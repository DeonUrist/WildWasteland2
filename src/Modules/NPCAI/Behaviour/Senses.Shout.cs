using UnityEngine;
using WildWasteland.Core.Events;
namespace WildWasteland.Modules.NPC
{
    /// <summary>NPCAI-owned response to the shared player shout input event.</summary>
    internal static partial class Senses
    {
        /// <summary>Plays the existing raider voice and notifies hostile NPC hearing at the event position/radius.</summary>
        internal static void OnPlayerShout(ref PlayerShout shout)
        {
            if (!On) return;
            FindPlayer(); if (_player == null) return;
            var clip = Voice();
            if (clip != null)
            {
                // your own voice: a 2D source on the camera, so it stays with you (PlayClipAtPoint left it hanging in the air where you shouted)
                var src = VoiceSource();
                if (src != null) src.PlayOneShot(clip, Mathf.Clamp01(NpcModule.ShoutVolume.Value));
            }
            Noise(_player, Core.Origin.Scene(shout.WorldPos), shout.Radius, Src.Shout, "the player shouting", a => a.Hostile.Contains("Player"), null, _player);
            if (WildWasteland.Core.Log.L2) NpcModule.Log.LogInfo("Senses: the player shouts (" + shout.Radius.ToString("0") + " m)" + (clip == null ? ", no voice clip found" : ""));
        }
    }
}
