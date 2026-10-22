namespace WildWasteland.Core
{
    /// <summary>Exact save-write transaction state; world loading alone never activates this lock.</summary>
    public interface ISaveTransactionState { bool Active {get;} }
}
