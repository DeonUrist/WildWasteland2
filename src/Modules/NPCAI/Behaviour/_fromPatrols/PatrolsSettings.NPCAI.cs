using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Blast-lance rider combat tuning (decisions, leap, lance); Wave A split of PatrolsSettings.cs.</summary>
    public sealed partial class PatrolsModule
    {
        internal static ConfigEntry<float> RiderRange, RiderIntervalMin, RiderIntervalMax, RiderLanceSpeed, RiderMaxThrowAngle, RiderJumpRange, RiderLeapSpeed, RiderBlastDamage, RiderBlastRadius, RiderLeapReachPercent, RiderBailRange;

        /// <summary>Binds the Combat tuning entries of the blast-lance rider decisions (range, throw angle, jump, intervals, leap reach, bail range).</summary>
        partial void BindNpcaiRiderTuning(ModuleConfig cfg)
        {
            RiderRange = cfg.Tool(TCombat, "RiderRange", 35f, "A blast-lance rider throws at a target closer than this, m (and only when a flat throw reaches it)", new AcceptableValueRange<float>(5f, 150f));
            RiderMaxThrowAngle = cfg.Tool(TCombat, "RiderMaxThrowAngle", 20f, "The rider only throws when the lance reaches the predicted spot with at most this many degrees up (a throw, not a lob)", new AcceptableValueRange<float>(0f, 45f));
            RiderJumpRange = cfg.Tool(TCombat, "RiderJumpRange", 15f, "\"Witness me!\": the rider leaps when your car is closer than this, m", new AcceptableValueRange<float>(3f, 30f));
            RiderIntervalMin = cfg.Tool(TCombat, "RiderIntervalMin", 5f, "Shortest pause between two of a rider's throws, s", new AcceptableValueRange<float>(1f, 60f));
            RiderIntervalMax = cfg.Tool(TCombat, "RiderIntervalMax", 9f, "Longest pause between two of a rider's throws, s", new AcceptableValueRange<float>(1f, 120f));
            RiderLeapReachPercent = cfg.Tool(TCombat, "RiderLeapReachPercent", 80f, "\"Witness me!\": the Warboy only leaps at a car within this % of his longest jump (RiderLeapSpeed x 1.2 s, the speeds of both cars counted)", new AcceptableValueRange<float>(10f, 100f));
            RiderBailRange = cfg.Tool(TCombat, "RiderBailRange", 6f, "A Warboy on a motorcycle who cannot turn to you jumps off when you are closer than this, m", new AcceptableValueRange<float>(1f, 30f));
        }

        /// <summary>Binds the Combat tuning entries of the rider leap and lance (leap speed, blast damage/radius, lance speed).</summary>
        partial void BindNpcaiRiderLeap(ModuleConfig cfg)
        {
            RiderLeapSpeed = cfg.Tool(TCombat, "RiderLeapSpeed", 11f, "\"Witness me!\": the leap's ground speed relative to the rider's car, m/s - a long, flat jump, no lob", new AcceptableValueRange<float>(5f, 40f));
            RiderBlastDamage = cfg.Tool(TCombat, "RiderBlastDamage", 60f, "\"Witness me!\": damage of the rider's blast at its centre (in your car too); falls to a third at the edge", new AcceptableValueRange<float>(0f, 300f));
            RiderBlastRadius = cfg.Tool(TCombat, "RiderBlastRadius", 7f, "\"Witness me!\": reach of the rider's blast, m (damage and the shove of your car)", new AcceptableValueRange<float>(1f, 20f));
            RiderLanceSpeed = cfg.Tool(TCombat, "RiderLanceSpeed", 30f, "Throw speed of a rider's lance, m/s (the player's is 30)", new AcceptableValueRange<float>(5f, 80f));
        }
    }
}
