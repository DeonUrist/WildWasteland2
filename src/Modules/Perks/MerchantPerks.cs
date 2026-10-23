using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Perks
{
    internal sealed class MerchantPerks : ISkillMerchantChoices
    {
        [Serializable] private sealed class Record { public string key; public bool stock, wanted; public string[] names; public int[] prices; }
        [Serializable] private sealed class Book { public int version = 1; public string lastCar; public Record[] merchants = new Record[0]; }
        private static readonly SaveKey State = SaveBus.Key("WildWasteland.Perks.Merchants.v1");
        private readonly Dictionary<string,Record> _records = new Dictionary<string,Record>();
        private readonly HashSet<PlayMakerFSM> _lists = new HashSet<PlayMakerFSM>();
        private readonly HashSet<PlayMakerFSM> _restored = new HashSet<PlayMakerFSM>();
        private readonly List<PlayMakerFSM> _dead = new List<PlayMakerFSM>();
        private readonly HashSet<GameObject> _cars = new HashSet<GameObject>();
        private readonly List<QuestDialogueButton> _fallback = new List<QuestDialogueButton>();
        private readonly HashSet<PlayMakerFSM> _pending = new HashSet<PlayMakerFSM>();
        private string _lastCar;
        private IModule _owner;
        private bool _loaded;
        private string _error;
        internal void Init(IModule owner)
        {
            _owner = owner; Hooks.EnterState.Postfix(owner,ListState);
            Scheduler.Interval(owner,"Perks.Merchant lists",.5f,Tick);
            WorldIndex.Watch(owner,(t,n) => n != null && n.StartsWith("Sell_Items",StringComparison.Ordinal),Found);
            WorldIndex.Watch(owner,(t,n) => n == "DriveTrigger",FoundCar);
        }
        private void FoundCar(Transform node,string name,Component component) { if (node.parent != null) _cars.Add(node.parent.gameObject); }
        private void Found(Transform node,string name,Component component)
        { var f = FsmUtil.Find(node.gameObject,"ItemList"); if (f != null) _lists.Add(f); }
        private static string Key(GameObject obj)
        {
            var world = Origin.World(obj.transform.position);
            return obj.name + "@" + Mathf.RoundToInt(world.x).ToString(CultureInfo.InvariantCulture) + "," + Mathf.RoundToInt(world.z).ToString(CultureInfo.InvariantCulture);
        }
        private Record Data(string key)
        { Record data; if (!_records.TryGetValue(key,out data)) { data = new Record { key = key }; _records.Add(key,data); } return data; }
        private void ListState(Fsm fsm,FsmState state)
        {
            if (fsm.Name != "ItemList") return;
            var list = FsmUtil.Find(fsm.GameObject,"ItemList"); if (list == null) return;
            _lists.Add(list);
            if (state.Name == "done" && _loaded) { RestoreList(list); EnsureWanted(list); }
        }
        private static void Display(PlayMakerFSM list, object[] names, object[] prices)
        {
            var nameRoot = list.FsmVariables.FindFsmGameObject("item_list")?.Value;
            var priceRoot = list.FsmVariables.FindFsmGameObject("price_list")?.Value;
            if (nameRoot == null) nameRoot = list.transform.Find("item_list")?.gameObject;
            if (priceRoot == null) priceRoot = list.transform.Find("price_list")?.gameObject;
            for (int i = 0; nameRoot != null && i < nameRoot.transform.childCount; i++)
            {
                var label = nameRoot.transform.GetChild(i).GetComponent<TMPro.TMP_Text>(); if (label != null) label.text = i < names.Length ? Convert.ToString(names[i]) : "";
                label = priceRoot != null && i < priceRoot.transform.childCount ? priceRoot.transform.GetChild(i).GetComponent<TMPro.TMP_Text>() : null;
                if (label != null) label.text = i < prices.Length ? (ModApi.Get<IPlayerSkills>()?.SalePrice(Convert.ToInt32(prices[i])) ?? Convert.ToInt32(prices[i])).ToString() : "";
            }
        }
        private void RestoreList(PlayMakerFSM list)
        {
            if (!_restored.Add(list)) return;
            Record record; if (!_records.TryGetValue("list:" + Key(list.gameObject),out record) || record.names == null) return;
            list.FsmVariables.FindFsmArray("ItemNameArray").Values = record.names.Cast<object>().ToArray();
            list.FsmVariables.FindFsmArray("ItemPriceArray").Values = record.prices.Cast<object>().ToArray();
            Display(list,record.names.Cast<object>().ToArray(),record.prices.Cast<object>().ToArray());
        }
        private void EnsureWanted(PlayMakerFSM list)
        {
            if (!SkillPerks.Has("MasterTrader") || list.ActiveStateName != "done") return;
            var player = GameRefs.Player; if (player == null || (list.transform.position - player.transform.position).sqrMagnitude > 40000) return;
            var car = PlayerState.InCarFsm?.FsmVariables.FindFsmGameObject("Car")?.Value;
            if (car != null) _lastCar = car.name;
            if (car == null && _lastCar != null) car = _cars.FirstOrDefault(c => c != null && c.name == _lastCar);
            if (car == null) { float nearest = 40000; foreach (var candidate in _cars) if (candidate != null && !(ModApi.Get<IPatrols>()?.IsPatrolVehicle(candidate) ?? false)) { float d = (candidate.transform.position - player.transform.position).sqrMagnitude; if (d < nearest) { car = candidate; nearest = d; } } }
            if (car == null) return;
            var allowedObject = FsmVariables.GlobalVariables.FindFsmGameObject("ItemBuySellList")?.Value;
            var allowed = allowedObject != null ? allowedObject.GetComponent<PlayMakerArrayListProxy>() : null; if (allowed == null) return;
            var eligible = new HashSet<string>();
            foreach (var item in allowed.arrayList) { var go = item as GameObject; if (go != null) eligible.Add(Names.Base(go.name)); }
            if (eligible.Count == 0) foreach (var item in allowed.preFillGameObjectList) if (item != null) eligible.Add(Names.Base(item.name));
            string best = null; int value = -1;
            foreach (var fsm in car.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (fsm.FsmName != "Value" || !eligible.Contains(Names.Base(fsm.gameObject.name))) continue;
                var amount = fsm.FsmVariables.FindFsmInt("SellValue"); var itemName = FsmUtil.Find(fsm.gameObject,"ItemName")?.FsmVariables.FindFsmString("ItemName");
                var forBuy = FsmUtil.Find(fsm.gameObject,"ForBuy")?.FsmVariables.FindFsmInt("ForBuyInt");
                if (amount == null || amount.Value <= 0 || amount.Value <= value || itemName == null || string.IsNullOrEmpty(itemName.Value) || forBuy != null && forBuy.Value != 0) continue;
                value = amount.Value; best = itemName.Value;
            }
            var names = list.FsmVariables.FindFsmArray("ItemNameArray"); var prices = list.FsmVariables.FindFsmArray("ItemPriceArray");
            if (best == null || names == null || prices == null || names.Values.Length == 0 || prices.Values.Length != names.Values.Length || names.Values.Any(entry => Convert.ToString(entry) == best)) return;
            var n = names.Values; var p = prices.Values; n[0] = best; p[0] = value; names.Values = n; prices.Values = p; Display(list,n,p);
        }
        private void Tick()
        {
            if (!_loaded) return;
            var currentCar = PlayerState.InCarFsm?.FsmVariables.FindFsmGameObject("Car")?.Value; if (currentCar != null) _lastCar = currentCar.name;
            if (ModApi.Get<IQuestApi>() == null)
            {
                var dialogue = ModApi.Get<IExternalQuestConversation>();
                if (dialogue != null && dialogue.TalkingNow && dialogue.Npc != null) { _fallback.Clear(); Append(dialogue.Npc.gameObject,_fallback); if (dialogue.NeedsSync(_fallback)) dialogue.SyncButtons(_fallback); }
            }
            _dead.Clear(); foreach (var list in _lists) { if (list == null) { _dead.Add(list); continue; } if (list.ActiveStateName == "done") { RestoreList(list); EnsureWanted(list); } }
            foreach (var list in _dead) { _lists.Remove(list); _restored.Remove(list); }
        }
        private PlayMakerFSM Nearest(GameObject speaker)
        { PlayMakerFSM best = null; float distance = 2500; foreach (var list in _lists) { if (list == null || list.ActiveStateName != "done") continue; float d = (list.transform.position - speaker.transform.position).sqrMagnitude; if (d < distance) { distance = d; best = list; } } return best; }
        public void Append(GameObject speaker,IList<QuestDialogueButton> choices)
        {
            if (!_loaded || _error != null || !SkillPerks.Has("MerchantReroll") || speaker == null || speaker.name.IndexOf("Merchant",StringComparison.OrdinalIgnoreCase) < 0) return;
            GameObject merchant = null; var runtime = ModApi.Get<ISkillMerchantRuntime>(); bool mobile = runtime != null && runtime.Resolve(speaker,out merchant);
            var list = Nearest(speaker); if (!mobile && list == null) return;
            string key = mobile ? "mobile:" + runtime.Identity(merchant) : "speaker:" + speaker.name; var data = Data(key);
            Action<bool> reroll = stock =>
            {
                if (!GameRefs.InPlay || SaveBus.Busy || !SkillPerks.Has("MerchantReroll") || (stock ? data.stock : data.wanted)) return;
                bool success = mobile ? runtime.Reroll(merchant,stock) : RerollNative(list,stock);
                if (!success) return;
                if (stock) data.stock = true; else data.wanted = true;
                Log.Always(_owner,"Merchant reroll: " + key + "; " + (stock ? "stock" : "wanted") + " used.");
            };
            if (!data.stock) choices.Add(new QuestDialogueButton { Key = "perk-stock", Text = "I am sure you have something better", Click = () => reroll(true) });
            if (!data.wanted) choices.Add(new QuestDialogueButton { Key = "perk-wanted", Text = "I would like to sell you something else", Click = () => reroll(false) });
        }
        private static void ResetList(PlayMakerFSM list)
        {
            foreach (string name in new[] { "number", "numberEnd", "index" }) { var number = list.FsmVariables.FindFsmInt(name); if (number != null) number.Value = 0; }
            foreach (var state in list.FsmStates) foreach (var action in state.Actions)
            { var child = action as GetNextChild; if (child != null) child.resetFlag = new FsmBool(true); var next = action as ArrayGetNext; if (next != null) next.resetFlag = new FsmBool(true); }
        }
        internal static bool RerollWanted(PlayMakerFSM list)
        {
            if (list == null || list.ActiveStateName != "done") return false;
            var names = list.FsmVariables.FindFsmArray("ItemNameArray"); var prices = list.FsmVariables.FindFsmArray("ItemPriceArray"); if (names == null || prices == null) return false;
            names.Values = new object[0]; prices.Values = new object[0]; var deletes = list.FsmVariables.FindFsmArray("ItemDelete"); if (deletes != null) deletes.Values = new object[0];
            ResetList(list); Display(list,new object[0],new object[0]); list.Fsm.SetState(list.FsmStates.Any(s => s.Name == "random") ? "random" : "create"); return true;
        }
        public bool ResetWanted(GameObject station)
        { var list = station != null ? FsmUtil.Find(station,"ItemList") : null; if (!RerollWanted(list)) return false; _records.Remove("list:" + Key(station)); _pending.Add(list); return true; }
        private bool RerollNative(PlayMakerFSM list,bool stock)
        {
            if (list == null) return false; if (!stock) return ResetWanted(list.gameObject);
            var camp = list.transform.parent; if (camp == null) return false;
            var spawners = camp.GetComponentsInChildren<PlayMakerFSM>(true).Where(f => f.FsmName == "ItemSpawner" && f.gameObject.name.StartsWith("MerchantItemsSpawn",StringComparison.Ordinal)).ToArray();
            if (spawners.Length == 0) return false;
            var global = FsmVariables.GlobalVariables.FindFsmGameObject("NewGO_ArrayList")?.Value;
            var registry = global != null ? global.GetComponents<PlayMakerArrayListProxy>().FirstOrDefault(p => p.referenceName == "ArrayList_Items") : null;
            if (registry == null) return false;
            var remove = new List<GameObject>();
            foreach (var obj in registry.arrayList)
            { var item = obj as GameObject; if (item == null || (item.transform.position - camp.position).sqrMagnitude > 2500) continue; var buy = FsmUtil.Find(item,"ForBuy")?.FsmVariables.FindFsmInt("ForBuyInt"); if (buy != null && buy.Value != 0) remove.Add(item); }
            foreach (var item in remove) { ItemSpawner.Unregister(item); item.SetActive(false); UnityEngine.Object.Destroy(item); }
            foreach (var fsm in spawners) { _pending.Add(fsm); ResetList(fsm); var on = fsm.FsmVariables.FindFsmInt("OnOff"); if (on != null) on.Value = 0; fsm.Fsm.SetState("trigger"); fsm.SendEvent("next"); }
            return true;
        }
        internal void Load(bool fresh)
        {
            _records.Clear(); _error = null; _restored.Clear();
            try
            {
                string text = fresh ? null : State.Read();
                if (text != null) { var book = RecordJson.ReadRecord<Book>(text); if (book == null || book.version != 1 || book.merchants == null) throw new FormatException("Invalid merchant perk record"); _lastCar = book.lastCar; foreach (var record in book.merchants) { if (record == null || string.IsNullOrEmpty(record.key) || record.names != null && (record.prices == null || record.names.Length != record.prices.Length)) throw new FormatException("Invalid merchant perk entry"); _records.Add(record.key,record); } }
                _loaded = true;
                WorldIndex.Rescan();
            }
            catch (Exception error) { _error = error.Message; Log.Error(_owner,"Merchant perks could not be loaded",error); }
        }
        internal void Save(string slot)
        {
            if (_error != null || !_loaded) { SaveBus.ReportFailure(_owner,_error ?? "Merchant perks not loaded"); return; }
            _pending.RemoveWhere(f => f == null || f.ActiveStateName == "done" || f.ActiveStateName == "spawned");
            if (_pending.Count > 0) { SaveBus.ReportFailure(_owner,"Merchant reroll is still being prepared. Close dialogue and wait for the merchant board/stock to finish before saving."); return; }
            foreach (var list in _lists) if (list != null && list.ActiveStateName == "done")
            { var record = Data("list:" + Key(list.gameObject)); record.names = list.FsmVariables.FindFsmArray("ItemNameArray").Values.Select(Convert.ToString).ToArray(); record.prices = list.FsmVariables.FindFsmArray("ItemPriceArray").Values.Select(Convert.ToInt32).ToArray(); }
            State.Write(_owner,slot,RecordJson.WriteRecord(new Book { lastCar = _lastCar, merchants = _records.Values.ToArray() }));
        }
        internal void Clear() { _records.Clear(); _lists.Clear(); _restored.Clear(); _cars.Clear(); _pending.Clear(); _lastCar = null; _fallback.Clear(); _loaded = false; _error = null; }
    }
}
