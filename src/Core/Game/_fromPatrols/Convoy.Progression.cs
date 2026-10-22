using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Game progression query: boss kills counted from the game's Boss_* global flags; Core progression query in Wave B (Wave A split of Patrols Spawning/Convoy.cs).</summary>
    internal partial class Convoy
    {
        internal static int BossKills()
        {
            int n = 0;
            try
            {
                foreach (var b in FsmVariables.GlobalVariables.BoolVariables)
                    if (b != null && b.Name != null && b.Name.StartsWith("Boss_") && b.Value) n++;
            }
            catch (Exception) { }
            return n;
        }
    }
}
