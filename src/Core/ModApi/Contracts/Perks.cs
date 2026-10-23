using UnityEngine;

namespace WildWasteland.Core
{
    public enum InsanityEventType { Vertigo, NoMoreDeath, Berserk, Delirium, Doppelganger }
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
        bool Trigger(InsanityEventType type);
        bool Choose(int index);
    }

    /// <summary>Read-only effects for one affected player. Survival currently provides only Entities.Player; queries for other players return false.</summary>
    public interface IPlayerMentalEffects
    {
        EntityId Player { get; }
        float Insanity { get; }
        /// <summary>Allocation-free presentation snapshot; false for an unaffected or remote player. Render it only through that player's camera.</summary>
        bool TryGetVisuals(EntityId player, out MentalBreakVisuals visuals);
        /// <summary>Temporary hostility toward this player only; never changes a faction's saved relation or hostility toward another player.</summary>
        bool ProvokesFaction(EntityId player, string faction);
    }

    /// <summary>One player's transient hallucinations. Elapsed derives from saved gameplay time; nothing here changes shared sky, time or lighting.</summary>
    public struct MentalBreakVisuals
    {
        public EntityId Player;
        public InsanityEventType EventType;
        public float Remaining, Elapsed, Fade, InsanityWeight, RedTint;
        public bool AlcoholDistortion, CoyoteDisguise, Doppelganger;
        public int SunCount, SkyFigureCount;
        public Color SkyColor;
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
