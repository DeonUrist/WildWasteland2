using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Survival
{
    /// <summary>Native insanity policy, food effects and human butchering.</summary>
    internal sealed class Insanity : IMentalBreak
    {
        private readonly SurvivalModule _owner;
        private readonly Dictionary<FsmStateAction, bool> _enabled = new Dictionary<FsmStateAction, bool>();
        private readonly Dictionary<FloatAdd, FsmFloat> _decay = new Dictionary<FloatAdd, FsmFloat>();
        private readonly Dictionary<FsmFloatAdd, FsmFloat> _food = new Dictionary<FsmFloatAdd, FsmFloat>();
        private readonly Dictionary<StringCompare, FsmString> _tags = new Dictionary<StringCompare, FsmString>();
        private readonly Dictionary<GameObject, bool> _attackers = new Dictionary<GameObject, bool>();
        private readonly Dictionary<GameObject, int> _aggressions = new Dictionary<GameObject, int>();
        private readonly HashSet<string> _corpses = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<GameObject> _cleanCut = new HashSet<GameObject>();
        private readonly List<GameObject> _prefabs = new List<GameObject>(), _sweep = new List<GameObject>();
        private readonly List<FsmFloatAdd> _foodSweep = new List<FsmFloatAdd>();
        private sealed class FoodLabel { internal FsmString Original; internal string ReducedText; }
        private readonly Dictionary<UiTextSetText, FoodLabel> _labels = new Dictionary<UiTextSetText, FoodLabel>();
        private readonly List<UiTextSetText> _labelSweep = new List<UiTextSetText>();
        private PlayMakerFSM _spawn;
        private bool _spawnOn, _pending;
        private float _breakRemaining, _checkRemaining = 300f;
        private bool _rewardBreak;
        private InsanityEventType _eventType;
        private float _heldInsanity;
        private readonly InsanityEpisode _episode;
        private static readonly SaveKey MentalState = SaveBus.Key("WildWasteland.Survival.MentalBreak.v1");
        private bool _restoreMental;
        private string _stateError;
        private FsmFloat _sanity;
        private PlayMakerFSM _sleep;
        private int _catalog = -1;
        internal Insanity(SurvivalModule owner) { _owner = owner; _episode = new InsanityEpisode(this, owner); }
        internal void Init()
        {
            Hooks.EnterState.Prefix(_owner, BeforeState);
            Hooks.CreateObjectEnter.Prefix(_owner, BeforeCreate);
            Hooks.CreateObjectEnter.Postfix(_owner, AfterCreate);
            Hooks.SendEventEnter.Prefix(_owner, BeforeSend);
            Hooks.SetFsmFloatEnter.Postfix(_owner, NativeHit);
            Hooks.UiTextSetTextEnter.Prefix(_owner, FoodHint);
            ModEvents.Subscribe<Hurt>(_owner, OnHurt);
            SaveBus.StateChanged += StateChanged;
            _episode.Init();
            Scheduler.Interval(_owner, "Survival.Insanity references", 1f, Resolve);
            Scheduler.EveryFrame(_owner, "Survival.Mental break", Tick);
        }
        private void StateChanged(string previous, string now)
        { if (now == "setSeed" || now == "Start") { Clear(); _restoreMental = false; } }
        internal void LoadBegin() { Clear(); _restoreMental = true; }
        internal void WorldReady()
        {
            if (!_restoreMental) return;
            _restoreMental = false;
            try
            {
                var record = MentalState.Read(); if (record == null) return;
                var values = record.Split('\n');
                bool legacy = values.Length == 5 && values[0] == "1";
                if (!legacy && (values.Length != 7 || values[0] != "2")) throw new FormatException("Invalid mental break record");
                _breakRemaining = float.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture);
                _pending = values[2] == "1"; _rewardBreak = values[3] == "1";
                _checkRemaining = float.Parse(values[4], System.Globalization.CultureInfo.InvariantCulture);
                if (float.IsNaN(_breakRemaining) || _breakRemaining < 0 || _breakRemaining > 60 || float.IsNaN(_checkRemaining) || _checkRemaining < 0 || _checkRemaining > 300
                    || values[2] != "0" && values[2] != "1" || values[3] != "0" && values[3] != "1" || _pending && (!_rewardBreak || _breakRemaining > 0)) throw new FormatException("Invalid mental break state");
                _breakRemaining = Mathf.Min(30, _breakRemaining);
                int type = legacy ? UnityEngine.Random.Range(0, 2) : int.Parse(values[5], System.Globalization.CultureInfo.InvariantCulture);
                if (type < 0 || type > 1) throw new FormatException("Invalid insanity event type");
                _eventType = (InsanityEventType)type;
                _heldInsanity = legacy ? (_rewardBreak ? 100 : FsmVariables.GlobalVariables.FindFsmFloat("Sanity")?.Value ?? 0) : float.Parse(values[6], System.Globalization.CultureInfo.InvariantCulture);
                if (float.IsNaN(_heldInsanity) || _heldInsanity < 0 || _heldInsanity > 100) throw new FormatException("Invalid frozen insanity");
                _episode.Resolve();
                if (Active) _episode.Start();
                HoldInsanity();
            }
            catch (Exception error) { _stateError = error.Message; Log.Error(_owner, "Mental break state could not be restored", error); }
        }
        internal void Save(string slot)
        {
            if (_stateError != null) { SaveBus.ReportFailure(_owner, _stateError); return; }
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            HoldInsanity();
            MentalState.Write(_owner, slot, "2\n" + _breakRemaining.ToString("R", culture) + "\n" + (_pending ? "1" : "0") + "\n" + (_rewardBreak ? "1" : "0") + "\n" + _checkRemaining.ToString("R", culture) + "\n" + ((int)_eventType).ToString(culture) + "\n" + _heldInsanity.ToString("R", culture));
        }
        internal void Resolve()
        {
            if (!GameRefs.WorldActive) return;
            _sanity = FsmVariables.GlobalVariables.FindFsmFloat("Sanity");
            _sleep = FsmUtil.Find(GameRefs.Player, "Sleep");
            _episode.Resolve();
            var spawn = FsmUtil.Find(GameRefs.GameManager, "SanityMutantSpawn");
            if (spawn != null)
            {
                if (_spawn != spawn) { _spawn = spawn; _spawnOn = spawn.enabled; }
                spawn.enabled = false;
            }
            var sanity = FsmUtil.Find(GameRefs.GameManager, "Sanity");
            if (sanity != null) foreach (var state in sanity.FsmStates) ConfigureSanity(state);
            if (!PrefabCatalog.Ready) { PrefabCatalog.Warm(); return; }
            if (_catalog != PrefabCatalog.Generation)
            {
                _catalog = PrefabCatalog.Generation; _corpses.Clear(); PrefabCatalog.AllRoots(_prefabs);
                foreach (var prefab in _prefabs)
                {
                    if (!Human(prefab)) continue;
                    var health = FsmUtil.Find(prefab, "Health"); if (health == null) continue;
                    foreach (var state in health.FsmStates) foreach (var action in state.Actions)
                    {
                        var create = action as CreateObject; var corpse = create != null ? create.gameObject.Value : null;
                        if (corpse != null && corpse.name.IndexOf("_Dead", StringComparison.OrdinalIgnoreCase) >= 0) _corpses.Add(corpse.name);
                    }
                }
                _prefabs.Clear(); HumanFood.Prepare();
                Log.Always(_owner, "Human meat/skull templates ready; butcherable human corpse types: " + _corpses.Count + ". Passive insanity decay and insanity monsters disabled.");
            }
            _sweep.Clear(); foreach (var pair in _attackers) if (pair.Key == null) _sweep.Add(pair.Key);
            foreach (var key in _sweep) { _attackers.Remove(key); _aggressions.Remove(key); }
            _sweep.Clear(); foreach (var pair in _aggressions) if (pair.Key == null) _sweep.Add(pair.Key);
            foreach (var key in _sweep) _aggressions.Remove(key);
            _foodSweep.Clear(); foreach (var pair in _food) if (pair.Key.Owner == null) _foodSweep.Add(pair.Key);
            foreach (var key in _foodSweep) _food.Remove(key);
            _labelSweep.Clear(); foreach (var pair in _labels) if (pair.Key.Owner == null) _labelSweep.Add(pair.Key);
            foreach (var key in _labelSweep) _labels.Remove(key);
        }
        internal static float FoodMultiplier { get { return ModApi.Get<IPlayerPerkEffects>()?.FoodInsanityMultiplier ?? 1f; } }
        internal static bool RawMeal(GameObject go) { return go != null && FsmUtil.Find(go, "Cook") != null && FsmUtil.Find(go, "Food") != null; }
        private static bool Human(GameObject go) { return go != null && (go.CompareTag("Coyotes") || go.CompareTag("Scrapyard")); }
        private static bool Player(GameObject go)
        {
            if (go == null) return false;
            if (go == GameRefs.Player) return true;
            var holder = GameRefs.PlayerCameraHolder; return holder != null && go.transform.IsChildOf(holder.transform);
        }
        private GameObject Corpse(GameObject hit)
        {
            for (var node = hit != null ? hit.transform : null; node != null; node = node.parent)
                if (_corpses.Contains(Names.Base(node.name))) return node.gameObject;
            return null;
        }
        private void ConfigureSanity(FsmState state)
        {
            foreach (var action in state.Actions)
            {
                var decay = action as FloatAdd; var enable = action as EnableFSM;
                if (decay != null && decay.floatVariable.Name == "Sanity" && decay.add.Value < 0 && decay.perSecond && !_decay.ContainsKey(decay))
                { _decay.Add(decay, decay.add); decay.add = 0f; }
                if (enable != null && enable.fsmName.Value == "SanityMutantSpawn")
                { if (!_enabled.ContainsKey(action)) _enabled.Add(action, action.Enabled); action.Enabled = false; }
            }
        }
        private bool BeforeState(Fsm fsm, FsmState state)
        {
            if (fsm.Name == "Sanity" && fsm.GameObject != null && fsm.GameObject.name == "__GameManager__") ConfigureSanity(state);
            else if (fsm.Name == "Food")
            {
                foreach (var action in state.Actions)
                {
                    var add = action as FsmFloatAdd;
                    if (add == null || add.variableName.Value != "SanityAdd") continue;
                    FsmFloat original;
                    if (!_food.TryGetValue(add, out original)) { original = add.add; _food.Add(add, original); add.add = new FsmFloat(); }
                    add.add.Value = original.Value > 0 ? original.Value * FoodMultiplier * (ModApi.Get<ICookingSkills>()?.InsanityMultiplier(fsm.GameObject) ?? 1) : original.Value;
                }
            }
            else if (fsm.Name == "Butcher" && state.Name == "checkTag")
            {
                var item = fsm.Variables.FindFsmGameObject("Item"); var human = item != null ? Corpse(item.Value) : null;
                if (!(ModApi.Get<IPlayerPerkEffects>()?.CanButcherHumans ?? false)) human = null;
                if (human != null) item.Value = human;
                foreach (var action in state.Actions)
                {
                    var compare = action as StringCompare; if (compare == null) continue;
                    FsmString original;
                    if (!_tags.TryGetValue(compare, out original)) { original = compare.compareTo; _tags.Add(compare, original); compare.compareTo = new FsmString(); }
                    compare.compareTo.Value = human != null ? human.tag : original.Value;
                }
            }
            return true;
        }
        private bool BeforeCreate(CreateObject action)
        {
            if (action.Fsm == null || action.Fsm.Name != "SanityMutantSpawn") return true;
            action.Finish(); return false;
        }
        private bool FoodHint(UiTextSetText action)
        {
            if (action.Fsm == null || action.Fsm.Name != "ItemName" || action.text == null || action.text.Value.IndexOf("Insanity +", StringComparison.Ordinal) < 0) return true;
            if (FsmUtil.Find(action.Owner, "Food") == null) return true;
            FoodLabel label;
            if (!_labels.TryGetValue(action, out label))
            {
                label = new FoodLabel { Original = action.text };
                var lines = label.Original.Value.Split('\n');
                for (int i = 0; i < lines.Length; i++) if (lines[i].StartsWith("Insanity +", StringComparison.Ordinal))
                {
                    float value;
                    if (float.TryParse(lines[i].Substring(10), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                        lines[i] = "Insanity +" + (value * .5f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " (Iron gut)";
                }
                label.ReducedText = string.Join("\n", lines); _labels.Add(action, label); action.text = new FsmString();
            }
            action.text.Value = FoodMultiplier < 1f ? label.ReducedText : label.Original.Value;
            return true;
        }
        private bool BeforeSend(SendEvent action)
        {
            if (action.Fsm == null || action.Fsm.Name != "Butcher" || action.sendEvent == null || action.sendEvent.Name != "Butcher") return true;
            var item = action.Fsm.Variables.FindFsmGameObject("Item"); var corpse = item != null ? Corpse(item.Value) : null;
            if (corpse == null) return true;
            if (!(ModApi.Get<IPlayerPerkEffects>()?.CanButcherHumans ?? false)) { action.Finish(); return false; }
            if (HumanFood.Butcher(corpse)) { Add(10, "butchered human"); Log.Always(_owner, "Butchered human: 2 raw human meat + 1 attachable human skull; insanity +10."); }
            action.Finish(); return false;
        }
        private void NativeHit(SetFsmFloat action)
        {
            if (action.fsmName.Value != "Bodypart" || action.variableName.Value != "Damage" || action.setValue.Value >= 0) return;
            var car = PlayerState.CarRoot;
            bool driving = action.Fsm.Name == "CarAttack" && car != null && (action.Owner == car.gameObject || action.Owner.transform.IsChildOf(car));
            Attack(action.Fsm.GetOwnerDefaultTarget(action.gameObject), driving || Player(action.Owner) || ModApi.Get<ICombatSources>()?.OwnerOf(action.Owner) == Entities.Player);
        }
        private void OnHurt(ref Hurt evt)
        {
            if (evt.Amount > 0) Attack(Entities.Find(evt.Victim), evt.Attacker == Entities.Player || Player(Entities.Find(evt.Attacker)));
        }
        private void Attack(GameObject victim, bool player)
        {
            var root = victim != null ? ActorQuery.RootOf(victim.transform) : null;
            if (root == null || root.gameObject == GameRefs.Player || FsmUtil.Find(root.gameObject, "Health") == null) return;
            _attackers[root.gameObject] = player;
            if (player && root.CompareTag("Coyotes"))
            {
                int frame;
                if (!_aggressions.TryGetValue(root.gameObject, out frame) || frame != Time.frameCount)
                { _aggressions[root.gameObject] = Time.frameCount; Add(5, "aggression against Coyotes"); }
            }
        }
        private void AfterCreate(CreateObject action)
        {
            if (action.Fsm != null && action.Fsm.Name == "Carcasse" && action.Fsm.ActiveStateName == "butcher" && SkillPerks.Has("CleanCut"))
            {
                var template = action.gameObject?.Value;
                if (template != null && FsmUtil.Find(template,"Food") != null && _cleanCut.Add(action.Owner) && UnityEngine.Random.value < .25f)
                {
                    var extra = UnityEngine.Object.Instantiate(template,action.Owner.transform.position + Vector3.up * .3f,Quaternion.identity);
                    if (!ItemSpawner.RegisterSaved(extra,template.name)) { UnityEngine.Object.Destroy(extra); Log.Warn(_owner,"Clean Cut: extra meat registration failed."); }
                    else if (Log.L1) Log.Main(_owner,"Clean Cut: +1 " + template.name + ".");
                }
            }
            if (action.Fsm == null || action.Fsm.Name != "Health" || action.Fsm.ActiveStateName != "spawn" || action.Owner == GameRefs.Player) return;
            var dead = action.storeObject != null ? action.storeObject.Value : null;
            if (dead == null || dead.name.IndexOf("_Dead", StringComparison.OrdinalIgnoreCase) < 0) return;
            if (Human(action.Owner)) _corpses.Add(Names.Base(dead.name));
            WorldIndex.QueueRoot(dead.transform);
            bool player;
            if (_attackers.TryGetValue(action.Owner, out player) && player)
            {
                if (Human(action.Owner)) Add((action.Owner.CompareTag("Coyotes") ? 20 : 2) * (ModApi.Get<IPlayerPerkEffects>()?.HumanKillInsanityMultiplier ?? 1f), "killed " + action.Owner.tag);
                var killed = new NpcKilled { Npc = Entities.Of(action.Owner), Killer = Entities.Player, Tag = action.Owner.tag, CorpseName = dead.name, WorldPos = Origin.World(dead.transform.position) };
                ModEvents.Publish(ref killed);
            }
            _attackers.Remove(action.Owner); _aggressions.Remove(action.Owner);
        }
        private void Add(float amount, string reason)
        {
            if (LocksInsanity) return;
            if (_sanity == null) _sanity = FsmVariables.GlobalVariables.FindFsmFloat("Sanity");
            if (_sanity == null) return;
            _sanity.Value = Mathf.Clamp(_sanity.Value + amount, 0, 100);
            if (Log.L1) Log.Main(_owner, "Insanity +" + amount + ": " + reason + "; now " + _sanity.Value + ".");
        }
        private void Tick()
        {
            HoldInsanity();
            if (!GameRefs.InGame || PlayerState.Dead || _pending || _stateError != null || _sleep != null && _sleep.ActiveStateName != "Awake") return;
            if (_breakRemaining > 0)
            {
                _episode.Tick();
                _breakRemaining = Mathf.Max(0, _breakRemaining - Time.deltaTime);
                if (_breakRemaining > 0) return;
                if (_rewardBreak) { _pending = true; ModApi.Get<IPlayerPerks>()?.PrepareInsanityChoices(); }
                _episode.End();
                if (!_rewardBreak) { _sanity.Value = _heldInsanity; _episode.ReleaseFreeze(false); }
                Log.Always(_owner, _rewardBreak ? "Mental break ended: choose an insanity perk." : "Random mental break ended: no perk awarded.");
                return;
            }
            if (_sanity != null && _sanity.Value >= 100) { Begin(true); return; }
            _checkRemaining -= Time.deltaTime;
            if (_checkRemaining > 0) return;
            _checkRemaining = 300f;
            float chance = ModApi.Get<IPlayerPerkEffects>()?.MentalBreakChance ?? 0;
            if (chance > 0 && UnityEngine.Random.value < chance) Begin(false);
        }
        public bool Pending { get { return _pending; } }
        public bool Active { get { return _breakRemaining > 0; } }
        public float Remaining { get { return _breakRemaining; } }
        public InsanityEventType EventType { get { return _eventType; } }
        public string EventName { get { return _eventType == InsanityEventType.Vertigo ? "VERTIGO" : "NO MORE DEATH"; } }
        public bool LocksInsanity { get { return Active || _pending; } }
        public bool WeaponsBlocked { get { return Active && _eventType == InsanityEventType.NoMoreDeath; } }
        internal void HoldInsanity()
        {
            if (!LocksInsanity) return;
            if (_sanity == null) _sanity = FsmVariables.GlobalVariables.FindFsmFloat("Sanity");
            if (_sanity != null) _sanity.Value = _heldInsanity;
        }
        public bool Trigger()
        { if (!GameRefs.InPlay || Active || _pending) return false; Begin(true); return true; }
        private void Begin(bool reward)
        {
            if (_sanity == null) _sanity = FsmVariables.GlobalVariables.FindFsmFloat("Sanity");
            _heldInsanity = reward ? 100 : Mathf.Clamp(_sanity != null ? _sanity.Value : 0, 0, 100);
            _eventType = (InsanityEventType)UnityEngine.Random.Range(0, 2);
            _rewardBreak = reward; _breakRemaining = 30; _checkRemaining = 300;
            HoldInsanity(); _episode.Start();
            Log.Always(_owner, (reward ? "Mental break started: " : "Random mental break started: ") + EventName + "; 30 gameplay seconds; insanity frozen at " + _heldInsanity + (reward ? "; perk choice after completion." : "; no perk reward."));
        }
        public bool Choose(int index)
        {
            var perks = ModApi.Get<IPlayerPerks>();
            if (!_pending || index < 0 || index > 3 || perks == null) return false;
            if (_sanity == null) _sanity = FsmVariables.GlobalVariables.FindFsmFloat("Sanity");
            if (_sanity == null) return false;
            var globals = FsmVariables.GlobalVariables;
            var hunger = globals.FindFsmFloat("Hunger"); var thirst = globals.FindFsmFloat("Thirst"); var fatigue = globals.FindFsmFloat("Fatigue");
            if (index == 3 && (hunger == null || thirst == null || fatigue == null)) return false;
            var choice = perks.InsanityChoice(index);
            if (!perks.ChooseInsanityPerk(choice.Id)) return false;
            if (index == 3)
            {
                if (SkillPerks.Has("StrongMind")) { hunger.Value = Mathf.Min(100,hunger.Value + 50); thirst.Value = Mathf.Min(100,thirst.Value + 50); fatigue.Value = Mathf.Min(100,fatigue.Value + 50); }
                else { hunger.Value = thirst.Value = 100; fatigue.Value = 80; }
                Log.Always(_owner,"Persevere: hunger=" + hunger.Value + "; thirst=" + thirst.Value + "; fatigue=" + fatigue.Value + ".");
            }
            _pending = false; _episode.ReleaseFreeze(true);
            _sanity.Value = index == 3 ? 35 : 0;
            Log.Always(_owner, "Mental break resolved: " + choice.Name + "; insanity reset to " + _sanity.Value + "."); return true;
        }
        internal void Clear()
        {
            _cleanCut.Clear();
            _episode.Clear();
            foreach (var pair in _enabled) pair.Key.Enabled = pair.Value;
            foreach (var pair in _decay) pair.Key.add = pair.Value;
            foreach (var pair in _food) pair.Key.add = pair.Value;
            foreach (var pair in _tags) pair.Key.compareTo = pair.Value;
            foreach (var pair in _labels) pair.Key.text = pair.Value.Original;
            if (_spawn != null) _spawn.enabled = _spawnOn;
            _attackers.Clear(); _aggressions.Clear(); _pending = false; _sanity = null; _catalog = -1; _spawn = null; _enabled.Clear(); _decay.Clear(); _food.Clear(); _tags.Clear(); _labels.Clear();
            _breakRemaining = 0; _checkRemaining = 300; _rewardBreak = false;
            _heldInsanity = 0; _eventType = InsanityEventType.Vertigo;
            _stateError = null;
            _sleep = null;
        }
        internal void Shutdown() { SaveBus.StateChanged -= StateChanged; Clear(); }
    }
}
