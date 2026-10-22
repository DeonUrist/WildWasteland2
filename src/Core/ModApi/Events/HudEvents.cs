using UnityEngine;

// HUD events (publisher: HUD).
// Event payloads (core-modapi.md 2, 9): structs with ids, enums, numbers, strings and WORLD-space vectors only (Origin.World),
// never GameObject/Component references (use EntityId / Entities). [Broadcast] marks events a future multiplayer transport may
// send; the rest are local. Publish with ModEvents.Publish(ref evt); check ModEvents.HasListeners<T>() before building costly
// payloads. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core.Events
{
    /// <summary>The player discovered a location.</summary>
    [Broadcast(Authority.Owner)]
    public struct LocationDiscovered : IModEvent
    {
        /// <summary>Location key "Label:x:z".</summary>
        public string LocationKey;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>proximity/binoculars/vantage.</summary>
        public string Method;
    }

    /// <summary>Fog of war chunks were explored (delta).</summary>
    [Broadcast(Authority.Owner)]
    public struct FogExplored : IModEvent
    {
        /// <summary>Explored chunk keys.</summary>
        public int[] Chunks;
        /// <summary>Exploration revision.</summary>
        public int Revision;
    }

    /// <summary>The map was opened. Local only.</summary>
    public struct MapOpened : IModEvent
    {
        /// <summary>Zoom factor.</summary>
        public float Zoom;
    }

    /// <summary>The map was closed. Local only.</summary>
    public struct MapClosed : IModEvent
    {
        /// <summary>Last zoom factor.</summary>
        public float Zoom;
    }
}
