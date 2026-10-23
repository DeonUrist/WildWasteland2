using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.Events;
using WildWasteland.Core.UI;
using BepInEx.Configuration;

namespace WildWasteland.Modules.Perks
{
    /// <summary>Player skills and permanent perks, saved inside the selected world transaction.</summary>
    public sealed partial class PerksModule : ModuleBase, IPlayerPerks, IPlayerPerkEffects, IPlayerSkills
    {
        private static readonly SaveKey State = SaveBus.Key("WildWasteland.Perks.Player.v1");
        private static readonly string[] Names = { "Melee", "Pistols", "Rifles", "Shotguns", "Unarmed", "Athletics", "Barter", "Crafting", "Medicine", "Survival", "Driving", "SMGs" };
        private static readonly int[] SkillOrder = { 0, 1, 11, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        private static readonly string[] Hints = {
            "Melee weapon damage +0-50%. Excludes fists.", "Pistol tracer spread -0-50%.", "Rifle, sniper, crossbow and launcher tracer spread -0-50%.", "Shotgun tracer spread -0-50%.", "Punch and kick damage +0-50%.",
            "Running speed +0-50%.", "Buy prices -0-50%; sell prices and quest payouts +0-50%.", "No effect yet.", "Healing item recovery +0-50%.", "Cooked nutrition +0-50% for food or pots you last handled.", "Vehicle impact damage scales from 1x to 5x.", "SMG tracer spread -0-50%." };
        private readonly int[] _skills = new int[12];
        private bool _loaded, _fresh, _choicesReady;
        private int _unlocked;
        private WeaponKind _roulette = WeaponKind.Other;
        private string _rouletteHint = PerkRoster.Hints[4];
        private float _lastKill = float.NegativeInfinity;
        private readonly int[] _choices = new int[3], _candidates = new int[10];
        private readonly Texture2D[] _icons = new Texture2D[10];
        private readonly Texture2D[] _skillIcons = new Texture2D[36];
        private readonly int[] _skillPerkOrder = new int[36];
        private Texture2D _persevereIcon;
        private GameObject _healthOwner;
        private HutongGames.PlayMaker.FsmFloat _health;
        private string _slot, _readError;
        private int _generation = -1;
        private readonly MentalBreakView _mental = new MentalBreakView();
        private readonly LedgerDropdown _mentalDropdown = new LedgerDropdown();
        private static readonly string[] MentalNames = { "Random", "Vertigo", "No More Death", "Berserk", "Delirium", "Doppelganger" };
        private readonly List<KeyValuePair<string, string>> _mentalOptions = new List<KeyValuePair<string, string>> {
            new KeyValuePair<string, string>("", "Random"), new KeyValuePair<string, string>("0", "Vertigo"),
            new KeyValuePair<string, string>("1", "No More Death"), new KeyValuePair<string, string>("2", "Berserk"), new KeyValuePair<string, string>("3", "Delirium"), new KeyValuePair<string, string>("4", "Doppelganger") };
        private int _mentalChoice = -1;
        private readonly SkillPerkNotice _notice = new SkillPerkNotice();
        private readonly MerchantPerks _merchants = new MerchantPerks();
        private ConfigEntry<float> _chancePerPerk;
        public override string Id { get { return "Perks"; } }
        public override string DisplayName { get { return "PERKS"; } }
        public override void BindConfig(ModuleConfig cfg)
        { _chancePerPerk = cfg.Tool("Mental break", "Chance per insanity perk (%)", 5f, "Every five minutes of normal gameplay, chance (%) = owned insanity perks multiplied by this value, capped at 100. Random breaks grant no perk. A break lasts 30 gameplay seconds and prevents sleep.", new AcceptableValueRange<float>(0, 100)); BindSkills(cfg); }
        public override void Settings(SettingsShell shell)
        {
            shell.Custom(this, SettingsShell.Page.Player, "", DrawFastXpGain, 10);
            shell.Resettable(this, SettingsShell.Page.Player, _fastXpGain);
            shell.Custom(this, SettingsShell.Page.Debug, "Survival", DrawDebug, 420);
            shell.Custom(this, SettingsShell.Page.Developer, "Perks", DrawDeveloper, 420);
        }
        private void DrawFastXpGain(ToolLayout layout) { SettingsShell.Switch(layout, "Fast XP gain", _fastXpGain); }
        private void DrawDebug(ToolLayout layout)
        {
            _mentalDropdown.Before();
            var mental = ModApi.Get<IMentalBreak>();
            if (layout.Button("Mental break", mental != null && GameRefs.InPlay && !mental.Active && !mental.Pending))
            { if (_mentalChoice < 0) mental.Trigger(); else mental.Trigger((InsanityEventType)_mentalChoice); }
            layout.Hint("Starts the selected 30-second insanity event with a perk choice afterward. Sleep is blocked during the event.");
            var row = layout.Next();
            Ledger.Label(new Rect(row.x, row.y, row.width * ToolLayout.LabelWidth, row.height), "Mental break event", Ledger.Small, Ledger.Muted);
            var anchor = new Rect(row.x + row.width * ToolLayout.LabelWidth, row.y, row.width * (1 - ToolLayout.LabelWidth), row.height);
            if (Ledger.Dropdown(anchor, MentalNames[_mentalChoice + 1]))
                _mentalDropdown.Open(anchor, _mentalOptions, _mentalChoice < 0 ? "" : _mentalChoice.ToString(CultureInfo.InvariantCulture), value => _mentalChoice = value.Length == 0 ? -1 : int.Parse(value, CultureInfo.InvariantCulture), anchor.yMax + 220);
            if (_mentalDropdown.IsOpen) layout.Space(200);
            _mentalDropdown.Draw(); _mentalDropdown.Flush();
        }
        private void DrawDeveloper(ToolLayout layout) { DrawSkillBoosts(layout); layout.Entry(_chancePerPerk); layout.Hint("100 XP per skill point. Athletics and Driving use XP per kilometre; other entries use XP per action."); for (int i = 0; i < SkillOrder.Length; i++) if (_xpRates[SkillOrder[i]] != null) layout.Entry(_xpRates[SkillOrder[i]]); }
        public override void Init()
        {
            InitSkills();
            int ordered = 0;
            foreach (int skill in SkillOrder) for (int i = 0; i < SkillPerkRoster.All.Length; i++)
                if ((int)SkillPerkRoster.All[i].Skill == skill) _skillPerkOrder[ordered++] = i;
            for (int i = 0; i < _icons.Length; i++) _icons[i] = Textures.LoadResource(this, "Perks.Icons." + PerkRoster.Icons[i] + ".png", TextureSettings.Ui);
            for (int i = 0; i < _skillIcons.Length; i++) _skillIcons[i] = Textures.LoadResource(this, "Perks.Icons." + SkillPerkRoster.All[i].Name.Replace("/", "-") + ".png", TextureSettings.Ui);
            _persevereIcon = Textures.LoadResource(this, "Perks.Icons.persevere.png", TextureSettings.Ui);
            ModEvents.Subscribe<PlayerWokeUp>(this, WokeUp);
            ModEvents.Subscribe<NpcKilled>(this, Killed);
            SaveBus.StateChanged += StateChanged;
            _mental.Init(this); _notice.Init(this); _merchants.Init(this);
            Scheduler.Interval(this, "Perks.Player state", .25f, ResolveWorld);
        }
        private void ResolveWorld()
        { if (GameRefs.InPlay && _readError == null && (!_loaded || _generation != SaveBus.Generation || _slot != SaveBus.Slot)) OnWorldReady(); }
        public override void ProvideApis() { ModApi.Provide<IPlayerPerks>(this, this); ModApi.Provide<IPlayerPerkEffects>(this, this); ModApi.Provide<IPlayerSkills>(this, this); ModApi.Provide<ISkillMerchantChoices>(this,_merchants); }
        public int SkillCount { get { return Names.Length; } }
        public PlayerSkill Skill(int index)
        { int skill = SkillOrder[index]; return new PlayerSkill { Id = Names[skill], Name = Names[skill], Hint = _skillHints[skill] ?? Hints[skill], Value = _skills[skill], Xp = _xp[skill] }; }
        public int PerkCount { get { return PerkRoster.Ids.Length + SkillPerkRoster.All.Length; } }
        public PlayerPerk Perk(int index)
        {
            if (index < 0 || index >= PerkCount) throw new ArgumentOutOfRangeException("index");
            if (index < SkillPerkRoster.All.Length)
            {
                int i = _skillPerkOrder[index]; var entry = SkillPerkRoster.All[i];
                bool unlocked = _loaded && _skills[(int)entry.Skill] >= entry.Level;
                string requirement = unlocked ? Names[(int)entry.Skill] + " " + entry.Level : "Requires " + Names[(int)entry.Skill] + " " + entry.Level + " (current: " + _skills[(int)entry.Skill] + ").";
                return new PlayerPerk { Id = entry.Id, Name = entry.Name, Hint = requirement + "\n\n" + entry.Hint, Unlocked = unlocked, Icon = _skillIcons[i] };
            }
            index -= SkillPerkRoster.All.Length;
            string hint = index == 4 ? _rouletteHint : PerkRoster.Hints[index];
            if (!Own(index)) hint = "Requires reaching 100 insanity, completing the mental break and choosing this perk when offered.\n\n" + hint;
            return new PlayerPerk { Id = PerkRoster.Ids[index], Name = PerkRoster.Names[index], Hint = hint, Unlocked = Own(index), Insanity = true, Icon = _icons[index] };
        }
        private bool Own(int index) { return _loaded && (_unlocked & (1 << index)) != 0; }
        public bool Has(string id)
        {
            for (int i = 0; i < PerkRoster.Ids.Length; i++) if (id == PerkRoster.Ids[i]) return Own(i);
            for (int i = 0; i < SkillPerkRoster.All.Length; i++) { var e = SkillPerkRoster.All[i]; if (e.Id == id) return _loaded && _skills[(int)e.Skill] >= e.Level; }
            return false;
        }
        public void PrepareInsanityChoices()
        {
            if (_choicesReady || !_loaded) return;
            int count = 0;
            for (int i = 0; i < PerkRoster.Ids.Length; i++) if (!Own(i)) _candidates[count++] = i;
            if (count == 0) for (int i = 0; i < PerkRoster.Ids.Length; i++) _candidates[count++] = i;
            int available = count;
            for (int i = 0; i < 3; i++)
            {
                if (count == 0) count = available;
                int pick = UnityEngine.Random.Range(0, count); _choices[i] = _candidates[pick];
                _candidates[pick] = _candidates[--count]; _candidates[count] = _choices[i];
            }
            _choicesReady = true;
        }
        public PlayerPerk InsanityChoice(int index)
        {
            PrepareInsanityChoices();
            if (index == 3) return new PlayerPerk { Id = "Persevere", Name = "Persevere", Hint = Has("StrongMind") ? "No perk. Hunger, thirst and fatigue +50 (capped). Insanity 35." : "No perk. Hunger 100, thirst 100, fatigue 80. Insanity 35.", Icon = _persevereIcon };
            var choice = Perk(SkillPerkRoster.All.Length + _choices[index]);
            choice.Hint = PerkRoster.Hints[_choices[index]]; return choice;
        }
        public bool ChooseInsanityPerk(string id)
        {
            if (!_loaded || _readError != null || !_choicesReady || !(ModApi.Get<IMentalBreak>()?.Pending ?? false)) return false;
            if (id == "Persevere") { _choicesReady = false; Log.Always(this, "Persevere selected: no insanity perk granted."); return true; }
            for (int i = 0; i < _choices.Length; i++) if (PerkRoster.Ids[_choices[i]] == id)
            { bool fresh = !Own(_choices[i]); _unlocked |= 1 << _choices[i]; _choicesReady = false; if (fresh) { var earned = new PlayerSkillPerkUnlocked { Id = id }; ModEvents.Publish(ref earned); } Log.Always(this, "Insanity perk selected: " + id + "."); return true; }
            return false;
        }
        public bool CanButcherHumans { get { return Own(0); } }
        public float FoodInsanityMultiplier { get { return Own(1) ? .5f : 1f; } }
        public float MutantDetectionMultiplier { get { return Own(3) ? .5f : 1f; } }
        public bool IgnoreOwnExplosions { get { return Own(5); } }
        public bool Paranoia { get { return Own(8); } }
        public float HumanKillInsanityMultiplier { get { return Own(9) ? .5f : 1f; } }
        public float MentalBreakChance
        {
            get { int count = 0; for (int i = 0; i < PerkRoster.Ids.Length; i++) if (Own(i)) count++; return Mathf.Clamp01(count * _chancePerPerk.Value * .01f); }
        }
        public float DamageMultiplier(WeaponKind kind, float healthFraction)
        { return (Own(4) && kind == _roulette ? 1.5f : 1f) * (Own(6) && healthFraction < .5f ? 2f : 1f); }
        private void Heal(float amount)
        {
            var player = GameRefs.Player;
            if (player != _healthOwner || _health == null)
            { _healthOwner = player; var fsm = FsmUtil.Find(player, "Health"); _health = fsm != null ? fsm.FsmVariables.FindFsmFloat("Health") : null; }
            if (_health != null && _health.Value >= .4f) _health.Value = Mathf.Min(100, _health.Value + amount);
        }
        private void WokeUp(ref PlayerWokeUp action)
        {
            _lastKill = float.NegativeInfinity;
            if (Own(7) && action.CorpseNearby) { Heal(20); Log.Always(this, "Corpse obsession: waking bonus +20 health (30 total)."); }
            if (!Own(4)) return;
            int kinds = 0, count = 0; var catalog = ModApi.Get<IWeaponCatalog>();
            if (catalog != null) for (int i = 0; i < catalog.Count; i++) { int kind = (int)catalog.KindOf(i); if (kind < (int)WeaponKind.Other) kinds |= 1 << kind; }
            if (kinds == 0) kinds = (1 << (int)WeaponKind.Pistol) | (1 << (int)WeaponKind.Melee);
            for (int i = 0; i < (int)WeaponKind.Other; i++) if ((kinds & (1 << i)) != 0) count++;
            int choice = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < (int)WeaponKind.Other; i++) if ((kinds & (1 << i)) != 0 && choice-- == 0) { _roulette = (WeaponKind)i; break; }
            UpdateRouletteHint();
            Log.Always(this, "Wasteland roulette: " + _roulette + " damage x1.5 until next sleep.");
            ModApi.Get<INotifications>()?.Push("WASTELAND ROULETTE: " + _roulette.ToString().ToUpperInvariant() + " DAMAGE x1.5", NotificationKind.Info, 6);
        }
        private void UpdateRouletteHint()
        { _rouletteHint = PerkRoster.Hints[4] + (_roulette != WeaponKind.Other ? "\n\nCurrent type: " + _roulette + " (1.5x damage)." : "\n\nSleep to choose your first weapon type."); }
        private void Killed(ref NpcKilled action)
        {
            if (action.Killer != Entities.Player || action.Tag == "Herbivore") return;
            float now = Time.time;
            if (Own(2) && now - _lastKill <= 5f) { Heal(5); if (Log.L1) Log.Main(this, "Bloodbath: consecutive kill +5 health."); }
            _lastKill = now;
        }
        private void Reset()
        { Array.Clear(_skills, 0, _skills.Length); ResetSkills(); _notice.Clear(); _merchants.Clear(); _unlocked = 0; _roulette = WeaponKind.Other; UpdateRouletteHint(); _lastKill = float.NegativeInfinity; _choicesReady = false; _loaded = false; _slot = null; _readError = null; _healthOwner = null; _health = null; }
        private void StateChanged(string previous, string now)
        { if (now == "setSeed" || now == "Start") { _nativeSkills.Clear(); Reset(); _fresh = true; } }
        public override void OnLoadBegin(string slot) { _nativeSkills.Clear(); _mental.Clear(); _mentalDropdown.Close(); Reset(); _fresh = false; }
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        { if (mode != LoadSceneMode.Additive) { _nativeSkills.Clear(); _mental.Clear(); _mentalDropdown.Close(); Reset(); _fresh = false; } }
        public override void OnWorldReady()
        {
            if (_loaded && _generation == SaveBus.Generation && _slot == SaveBus.Slot) return;
            bool fresh = _fresh; Reset(); _slot = SaveBus.Slot; _generation = SaveBus.Generation; _fresh = false;
            try
            {
                string record = fresh ? null : State.Read(_slot);
                if (record != null)
                {
                    string[] lines = record.Split('\n');
                    bool legacy = lines.Length == 12 && lines[0] == "1";
                    bool driving = lines.Length == 25 && lines[0] == "4";
                    bool smgs = lines.Length == 27 && lines[0] == "5";
                    bool skillsXp = smgs || driving || lines.Length == 23 && lines[0] == "3";
                    int storedCount = smgs ? 12 : driving ? 11 : 10;
                    if (!legacy && !skillsXp && (lines.Length != 13 || lines[0] != "2")) throw new FormatException("Invalid PERKS player record");
                    for (int i = 0; i < storedCount; i++)
                    {
                        int value;
                        if (!int.TryParse(lines[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0 || value > 100) throw new FormatException("Invalid skill value");
                        _skills[i] = value;
                    }
                    if (!int.TryParse(lines[storedCount + 1], out _unlocked) || _unlocked < 0 || _unlocked >= (legacy ? 2 : 1 << PerkRoster.Ids.Length)) throw new FormatException("Invalid perk flags");
                    int roulette;
                    if (!legacy && (!int.TryParse(lines[storedCount + 2], out roulette) || roulette < 0 || roulette > (int)WeaponKind.Other)) throw new FormatException("Invalid roulette type");
                    if (!legacy) _roulette = (WeaponKind)int.Parse(lines[storedCount + 2], CultureInfo.InvariantCulture);
                    if (skillsXp) for (int i = 0; i < storedCount; i++)
                    {
                        float xp;
                        if (!float.TryParse(lines[storedCount + 3 + i], NumberStyles.Float, CultureInfo.InvariantCulture, out xp) || float.IsNaN(xp) || xp < 0 || xp >= 100 || _skills[i] == 100 && xp != 0) throw new FormatException("Invalid skill XP");
                        _xp[i] = xp;
                    }
                }
                _loaded = true;
                _merchants.Load(fresh);
                for (int i = 0; i < _skills.Length; i++) RefreshSkillHint(i);
                _nativeSkills.RefreshPrices();
                UpdateRouletteHint();
                Log.Always(this, "Player skills loaded: " + SkillCount + "; perk flags=" + _unlocked + "; roulette=" + _roulette + ".");
            }
            catch (Exception error) { _readError = error.Message; Log.Error(this, "Player perks could not be loaded", error); }
        }
        public override void OnSaveBegin(string slot)
        {
            _merchants.Save(slot);
            if (_readError != null || !_loaded) { SaveBus.ReportFailure(this, _readError ?? "Player perks have not loaded"); return; }
            var record = new StringBuilder("5");
            for (int i = 0; i < _skills.Length; i++) record.Append('\n').Append(_skills[i].ToString(CultureInfo.InvariantCulture));
            record.Append('\n').Append(_unlocked.ToString(CultureInfo.InvariantCulture)).Append('\n').Append(((int)_roulette).ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < _xp.Length; i++) record.Append('\n').Append(_xp[i].ToString("R", CultureInfo.InvariantCulture));
            State.Write(this, slot, record.ToString());
        }
        public override void Shutdown() { SaveBus.StateChanged -= StateChanged; _nativeSkills.Clear(); _mental.Clear(); _mentalDropdown.Close(); _notice.Clear(); _merchants.Clear(); Array.Clear(_icons, 0, _icons.Length); Array.Clear(_skillIcons, 0, _skillIcons.Length); _persevereIcon = null; }
    }
}
