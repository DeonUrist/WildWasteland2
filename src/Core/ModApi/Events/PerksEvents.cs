namespace WildWasteland.Core.Events
{
    /// <summary>Completed action units: one kill/trade/quest/heal, ten raw nutrition, or one kilometre on foot.</summary>
    public struct PlayerSkillAction : IModEvent
    {
        public PlayerSkillId Skill;
        public float Units;
    }
    /// <summary>Gunplay attributed the final hit before the native creature corpse was created.</summary>
    public struct PlayerWeaponKill : IModEvent
    {
        public WeaponKind Kind;
        public bool Unarmed;
        public EntityId Victim;
        public UnityEngine.Vector3 WorldPos;
    }
    public struct PlayerSkillPerkUnlocked : IModEvent { public string Id; }
    /// <summary>Survival publishes once after native sleep completes, on foot or seated.</summary>
    public struct PlayerWokeUp : IModEvent
    {
        public bool CorpseNearby;
    }
}
