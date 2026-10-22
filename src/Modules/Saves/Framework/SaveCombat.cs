using WildWasteland.Core;
using UI = WildWasteland.Core.UI;
using Events = WildWasteland.Core.Events;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Saves
{
    // Observe native detection too: turning NPC AI off must not turn autosave safety off.
    internal static class SaveCombat
    {
        /// <summary>Caches native detection, target and health variables for autosave safety.</summary>
        sealed class Actor { internal PlayMakerFSM Fsm, Attack; internal FsmGameObject Target; internal FsmFloat Health; }
        static readonly Dictionary<PlayMakerFSM, Actor> Actors = new Dictionary<PlayMakerFSM, Actor>();
        static readonly List<PlayMakerFSM> Dead = new List<PlayMakerFSM>();
        static readonly FsmEvent Damage = FsmEvent.GetFsmEvent("Damage"), AnimalAttack = FsmEvent.GetFsmEvent("Animal_Attack"), AnimalRangedAttack = FsmEvent.GetFsmEvent("Animal_Attack_Ranged");
        static int _scan;
        static bool _scanning;
        static float _quietUntil;
        internal static bool Unsafe { get; private set; }
        /// <summary>Observes native combat and discovers detection FSMs under Saves lifetime ownership.</summary>
        internal static void Install(IModule owner)
        {
            Hooks.FsmEnable.Postfix(owner, Track);
            Hooks.ProcessEvent.Prefix(owner, Event);
            Hooks.EnterState.Postfix(owner, Attack);
            SaveBus.StateChanged += State;
            Scheduler.Sliced(owner, "Autosave combat discovery", 500, Discover);
            Reset();
        }
        /// <summary>Detaches native state observation and clears tracked actors when Saves stops.</summary>
        internal static void Shutdown() { SaveBus.StateChanged -= State; Actors.Clear(); Dead.Clear(); _scan = 0; _scanning = false; _quietUntil = 0; Unsafe = false; }
        /// <summary>Restarts conservative combat discovery at load and new-world boundaries.</summary>
        static void State(string previous, string current) { if (current == "LoadGame" || current == "setSeed") Reset(); }
        /// <summary>Forgets old actors and blocks autosave until sliced initial discovery completes.</summary>
        static void Reset() { Actors.Clear(); Dead.Clear(); _scan = 0; _scanning = true; _quietUntil = 0; Unsafe = true; }
        /// <summary>Extends the existing ten-second quiet period after combat activity.</summary>
        internal static void Activity()
        { if (SaveSystem.LocalReady) { _quietUntil = Time.unscaledTime + 10f; Unsafe = true; } }
        /// <summary>Registers a native Detection FSM once for ongoing target observation.</summary>
        static void Track(PlayMakerFSM fsm)
        {
            if (!SaveSystem.LocalWorld || fsm == null || fsm.FsmName != "Detection" || Actors.ContainsKey(fsm)) return;
            Actors.Add(fsm, new Actor { Fsm = fsm });
        }
        /// <summary>Inspects one native FSM per scheduler slice while the current world is ready.</summary>
        static bool Discover()
        {
            if (!SaveSystem.LocalReady || !_scanning) return false;
            var fsms = PlayMakerFSM.FsmList;
            if (_scan >= fsms.Count) { _scanning = false; return false; }
            Track(fsms[_scan++]); return true;
        }
        /// <summary>Observes cached native damage and animal attack events without consuming them.</summary>
        static bool Event(Fsm fsm, FsmEvent ev, FsmEventData data)
        {
            if (!SaveSystem.LocalWorld || ev == null || fsm == null) return true;
            if (ReferenceEquals(ev, Damage) || ReferenceEquals(ev, AnimalAttack) || ReferenceEquals(ev, AnimalRangedAttack)) Activity();
            return true;
        }
        /// <summary>Observes native attack-state transitions even when the NPC feature module is disabled.</summary>
        static void Attack(Fsm fsm, FsmState state)
        {
            if (!SaveSystem.LocalWorld || fsm == null || fsm.Name != "Attack" || state == null) return;
            if (state.Name == "fire" || state.Name == "attack" || state.Name == "attack_ranged" || state.Name == "throw") Activity();
        }
        /// <summary>Updates autosave safety from native targets, discovery completion and the combat quiet period.</summary>
        internal static bool Check()
        {
            bool active = false;
            Dead.Clear();
            foreach (var pair in Actors)
            {
                var a = pair.Value; var fsm = a.Fsm;
                if (fsm == null) { Dead.Add(pair.Key); continue; }
                if (!fsm.enabled || !fsm.gameObject.activeInHierarchy) continue;
                if (a.Target == null)
                {
                    a.Target = fsm.FsmVariables.FindFsmGameObject("detectedObj");
                    var hp = FsmUtil.Find(fsm.gameObject, "Health");
                    a.Health = hp != null ? hp.FsmVariables.FindFsmFloat("Health") : null;
                    a.Attack = FsmUtil.Find(fsm.gameObject, "Attack");
                }
                if (a.Attack != null && !a.Attack.enabled) continue;
                if (a.Health != null && a.Health.Value < 1) continue;
                var target = a.Target != null ? a.Target.Value : null;
                // NPC search ghosts have no faction and are not an ongoing fight.
                if (target != null && target.activeInHierarchy && !target.CompareTag("Untagged")) { active = true; break; }
            }
            foreach (var fsm in Dead) Actors.Remove(fsm);
            if (active) Activity();
            Unsafe = _scanning || active || Time.unscaledTime < _quietUntil;
            return Unsafe;
        }
    }
}
