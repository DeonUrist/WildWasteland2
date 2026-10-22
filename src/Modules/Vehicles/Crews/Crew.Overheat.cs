using System;
using System.Collections;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Raider engine heat rule (Vehicles part of Crew): with [Patrols] Raiders overheat off, the engine heat value is held at 0 while the crew holds the car.</summary>
    internal partial class Crew
    {
        // 2.6.2 [Balance] RaidersOverheat off: while the crew holds the car its engine's own heat value (the engine item's [heatValue] FSM,
        // copied every frame by the engine's getHeatValue into engineOil_Temperature and by the radiator's into water_Temperature) is 0, so the
        // oil only cools (-0.4/s) and never reaches the game's 120 (overheating: smoke, steam, engine Condition -0.5/s). The original value is
        // put back when the seat lock lifts (driver dead, crew bailed, explosion) - a taken car or a looted engine heats like any other.
        // No polling: applied on lock changes, once 2 s after Init and when the setting changes.
        /// <summary>Applies (lock held, setting off) or restores the engine heat values of this crew's car.</summary>
        internal void ApplyOverheat()
        {
            if (_car == null) return;
            try
            {
                bool cool = _locked && !PatrolsModule.RaidersOverheat.Value;   // refs-ok: Patrols crew setting read inside Patrols' Crew class
                foreach (var f in _car.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (f == null || f.FsmName != "heatValue") continue;
                    var v = f.FsmVariables.GetFsmFloat("heatValue");
                    if (v == null || Known(v)) continue;
                    _heat.Add(new System.Collections.Generic.KeyValuePair<FsmFloat, float>(v, v.Value));
                }
                foreach (var kv in _heat) if (kv.Key != null) kv.Key.Value = cool ? 0f : kv.Value;
                if (_heat.Count > 0 && Log.L2) Log.Deep(VehiclesModule.Instance, "Crew: " + _car.name + " engine heat " + (cool ? "off (RaidersOverheat off)" : "normal") + " on " + _heat.Count + " engine(s)");
            }
            catch (Exception e) { Log.Deep(VehiclesModule.Instance, "Crew: overheat setting: " + e.Message); }
        }

        /// <summary>True when the heat variable is already remembered (a loop, no LINQ closure).</summary>
        private bool Known(FsmFloat v)
        {
            foreach (var kv in _heat) if (kv.Key == v) return true;
            return false;
        }

        /// <summary>Coroutine: applies the heat rule 2 s after the crew took the car.</summary>
        private IEnumerator OverheatLater()
        {
            yield return new WaitForSeconds(2f);
            ApplyOverheat();
        }

        /// <summary>Re-applies the heat rule on every crew (setting changed).</summary>
        internal static void ApplyOverheatAll() { foreach (var c in FindObjectsOfType<Crew>()) c.ApplyOverheat(); }
    }
}
