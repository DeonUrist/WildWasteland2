using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// Storm part owned by NPCAI: the sight / hearing factors NPC senses apply inside a storm ([NPCAI / Senses] StormSight / StormHearing)
    /// and the "inside a sandstorm" test. With the Survival storm query (IStormQueries, README 6.2: replaces the fixed 2500 m circle that
    /// treats Weather-replaced storms as parked) a point counts as inside at <see cref="ExposureInside"/> exposure or more; without the
    /// provider the native-storm reader of this class decides as before.
    /// </summary>
    internal static partial class Storm
    {
        /// <summary>Storm exposure (0..1) from which a point counts as inside a sandstorm for the senses.</summary>
        internal const float ExposureInside = 0.5f;

        /// <summary>Sight range factor inside a storm ([Senses] StormSight %).</summary>
        internal static float SightFactor { get { return Mathf.Clamp01(NpcaiSettings.StormSight.Value / 100f); } }
        /// <summary>Hearing range factor inside a storm ([Senses] StormHearing %).</summary>
        internal static float HearingFactor { get { return Mathf.Clamp01(NpcaiSettings.StormHearing.Value / 100f); } }

        /// <summary>Any storm can affect the senses now (Survival query: active storms; else the native reader's list).</summary>
        internal static bool AnyForSenses { get { var q = ModApi.Get<IStormQueries>(); return q != null ? q.ActiveStorms > 0 || Dust : Any; } }

        /// <summary>The scene point is inside a sandstorm for the senses (Survival exposure, else the native reader's circle).</summary>
        internal static bool InForSenses(Vector3 scene)
        {
            var q = ModApi.Get<IStormQueries>();
            if (q == null) return In(scene);
            return q.StormExposure(Origin.World(scene)) >= ExposureInside || InDust(scene);
        }
    }
}
