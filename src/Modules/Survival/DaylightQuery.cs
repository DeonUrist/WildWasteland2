using ES3Internal;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Survival
{
    /// <summary>Shared daylight query backed by the existing dawn/dusk policy and Core clock.</summary>
    internal sealed class DaylightQuery : IDayLight
    {
        /// <summary>Retains the native sun-elevation hysteresis used for vehicle lights.</summary>
        public bool Night { get { return DaylightState.Dark; } }
        /// <summary>Normalized daylight derived from the native game clock, with native dark fallback.</summary>
        public float Level { get { return Clock.Ready ? Mathf.Clamp01(Mathf.Sin((float)(Clock.GameHours % 24 - 6) * Mathf.PI / 12)) : Night ? 0 : 1; } }
        /// <summary>Darkens daylight by the shared storm field at a world position.</summary>
        public float At(Vector3 world) { var storms = ModApi.Get<IStormQueries>(); return Level * (storms == null ? 1 : 1 - storms.StormExposure(world) * .75f); }
    }
}
