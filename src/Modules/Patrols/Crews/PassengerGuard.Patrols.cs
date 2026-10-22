using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Crew orchestration of PassengerGuard: attach as passenger, turret gunner or burst-firing driver; Patrols part (Wave A split of Crews/Passenger.cs).</summary>
    internal partial class PassengerGuard
    {
        // a gunner on a turret spot (roof / motorcycle seat): passenger combat, but all around - no fire arc, the Pose turns the whole body
        internal static PassengerGuard AttachTurret(GameObject who, GameObject car, Transform anchor, float arc)
        {
            Attach(who, car, anchor);
            var guard = who.GetComponent<PassengerGuard>();
            guard._turret = true; guard._turretArc = arc; guard._nextCombatCheck = 0f;
            return guard;
        }

        // A shooting mob at the wheel: like a passenger, but it only shoots in bursts at random intervals and sits in the
        // generic driver pose (hands on the wheel) in between.
        internal static PassengerGuard AttachDriver(GameObject driver, GameObject car, Transform sit)
        {
            Attach(driver, car, sit);
            var guard = driver.GetComponent<PassengerGuard>();
            guard.SetDriverMode(sit);
            return guard;
        }

        internal void SetDriverMode(Transform sit)
        {
            _anchor = sit;
            _driverMode = true; _nextCombatCheck = 0f;
            _shooting = false;
            _ranged = _rangedPrefab && PatrolsModule.RangedCombat.Value;
            if (_ranged) foreach (var fsm in _combat) if (fsm != null) fsm.enabled = true;
            if (_attack != null) _attack.enabled = false;
            if (_pose != null) { _pose.SetShooting(false); _pose.SetAim(Vector3.zero, false); }
            ScheduleBurst();
            PatrolsModule.Verbose("Driver: " + _passenger.name + (_ranged ? " shoots in bursts every " + PatrolsModule.FireIntervalMin.Value + "-" + PatrolsModule.FireIntervalMax.Value + " s" : " does not shoot"));
        }

        private void ScheduleBurst()
        {
            _nextBurst = Time.time + UnityEngine.Random.Range(PatrolsModule.FireIntervalMin.Value, Mathf.Max(PatrolsModule.FireIntervalMin.Value, PatrolsModule.FireIntervalMax.Value));
        }

        internal static void Attach(GameObject passenger, GameObject car, Transform anchor)
        {
            var guard = passenger.GetComponent<PassengerGuard>() ?? passenger.AddComponent<PassengerGuard>();
            guard._passenger = passenger;
            guard._car = car;
            guard._anchor = anchor; guard._nextCombatCheck = 0f;
            guard._rangedPrefab = IsRanged(passenger);
            guard._ranged = guard._rangedPrefab && PatrolsModule.RangedCombat.Value;
            guard._combat = Array.FindAll(passenger.GetComponents<PlayMakerFSM>(), f => Array.IndexOf(CombatFsms, f.FsmName) >= 0);
            guard._attack = Array.Find(guard._combat, f => f.FsmName == "Attack");
            guard._fsms = passenger.GetComponents<PlayMakerFSM>();
            guard._meleeWeapons = guard._rangedPrefab ? FindMeleeWeapons(passenger) : new GameObject[0];
            var rs = new List<Renderer>(); var cs = new List<Collider>();
            foreach (var w in guard._meleeWeapons) { rs.AddRange(w.GetComponentsInChildren<Renderer>(true)); cs.AddRange(w.GetComponentsInChildren<Collider>(true)); }
            guard._meleeRenderers = rs.ToArray(); guard._meleeColliders = cs.ToArray();
            guard.IndexTargetSources();
            guard.HideMeleeWeapons();
            guard._pose = passenger.GetComponent<Pose>();
            if (guard._pose != null) guard._pose.ConfigurePassengerAim();
            MuteNonCombatAi(passenger, PatrolsModule.RangedCombat.Value);
            PatrolsModule.Verbose("Passenger: " + passenger.name + " fixed in driver pose; "
                + (guard._ranged ? "vanilla ranged fire and Sound2 enabled in +/-" + PatrolsModule.FireArc.Value + " degree arc" : "passive"));
        }
    }
}
