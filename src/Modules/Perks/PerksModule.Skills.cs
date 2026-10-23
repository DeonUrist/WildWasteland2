using System;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Perks
{
    public sealed partial class PerksModule
    {
        private readonly float[] _xp = new float[12];
        private readonly string[] _skillHints = new string[12];
        private readonly ConfigEntry<float>[] _xpRates = new ConfigEntry<float>[12];
        private ConfigEntry<bool> _fastXpGain;
        private readonly NativeSkills _nativeSkills = new NativeSkills();
        private void BindSkills(ModuleConfig cfg)
        {
            _fastXpGain = cfg.Player("Skills", "Fast XP gain", true, "Multiply configured skill XP rates by 5. Off uses the configured base rates.");
            for (int i = 0; i < _xpRates.Length; i++) if (i != (int)PlayerSkillId.Crafting)
                _xpRates[i] = cfg.Tool("Skills", Names[i] + (i == (int)PlayerSkillId.Athletics ? " XP per km on foot" : i == (int)PlayerSkillId.Driving ? " XP per km driven" : " XP per action"), i == (int)PlayerSkillId.Athletics ? 200f : i == (int)PlayerSkillId.Driving ? 100f : 25f,
                    "100 XP earns each +1 skill point, up to 100. No exponential progression. Survival: one action per raw item cooked or per 10 raw nutrition cooked in stew. Athletics and Driving: one unit per kilometre.", new AcceptableValueRange<float>(0, 10000));
        }
        private void InitSkills()
        { ModEvents.Subscribe<PlayerSkillAction>(this, SkillAction); ModEvents.Subscribe<PlayerWeaponKill>(this, WeaponKill); _nativeSkills.Init(this); }
        private void ResetSkills()
        { Array.Clear(_xp, 0, _xp.Length); Array.Clear(_skillHints, 0, _skillHints.Length); _nativeSkills.ResetTravel(); }
        private void WeaponKill(ref PlayerWeaponKill kill)
        { int skill = WeaponSkill(kill.Kind, kill.Unarmed); if (skill < 0) return; var action = new PlayerSkillAction { Skill = (PlayerSkillId)skill, Units = 1 }; SkillAction(ref action); }
        private void RefreshSkillHint(int i)
        {
            var hint = new System.Text.StringBuilder(Hints[i]);
            foreach (var perk in SkillPerkRoster.All) if ((int)perk.Skill == i)
                hint.Append("\n\n").Append(perk.Level).Append(" - ").Append(perk.Name).Append(_skills[i] >= perk.Level ? " (UNLOCKED)" : " (LOCKED)").Append("\n").Append(perk.Hint);
            _skillHints[i] = hint.ToString();
        }
        private void AnnounceSkillPerks(int skill, int before)
        {
            foreach (var perk in SkillPerkRoster.All) if ((int)perk.Skill == skill && before < perk.Level && _skills[skill] >= perk.Level)
            { var unlocked = new PlayerSkillPerkUnlocked { Id = perk.Id }; ModEvents.Publish(ref unlocked); Log.Always(this, "Skill perk unlocked: " + perk.Name + " (" + Names[skill] + " " + perk.Level + ")."); }
        }
        private void DrawSkillBoosts(Core.UI.ToolLayout layout)
        {
            layout.Heading("Increase skills");
            for (int row = 0; row < SkillOrder.Length; row++)
            {
                int i = SkillOrder[row];
                if (!layout.Button(Names[i] + ": " + _skills[i] + " / 100   (+5)", _loaded && _readError == null && GameRefs.WorldActive && GameRefs.SaveLoadState == "isPlay" && _skills[i] < 100)) continue;
                int gained = Mathf.Min(5, 100 - _skills[i]); _skills[i] += gained;
                if (_skills[i] == 100) _xp[i] = 0;
                RefreshSkillHint(i); AnnounceSkillPerks(i, _skills[i] - gained);
                if (i == (int)PlayerSkillId.Barter) _nativeSkills.RefreshPrices();
                Log.Always(this, "Skill increased: " + Names[i] + " +" + gained + " -> " + _skills[i] + ".");
            }
            layout.Hint("Adds 5 points to the selected skill, up to 100. Save the game to keep the change. Crafting still has no gameplay effect.");
        }
        private void SkillAction(ref PlayerSkillAction action)
        {
            int i = (int)action.Skill;
            if (!_loaded || _readError != null || !GameRefs.InPlay || i < 0 || i >= _skills.Length || _xpRates[i] == null || _skills[i] == 100 || float.IsNaN(action.Units) || float.IsInfinity(action.Units) || action.Units <= 0) return;
            float xp = _xp[i] + action.Units * _xpRates[i].Value * (_fastXpGain.Value ? 5f : 1f);
            int gained = Mathf.Min(100 - _skills[i], Mathf.FloorToInt(xp / 100f));
            _skills[i] += gained; _xp[i] = _skills[i] == 100 ? 0 : xp - gained * 100;
            if (gained > 0) AnnounceSkillPerks(i, _skills[i] - gained);
            RefreshSkillHint(i);
            if (gained > 0 && action.Skill == PlayerSkillId.Barter) _nativeSkills.RefreshPrices();
            if (gained > 0) Log.Always(this, "Skill increased: " + Names[i] + " +" + gained + " -> " + _skills[i] + ".");
        }
        public float Bonus(PlayerSkillId skill) { return _loaded ? _skills[(int)skill] * .005f : 0; }
        internal static int WeaponSkill(WeaponKind kind, bool unarmed)
        {
            if (unarmed) return (int)PlayerSkillId.Unarmed;
            switch (kind)
            {
                case WeaponKind.Melee: return (int)PlayerSkillId.Melee;
                case WeaponKind.Pistol: return (int)PlayerSkillId.Pistols;
                case WeaponKind.Smg: return (int)PlayerSkillId.Smgs;
                case WeaponKind.Rifle: case WeaponKind.Sniper: case WeaponKind.Crossbow: case WeaponKind.Launcher: return (int)PlayerSkillId.Rifles;
                case WeaponKind.Shotgun: return (int)PlayerSkillId.Shotguns;
                default: return -1;
            }
        }
        public float DamageMultiplier(WeaponKind kind, bool unarmed)
        { return unarmed ? 1 + Bonus(PlayerSkillId.Unarmed) : kind == WeaponKind.Melee ? 1 + Bonus(PlayerSkillId.Melee) : 1; }
        public float SpreadMultiplier(WeaponKind kind)
        {
            int skill = WeaponSkill(kind, false); float factor = skill >= 0 && kind != WeaponKind.Melee ? 1 - Bonus((PlayerSkillId)skill) : 1;
            if (kind == WeaponKind.Smg && SkillPerks.Moving && Has("RunAndGun")) factor *= .75f;
            if (SkillPerks.Rifle(kind) && (ModApi.Get<IWeaponState>()?.Aiming ?? false) && Has("Marksman")) factor *= .75f;
            return factor;
        }
        public int PurchasePrice(int price) { return price <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(price * (1 - Bonus(PlayerSkillId.Barter)))); }
        public int SalePrice(int price) { return price <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(price * (1 + Bonus(PlayerSkillId.Barter)))); }
        public int QuestPayout(int amount) { return SalePrice(amount); }
        public float ImpactMultiplier { get { return 1 + Bonus(PlayerSkillId.Driving) * 8f; } }
    }
}
