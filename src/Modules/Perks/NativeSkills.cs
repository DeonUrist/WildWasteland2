using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Perks
{
    /// <summary>Native transaction boundaries and cached on-foot movement; no scene searches.</summary>
    internal sealed partial class NativeSkills
    {
        private sealed class Trade { internal FsmInt Original; internal readonly FsmInt Modified = new FsmInt(); internal int Before; internal bool Active; }
        private sealed class Healing { internal FsmFloat Original, Health; internal readonly FsmFloat Modified = new FsmFloat(); internal float Before; internal bool Active; }
        private sealed class Running { internal FsmFloat Original, OriginalX; internal readonly FsmFloat Modified = new FsmFloat(), ModifiedX = new FsmFloat(); }
        private readonly Dictionary<IntAdd, Trade> _trades = new Dictionary<IntAdd, Trade>();
        private readonly Dictionary<FsmFloatAdd, Healing> _healing = new Dictionary<FsmFloatAdd, Healing>();
        private readonly Dictionary<SetVelocity, Running> _running = new Dictionary<SetVelocity, Running>();
        private readonly Dictionary<TMPro.TMP_Text, int> _boards = new Dictionary<TMPro.TMP_Text, int>();
        private readonly List<FsmStateAction> _deadActions = new List<FsmStateAction>();
        private readonly List<TMPro.TMP_Text> _deadBoards = new List<TMPro.TMP_Text>();
        private Vector3 _last;
        private float _lastTime;
        private bool _travel;
        private Transform _travelOwner;
        private PlayMakerFSM _movement;
        private FsmFloat _horizontal, _vertical;
        internal void Init(IModule owner)
        {
            InitNativePerks(owner);
            Hooks.GetFsmIntEnter.Postfix(owner, PriceRead);
            Hooks.IntAddEnter.Prefix(owner, BeforeTrade); Hooks.IntAddEnter.Postfix(owner, AfterTrade);
            Hooks.FsmFloatAddEnter.Prefix(owner, BeforeHealing); Hooks.FsmFloatAddEnter.Postfix(owner, AfterHealing);
            Hooks.SetVelocityDo.Prefix(owner, Run);
            Hooks.EnterState.Postfix(owner, Board);
            Scheduler.Interval(owner, "Perks.Travel", .25f, Travel);
            Scheduler.Interval(owner, "Perks.Native action cleanup", 1f, Sweep);
        }
        private void PriceRead(GetFsmInt action)
        {
            if (action.Fsm.Name == "Buyable" && action.fsmName.Value == "Value" && action.variableName.Value == "BuyValue")
                action.storeValue.Value = ModApi.Get<IPlayerSkills>()?.PurchasePrice(action.storeValue.Value) ?? action.storeValue.Value;
        }
        private void Board(Fsm fsm, FsmState state)
        {
            if (fsm.Name != "ItemList" || state.Name != "setList") return;
            var target = fsm.Variables.FindFsmGameObject("priceText"); var price = fsm.Variables.FindFsmInt("SellValue");
            var label = target != null && target.Value != null ? target.Value.GetComponent<TMPro.TMP_Text>() : null;
            if (label == null || price == null) return;
            _boards[label] = price.Value; label.text = (ModApi.Get<IPlayerSkills>()?.SalePrice(price.Value) ?? price.Value).ToString();
        }
        internal void RefreshPrices()
        { foreach (var pair in _boards) if (pair.Key != null) pair.Key.text = (ModApi.Get<IPlayerSkills>()?.SalePrice(pair.Value) ?? pair.Value).ToString(); }
        private bool BeforeTrade(IntAdd action)
        {
            if (action.intVariable.Name != "Money" || action.Fsm.Name != "Buyable" && action.Fsm.Name != "SellItemsHere") return true;
            Trade write;
            if (!_trades.TryGetValue(action, out write)) { write = new Trade(); _trades.Add(action, write); }
            write.Original = action.add; write.Before = action.intVariable.Value; write.Active = true;
            if (action.Fsm.Name == "SellItemsHere") { write.Modified.Value = ModApi.Get<IPlayerSkills>()?.SalePrice(action.add.Value) ?? action.add.Value; action.add = write.Modified; }
            return true;
        }
        private void AfterTrade(IntAdd action)
        {
            Trade write; if (!_trades.TryGetValue(action, out write) || !write.Active) return;
            write.Active = false; action.add = write.Original;
            if (action.intVariable.Value != write.Before) Award(PlayerSkillId.Barter, 1);
        }
        private bool BeforeHealing(FsmFloatAdd action)
        {
            if (action.Fsm.Name != "SlotEmptyFull" || action.Owner == null || action.Owner.name != "Bandage_Item" && action.Owner.name != "FirstAid_Item"
                || action.fsmName.Value != "Health" || action.variableName.Value != "Health" || action.add.Value <= 0) return true;
            Healing write;
            if (!_healing.TryGetValue(action, out write)) { write = new Healing(); _healing.Add(action, write); }
            var fsm = FsmUtil.Find(action.Fsm.GetOwnerDefaultTarget(action.gameObject), "Health");
            write.Health = fsm != null ? fsm.FsmVariables.FindFsmFloat("Health") : null;
            if (write.Health == null || write.Health.Value < .4f) return true;
            write.Original = action.add; write.Before = write.Health.Value; write.Active = true;
            write.Modified.Value = action.add.Value * (1 + (ModApi.Get<IPlayerSkills>()?.Bonus(PlayerSkillId.Medicine) ?? 0)); action.add = write.Modified;
            return true;
        }
        private void AfterHealing(FsmFloatAdd action)
        {
            Healing write; if (!_healing.TryGetValue(action, out write) || !write.Active) return;
            write.Active = false; action.add = write.Original;
            if (write.Before < 100 && write.Health.Value > write.Before) Award(PlayerSkillId.Medicine, 1);
        }
        private bool Run(SetVelocity action)
        {
            if (action.Fsm.Name != "Movement" || action.Owner != GameRefs.Player || action.Fsm.ActiveStateName != "Running") return true;
            Running write;
            if (!_running.TryGetValue(action, out write)) { write = new Running { Original = action.z, OriginalX = action.x }; _running.Add(action, write); action.z = write.Modified; }
            write.Modified.Value = write.Original.Value * (write.Original.Value > 0 ? 1 + (ModApi.Get<IPlayerSkills>()?.Bonus(PlayerSkillId.Athletics) ?? 0) : 1);
            if (SkillPerks.Has("Unstoppable"))
            {
                var axes = Vector2.ClampMagnitude(new Vector2(InsaneSystems.InputManager.InputController.GetKeyAxisActionValue("Horizontal"),InsaneSystems.InputManager.InputController.GetKeyAxisActionValue("Vertical")),1);
                float speed = 5 * (1 + (ModApi.Get<IPlayerSkills>()?.Bonus(PlayerSkillId.Athletics) ?? 0));
                write.Modified.Value = axes.y * speed; write.ModifiedX.Value = axes.x * speed; action.x = write.ModifiedX;
            }
            else action.x = write.OriginalX;
            return true;
        }
        private void Travel()
        {
            var player = GameRefs.Player;
            if (!GameRefs.InGame || player == null || PlayerState.Dead) { _travel = false; return; }
            var car = PlayerState.InCar ? PlayerState.CarRoot : null;
            bool driving = car != null;
            if (!driving && (PlayerState.InVehicleTransition || PlayerState.MoveState.Length == 0 || PlayerState.JumpState == "Jump" || PlayerState.JumpState == "Falling")) { _travel = false; return; }
            if (_movement == null || _movement.gameObject != player)
            { _movement = FsmUtil.Find(player, "Movement"); _horizontal = _movement?.FsmVariables.FindFsmFloat("AxisHorizontal"); _vertical = _movement?.FsmVariables.FindFsmFloat("AxisVertical"); }
            var owner = driving ? car : player.transform;
            Vector3 position = Origin.World(owner.position); float now = Time.time;
            if (_travel && _travelOwner == owner && (driving || _horizontal != null && Mathf.Abs(_horizontal.Value) > .01f || _vertical != null && Mathf.Abs(_vertical.Value) > .01f))
            {
                Vector3 delta = position - _last; delta.y = 0; float distance = delta.magnitude, elapsed = now - _lastTime;
                if (elapsed > 0 && elapsed <= .75f && distance <= (driving ? 150f : 15f) * elapsed) Award(driving ? PlayerSkillId.Driving : PlayerSkillId.Athletics, distance * .001f);
            }
            _last = position; _lastTime = now; _travelOwner = owner; _travel = true;
        }
        private static void Award(PlayerSkillId skill, float units)
        { if (units <= 0) return; var action = new PlayerSkillAction { Skill = skill, Units = units }; ModEvents.Publish(ref action); }
        internal void ResetTravel() { _travel = false; _travelOwner = null; _movement = null; _horizontal = _vertical = null; }
        private void Sweep()
        {
            _deadActions.Clear(); foreach (var pair in _trades) if (pair.Key.Owner == null) _deadActions.Add(pair.Key);
            foreach (var action in _deadActions) _trades.Remove((IntAdd)action);
            _deadActions.Clear(); foreach (var pair in _healing) if (pair.Key.Owner == null) _deadActions.Add(pair.Key);
            foreach (var action in _deadActions) _healing.Remove((FsmFloatAdd)action);
            _deadBoards.Clear(); foreach (var pair in _boards) if (pair.Key == null) _deadBoards.Add(pair.Key);
            foreach (var label in _deadBoards) _boards.Remove(label);
            _deadActions.Clear(); _deadBoards.Clear();
        }
        internal void Clear()
        {
            ClearNativePerks();
            foreach (var pair in _trades) if (pair.Value.Active) pair.Key.add = pair.Value.Original;
            foreach (var pair in _healing) if (pair.Value.Active) pair.Key.add = pair.Value.Original;
            foreach (var pair in _running) { pair.Key.z = pair.Value.Original; pair.Key.x = pair.Value.OriginalX; }
            foreach (var pair in _boards) if (pair.Key != null) pair.Key.text = pair.Value.ToString();
            _trades.Clear(); _healing.Clear(); _running.Clear(); _boards.Clear(); ResetTravel();
        }
    }
}
