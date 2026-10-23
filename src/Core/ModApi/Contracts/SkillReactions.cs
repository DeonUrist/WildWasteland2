using UnityEngine;

namespace WildWasteland.Core
{
    public enum PerkEnemyClass { Other, Small, Humanoid, Quadruped, LargeMutant, Juggernaut }
    public interface ISkillReactions
    {
        PerkEnemyClass ClassOf(GameObject npc);
        float MaximumHealth(GameObject npc);
        bool Unaware(GameObject npc);
        bool IsHeld(GameObject npc);
        bool IsBodyCollider(GameObject hit);
        void React(GameObject npc, Vector3 push, float seconds, bool ragdoll);
    }
    public interface INpcRecovery
    {
        bool Available(GameObject npc);
        float Begin(GameObject npc, Vector3 chestForward);
        void End(GameObject npc);
    }
    public static class SkillPerks
    {
        public static bool Has(string id) { return ModApi.Get<IPlayerPerks>()?.Has(id) ?? false; }
        public static bool Moving
        {
            get { var rb = GameRefs.Player != null ? GameRefs.Player.GetComponent<Rigidbody>() : null; if (rb == null || PlayerState.InCar) return false; var v = rb.velocity; return v.x * v.x + v.z * v.z > .1f; }
        }
        public static bool Rifle(WeaponKind kind) { return kind == WeaponKind.Rifle || kind == WeaponKind.Sniper || kind == WeaponKind.Crossbow; }
    }
}
