using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MapMagic.Terrains;
using Den.Tools;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Towns queries used by Patrols (TownsApi.Provider: travel stops, merchant seeding, patrol bases, parking, travel blocks,
    /// vehicle gate waypoints) and the role/condition setters. Wave A (A6): entry split of Towns/TownRuntime.cs (POI part, B13:
    /// becomes the ITownsApi provider with Core DTOs instead of Patrols.TownStop/TrafficBase/TrafficSpace).
    /// </summary>
    internal sealed partial class TownRuntime
    {
        public bool SetRole(string key, int lot, TownRole role)
        {
            TownRecord record;
            if (!_records.TryGetValue(key, out record) || lot < 0 || lot >= record.Plan.LotCount || ((int)role & ~63) != 0) return false;
            record.Plan.Lots[lot].Role = role;
            Log.Always("[Towns] lot role " + key + "/" + lot + " = " + role + " (persists with next world save)"); return true;
        }
        internal void TravelStops(List<Patrols.TownStop> into)
        {
            foreach (var record in _records.Values)
            {
                var plan = record.Plan;
                // (WW) a Coyotes town always takes part (its merchant bus must be there), others once their faction holds it
                if (!plan.HasTown || plan.PathsVersion < 3) continue;
                if (plan.Faction != TownFaction.Coyotes && (!plan.Owned || plan.Condition != TownCondition.Inhabited)) continue;
                foreach (var lot in plan.Lots) if (lot.IsParking && !lot.Skipped)
                {
                    into.Add(new Patrols.TownStop { Id = plan.Key, Name = plan.Name, Faction = plan.Faction.ToString(),
                        Parking = plan.World(lot.LocalPosition), Heading = Quaternion.Euler(0, plan.Yaw + lot.Yaw, 0) * Vector3.forward,
                        Center = plan.WorldCenter, Radius = plan.Radius, MerchantSeeded = plan.MerchantSeeded, CanTravel = plan.AccessPath.Length > 1, Lane = false,
                        RoadApproach = plan.AccessPath.Length > 1 ? (Vector3?)plan.World(plan.AccessPath[plan.AccessPath.Length - 1]) : null }); break;
                }
            }
        }
        internal void MerchantSeeded(string key)
        { TownRecord record; if (_records.TryGetValue(key, out record)) record.Plan.MerchantSeeded = true; }
        internal int TravelRevision
        { get { int count = 0; foreach (var record in _records.Values) if (record.Plan.HasTown) count++; return unchecked(_epoch * 397 ^ count); } }
        internal bool PatrolBase(string id)
        {
            TownRecord record;
            if (id == null || !_records.TryGetValue(id, out record) || !record.Plan.HasTown || record.Plan.Condition != TownCondition.Inhabited) return false;
            foreach (var lot in record.Plan.Lots) if (lot.IsParking && !lot.Skipped) return true;
            return false;
        }
        internal int PatrolCount(Vector3 player, float range)
        {
            int count = 0;
            foreach (var record in _records.Values)
            {
                var plan = record.Plan; if (!plan.HasTown || plan.Condition != TownCondition.Inhabited) continue;
                foreach (var lot in plan.Lots) if (lot.IsParking && !lot.Skipped)
                { var d = plan.World(lot.LocalPosition) - player; d.y = 0; if (d.sqrMagnitude <= range * range) count++; break; }
            }
            return count;
        }
        internal void PatrolBases(List<Patrols.TrafficBase> into)
        {
            foreach (var record in _records.Values)
            {
                var plan = record.Plan;
                if (!plan.HasTown || plan.Condition != TownCondition.Inhabited) continue;
                foreach (var lot in plan.Lots) if (lot.IsParking && !lot.Skipped)
                { into.Add(new Patrols.TrafficBase { id = plan.Key, name = plan.Name, faction = plan.Faction.ToString(),
                    center = plan.World(lot.LocalPosition), apron = plan.World(lot.LocalPosition), gate = plan.World(lot.LocalPosition), size = new Vector3(24, 0, 40) }); break; }
            }
        }
        internal bool ParkingOutside(Vector3 world, out Vector3 parking)
        {
            parking = world;
            foreach (var record in _records.Values) if (record.Plan.HasTown)
            {
                var plan = record.Plan; var p = world - plan.WorldCenter; p.y = 0;
                if (p.sqrMagnitude > plan.Radius * plan.Radius) continue;
                var frame = TownPerimeter.Frame(plan);
                if (!frame.Contains(p)) continue;
                foreach (var lot in plan.Lots) if (lot.IsParking && !lot.Skipped) { parking = plan.World(lot.LocalPosition); return true; }
            }
            return false;
        }
        internal void TravelBlocks(List<Bounds> into)
        {
            foreach (var record in _records.Values) if (record.Plan.HasTown)
            { var plan = record.Plan; var frame = TownPerimeter.Frame(plan); frame.center += new Vector3(plan.WorldCenter.x, 0, plan.WorldCenter.z);
                var size = frame.size; size.y = 10000; frame.size = size; frame.Expand(new Vector3(28, 0, 28)); into.Add(frame); }
        }
        public bool SetCondition(string key, TownCondition condition)
        {
            TownRecord record;
            if (!_records.TryGetValue(key, out record) || !Enum.IsDefined(typeof(TownCondition), condition)) return false;
            record.Plan.Condition = condition;
            Log.Always("[Towns] condition " + key + " = " + condition + " (persists with next world save)"); return true;
        }
        internal bool VehicleWaypoint(Vector3 position, Vector3 goal, out Vector3 waypoint, out bool hold)
        {
            Vector3 parking;
            bool redirected = ParkingOutside(Patrols.TrafficSpace.World(goal), out parking);
            if (redirected) goal = Patrols.TrafficSpace.Scene(parking);
            waypoint = goal; hold = false;
            foreach (var live in _sites)
            {
                var site = live.Site; if (site.Anchor == null || site.Walls == null) continue;
                Vector3 next;
                if (!site.Walls.Vehicles.Waypoint(site.Anchor.InverseTransformPoint(position) - site.Plan.WorldCenter,
                    site.Anchor.InverseTransformPoint(goal) - site.Plan.WorldCenter, out next, out hold)) continue;
                waypoint = site.Ground(site.Anchor.TransformPoint(site.Plan.WorldCenter + next)); return true;
            }
            return redirected;
        }
    }
}
