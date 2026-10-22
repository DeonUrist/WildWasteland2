using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Aim part owned by Gunplay: NPC aim spread by distance (Steps, SpreadFactor).</summary>
    internal static partial class Aim
    {
        // Steps of 5 m beyond the base distance (0 at or below it).
        internal static int Steps(float distance)
        {
            float beyond = distance - BaseDistance;
            return beyond <= 0f ? 0 : (int)(beyond / 5f);
        }

        internal static float SpreadFactor(float distance)
        {
            return 1f + Steps(distance) * SpreadPer5m / 100f;
        }
    }
}
