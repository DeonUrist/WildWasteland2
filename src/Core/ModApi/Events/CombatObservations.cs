namespace WildWasteland.Core.Events
{
    /// <summary>A native creature attack was observed; tactical/UI listeners resolve the ids synchronously.</summary>
    public struct AttackObserved : IModEvent { public EntityId Victim,Attacker; }
    /// <summary>A fitted vehicle part was attacked, published before the native part can detach.</summary>
    public struct PartAttackObserved : IModEvent { public EntityId Part,Attacker; }
}
