using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    [Serializable] public sealed class PatrolRiderData
    {
        public string prefab = "";
        public float health = -1f;
        public float[] position;
        public bool standing;
    }

    // Native ES3 owns vehicles, fitted/missing parts, condition, fluids and existing items. Only unsaved seated bodies need a delta.
    internal static class PatrolCrewDelta
    {
        private const int Magic = 0x32524457, FirstMagic = 0x31524457; // WDR2: hitch identity; WDR1: seated riders
        internal static PatrolRiderData[] Capture(PatrolMarker marker)
        {
            var result = new PatrolRiderData[PatrolMarker.TotalTurretSlots];
            for (int i = 0; i < result.Length; i++)
            {
                var rider = marker.Riders[i]; var anchor = marker.RiderAnchors[i];
                var guard = rider != null ? rider.GetComponent<RiderGuard>() : null;
                if (rider == null || anchor == null || guard == null || !guard.Mounted || Patrol.GetHealth(rider) == 0f) continue;
                var p = anchor.localPosition;
                result[i] = new PatrolRiderData { prefab = CrewAppearance.Identity(rider, TemplateExporter.PrefabName(rider.name.Replace("(Rider)", ""))),
                    health = Patrol.GetHealth(rider), position = new[] { p.x, p.y, p.z }, standing = guard.StandingPose };
            }
            return result;
        }
        internal static void Restore(GameObject car, PatrolMarker marker, PatrolRiderData[] riders)
        {
            if (riders == null || marker.Exploded) return;
            marker.Trailer = TrailerSupport.Attached(car);
            for (int i = 0; i < riders.Length && i < marker.Riders.Length; i++)
            {
                var saved = riders[i];
                if (saved == null || string.IsNullOrEmpty(saved.prefab) || saved.health == 0f || marker.Riders[i] != null) continue;
                if (i >= PatrolMarker.VehicleTurretSlots && marker.Trailer == null) continue;
                var rider = Rider.Seat(car, marker, i, saved.prefab, saved.position,
                    i < PatrolMarker.VehicleTurretSlots ? null : marker.Trailer, saved.standing, false);
                if (rider != null && saved.health >= 0f)
                { Patrol.SetHealth(rider, saved.health); rider.AddComponent<Patrol.LateHealth>().Value = saved.health; }
            }
        }
        internal static void RestoreHitch(GameObject car, PatrolCarData data, List<GameObject> roots)
        {
            if (data.trailerName == null) return;
            // Native TrailerLoad uses a scaled wait and a short spherecast, so it cannot resolve an articulated hitch during paused loading.
            // An empty saved identity must also suppress that search: a deliberately detached trailer stays detached.
            var load = Core.FsmUtil.Find(car, "TrailerLoad"); if (load != null) load.enabled = false;
            if (data.trailerName.Length == 0) return;
            foreach (var trailer in roots)
            {
                if (trailer == null || trailer.name != data.trailerName || !TrailerSupport.IsTrailer(trailer)) continue;
                try { TrailerSupport.Connect(car, trailer, true); PatrolsModule.Verbose("Load hitch restored: " + car.name + " -> " + trailer.name + "; native poses retained"); }
                catch (Exception e) { Core.Log.Warn("[Patrols] Saved hitch skipped for " + car.name + ": " + e.Message); }
                return;
            }
            Core.Log.Warn("[Patrols] Saved trailer not found; nothing rebuilt: " + data.trailerName);
        }
        internal static void Write(BinaryWriter writer, PatrolCarData[] cars)
        {
            writer.Write(Magic); writer.Write(cars.Length);
            foreach (var car in cars)
            {
                writer.Write(car.trailerName ?? "");
                var riders = car.riders; writer.Write(riders != null);
                if (riders == null) continue;
                for (int i = 0; i < PatrolMarker.TotalTurretSlots; i++)
                {
                    var rider = i < riders.Length ? riders[i] : null; writer.Write(rider != null);
                    if (rider == null) continue;
                    writer.Write(rider.prefab ?? ""); writer.Write(rider.health); writer.Write(rider.standing);
                    for (int j = 0; j < 3; j++) writer.Write(rider.position[j]);
                }
            }
        }
        internal static void Read(BinaryReader reader, PatrolCarData[] cars)
        {
            int magic = reader.ReadInt32(); if (magic != Magic && magic != FirstMagic) return;
            if (reader.ReadInt32() != cars.Length) throw new InvalidDataException("Invalid crew delta count");
            foreach (var car in cars)
            {
                if (magic == Magic)
                {
                    car.trailerName = reader.ReadString();
                    if (car.trailerName.Length > 4096) throw new InvalidDataException("Invalid trailer identity");
                }
                if (!reader.ReadBoolean()) continue;
                car.riders = new PatrolRiderData[PatrolMarker.TotalTurretSlots];
                for (int i = 0; i < car.riders.Length; i++)
                {
                    if (!reader.ReadBoolean()) continue;
                    var rider = new PatrolRiderData { prefab = reader.ReadString(), health = reader.ReadSingle(), standing = reader.ReadBoolean(), position = new float[3] };
                    if (rider.prefab.Length > 256 || float.IsNaN(rider.health) || float.IsInfinity(rider.health)) throw new InvalidDataException("Invalid crew delta");
                    for (int j = 0; j < 3; j++)
                    {
                        rider.position[j] = reader.ReadSingle();
                        if (float.IsNaN(rider.position[j]) || float.IsInfinity(rider.position[j])) throw new InvalidDataException("Invalid rider position");
                    }
                    car.riders[i] = rider;
                }
            }
        }
    }
}
