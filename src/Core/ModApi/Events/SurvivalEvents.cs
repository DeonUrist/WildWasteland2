using UnityEngine;

// Weather and survival events (publisher: Survival).
// Event payloads (core-modapi.md 2, 9): structs with ids, enums, numbers, strings and WORLD-space vectors only (Origin.World),
// never GameObject/Component references (use EntityId / Entities). [Broadcast] marks events a future multiplayer transport may
// send; the rest are local. Publish with ModEvents.Publish(ref evt); check ModEvents.HasListeners<T>() before building costly
// payloads. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core.Events
{
    /// <summary>Storm life phases.</summary>
    public enum StormPhase { Forming, Active, Critical, Ending }

    /// <summary>A storm appeared.</summary>
    [Broadcast(Authority.Host)]
    public struct StormSpawned : IModEvent
    {
        /// <summary>Storm id (ES3 reference id or manual GUID hash).</summary>
        public long StormId;
        /// <summary>Region (world space).</summary>
        public WeatherRegion Region;
        /// <summary>Natural (false = manual preview).</summary>
        public bool Natural;
    }

    /// <summary>A storm ended/was removed.</summary>
    [Broadcast(Authority.Host)]
    public struct StormRemoved : IModEvent
    {
        /// <summary>Storm id.</summary>
        public long StormId;
    }

    /// <summary>A storm changed phase.</summary>
    [Broadcast(Authority.Host)]
    public struct StormPhaseChanged : IModEvent
    {
        /// <summary>Storm id.</summary>
        public long StormId;
        /// <summary>New phase.</summary>
        public StormPhase Phase;
        /// <summary>Age, seconds.</summary>
        public float Age;
    }

    /// <summary>A storm hazard hit something.</summary>
    [Broadcast(Authority.Host)]
    public struct StormHazardHit : IModEvent
    {
        /// <summary>Storm id.</summary>
        public long StormId;
        /// <summary>Target entity.</summary>
        public EntityId Target;
        /// <summary>player/NPC/vehicle.</summary>
        public string TargetKind;
        /// <summary>Damage or impulse.</summary>
        public float Amount;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>Lightning struck (clients play thunder by distance).</summary>
    [Broadcast(Authority.Host)]
    public struct LightningStrike : IModEvent
    {
        /// <summary>Storm id.</summary>
        public long StormId;
        /// <summary>Strike, world space.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A stew pot appeared.</summary>
    [Broadcast(Authority.Host)]
    public struct StewPotSpawned : IModEvent
    {
        /// <summary>Pot entity.</summary>
        public EntityId Pot;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>natural/spawner/save.</summary>
        public string Source;
    }

    /// <summary>Stew pot contents changed.</summary>
    [Broadcast(Authority.Host)]
    public struct StewPotContentChanged : IModEvent
    {
        /// <summary>Pot entity.</summary>
        public EntityId Pot;
        /// <summary>Total litres.</summary>
        public float Litres;
        /// <summary>Raw litres.</summary>
        public float Raw;
        /// <summary>Cooked litres.</summary>
        public float Cooked;
        /// <summary>Insanity amount.</summary>
        public float Insanity;
        /// <summary>Spoiled.</summary>
        public bool Spoiled;
        /// <summary>On a fire.</summary>
        public bool Heating;
    }

    /// <summary>Someone drank from a pot.</summary>
    [Broadcast(Authority.Host)]
    public struct StewPotDrink : IModEvent
    {
        /// <summary>Pot entity.</summary>
        public EntityId Pot;
        /// <summary>Drinker entity.</summary>
        public EntityId Actor;
        /// <summary>Litres drunk.</summary>
        public float Litres;
    }

    /// <summary>A pot was poured into another.</summary>
    [Broadcast(Authority.Host)]
    public struct StewPotPour : IModEvent
    {
        /// <summary>Source pot entity.</summary>
        public EntityId Pot;
        /// <summary>Target pot entity.</summary>
        public EntityId TargetPot;
        /// <summary>Litres poured.</summary>
        public float Litres;
    }
}
