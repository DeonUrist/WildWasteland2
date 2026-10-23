using UnityEngine;

namespace WildWasteland.Core
{
    public enum InsanityEventType { Vertigo, NoMoreDeath }
    /// <summary>Read-only skill definition and current player value.</summary>
    public struct PlayerSkill
    {
        public string Id, Name, Hint;
        public int Value;
        public float Xp;
    }

    /// <summary>Read-only permanent perk with borrowed artwork.</summary>
    public struct PlayerPerk
    {
        public string Id, Name, Hint;
        public bool Unlocked;
        public bool Insanity;
        public Texture2D Icon;
    }

    /// <summary>PERKS owns player skill values and permanent perk unlocks.</summary>
    public interface IPlayerPerks
    {
        int SkillCount { get; }
        PlayerSkill Skill(int index);
        int PerkCount { get; }
        PlayerPerk Perk(int index);
        bool Has(string id);
        bool ChooseInsanityPerk(string id);
        PlayerPerk InsanityChoice(int index);
        void PrepareInsanityChoices();
    }

    /// <summary>Survival owns the required mental-break decision and insanity reset.</summary>
    public interface IMentalBreak
    {
        bool Pending { get; }
        bool Active { get; }
        float Remaining { get; }
        InsanityEventType EventType { get; }
        string EventName { get; }
        bool LocksInsanity { get; }
        bool WeaponsBlocked { get; }
        bool Trigger();
        bool Choose(int index);
    }

    /// <summary>Allocation-free gameplay queries. Actions arrive through PlayerWokeUp and NpcKilled events.</summary>
    public interface IPlayerPerkEffects
    {
        bool CanButcherHumans { get; }
        float FoodInsanityMultiplier { get; }
        float MutantDetectionMultiplier { get; }
        bool IgnoreOwnExplosions { get; }
        bool Paranoia { get; }
        float MentalBreakChance { get; }
        float HumanKillInsanityMultiplier { get; }
        float DamageMultiplier(WeaponKind kind, float healthFraction);
    }

    /// <summary>Gunplay's explicit damage-source ownership, including thrown explosives and chain reactions.</summary>
    public interface ICombatSources
    {
        EntityId OwnerOf(GameObject source);
        void Assign(GameObject source, EntityId owner);
        void RecordHit(GameObject target, EntityId attacker, WeaponKind kind, bool unarmed);
        float PrepareMelee(GameObject target, float damage, bool cutting);
        void FinishMelee(GameObject target, float damage);
    }

    /// <summary>Registered enemies without scene scans or physics queries.</summary>
    public interface INpcEnemies
    {
        void NearPlayer(Vector3 scene, float radius, System.Collections.Generic.List<GameObject> into);
    }
}
