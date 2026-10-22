using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>HUD side of the vanilla hit observer (Wave A split, B10): whether hit feedback wants vanilla hits and whether the Guns module reports them itself.</summary>
    internal static partial class VanillaHits
    {
        /// <summary>HitFeedback on and something to show (= old Api.Enabled), recomputed on setting changes.</summary>
        internal static bool Observing;
        /// <summary>The Guns module reports hits itself (Services.Hud.ExternalReporter) and is running.</summary>
        internal static bool Quiet { get { return HitFeedbackService.External && Services.Guns != null; } }

        /// <summary>Recomputes <see cref="Observing"/> from the HUD hit-feedback switches.</summary>
        internal static void UpdateFlags()
        {
            var api = Services.Hud as HitFeedbackService;
            Observing = api != null && api.Enabled;
        }
    }
}
