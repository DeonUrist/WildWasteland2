using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Survival
{
    internal sealed class SleepRecovery
    {
        private readonly IModule _owner;
        private readonly Dictionary<FsmFloatAdd, FsmFloat> _nativeHealing = new Dictionary<FsmFloatAdd, FsmFloat>();
        private WorldWatch _corpses;
        private bool _sleeping;
        private float _nextNotice;
        internal SleepRecovery(IModule owner) { _owner = owner; }
        internal void Init()
        {
            Hooks.EnterState.Prefix(_owner, BeforeState);
            Hooks.ProcessEvent.Prefix(_owner, BeforeEvent);
            _corpses = WorldIndex.Watch(_owner, Corpse, null);
        }
        private static bool Corpse(Transform transform, string name)
        { return name != null && name.IndexOf("_Dead", System.StringComparison.OrdinalIgnoreCase) >= 0 || transform.CompareTag("Carcass"); }
        private bool BeforeState(Fsm fsm, FsmState state)
        {
            if (fsm.Name != "Sleep" || state.Name != "checkInCar" || fsm.GameObject != GameRefs.Player) return true;
            _sleeping = true;
            foreach (var action in state.Actions)
            {
                var heal = action as FsmFloatAdd;
                if (heal == null || heal.variableName.Value != "Health") continue;
                if (!_nativeHealing.ContainsKey(heal)) { _nativeHealing.Add(heal, heal.add); heal.add = 0f; }
            }
            return true;
        }
        private bool BeforeEvent(Fsm fsm, FsmEvent evt, FsmEventData data)
        {
            if (evt != null && evt.Name == "Activate" && fsm.Name == "Sleep" && fsm.GameObject == GameRefs.Player && (ModApi.Get<IMentalBreak>()?.Active ?? false))
            {
                if (Time.unscaledTime >= _nextNotice)
                { _nextNotice = Time.unscaledTime + 2; ModApi.Get<INotifications>()?.Push("Cannot sleep during a mental break", NotificationKind.Warning, 2); }
                return false;
            }
            if (!_sleeping || evt == null || evt.Name != "back" || fsm.Name != "Sleep" || fsm.GameObject != GameRefs.Player
                || fsm.ActiveStateName != "SleepOnFoot" && fsm.ActiveStateName != "SleepInCar") return true;
            _sleeping = false;
            var player = GameRefs.Player;
            var healthFsm = FsmUtil.Find(player, "Health"); var health = healthFsm != null ? healthFsm.FsmVariables.FindFsmFloat("Health") : null;
            if (health == null || health.Value < .4f) return true;
            health.Value = Mathf.Min(100, health.Value + 10);
            bool nearby = false;
            foreach (var corpse in _corpses.Found)
                if (corpse != null && corpse.gameObject.activeInHierarchy && (corpse.position - player.transform.position).sqrMagnitude <= 4f) { nearby = true; break; }
            var woke = new PlayerWokeUp { CorpseNearby = nearby }; ModEvents.Publish(ref woke);
            Log.Always(_owner, "Sleep completed: normal healing +10; corpse within 2 m=" + nearby + ".");
            return true;
        }
        internal void Clear()
        { foreach (var pair in _nativeHealing) pair.Key.add = pair.Value; _nativeHealing.Clear(); _sleeping = false; }
    }
}
