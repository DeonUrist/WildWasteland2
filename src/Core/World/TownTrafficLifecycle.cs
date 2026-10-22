namespace WildWasteland.Core
{
    /// <summary>POI town runtime stopped; consumers detach their pending town journey state before geometry disappears.</summary>
    public struct TownTrafficStopped : IModEvent { }
}
