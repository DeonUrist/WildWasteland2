using UnityEngine;

// Loot events (publisher: Loot).
// Event payloads (core-modapi.md 2, 9): structs with ids, enums, numbers, strings and WORLD-space vectors only (Origin.World),
// never GameObject/Component references (use EntityId / Entities). [Broadcast] marks events a future multiplayer transport may
// send; the rest are local. Publish with ModEvents.Publish(ref evt); check ModEvents.HasListeners<T>() before building costly
// payloads. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core.Events
{
    /// <summary>A spawner produced items.</summary>
    [Broadcast(Authority.Host)]
    public struct LootSpawned : IModEvent
    {
        /// <summary>Spawner key/path.</summary>
        public string SpawnerId;
        /// <summary>Loot table id.</summary>
        public string TableId;
        /// <summary>Item entity ids (EntityId.Value).</summary>
        public long[] Items;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>Spawner progress changed (saved per owner).</summary>
    [Broadcast(Authority.Host)]
    public struct SpawnerProgressChanged : IModEvent
    {
        /// <summary>Town/POI/marker key.</summary>
        public string OwnerKey;
        /// <summary>Spawner path.</summary>
        public string SpawnerPath;
        /// <summary>Spawner active.</summary>
        public bool On;
        /// <summary>Spawned so far.</summary>
        public int Number;
        /// <summary>Target count.</summary>
        public int NumberEnd;
    }

    /// <summary>A natural loot spawn was replaced by a substitution rule. Local only.</summary>
    public struct NaturalLootSubstituted : IModEvent
    {
        /// <summary>Spawner path.</summary>
        public string SpawnerPath;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Original prefab id.</summary>
        public long OriginalPrefabId;
        /// <summary>Replacement prefab id.</summary>
        public long NewPrefabId;
    }
}
