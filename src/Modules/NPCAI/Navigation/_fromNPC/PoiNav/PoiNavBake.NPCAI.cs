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
        internal static readonly int Mask = ~((1 << 1) | (1 << 2) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 7) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 12) | (1 << 13) | (1 << 15) | (1 << 17) | (1 << 19) | (1 << 22));
    }
}
