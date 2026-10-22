using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Senses part: what a bailed-out crew and an informed NPC know (Patrols bail-out, vehicle explosions, INpcApi.Inform).</summary>
    internal static partial class Senses
    {
        // ---------- Apocapatrol (only when that mod is loaded) ----------
        // A crew that bails out is a fresh NPC spawned beside the car (Patrol.BailOut instantiates the prefab and deletes the seated one), so
        // it would start with no idea where the player is. A Harmony postfix on Patrol.BailOut hands the new NPC the crew's knowledge: the
        // seated NPC's own target, or the player when within BailOutAwareRange of the car (the crews hunt the player). When the new NPC
        // registers it fights at once if it sees that target, otherwise it gets a sight-ranked ghost at the target's position and searches.
        // A postfix on Explode.Blast makes a car explosion a gunshot-ranked noise (ExplosionRange) at the wreck.
        private static readonly Dictionary<int, KeyValuePair<GameObject, float>> _handover = new Dictionary<int, KeyValuePair<GameObject, float>>();

        /// <summary>(WW) Core.Services.CrewBailedOut (raised by the Patrols module at the end of Patrol.BailOut; __result = the new on-foot NPC)</summary>
        internal static void AfterBailOut(GameObject car, GameObject pax, GameObject __result)
        {
            try
            {
                if(SimpleBandits.Has(__result))return;
                if (!On || !NpcaiSettings.BailOutAware.Value || __result == null) return;
                GameObject target = null;
                if (pax != null)
                    foreach (var f in pax.GetComponents<PlayMakerFSM>())
                        if (f != null && f.FsmName == "Detection" && f.Fsm != null && f.Fsm.Initialized)
                        {
                            var v = f.FsmVariables.FindFsmGameObject("detectedObj");
                            if (v != null && v.Value != null && v.Value.name.IndexOf("NPCAI.Ghost", StringComparison.Ordinal) < 0) target = v.Value.transform.root.gameObject;
                            break;
                        }
                if (target == null || (_playerCar != null && target == _playerCar)) { FindPlayer(); target = _player; }
                if (target == null) return;
                Vector3 from = car != null ? car.transform.position : __result.transform.position;
                if (target == _player && (target.transform.position - from).magnitude > NpcaiSettings.BailOutAwareRange.Value) return;
                Inform(__result, target);
            }
            catch (Exception e) { NpcaiRuntime.Error("Senses: bail-out handover: " + e); }
        }

        /// <summary>(WW) Core.Services.VehicleExploded (raised by the Patrols module in Explode.Blast)</summary>
        internal static void AfterBlast(GameObject car)
        {
            try
            {
                if (!On || car == null || NpcaiSettings.ExplosionRange.Value <= 0f) return;
                Noise(car, car.transform.position, NpcaiSettings.ExplosionRange.Value, Src.Gunshot, Prefab(car.name) + " exploding", null, null);
            }
            catch (Exception e) { NpcaiRuntime.Error("Senses: explosion noise: " + e); }
        }

        /// <summary>tell an NPC (now, or the moment it registers) that it knows about target</summary>
        internal static void Inform(GameObject npc, GameObject target)
        {
            if (npc == null || target == null) return;
            var a = Get(npc);
            _handover[npc.GetInstanceID()] = new KeyValuePair<GameObject, float>(target, Time.time);
            if (a != null) Handover(a);
        }

        /// <summary>Applies a bail-out / Inform handover: engage the target when seen, else a vision ghost where the crew last had it.</summary>
        private static void Handover(Agent a)
        {
            if(SimpleBandits.Has(a.Owner)){_handover.Remove(a.Owner.GetInstanceID());return;}
            if (_handover.Count == 0) return;
            KeyValuePair<GameObject, float> h;
            int id = a.Owner.GetInstanceID();
            if (!_handover.TryGetValue(id, out h)) return;
            _handover.Remove(id);
            if (h.Key == null || Time.time - h.Value > 30f) return;
            if (Visible(a, Eye(a), h.Key, h.Key == _player, InStormOf(h.Key))) { Engage(a, h.Key); if (WildWasteland.Core.Log.L2) Log(a, "bailed out and sees " + Name(h.Key)); return; }
            float now = Time.time;
            a.LastSeen = h.Key.transform.position;
            var g = GetOrMake(Src.Vision, a.LastSeen, h.Key, "crew knew where " + Name(h.Key) + " was", 6f, 2f, now);
            g.Subject = h.Key;
            if (Assign(a, g, Src.Vision, now)) if (WildWasteland.Core.Log.L2) Log(a, "bailed out, goes where its crew last had " + Name(h.Key) + " (ghost #" + g.Id + ")");
        }

    }
}
