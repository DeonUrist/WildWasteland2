using UnityEngine;

// Patrol, journey and crew events (publisher: Patrols).
// Event payloads (core-modapi.md 2, 9): structs with ids, enums, numbers, strings and WORLD-space vectors only (Origin.World),
// never GameObject/Component references (use EntityId / Entities). [Broadcast] marks events a future multiplayer transport may
// send; the rest are local. Publish with ModEvents.Publish(ref evt); check ModEvents.HasListeners<T>() before building costly
// payloads. FROZEN after W0 (changes via docs/refactor/requests/).
namespace WildWasteland.Core.Events
{
    /// <summary>Why a journey ended or virtualized.</summary>
    public enum JourneyEndReason { Arrived, Expired, Captured, Destroyed, Unloaded, Cancelled }

    /// <summary>Why a crew member left its seat.</summary>
    public enum CrewLeaveReason { Combat, Stuck, Vacated, Death, Jump, Order }

    /// <summary>A spawn roll produced a group.</summary>
    [Broadcast(Authority.Host)]
    public struct PatrolGroupRolled : IModEvent
    {
        /// <summary>Roll id.</summary>
        public int EventId;
        /// <summary>Group definition id.</summary>
        public string GroupId;
        /// <summary>Faction.</summary>
        public string Faction;
        /// <summary>patrol/convoy/merchant/battle/foot/poi/test.</summary>
        public string Kind;
        /// <summary>Template names.</summary>
        public string[] Templates;
    }

    /// <summary>A journey was planned.</summary>
    [Broadcast(Authority.Host)]
    public struct JourneyCreated : IModEvent
    {
        /// <summary>Journey id.</summary>
        public int JourneyId;
        /// <summary>Source camp/town key.</summary>
        public string SourceId;
        /// <summary>Destination key.</summary>
        public string DestinationId;
        /// <summary>Faction.</summary>
        public string Faction;
        /// <summary>Kind flags (convoy 1, merchant 2, foot 4, roaming 8, town-bound 16).</summary>
        public int Flags;
        /// <summary>Template names.</summary>
        public string[] Templates;
    }

    /// <summary>A journey left its source.</summary>
    [Broadcast(Authority.Host)]
    public struct JourneyDeparted : IModEvent
    {
        /// <summary>Journey id.</summary>
        public int JourneyId;
        /// <summary>Departure world position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A virtual journey became physical vehicles near the player.</summary>
    [Broadcast(Authority.Host)]
    public struct JourneyMaterialized : IModEvent
    {
        /// <summary>Journey id.</summary>
        public int JourneyId;
        /// <summary>Vehicle entity ids (EntityId.Value).</summary>
        public long[] Vehicles;
    }

    /// <summary>A physical journey went back to virtual simulation.</summary>
    [Broadcast(Authority.Host)]
    public struct JourneyVirtualized : IModEvent
    {
        /// <summary>Journey id.</summary>
        public int JourneyId;
        /// <summary>Why (Unloaded normally).</summary>
        public JourneyEndReason Reason;
    }

    /// <summary>A journey ended.</summary>
    [Broadcast(Authority.Host)]
    public struct JourneyEnded : IModEvent
    {
        /// <summary>Journey id.</summary>
        public int JourneyId;
        /// <summary>Why.</summary>
        public JourneyEndReason Reason;
    }

    /// <summary>Periodic AI driving inputs (5-10 Hz) for replication.</summary>
    [Broadcast(Authority.Host)]
    public struct VehicleDrivingState : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>Throttle -1..1.</summary>
        public float Throttle;
        /// <summary>Steer -1..1.</summary>
        public float Steer;
        /// <summary>Brake 0..1.</summary>
        public float Brake;
        /// <summary>Gear.</summary>
        public int Gear;
        /// <summary>Handbrake.</summary>
        public bool Handbrake;
        /// <summary>Engine running.</summary>
        public bool Engine;
    }

