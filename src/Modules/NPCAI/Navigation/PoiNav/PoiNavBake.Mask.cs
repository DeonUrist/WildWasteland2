using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>PoiNavBake part owned by NPCAI: the collider mask the runtime walker also uses (PoiNav ground checks).</summary>
    internal sealed partial class PoiNavBake
    {
        // the grid-map bake mask (Nav.BakeMask): no Ignore Raycast, cars, loose items, actors...
        internal static readonly int Mask = NavMasks.Solid;
    }
}
