using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>NPC-owned procedural bite timing; Presentation applies the requested bone and rig displacement.</summary>
    internal static partial class DogBite
    {
        // LateUpdate, after the Animator: the lunge on the head / neck bones (and a short jolt of the body)
        /// <summary>LateUpdate: the procedural lunge on the head / neck bones.</summary>
        internal static void LateTick()
        {

            if (_list.Count == 0) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < _list.Count; i++)
            {
                var d = _list[i];
                if (d.A != null && Core.ModApi.Get<Core.ICaninePresentation>()?.Owns(d.A.Owner) == true) { d.BiteT = -1f; continue; }
                if (d.T == null) continue;
                if (d.BiteT < 0f) { if (d.Applied && d.Rig != null) { d.Rig.localPosition = d.RigBase; d.Applied = false; } continue; }
                d.BiteT += dt;
                float t = d.BiteT, len = 0.42f;
                if (t >= len) { d.BiteT = -1f; continue; }
                // draw back (up) 0-0.12 s, snap forward-down 0.12-0.2 s, ease back to 0.42 s
                float ang = t < 0.12f ? Mathf.Lerp(0f, -18f, t / 0.12f) : t < 0.2f ? Mathf.Lerp(-18f, 34f, (t - 0.12f) / 0.08f) : Mathf.Lerp(34f, 0f, (t - 0.2f) / (len - 0.2f));
                float push = t < 0.12f ? Mathf.Lerp(0f, -0.06f, t / 0.12f) : t < 0.2f ? Mathf.Lerp(-0.06f, 0.18f, (t - 0.12f) / 0.08f) : Mathf.Lerp(0.18f, 0f, (t - 0.2f) / (len - 0.2f));
                Core.ModApi.Get<Core.IAnimalVisuals>()?.Bite(d.T,d.Rig,d.RigBase,d.Bones,ang,push);
                if(d.Rig!=null)d.Applied=true;
            }
        }
    }
}