    /// <summary>A crew member took a seat.</summary>
    [Broadcast(Authority.Host)]
    public struct CrewSeated : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>NPC entity.</summary>
        public EntityId Npc;
        /// <summary>Seat index (0 = driver).</summary>
        public int Seat;
        /// <summary>Appearance identity.</summary>
        public string Identity;
    }

    /// <summary>An occupant left a raider car on foot (id-based successor of Services.CrewBailedOut).</summary>
    [Broadcast(Authority.Host)]
    public struct CrewBailedOut : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>New on-foot NPC entity.</summary>
        public EntityId Npc;
        /// <summary>Previous passenger object entity.</summary>
        public EntityId Passenger;
        /// <summary>Seat index.</summary>
        public int Seat;
        /// <summary>Why.</summary>
        public CrewLeaveReason Reason;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
        /// <summary>Velocity at exit.</summary>
        public Vector3 Velocity;
    }

    /// <summary>A crew member got out normally.</summary>
    [Broadcast(Authority.Host)]
    public struct CrewDismounted : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>NPC entity.</summary>
        public EntityId Npc;
        /// <summary>Seat index.</summary>
        public int Seat;
        /// <summary>Why.</summary>
        public CrewLeaveReason Reason;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A dismounted crew member boarded again.</summary>
    [Broadcast(Authority.Host)]
    public struct CrewReboarded : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>NPC entity.</summary>
        public EntityId Npc;
        /// <summary>Seat index.</summary>
        public int Seat;
    }

    /// <summary>A passenger took over driving.</summary>
    [Broadcast(Authority.Host)]
    public struct DriverPromoted : IModEvent
    {
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>NPC entity.</summary>
        public EntityId Npc;
        /// <summary>Previous seat index.</summary>
        public int FromSeat;
    }

    /// <summary>A patrol vehicle rammed something.</summary>
    [Broadcast(Authority.Host)]
    public struct RamHit : IModEvent
    {
        /// <summary>Rammer entity.</summary>
        public EntityId Vehicle;
        /// <summary>Target entity.</summary>
        public EntityId Target;
        /// <summary>Damage.</summary>
        public float Damage;
        /// <summary>Relative speed, m/s.</summary>
        public float RelativeSpeed;
        /// <summary>Push vector.</summary>
        public Vector3 Push;
    }

    /// <summary>A rider threw a blast lance.</summary>
    [Broadcast(Authority.Host)]
    public struct LanceThrown : IModEvent
    {
        /// <summary>Rider entity.</summary>
        public EntityId Rider;
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>Origin, world space.</summary>
        public Vector3 OriginWorld;
        /// <summary>Velocity.</summary>
        public Vector3 Velocity;
    }

    /// <summary>A rider leapt from a vehicle.</summary>
    [Broadcast(Authority.Host)]
    public struct RiderLeap : IModEvent
    {
        /// <summary>Rider entity.</summary>
        public EntityId Rider;
        /// <summary>Vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>Launch, world space.</summary>
        public Vector3 FromWorld;
        /// <summary>Launch velocity.</summary>
        public Vector3 Velocity;
    }

    /// <summary>A rider detonated.</summary>
    [Broadcast(Authority.Host)]
    public struct RiderDetonated : IModEvent
    {
        /// <summary>Rider entity.</summary>
        public EntityId Rider;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A merchant stopped at a place.</summary>
    [Broadcast(Authority.Host)]
    public struct MerchantVisit : IModEvent
    {
        /// <summary>Journey id.</summary>
        public int JourneyId;
        /// <summary>Merchant vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>Stop key.</summary>
        public string StopId;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>Merchant stock changed (purchase/restock).</summary>
    [Broadcast(Authority.Host)]
    public struct MerchantStockChanged : IModEvent
    {
        /// <summary>Journey id.</summary>
        public int JourneyId;
        /// <summary>Merchant vehicle entity.</summary>
        public EntityId Vehicle;
        /// <summary>Slot index.</summary>
        public int Slot;
        /// <summary>Bought by the player.</summary>
        public bool Purchased;
    }

    /// <summary>Two journeys started fighting.</summary>
    [Broadcast(Authority.Host)]
    public struct FactionBattleStarted : IModEvent
    {
        /// <summary>Battle id.</summary>
        public int BattleId;
        /// <summary>First journey.</summary>
        public int JourneyA;
        /// <summary>Second journey.</summary>
        public int JourneyB;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A faction battle ended.</summary>
    [Broadcast(Authority.Host)]
    public struct FactionBattleEnded : IModEvent
    {
        /// <summary>Battle id.</summary>
        public int BattleId;
        /// <summary>Winning journey (-1 none).</summary>
        public int WinnerJourney;
        /// <summary>World position.</summary>
        public Vector3 WorldPos;
    }

    /// <summary>A camp called reinforcements.</summary>
    [Broadcast(Authority.Host)]
    public struct ReinforcementsCalled : IModEvent
    {
        /// <summary>Camp key.</summary>
        public string CampId;
        /// <summary>Reinforcement journey.</summary>
        public int JourneyId;
    }

    /// <summary>A journey visits a town.</summary>
    [Broadcast(Authority.Host)]
    public struct TownVisit : IModEvent
    {
        /// <summary>Journey id.</summary>
        public int JourneyId;
        /// <summary>Town key.</summary>
        public string TownKey;
        /// <summary>Parked (false = passing).</summary>
        public bool Parked;
        /// <summary>Resident vehicle.</summary>
        public bool Resident;
    }
}
