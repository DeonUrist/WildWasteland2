using UnityEngine;

namespace WildWasteland.Core
{
    public enum PlayerSkillId { Melee, Pistols, Rifles, Shotguns, Unarmed, Athletics, Barter, Crafting, Medicine, Survival, Driving, Smgs }

    /// <summary>PERKS owns linear XP and numeric skill effects; gameplay owners report completed actions.</summary>
    public interface IPlayerSkills
    {
        float Bonus(PlayerSkillId skill);
        float DamageMultiplier(WeaponKind kind, bool unarmed);
        float SpreadMultiplier(WeaponKind kind);
        int PurchasePrice(int price);
        int SalePrice(int price);
        int QuestPayout(int amount);
        float ImpactMultiplier { get; }
    }

    /// <summary>Survival tracks the most recent cooking-item handler. Other actors may replace player credit.</summary>
    public interface ICookingSkills
    {
        void Touch(GameObject item, EntityId actor);
        float NutritionMultiplier(GameObject item);
        float InsanityMultiplier(GameObject item);
        void StewCooked(GameObject item, float rawNutrition);
    }
}
