using UnityEngine;

// Core lifecycle and world events (publisher: Core ModuleHost, Origin, WorldTiles, Hotkeys, Optimization, Fixes, GameRefs).
// Event payloads (core-modapi.md 2, 9): structs with ids, enums, numbers, strings and WORLD-space vectors only (Origin.World),
// never GameObject/Component references (use EntityId / Entities). [Broadcast] marks events a future multiplayer transport may
// send; the rest are local. Publish with ModEvents.Publish(ref evt); check ModEvents.HasListeners<T>() before building costly
// payloads. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core.Events
{
    /// <summary>Module lifecycle states as published (mirror of ModuleState).</summary>
    public enum ModuleStateKind { NotStarted, Running, OffByConfig, OffConflict, InitFailed, DisabledAfterErrors }

    /// <summary>Why the culler/cleanup touched an item.</summary>
    public enum CullReason { Far, Fell, Cleanup, Restored }

    /// <summary>A module changed lifecycle state (ModuleHost -> Debug, HUD). Local only.</summary>
    public struct ModuleStateChanged : IModEvent
    {
        /// <summary>Module id.</summary>
        public string ModuleId;
        /// <summary>Previous state.</summary>
        public ModuleStateKind Old;
        /// <summary>New state.</summary>
        public ModuleStateKind New;
        /// <summary>Last error text (null when none).</summary>
        public string Error;
    }

    /// <summary>FloatingOrigin jumped (Origin -> all). Add Delta to cached scene positions. Local only.</summary>
    public struct OriginShifted : IModEvent
    {
        /// <summary>Old total offset minus new total offset (scene delta to add).</summary>
        public Vector3 Delta;
        /// <summary>New total offset.</summary>
        public Vector3 TotalOffset;
    }

    /// <summary>A MapMagic terrain tile was applied (WorldTiles -> POI, Patrols, HUD, Quests, Optimization). Local only.</summary>
    public struct TileApplied : IModEvent
    {
        /// <summary>Tile coordinate x.</summary>
        public int TileX;
        /// <summary>Tile coordinate z.</summary>
        public int TileZ;
        /// <summary>Tile rectangle in world (x,z) space.</summary>
        public Rect WorldRect;
        /// <summary>Draft (low detail) tile instead of main.</summary>
        public bool Draft;
    }

    /// <summary>A MapMagic tile finished generating (capture data here, consume in TileApplied). Local only.</summary>
    public struct TileFinalized : IModEvent
    {
        /// <summary>Tile coordinate x.</summary>
        public int TileX;
        /// <summary>Tile coordinate z.</summary>
        public int TileZ;
        /// <summary>Tile rectangle in world (x,z) space.</summary>
        public Rect WorldRect;
        /// <summary>Draft tile.</summary>
        public bool Draft;
    }

    /// <summary>A MapMagic tile moved/was recycled to another coordinate. Local only.</summary>
    public struct TileMoved : IModEvent
    {
        /// <summary>New tile coordinate x.</summary>
        public int TileX;
        /// <summary>New tile coordinate z.</summary>
        public int TileZ;
        /// <summary>Previous coordinate x.</summary>
        public int OldTileX;
        /// <summary>Previous coordinate z.</summary>
        public int OldTileZ;
        /// <summary>New rectangle in world (x,z) space.</summary>
        public Rect WorldRect;
    }

    /// <summary>Terrain heights changed inside a world rectangle (Compatibility Vistas, POI flattening). Local only.</summary>
    public struct TerrainEdited : IModEvent
    {
        /// <summary>Edited rectangle in world (x,z) space.</summary>
        public Rect WorldRect;
        /// <summary>Who edited ("Vistas", "POI"...).</summary>
        public string Source;
        /// <summary>Source revision counter.</summary>
        public int Revision;
    }

    /// <summary>A registered hotkey fired (Hotkeys -> MP input replay later). Local only.</summary>
    public struct HotkeyPressed : IModEvent
    {
        /// <summary>Config key name of the hotkey.</summary>
        public string Key;
        /// <summary>Owning module id ("Core" for Core).</summary>
        public string OwnerId;
    }

    /// <summary>The culler or save cleanup deactivated/destroyed/restored an item (Optimization Culler, Saves cleanup -> Loot).</summary>
    [Broadcast(Authority.Host)]
    public struct ItemCulled : IModEvent
    {
        /// <summary>Item entity.</summary>
        public EntityId Item;
        /// <summary>Save-stable item name.</summary>
        public string ItemName;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Why.</summary>
        public CullReason Reason;
        /// <summary>Destroyed (true) or only deactivated/restored.</summary>
        public bool Destroyed;
    }

    /// <summary>Car LOD froze a far vehicle (kinematic, sleeping).</summary>
    [Broadcast(Authority.Host)]
    public struct VehicleSimulationFrozen : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>Car LOD resumed a vehicle simulation.</summary>
    [Broadcast(Authority.Host)]
    public struct VehicleSimulationUnfrozen : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>Inventory supply items were dropped into the world (Fixes InventorySupplies -> HUD).</summary>
    [Broadcast(Authority.Owner)]
    public struct SupplyDropped : IModEvent
    {
        /// <summary>Supply kind key.</summary>
        public string Kind;
        /// <summary>Quantity dropped.</summary>
        public int Quantity;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Entity ids (EntityId.Value) of the new items; may be null.</summary>
        public long[] ItemIds;
    }

    /// <summary>Player money changed (GameRefs -> HUD, Quests).</summary>
    [Broadcast(Authority.Host)]
    public struct MoneyChanged : IModEvent
    {
        /// <summary>Previous amount.</summary>
        public int Old;
        /// <summary>New amount.</summary>
        public int New;
        /// <summary>Cause ("quest", "trade"...; may be null).</summary>
        public string Reason;
    }

    /// <summary>The player shouted (Core input -> NPCAI).</summary>
    [Broadcast(Authority.Owner)]
    public struct PlayerShout : IModEvent
    {
        /// <summary>Player world position.</summary>
        public Vector3 WorldPos;
        /// <summary>Audible radius, metres.</summary>
        public float Radius;
    }
}
