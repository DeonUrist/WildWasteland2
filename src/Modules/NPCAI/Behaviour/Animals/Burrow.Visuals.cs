using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Projects current burrow decisions to the visual service after native animation.</summary>
    internal static partial class Burrow
    {
        // runs in LateUpdate, after the Animator: the body (the Rig child) is sunk along the NPC's own up axis.
        // Amount 0..1 = the burrow (the eyes stay up), 1..2 = a hunter's dive (the tail goes under too)
        /// <summary>LateUpdate: sinks the rig along the NPC's up axis (0..1 burrow, 1..2 a hunter's dive).</summary>
        internal static void LateTick()
        {
            for (int i = 0; i < _list.Count; i++)
            {
                var b = _list[i];
                if (b.Rig == null || b.T == null) continue;
                if (b.Amount <= 0.0005f && !b.Applied) continue;
                float a1 = Mathf.Clamp01(b.Amount), a2 = Mathf.Clamp01(b.Amount - 1f);
                float sink = b.Depth * a1 * a1 * (3f - 2f * a1) + (b.Deep - b.Depth) * a2 * a2 * (3f - 2f * a2);
                Core.ModApi.Get<Core.IAnimalVisuals>()?.Sink(b.Rig,b.T,b.RigBase,sink);
                b.Applied = b.Amount > 0.0005f;
                if (!b.Applied) b.Rig.localPosition = b.RigBase;
            }
        }

        /// <summary>Requests a sand effect using the NPC policy tuning.</summary>
        private static void Fx(B b, Vector3 at) { Core.ModApi.Get<Core.IAnimalVisuals>()?.Sand(at,b.Hunter?NpcaiSettings.HuntFxScale.Value:NpcaiSettings.BurrowFxScale.Value,b.Hunter?NpcaiSettings.HuntFxVolume.Value:NpcaiSettings.BurrowFxVolume.Value,b.Hunter?NpcaiSettings.HuntFxPitch.Value:NpcaiSettings.BurrowFxPitch.Value,NpcaiSettings.BurrowFxRange.Value,b.Hunter); }
    }
}
