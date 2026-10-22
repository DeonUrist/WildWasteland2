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
            bool voiced=Core.ModApi.Get<Core.INpcVoice>()?.Shout(NpcaiSettings.ShoutVolume.Value)==true;
            Noise(_player, Core.Origin.Scene(shout.WorldPos), shout.Radius, Src.Shout, "the player shouting", a => a.Hostile.Contains("Player"), null, _player);
            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: the player shouts (" + shout.Radius.ToString("0") + " m)" + (!voiced ? ", no voice clip found" : ""));
        }
    }
}
