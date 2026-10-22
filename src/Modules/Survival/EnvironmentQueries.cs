using ES3Internal;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Survival
{
    public sealed partial class SurvivalModule
    {
        readonly ShelterRegistry _shelters = new ShelterRegistry();
        StormQueries _stormQueries;
        readonly DaylightQuery _daylight = new DaylightQuery();
    }
}
