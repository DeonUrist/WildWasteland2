using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
namespace WildWasteland.Modules.Patrols
{
    // WW: the cache is no longer a component added to the player's (saved) car (AGENTS.md 5.10) but a static table keyed by the car's
    // instance id (refreshed every 5 s as before, swept on scene load). The component type stays, empty, so cars saved by Apocapatrol
    // with it still load (Core.SaveTypes maps the old type name here).
    internal sealed partial class PatrolVisibility : MonoBehaviour
    {
    }
}
