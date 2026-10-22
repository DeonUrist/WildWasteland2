using System;
using System.Collections;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Raider engine heat rule (RaidersOverheat off: engine heatValue held at 0 while the crew holds the car); Vehicles part of Crew (Wave A split).</summary>
    internal partial class Crew
    {
        // 2.6.2 [Balance] RaidersOverheat off: while the crew holds the car its engine's own heat value (the engine item's [heatValue] FSM,
        // copied every frame by the engine's getHeatValue into engineOil_Temperature and by the radiator's into water_Temperature) is 0, so the
        // oil only cools (-0.4/s) and never reaches the game's 120 (overheating: smoke, steam, engine Condition -0.5/s). The original value is
        // put back when the seat lock lifts (driver dead, crew bailed, explosion) - a taken car or a looted engine heats like any other.
        // No polling: applied on lock changes, once 2 s after Init and when the setting changes (Plugin).
        internal void ApplyOverheat()
        {
            if (_car == null) return;
            try
            {
                bool cool = _locked && !PatrolsModule.RaidersOverheat.Value;
                foreach (var f in _car.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (f == null || f.FsmName != "heatValue") continue;
                    var v = f.FsmVariables.GetFsmFloat("heatValue");
                    if (v == null || _heat.Any(kv => kv.Key == v)) continue;
                    _heat.Add(new System.Collections.Generic.KeyValuePair<FsmFloat, float>(v, v.Value));
                }
                foreach (var kv in _heat) if (kv.Key != null) kv.Key.Value = cool ? 0f : kv.Value;
                if (_heat.Count > 0) PatrolsModule.Verbose("Crew: " + _car.name + " engine heat " + (cool ? "off (RaidersOverheat off)" : "normal") + " on " + _heat.Count + " engine(s)");
            }
            catch (Exception e) { PatrolsModule.Verbose("Crew: overheat setting: " + e.Message); }
        }
        private IEnumerator OverheatLater()
        {
            yield return new WaitForSeconds(2f);
            ApplyOverheat();
        }
        internal static void ApplyOverheatAll() { foreach (var c in FindObjectsOfType<Crew>()) c.ApplyOverheat(); }
    }
}
