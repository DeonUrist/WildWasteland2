using System;
using System.Collections;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Seated-crew AI muting (disables NPC AI/mover FSMs); NPCAI part of Crew, future INpcApi.SetSeated (Wave A split).</summary>
    internal partial class Crew
    {
        // Switch off the AI / body-mover FSMs; called right after Instantiate (before their Start) and every frame as a guard,
        // because Detection/Damage carry EnableFSM actions that could switch them back on.
        /// <summary>Mutes the AI / mover FSMs of one NPC (allowed FSMs stay on).</summary>
        internal static void MuteAi(GameObject who, string[] allowedFsms = null)
        {
            if (who == null) return;
            MuteAi(who.GetComponents<PlayMakerFSM>(), allowedFsms);
        }

        /// <summary>INpcControl.SetSeated: seated = the AI / mover FSMs muted (MuteAi), unseated = the muted FSMs switched back on.</summary>
        internal static void SetSeated(GameObject who, bool seated)
        {
            if (who == null) return;
            if (seated) { MuteAi(who); return; }
            foreach (var f in who.GetComponents<PlayMakerFSM>())
                if (f != null && !f.enabled && Array.IndexOf(MutedFsms, f.FsmName) >= 0) f.enabled = true;
        }

        /// <summary>Disables every enabled FSM of the muted list except the allowed ones (null = none allowed).</summary>
        internal static void MuteAi(PlayMakerFSM[] fsms, string[] allowedFsms)
        {
            if (fsms == null) return;
            foreach (var f in fsms)
                if (f != null && f.enabled && Array.IndexOf(MutedFsms, f.FsmName) >= 0
                    && (allowedFsms == null || Array.IndexOf(allowedFsms, f.FsmName) < 0)) f.enabled = false;
        }

        // a shooting driver keeps its combat FSMs (its guard gates the Attack FSM); everything else is muted
        private PlayMakerFSM[] _driverFsms; private GameObject _driverFsmsOf;   // the driver's FSMs, fetched once (the guard runs twice a second)
        /// <summary>The crew guard: mutes the driver's AI, keeping its combat FSMs when it is a shooting driver.</summary>
        internal void MuteAi()
        {
            if (_driver == null) return;
            if (_driverFsms == null || _driverFsmsOf != _driver) { _driverFsms = _driver.GetComponents<PlayMakerFSM>(); _driverFsmsOf = _driver; }
            MuteAi(_driverFsms, Marker?.SetupOnly!=true && _driver.GetComponent<PassengerGuard>() != null ? PassengerGuard.CombatFsms : null);   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
        }
    }
}
