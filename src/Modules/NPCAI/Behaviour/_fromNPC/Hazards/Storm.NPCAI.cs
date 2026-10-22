using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Storm part owned by NPCAI: the sight / hearing factors NPC senses apply inside a storm ([Senses] StormSight / StormHearing).</summary>
    internal static partial class Storm
    {
        internal static float SightFactor { get { return Mathf.Clamp01(NpcModule.StormSight.Value / 100f); } }
        internal static float HearingFactor { get { return Mathf.Clamp01(NpcModule.StormHearing.Value / 100f); } }
    }
}
