using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    internal static class TownStore
    {
        private const int Magic = 0x5757544E, Version = 6;
        internal static byte[] Serialize(Dictionary<string, TownRecord> records)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic); w.Write(Version); w.Write(TownPlan.GeneratorVersion); w.Write(records.Count);
                foreach (var record in records.Values)
                {
                    var p = record.Plan;
                    w.Write(p.Key); w.Write(p.HasTown); w.Write((int)p.Condition); V(w, p.WorldCenter);
                    w.Write(p.Yaw); w.Write(p.Radius); w.Write(p.Feather); w.Write(p.StreetWidth);
                    w.Write(p.OriginalRelief); w.Write(p.MaximumEarthwork); w.Write(p.Decision ?? "");
                    w.Write(p.LayoutVersion); w.Write((int)p.Faction); w.Write(p.Name ?? ""); w.Write(p.Discovered); w.Write(p.MainNodes);
                    w.Write(p.FactionMembers); w.Write(p.PopulationMeasured); w.Write(p.MerchantSeeded);
                    w.Write(p.Lots.Length);
                    foreach (var lot in p.Lots)
                    {
                        w.Write(lot.Prefab); V(w, lot.LocalPosition); w.Write(lot.Yaw); w.Write((int)lot.Role);
                        V(w, lot.BoundsCenter); w.Write(lot.HalfX); w.Write(lot.HalfZ); w.Write(lot.Street);
                        V(w, lot.Entry); w.Write(lot.HasEntry); w.Write(lot.EntryNode); w.Write(lot.Actors); w.Write(lot.Loot); w.Write(lot.Vehicles);
                        w.Write(lot.Hounds);
                        w.Write(lot.Skipped);
                    }
                    w.Write(p.StreetNodes.Length); foreach (var node in p.StreetNodes) V(w, node);
                    w.Write(p.StreetEdges.Length); foreach (int edge in p.StreetEdges) w.Write(edge);
                    w.Write(p.PathsVersion); w.Write(p.PathWidth); w.Write(p.AccessWidth);
                    w.Write(p.AccessPath.Length); foreach (var point in p.AccessPath) V(w, point);
                    w.Write(p.GradeAccess);
                    w.Write(record.Spawners.Count);
                    foreach (var lot in record.Spawners)
                    {
                        w.Write(lot.Key); w.Write(lot.Value.Length);
                        foreach (var spawn in lot.Value) { w.Write(spawn.Path); w.Write(spawn.OnOff); w.Write(spawn.Number); w.Write(spawn.NumberEnd); }
                    }
                }
                w.Flush(); return stream.ToArray();
            }
        }
        internal static Dictionary<string, TownRecord> Read(string path)
        { return File.Exists(path) ? Deserialize(File.ReadAllBytes(path)) : new Dictionary<string, TownRecord>(StringComparer.Ordinal); }
        internal static Dictionary<string, TownRecord> Deserialize(byte[] bytes)
        {
            var records = new Dictionary<string, TownRecord>(StringComparer.Ordinal);
            using (var r = new BinaryReader(new MemoryStream(bytes, false)))
            {
                int magic = r.ReadInt32(), version = r.ReadInt32(), generator = r.ReadInt32();
                if (magic != Magic || version < 1 || version > Version || generator < 1 || generator > TownPlan.GeneratorVersion)
                    throw new InvalidDataException("Unsupported Towns save/generator version; preserve the file and matching DLL.");
                int count = Count(r, 100000);
                for (int i = 0; i < count; i++)
                {
                    var p = new TownPlan { Key = r.ReadString(), HasTown = r.ReadBoolean(), Condition = (TownCondition)r.ReadInt32(),
                        WorldCenter = V(r), Yaw = F(r), Radius = F(r), Feather = F(r), StreetWidth = F(r),
                        OriginalRelief = F(r), MaximumEarthwork = F(r), Decision = r.ReadString() };
                    if (!Enum.IsDefined(typeof(TownCondition), p.Condition)) throw new InvalidDataException("Town condition");
                    p.LayoutVersion = version >= 2 ? r.ReadInt32() : 1;
                    if (version >= 2)
                    {
                        p.Faction = (TownFaction)r.ReadInt32(); p.Name = r.ReadString(); p.Discovered = r.ReadBoolean(); p.MainNodes = r.ReadInt32();
                        if (!Enum.IsDefined(typeof(TownFaction), p.Faction) || p.LayoutVersion < 1 || p.LayoutVersion > 8) throw new InvalidDataException("Town population/layout");
                        if (version >= 6) { p.FactionMembers = Count(r, 100000); p.PopulationMeasured = r.ReadBoolean(); p.MerchantSeeded = r.ReadBoolean(); }
                    }
                    p.Lots = new TownLot[Count(r, 32)];
                    for (int j = 0; j < p.Lots.Length; j++)
                    {
                        var lot = new TownLot { Prefab = r.ReadString(), LocalPosition = V(r), Yaw = F(r), Role = (TownRole)r.ReadInt32(), BoundsCenter = V(r),
                            HalfX = F(r), HalfZ = F(r), Street = r.ReadInt32() };
                        if (version >= 2)
                        {
                            lot.Entry = V(r); lot.HasEntry = r.ReadBoolean(); lot.EntryNode = r.ReadInt32();
                            lot.Actors = Count(r, 320); lot.Loot = Count(r, 320); lot.Vehicles = Count(r, 1);
                            lot.Hounds = r.ReadBoolean();
                            if (version >= 5) lot.Skipped = r.ReadBoolean();
                        }
                        p.Lots[j] = lot;
                    }
                    if (version == 1) TownPopulation.Legacy(p);
                    TownPopulation.Classify(p); // Saved faction defines the settlement even before its residents have spawned.
                    p.StreetNodes = new Vector3[Count(r, 2048)]; for (int j = 0; j < p.StreetNodes.Length; j++) p.StreetNodes[j] = V(r);
                    p.StreetEdges = new int[Count(r, 8192)];
                    for (int j = 0; j < p.StreetEdges.Length; j++)
                    { int e = r.ReadInt32(); if (e < 0 || e >= p.StreetNodes.Length) throw new InvalidDataException("Street index"); p.StreetEdges[j] = e; }
                    if ((p.StreetEdges.Length & 1) != 0) throw new InvalidDataException("Street edge pairs");
                    if (version >= 3)
                    {
                        p.PathsVersion = Count(r, 7); p.PathWidth = F(r); p.AccessWidth = F(r);
                        p.AccessPath = new Vector3[Count(r, 2048)]; for (int j = 0; j < p.AccessPath.Length; j++) p.AccessPath[j] = V(r);
                        if (version >= 4) p.GradeAccess = r.ReadBoolean();
                        if (p.PathsVersion == 1 && (p.PathWidth < 1 || p.PathWidth > 9 || p.AccessWidth < 1 || p.AccessWidth > 12 || p.AccessPath.Length < 2))
                            throw new InvalidDataException("Town path surface/connection");
                    }
                    if (p.MainNodes < 0 || p.MainNodes > p.StreetNodes.Length) throw new InvalidDataException("Main street length");
                    foreach (var lot in p.Lots)
                        if (p.HasTown && (lot.Street < 0 || lot.Street >= p.StreetNodes.Length || lot.HasEntry && (lot.EntryNode < 0 || lot.EntryNode >= p.StreetNodes.Length)))
                            throw new InvalidDataException("Lot entrance/street index");
                    var record = new TownRecord { Plan = p }; int lotCount = Count(r, 32);
                    for (int j = 0; j < lotCount; j++)
                    {
                        int lot = r.ReadInt32(); if (lot < 0 || lot >= p.Lots.Length) throw new InvalidDataException("Lot index");
                        var states = new SpawnerState[Count(r, 128)];
                        for (int k = 0; k < states.Length; k++) states[k] = new SpawnerState { Path = r.ReadString(), OnOff = r.ReadInt32(), Number = r.ReadInt32(), NumberEnd = r.ReadInt32() };
                        record.Spawners.Add(lot, states);
                    }
                    records.Add(p.Key, record);
                }
                if (r.BaseStream.Position != r.BaseStream.Length) throw new InvalidDataException("Trailing Towns save data");
            }
            return records;
        }
        private static int Count(BinaryReader r, int max)
        { int n = r.ReadInt32(); if (n < 0 || n > max) throw new InvalidDataException("Invalid Towns record length"); return n; }
        private static float F(BinaryReader r)
        { float f = r.ReadSingle(); if (float.IsNaN(f) || float.IsInfinity(f)) throw new InvalidDataException("Non-finite Towns value"); return f; }
        private static void V(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        private static Vector3 V(BinaryReader r) { return new Vector3(F(r), F(r), F(r)); }
    }
}
