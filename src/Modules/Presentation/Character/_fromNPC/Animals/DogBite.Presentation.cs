using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>DogBite part owned by Presentation: the procedural head-lunge bite pose applied in LateUpdate.</summary>
    internal static partial class DogBite
    {
        // LateUpdate, after the Animator: the lunge on the head / neck bones (and a short jolt of the body)
        internal static void LateTick()
        {
            DogAnimations.LateTick();
            if (_list.Count == 0) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < _list.Count; i++)
            {
                var d = _list[i];
                if (d.A != null && DogAnimations.Owns(d.A.Owner)) { d.BiteT = -1f; continue; }
                if (d.T == null) continue;
                if (d.BiteT < 0f) { if (d.Applied && d.Rig != null) { d.Rig.localPosition = d.RigBase; d.Applied = false; } continue; }
                d.BiteT += dt;
                float t = d.BiteT, len = 0.42f;
                if (t >= len) { d.BiteT = -1f; continue; }
                // draw back (up) 0-0.12 s, snap forward-down 0.12-0.2 s, ease back to 0.42 s
                float ang = t < 0.12f ? Mathf.Lerp(0f, -18f, t / 0.12f) : t < 0.2f ? Mathf.Lerp(-18f, 34f, (t - 0.12f) / 0.08f) : Mathf.Lerp(34f, 0f, (t - 0.2f) / (len - 0.2f));
                float push = t < 0.12f ? Mathf.Lerp(0f, -0.06f, t / 0.12f) : t < 0.2f ? Mathf.Lerp(-0.06f, 0.18f, (t - 0.12f) / 0.08f) : Mathf.Lerp(0.18f, 0f, (t - 0.2f) / (len - 0.2f));
                Vector3 axis = d.T.right;
                if (d.Bones != null)
                    for (int b = 0; b < d.Bones.Length; b++) if (d.Bones[b] != null) d.Bones[b].rotation = Quaternion.AngleAxis(ang * (b == d.Bones.Length - 1 ? 0.4f : 0.3f), axis) * d.Bones[b].rotation;
                if (d.Rig != null) { d.Rig.localPosition = d.RigBase + d.T.InverseTransformVector(d.T.forward * push); d.Applied = true; }
            }
        }
    }
}
