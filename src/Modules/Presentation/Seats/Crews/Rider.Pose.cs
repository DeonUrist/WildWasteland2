using WildWasteland.Modules.Presentation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>Rider animation helpers (humanoid clips via the animation service, bone lookup); Presentation part of Rider (Wave A split of Crews/Rider.cs).</summary>
    internal static class RiderVisualHelpers
    {
        /// <summary>Parses the authored lance offset without depending on traffic helpers.</summary>
        internal static Vector3 Parse(string text,Vector3 fallback)
        {
            try{var values=(text??"").Split(',');if(values.Length!=3)return fallback;return new Vector3(float.Parse(values[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(values[1],System.Globalization.CultureInfo.InvariantCulture),float.Parse(values[2],System.Globalization.CultureInfo.InvariantCulture));}catch{return fallback;}
        }
        // the player animation framework's humanoid clips (WW: Core.ModApi.Get<Core.IAnimationApi>().GetClip, ex reflection on Apocaplayer.Anims.Get)
        // - null without the provider (PlayerBody off) or the clip
        /// <summary>A humanoid clip of the animation service, or null without the provider or the clip.</summary>
        internal static AnimationClip Clip(string name) { return PlayerAnims.Clip(name); }

        /// <summary>The transform under root whose name tail equals the bone name.</summary>
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
