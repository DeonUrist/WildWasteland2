using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    /// <summary>Detached public town geometry and template authoring API.</summary>
    internal sealed partial class TownRuntime : ITownsApi
    {
        /// <summary>Geometry changes whenever plans, live structures or world state change.</summary>
        public int Revision { get { return TravelRevision; } }
        /// <summary>Copies all accepted plans into consumer-owned summaries.</summary>
        public void Towns(List<TownInfo> into) { into.Clear(); foreach (var record in _records.Values) if (record.Plan.HasTown) into.Add(Info(record.Plan)); }
        /// <summary>Finds the first accepted town footprint containing a world point.</summary>
        public bool TryTownAt(Vector3 world, out TownInfo town)
        { foreach (var record in _records.Values) { var p = record.Plan; if (p.HasTown && TownPerimeter.Outline(p).Contains(world - p.WorldCenter, 0)) { town = Info(p); return true; } } town = default(TownInfo); return false; }
        /// <summary>Finds a known town by stable plan key.</summary>
        public bool TryGet(string key, out TownInfo town)
        { TownRecord record; if (_records.TryGetValue(key, out record) && record.Plan.HasTown) { town = Info(record.Plan); return true; } town = default(TownInfo); return false; }
        /// <summary>Returns a detached world polygon.</summary>
        public bool TryOutline(string key, List<Vector3> worldPolygon)
        { worldPolygon.Clear(); TownRecord r; if (!_records.TryGetValue(key, out r) || !r.Plan.HasTown) return false; foreach (var p in TownPerimeter.Outline(r.Plan).Vertices) worldPolygon.Add(p + r.Plan.WorldCenter); return true; }
        /// <summary>Copies four cardinal gates in world coordinates.</summary>
        public void Gates(string key, List<TownGate> into)
        { into.Clear(); TownRecord r; if (!_records.TryGetValue(key, out r) || !r.Plan.HasTown) return; var frame = TownPerimeter.Frame(r.Plan); for (int side = 0; side < 4; side++) into.Add(new TownGate { WorldPos = r.Plan.WorldCenter + TownPerimeter.Gate(frame, side), Outward = TownPerimeter.Normal(side), Width = TownPerimeter.GateHalf * 2 }); }
        /// <summary>Copies street nodes and edge pairs without exposing mutable plans.</summary>
        public void Streets(string key, List<Vector3> nodes, List<int> edgePairs)
        { nodes.Clear(); edgePairs.Clear(); TownRecord r; if (!_records.TryGetValue(key, out r) || !r.Plan.HasTown) return; foreach (var node in r.Plan.StreetNodes) nodes.Add(r.Plan.World(node)); edgePairs.AddRange(r.Plan.StreetEdges); }
        /// <summary>Finds the nearest authored external parking lot.</summary>
        public bool TryParkingSlot(string key, Vector3 nearWorld, bool outsideOnly, out ParkingSlotInfo slot)
        { slot = default(ParkingSlotInfo); TownRecord r; if (!_records.TryGetValue(key, out r) || !r.Plan.HasTown) return false; float best = float.PositiveInfinity; bool found = false; for (int i = 0; i < r.Plan.Lots.Length; i++) { var lot = r.Plan.Lots[i]; if (!lot.IsParking || lot.Skipped) continue; var p = r.Plan.World(lot.LocalPosition); float d = (p - nearWorld).sqrMagnitude; if (d >= best) continue; best = d; slot = new ParkingSlotInfo { WorldPos = p, Yaw = r.Plan.Yaw + lot.Yaw, Outside = true, Lot = i, Index = i }; found = true; } return found; }
        /// <summary>Returns the authored role flags of a lot.</summary>
        public string LotRole(string key, int lot)
        { TownRecord r; return _records.TryGetValue(key, out r) && lot >= 0 && lot < r.Plan.Lots.Length ? r.Plan.Lots[lot].Role.ToString() : null; }
        /// <summary>Tests exact building ownership.</summary>
        public bool IsTownBuilding(Transform root) { return TownMarkers.Owns(root); }
        /// <summary>Copies template ids.</summary>
        public void Templates(List<string> into) { TemplateStore.Ids(into); }
        /// <summary>Reads editable template JSON.</summary>
        public bool TryGetTemplate(string templateId, out string json) { return TemplateStore.Get(templateId, out json); }
        /// <summary>Saves validated authoring rules for future town decisions.</summary>
        public bool SaveTemplate(string templateId, string json) { return TemplateStore.Save(templateId, json); }
        /// <summary>Projects one private plan to stable public metadata.</summary>
        private TownInfo Info(TownPlan p)
        { bool built = false; foreach (var live in _sites) if (live.Site.Plan == p && live.Site.Anchor != null) { built = live.Finished; break; } return new TownInfo { Key = p.Key, TemplateId = p.TemplateId, Faction = p.Faction.ToString(), Condition = p.Condition.ToString(), WorldCenter = p.WorldCenter, Yaw = p.Yaw, Radius = p.Radius, HasWalls = TemplateWalls(p) && TownPerimeter.Allegiance(p), Built = built, LotCount = p.LotCount }; }
    }
}
