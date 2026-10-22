using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    /// <summary>Describes the town condition data used by this owner.</summary>
    public enum TownCondition { Ruined, Inhabited }
    /// <summary>Describes the town faction data used by this owner.</summary>
    public enum TownFaction { Animals, Mutants, Scrapyard, Coyotes }
    [Flags]
    public enum TownRole { None = 0, Residence = 1, Shop = 2, GasStation = 4, ParkingLot = 8, Workshop = 16, Civic = 32 }

    // Numeric, immutable during generation. No Unity objects cross to a worker.
    internal sealed class BuildingSpec
    {
        internal string Name;
        internal Vector3 Center;
        internal float HalfX, HalfZ;
        internal int ColliderCount;
        internal TownBakeData Bake;
        internal Vector3 Entry, Outward;
        internal TownFrontage[] Frontages;
        internal TownFrontage[] TiltedFrontages;
        internal bool Crane, Wreck, Merchant, Camp;
        /// <summary>Tests whether the measured footprint exceeds the large-building limit.</summary>
        internal bool Big { get { return HalfX > 25 || HalfZ > 25; } }
    }
    public sealed class TownLot
    {
        public string Prefab { get; internal set; }
        public bool IsParking { get { return Prefab == "Towns_Parking"; } }
        /// <summary>Tests whether the committed prefab is an authored merchant lot.</summary>
        public bool IsMerchant { get { return Merchants.Contains(Prefab); } }
        internal static readonly HashSet<string> Merchants = new HashSet<string>(StringComparer.Ordinal) { "Camp_10" };
        public Vector3 LocalPosition { get; internal set; }
        public float Yaw { get; internal set; }
        public TownRole Role { get; internal set; }
        internal Vector3 BoundsCenter;
        internal float HalfX, HalfZ;
        internal int Street;
        internal Vector3 Entry;
        internal bool HasEntry;
        internal int EntryNode = -1;
        internal int Actors, Loot, Vehicles;
        internal bool Hounds;
        internal bool Skipped, External;
        /// <summary>Returns an independent editable copy of the current data.</summary>
        internal TownLot Copy() { return (TownLot)MemberwiseClone(); }
    }
    public sealed class TownPlan
    {
        public const int GeneratorVersion = 9;
        internal int LayoutVersion = 8;
        public string Key { get; internal set; }
        internal string TemplateId;
        internal string TemplateJson;
        internal TownTemplate Rules;
        public bool HasTown { get; internal set; }
        public TownCondition Condition { get; internal set; }
        public TownFaction Faction { get; internal set; }
        public int FactionMembers { get; internal set; }
        /// <summary>Reports accepted human-faction ownership before resident spawning.</summary>
        public bool Owned { get { return HasTown && (Faction == TownFaction.Coyotes || Faction == TownFaction.Scrapyard); } }
        internal bool PopulationMeasured;
        internal bool MerchantSeeded;
        public string Name { get; internal set; }
        public bool Discovered { get; internal set; }
        public Vector3 WorldCenter { get; internal set; }
        public float Yaw { get; internal set; }
        public float Radius { get; internal set; }
        public float Feather { get; internal set; }
        public float StreetWidth { get; internal set; }
        public float OriginalRelief { get; internal set; }
        public float MaximumEarthwork { get; internal set; }
        public string Decision { get; internal set; }
        internal TownLot[] Lots = new TownLot[0];
        internal Vector3[] StreetNodes = new Vector3[0];
        internal int[] StreetEdges = new int[0];
        internal int MainNodes;
        internal int PathsVersion;
        internal TownOutline Outline;
        internal TownOverlayPlan Overlay;        // derived from the plan + prefab templates; never saved, recomputed per generation
        internal TownDressingPlan DressingOnly;  // main-thread fallback when no worker overlay reached this plan object
        internal float PathWidth, AccessWidth;
        internal bool GradeAccess;
        internal Vector3[] AccessPath = new Vector3[0];
        /// <summary>Copies mutable lot/path state while invalidating derived geometry caches.</summary>
        internal TownPlan CopyPaths()
        {
            var p = (TownPlan)MemberwiseClone(); p.Outline = null; p.Overlay = null; p.DressingOnly = null; p.Lots = new TownLot[Lots.Length];
            for (int i = 0; i < Lots.Length; i++) p.Lots[i] = Lots[i].Copy();
            return p;
        }
        /// <summary>Returns the committed number of lots.</summary>
        public int LotCount { get { return Lots.Length; } }
        /// <summary>Returns the committed lot at the requested index.</summary>
        public TownLot GetLot(int index) { return Lots[index]; }
        /// <summary>Converts a lot-local point to stable world coordinates.</summary>
        internal Vector3 World(Vector3 local) { return WorldCenter + Quaternion.Euler(0, Yaw, 0) * local; }
    }
    internal sealed class GenerationInput
    {
        internal string Key;
        internal TownTemplate[] Templates;
        internal int Seed, Min, Max, Chance, Inhabited;
        internal bool ParkingCompanions;
        internal float MaxRelief, MaxEarthwork, MaxGrade, Feather;
        internal float Gap;
        internal BuildingSpec[] Buildings;
        internal TownPlan Saved;
        internal string Error;
        internal int RuinsMin, RuinsMax, HumansMin, HumansMax, LootMin, LootMax;
        internal float AccessLength;
        internal bool EnsureTown;
        internal TownLoading.Work Progress;
        internal bool ExcludeStartingChunk;
    }
    internal sealed class SpawnerState
    {
        internal string Path;
        internal int OnOff, Number, NumberEnd;
    }
    internal sealed class TownRecord
    {
        internal TownPlan Plan;
        internal readonly Dictionary<int, SpawnerState[]> Spawners = new Dictionary<int, SpawnerState[]>();
    }
    public sealed class TownSite
    {
        public TownPlan Plan { get; internal set; }
        internal Transform Anchor;
        internal Terrain GroundTerrain;
        internal TownGroundPad GroundPad;
        internal TownNavContext NavContext;
        internal PopulationArea PopulationArea;
        internal GameObject Streets;
        internal GameObject[] Buildings;
        internal PlayMakerFSM[][] Spawners;
        internal ITownNavWork Navigation;
        internal ITownOverlayWork Overlap;
        internal TownLoading.Work Loading;
        internal int NextLot;
        internal int[][] PopulationCounts, LootCounts;
        internal TownRecord Record;
        internal readonly List<Bounds> DebrisBlockers = new List<Bounds>();
        internal bool NavigationStopped;
        internal bool MerchantReady;
        internal TownWalls Walls;
        internal GameObject Staged;               // next lot's prefab clone, deserialized one build slot before its activation
        internal int PrepSteps;                   // completed overlap/navigation steps (work-based preparation budget)
        internal readonly HashSet<int> CountedMembers = new HashSet<int>();
        /// <summary>Converts a plan-local point through the live map anchor.</summary>
        internal Vector3 Scene(Vector3 local) { return Anchor.TransformPoint(Plan.World(local)); }
        /// <summary>Projects a point onto the supplied town terrain when present.</summary>
        internal Vector3 Ground(Vector3 point, float offset = 0)
        {
            if (GroundTerrain != null) point.y = GroundTerrain.SampleHeight(point) + GroundTerrain.transform.position.y + offset;
            return point;
        }
        /// <summary>Stages or resolves the native geometry of one committed lot.</summary>
        public GameObject Building(int lot) { return Buildings[lot]; }
        /// <summary>Returns the authored exterior parking pose and footprint.</summary>
        public bool Parking(out Vector3 center, out Quaternion rotation, out Vector2 size)
        {
            foreach (var lot in Plan.Lots) if (lot.IsParking)
            { center = Scene(lot.LocalPosition); rotation = Anchor.rotation * Quaternion.Euler(0, Plan.Yaw + lot.Yaw, 0); size = new Vector2(lot.HalfX * 2, lot.HalfZ * 2); return true; }
            center = Vector3.zero; rotation = Quaternion.identity; size = Vector2.zero; return false;
        }
    }
    /// <summary>
    /// The running Towns module for the Patrols queries (Pilot, CrewPoi, TrafficLocations, RoadPlanner, WorldTraffic, TownTraffic).
    /// Wave A (A6): the unused public wrappers Count/Get/SetRole/SetCondition were deleted (README 7); B13 replaces this static
    /// with the Core ITownsApi provider.
    /// </summary>
    public static class TownsApi
    {
        internal static TownRuntime Provider;
    }
}
