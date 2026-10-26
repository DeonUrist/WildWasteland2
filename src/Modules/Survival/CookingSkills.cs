using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Survival
{
    /// <summary>Native item names retain cooking ownership, fractional stew credit and baked food nutrition across saves.</summary>
    internal sealed class CookingSkills : ICookingSkills
    {
        private sealed class Item { internal bool Player, Converted, BestParts; internal float Bonus = 1, RawCredit; }
        private static readonly SaveKey State = SaveBus.Key("WildWasteland.Survival.CookingSkills.v1");
        private readonly IModule _owner;
        private readonly Dictionary<GameObject, Item> _items = new Dictionary<GameObject, Item>();
        private readonly Dictionary<string, Item> _saved = new Dictionary<string, Item>(StringComparer.Ordinal);
        private readonly Dictionary<FloatAdd, FsmFloat> _nutrition = new Dictionary<FloatAdd, FsmFloat>();
        private readonly Dictionary<UiTextSetText, FsmString> _labels = new Dictionary<UiTextSetText, FsmString>();
        private readonly List<GameObject> _deadItems = new List<GameObject>();
        private readonly List<FsmStateAction> _deadActions = new List<FsmStateAction>();
        private string _error;
        private bool _fresh;
        internal CookingSkills(IModule owner) { _owner = owner; }
        internal void Init()
        {
            Hooks.EnterState.Postfix(_owner, Grabbed);
            Hooks.CreateObjectEnter.Postfix(_owner, Cooked);
            WorldIndex.Watch(_owner, (t, name) => name != null && _saved.ContainsKey(name), Restored);
            SaveBus.StateChanged += StateChanged;
            Scheduler.Interval(_owner, "Survival.Cooking skill cleanup", 1f, Sweep);
        }
        private void StateChanged(string previous, string now) { if (now == "Start" || now == "setSeed") { Clear(); _fresh = true; } }
        private Item Data(GameObject item)
        {
            Item data;
            if (_items.TryGetValue(item, out data)) return data;
            if (_saved.TryGetValue(item.name, out data)) _saved.Remove(item.name); else data = new Item();
            _items.Add(item, data); return data;
        }
        public void Touch(GameObject item, EntityId actor)
        {
            if (item == null || FsmUtil.Find(item, "Cook") == null && item.transform.Find("StewPot.Visual") == null) return;
            var data = Data(item); bool player = actor == Entities.Player;
            if (data.Player != player) data.RawCredit = 0;
            data.Player = player;
        }
        public float NutritionMultiplier(GameObject item)
        { return item != null && Data(item).Player ? 1 + (ModApi.Get<IPlayerSkills>()?.Bonus(PlayerSkillId.Survival) ?? 0) : 1; }
        public float InsanityMultiplier(GameObject item) { return item != null && Data(item).BestParts ? .75f : 1; }
        public void StewCooked(GameObject item, float rawNutrition)
        {
            if (item == null || rawNutrition <= 0) return;
            var data = Data(item); if (!data.Player) return;
            if (SkillPerks.Has("OnlyBestParts")) data.BestParts = true;
            data.RawCredit += rawNutrition;
            int actions = Mathf.FloorToInt((data.RawCredit + .00001f) / 10f);
            if (actions <= 0) return;
            data.RawCredit = Mathf.Max(0, data.RawCredit - actions * 10f); Award(actions);
        }
        private void Grabbed(Fsm fsm, FsmState state)
        {
            if (fsm.Name != "GrabItem" || state.Name != "ItemInHand") return;
            var item = fsm.Variables.FindFsmGameObject("Item"); var hand = FsmVariables.GlobalVariables.FindFsmGameObject("Hand");
            if (item != null && item.Value != null && hand != null && hand.Value != null && item.Value.transform.IsChildOf(hand.Value.transform)) Touch(item.Value, Entities.Player);
        }
        private void Cooked(CreateObject action)
        {
            if (action.Fsm == null || action.Fsm.Name != "Cook") return;
            Item raw; if (!_items.TryGetValue(action.Owner, out raw))
            { if (!_saved.ContainsKey(action.Owner.name)) return; raw = Data(action.Owner); }
            var made = action.storeObject != null ? action.storeObject.Value : null;
            if (!raw.Player || raw.Converted || made == null || FsmUtil.Find(made, "Food") == null || FsmUtil.Find(made, "Cook") != null) return;
            raw.Converted = true;
            var cooked = Data(made); cooked.Bonus = NutritionMultiplier(action.Owner); cooked.BestParts = SkillPerks.Has("OnlyBestParts"); Apply(made, cooked); Award(1);
        }
        private static void Award(float units)
        { var action = new PlayerSkillAction { Skill = PlayerSkillId.Survival, Units = units }; ModEvents.Publish(ref action); }
        private void Restored(Transform node, string name, Component component)
        { if (_saved.ContainsKey(name)) Apply(node.gameObject, Data(node.gameObject)); }
        private void Apply(GameObject item, Item data)
        {
            if (data.Bonus == 1) return;
            var food = FsmUtil.Find(item, "Food"); if (food == null) return;
            foreach (var state in food.FsmStates) foreach (var action in state.Actions)
            {
                var add = action as FloatAdd;
                if (add == null || add.floatVariable.Name != "Hunger" || add.add.Value >= 0) continue;
                FsmFloat original;
                if (!_nutrition.TryGetValue(add, out original)) { original = add.add; _nutrition.Add(add, original); }
                add.add = new FsmFloat(original.Value * data.Bonus);
            }
            var names = FsmUtil.Find(item, "ItemName"); if (names == null) return;
            foreach (var state in names.FsmStates) foreach (var action in state.Actions)
            {
                var label = action as UiTextSetText;
                if (label == null || label.text == null || label.text.Value.IndexOf("Hunger -", StringComparison.Ordinal) < 0) continue;
                FsmString original;
                if (!_labels.TryGetValue(label, out original)) { original = label.text; _labels.Add(label, original); }
                var lines = original.Value.Split('\n');
                for (int i = 0; i < lines.Length; i++) if (lines[i].StartsWith("Hunger -", StringComparison.Ordinal))
                {
                    float value; if (float.TryParse(lines[i].Substring(8), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) lines[i] = "Hunger -" + (value * data.Bonus).ToString("0.##", CultureInfo.InvariantCulture);
                }
                label.text = new FsmString { Value = string.Join("\n", lines) };
            }
        }
        internal void Load()
        {
            try
            {
                string record = _fresh ? null : State.Read(); _fresh = false;
                if (record == null) return;
                var lines = record.Split('\n'); bool current = lines[0] == "2"; if (!current && lines[0] != "1") throw new FormatException("Invalid cooking skill record");
                for (int i = 1; i < lines.Length; i++)
                {
                    var fields = lines[i].Split('\t'); if (fields.Length != (current ? 5 : 4) || fields[1] != "0" && fields[1] != "1" || current && fields[4] != "0" && fields[4] != "1") throw new FormatException("Invalid cooking ownership");
                    var item = new Item { Player = fields[1] == "1", Bonus = float.Parse(fields[2], CultureInfo.InvariantCulture), RawCredit = float.Parse(fields[3], CultureInfo.InvariantCulture) };
                    item.BestParts = current && fields[4] == "1";
                    if (float.IsNaN(item.Bonus) || item.Bonus < 1 || item.Bonus > 1.5f || float.IsNaN(item.RawCredit) || item.RawCredit < 0 || item.RawCredit >= 10) throw new FormatException("Invalid cooking bonus");
                    _saved.Add(Encoding.UTF8.GetString(Convert.FromBase64String(fields[0])), item);
                }
                WorldIndex.Rescan();
            }
            catch (Exception error) { _error = error.Message; Log.Error(_owner, "Cooking skill state could not be loaded", error); }
        }
        internal void LoadBegin() { Clear(); _fresh = false; }
        private void Sweep()
        {
            _deadItems.Clear(); foreach (var pair in _items) if (pair.Key == null) _deadItems.Add(pair.Key);
            foreach (var item in _deadItems) _items.Remove(item);
            _deadActions.Clear(); foreach (var pair in _nutrition) if (pair.Key.Owner == null) _deadActions.Add(pair.Key);
            foreach (var action in _deadActions) _nutrition.Remove((FloatAdd)action);
            _deadActions.Clear(); foreach (var pair in _labels) if (pair.Key.Owner == null) _deadActions.Add(pair.Key);
            foreach (var action in _deadActions) _labels.Remove((UiTextSetText)action);
            _deadItems.Clear(); _deadActions.Clear();
        }
        internal void Save(string slot)
        {
            if (_error != null) { SaveBus.ReportFailure(_owner, _error); return; }
            var record = new StringBuilder("2");
            foreach (var pair in _saved) Append(record, pair.Key, pair.Value);
            foreach (var pair in _items) if (pair.Key != null) Append(record, pair.Key.name, pair.Value);
            State.Write(_owner, slot, record.ToString());
        }
        private static void Append(StringBuilder record, string name, Item item)
        {
            if (!item.Player && item.Bonus == 1 && item.RawCredit == 0 && !item.BestParts) return;
            record.Append('\n').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(name))).Append('\t').Append(item.Player ? '1' : '0').Append('\t').Append(item.Bonus.ToString("R", CultureInfo.InvariantCulture)).Append('\t').Append(item.RawCredit.ToString("R", CultureInfo.InvariantCulture)).Append('\t').Append(item.BestParts ? '1' : '0');
        }
        internal void Clear()
        {
            foreach (var pair in _nutrition) pair.Key.add = pair.Value;
            foreach (var pair in _labels) pair.Key.text = pair.Value;
            _nutrition.Clear(); _labels.Clear(); _items.Clear(); _saved.Clear(); _error = null;
        }
        internal void Shutdown() { SaveBus.StateChanged -= StateChanged; Clear(); }
    }
}
