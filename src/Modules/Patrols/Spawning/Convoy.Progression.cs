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
        /// <summary>Existing convoy difficulty input forwarding to native Core progression.</summary>
        [Obsolete("refactor: use Core.Progression.BossKills")]
        internal static int BossKills()
        {
            return Core.Progression.BossKills();
        }
    }
}
