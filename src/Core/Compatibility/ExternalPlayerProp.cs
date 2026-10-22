using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Begins a local external player-prop update. One active source owns the presentation until a later Begin replaces it.</summary>
    public struct ExternalPlayerPropPoseBegin : IModEvent { public string SourceId; }
    /// <summary>Reports the actual external held model after its native pose update; Presentation alone owns rendering policy.</summary>
    public struct ExternalPlayerPropPoseReady : IModEvent { public string SourceId; public Transform Model; public bool ThirdPerson; }
    /// <summary>Releases an external prop source after unequip, failed native update or compatibility shutdown.</summary>
    public struct ExternalPlayerPropReleased : IModEvent { public string SourceId; }
}
