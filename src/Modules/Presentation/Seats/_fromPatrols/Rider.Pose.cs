using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Rider animation helpers (humanoid clips via the animation service, bone lookup); Presentation part of Rider (Wave A split of Crews/Rider.cs).</summary>
    internal static partial class Rider
    {
        // the player animation framework's humanoid clips (WW: Core.Services.Animation.GetClip, ex reflection on Apocaplayer.Anims.Get)
        // - null without the provider (PlayerBody off) or the clip
        internal static AnimationClip Clip(string name) { return PlayerAnims.Clip(name); }

        internal static Transform Bone(Transform root, string bone)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name; int i = n.LastIndexOf(':'); if (i < 0) i = n.LastIndexOf('_');
                if ((i >= 0 ? n.Substring(i + 1) : n) == bone) return t;
            }
            return null;
        }
    }
}
