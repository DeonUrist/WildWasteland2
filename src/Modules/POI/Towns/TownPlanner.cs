using System;
using System.Collections.Generic;
using Den.Tools.Matrices;
using MapMagic.Products;
using UnityEngine;

namespace WildWasteland.Modules.POI
{
    internal static class TownPlanner
    {
        /// <summary>Hashes a stable identity for deterministic placement and progress draws.</summary>
        internal static int Hash(string s)
        { uint h = 2166136261; foreach (char c in s) h = unchecked((h ^ c) * 16777619); return (int)(h & 0x7fffffff); }
        /// <summary>Draws a reproducible floating-point value between the supplied limits.</summary>
        private static float Range(System.Random r, float min, float max) { return min + (float)r.NextDouble() * (max - min); }

        /// <summary>Builds deterministic numeric planning data from the supplied geometry and seed.</summary>
        internal static TownPlan Make(GenerationInput input, TileData data, MatrixWorld heights,
            List<Vector3> avoid, TownWorldRoads roads, StopToken stop)
        {
            if (input.Saved != null)
            {
                var saved = input.Saved;
                if (WorldGeneration.Present) { saved = saved.CopyPaths(); saved.AccessPath = new Vector3[0]; saved.GradeAccess = false; saved.Feather = 0; saved.MaximumEarthwork = 0; }
                return saved;
            }
            var plan = new TownPlan { Key = input.Key, Decision = "chance", Feather = WorldGeneration.Present ? 0 : input.Feather, StreetWidth = 1.5f };
            var random = new System.Random(Hash(input.Key + (WorldGeneration.Present ? "|" + WorldGeneration.RoadIdentity : "")));
            if (input.ExcludeStartingChunk) { plan.Decision = "starting chunk excluded"; return plan; }
            if (random.Next(100) >= input.Chance) return plan;
            int count = random.Next(input.Min, input.Max + 1);
            plan.Condition = random.Next(100) < input.Inhabited ? TownCondition.Inhabited : TownCondition.Ruined;
            plan.Faction = plan.Condition == TownCondition.Ruined ? (random.Next(2) == 0 ? TownFaction.Animals : TownFaction.Mutants)
                : (random.Next(2) == 0 ? TownFaction.Scrapyard : TownFaction.Coyotes);
            var templates = new List<TownTemplate>(); int weight = 0;
            if (input.Templates != null) foreach (var t in input.Templates) if (t.Faction == plan.Faction && t.Weight > 0) { templates.Add(t); weight += t.Weight; }
            if (weight == 0) { plan.Decision = "skipped: no enabled faction template"; return plan; }
            int choice = random.Next(weight); TownTemplate template = templates[0];
            foreach (var t in templates) { choice -= t.Weight; if (choice < 0) { template = t; break; } }
            plan.TemplateId = template.Id; plan.TemplateJson = Core.Json.Write(template.Data); plan.Rules = TownTemplate.Parse(template.Id, plan.TemplateJson); if (!template.UseGlobalLotRange) count = random.Next(template.MinLots, template.MaxLots + 1);
            if (input.ParkingCompanions) count = Math.Min(count, 32 - template.ExternalParkingPois.Length);
            if (template.AllowedPois.Length > 0) { var allowed = new HashSet<string>(template.AllowedPois, StringComparer.Ordinal); var specs = new List<BuildingSpec>(); foreach (var spec in input.Buildings) if (allowed.Contains(spec.Name) || Array.IndexOf(template.ExternalParkingPois,spec.Name)>=0) specs.Add(spec); input.Buildings = specs.ToArray(); }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (stop != null && stop.stop) return null;
                plan.Radius = 0; plan.Yaw = Range(random, 0, 360);
                BuildLayout(plan, input, random, count);
                AddParkingCompanions(plan,input);
                if (template.RequireMerchant && !Array.Exists(plan.Lots, lot => lot.IsMerchant)) { plan.Decision = "skipped: required merchant unavailable"; plan.Lots = new TownLot[0]; return plan; }
                if (plan.Lots.Length == 0) break;
                var sites = TownSiting.Find(plan, input, data, heights, avoid, roads, random, stop);
                if (sites.Count == 0) continue;
                var site = sites[0]; plan.WorldCenter = site.Center; plan.OriginalRelief = site.Relief; plan.MaximumEarthwork = WorldGeneration.Present ? 0 : site.Relief * .5f;
                TownWalkGraph.Build(plan, input.Buildings, stop);
                TownParking.Access(plan, heights, data.globals.height, avoid);
                plan.HasTown = true; plan.Decision = "accepted compact site; attempt " + (attempt + 1) + (WorldGeneration.Present ? "; planned foundation, no access roads" : "; no internal road surfaces");
                TownPopulation.Assign(plan, input, random); return plan;
            }
            plan.Lots = new TownLot[0]; plan.HasTown = false;
            plan.Decision = "skipped: no compact site after two attempts";
            return plan;
        }
        /// <summary>Builds the compact lot layout from measured frontages and template rules.</summary>
        private static void BuildLayout(TownPlan plan, GenerationInput input, System.Random random, int count)
        {
            var lots = new List<TownLot>(); var specs = new List<BuildingSpec>(); var sides = new List<int>();
            float[] cursor = { 0, 0 }; bool crane = false, big = false; int wrecks = 0, camps = 0;
            var frontiers = new[] { new Dictionary<int, float>(), new Dictionary<int, float>() };
            for (int i = 0; i < count; i++)
            {
                if (i == 2)
                {
                    lots.Add(new TownLot { Prefab = TownParking.Spec.Name, Yaw = 90, Entry = TownParking.Spec.Entry, HasEntry = true,
                        HalfX = 12, HalfZ = 20, Role = TownRole.ParkingLot });
                    specs.Add(TownParking.Spec); sides.Add(-1); continue;
                }
                bool wantWreck = wrecks < 2 && (i == 3 || count >= 8 && i == count - 1);
                var choices = new List<BuildingSpec>();
                // (WW) camps mix in (at most two per town, one of them large)
                foreach (var candidate in input.Buildings) if (!candidate.Merchant && Array.IndexOf(plan.Rules.ExternalParkingPois,candidate.Name)<0 && (!crane || !candidate.Crane) && candidate.Wreck == wantWreck && Fits(candidate, big, camps)) choices.Add(candidate);
                if (choices.Count == 0) foreach (var candidate in input.Buildings) if (!candidate.Merchant && Array.IndexOf(plan.Rules.ExternalParkingPois,candidate.Name)<0 && (!crane || !candidate.Crane) && !candidate.Wreck && Fits(candidate, big, camps)) choices.Add(candidate);
                if (i != 2 && choices.Count == 0) break;
                var spec = i == 2 ? TownParking.Spec : choices[random.Next(choices.Count)];
                // (WW) a Coyotes town ALWAYS has a merchant lot when any merchant camp is in the catalog
                if (i == 0 && plan.Rules.RequireMerchant)
                {
                    var merchants = new List<BuildingSpec>(); foreach (var candidate in input.Buildings) if (candidate.Merchant) merchants.Add(candidate);
                    if (merchants.Count > 0) spec = merchants[random.Next(merchants.Count)];
                }
                crane |= spec.Crane; if (spec.Wreck) wrecks++; if (spec.Camp) camps++; big |= spec.Camp && spec.Big;
                int row = i < 2 ? i : cursor[0] <= cursor[1] ? 0 : 1, side = row == 0 ? -1 : 1;
                int tilt = i == 2 ? 0 : random.Next(3) - 1;
                float yaw = (row == 0 ? 0 : 180) - Mathf.Atan2(spec.Outward.x, spec.Outward.z) * Mathf.Rad2Deg + tilt * 9;
                var rotation = Quaternion.Euler(0, yaw, 0);
                var frontage = tilt == 0 ? spec.Frontages[row] : spec.TiltedFrontages[row * 2 + (tilt < 0 ? 0 : 1)];
                float setback = side * (1.1f + frontage.Protrusion + Range(random, .25f, 1.1f));
                float gap = Range(random, .65f, 1.35f) * Mathf.Clamp(input.Gap * .5f, .75f, 1.25f);
                int bandOffset = Mathf.FloorToInt(setback); float x = 0, packed = float.NegativeInfinity;
                foreach (var band in frontage.Min)
                { float end; if (frontiers[row].TryGetValue(band.Key + bandOffset, out end)) packed = Mathf.Max(packed, end + gap - band.Value); }
                if (!float.IsNegativeInfinity(packed)) x = packed;
                else if (frontiers[row].Count > 0) x = cursor[row] + gap + spec.HalfX;
                foreach (var band in frontage.Max)
                { int key = band.Key + bandOffset; float old, end = x + band.Value; if (!frontiers[row].TryGetValue(key, out old) || end > old) frontiers[row][key] = end; }
                float radians = yaw * Mathf.Deg2Rad;
                float extent = Mathf.Abs(Mathf.Cos(radians)) * spec.HalfX + Mathf.Abs(Mathf.Sin(radians)) * spec.HalfZ;
                var entrance = rotation * spec.Entry; var center = rotation * spec.Center;
                cursor[row] = Mathf.Max(cursor[row], x + center.x - entrance.x + extent);
                lots.Add(new TownLot { Prefab = spec.Name, Yaw = yaw, Entry = spec.Entry, HasEntry = true,
                    LocalPosition = new Vector3(x, 0, setback) - entrance, BoundsCenter = spec.Center, HalfX = spec.HalfX, HalfZ = spec.HalfZ,
                    Role = i == 0 ? TownRole.Shop : i == 1 ? TownRole.GasStation : i == 2 ? TownRole.ParkingLot : TownRole.Residence });
                specs.Add(spec); sides.Add(side);
            }
            for (int i = 0; i < lots.Count; i++)
            {
                if (lots[i].IsParking) continue;
                lots[i].LocalPosition -= Vector3.right * cursor[sides[i] < 0 ? 0 : 1] * .5f;
                var entry = Entrance(lots[i]); var position = lots[i].LocalPosition;
                position.z += .6f * Mathf.Sin(entry.x * .07f);
                // Reserve a small central wreck garden with narrow walking space around its edges.
                if (Mathf.Abs(entry.x) < specs[i].HalfX + 8 && lots.Count >= 5) position.z += sides[i] * 5;
                lots[i].LocalPosition = position;
                var r = Quaternion.Euler(0, lots[i].Yaw, 0);
                for (int c = 0; c < 4; c++)
                {
                    var p = position + r * (specs[i].Center + new Vector3((c & 1) == 0 ? -specs[i].HalfX : specs[i].HalfX, 0, (c & 2) == 0 ? -specs[i].HalfZ : specs[i].HalfZ));
                    plan.Radius = Mathf.Max(plan.Radius, new Vector2(p.x, p.z).magnitude + 3);
                }
            }
            // The open vehicle apron does not consume a position in the dense pedestrian rows.
            // Its outward (+X) half is open to the world, so a truck never crosses the town to park.
            foreach (var lot in lots) if (lot.IsParking)
            {
                lot.LocalPosition = new Vector3(plan.Radius + 30, 0, 0);
                plan.Radius = Mathf.Max(plan.Radius, lot.LocalPosition.x + 24);
            }
            plan.Lots = lots.ToArray();
            TownParking.Outside(plan);
        }
        /// <summary>Appends independently persisted exterior companion lots from the saved template rules.</summary>
        private static void AddParkingCompanions(TownPlan plan,GenerationInput input)
        {if(!input.ParkingCompanions||plan.Rules.ExternalParkingPois.Length==0)return;TownLot parking=null;foreach(var l in plan.Lots)if(l.IsParking){parking=l;break;}if(parking==null)return;var lots=new List<TownLot>(plan.Lots);float advance=0;var facing=Quaternion.Euler(0,parking.Yaw,0);foreach(string name in plan.Rules.ExternalParkingPois){BuildingSpec spec=null;foreach(var b in input.Buildings)if(b.Name==name){spec=b;break;}if(spec==null)throw new InvalidOperationException("External parking POI unavailable: "+name);if(lots.Count>=32)throw new InvalidOperationException("Town template exceeds 32 lots including parking companions");float yaw=parking.Yaw+180;var rot=Quaternion.Euler(0,yaw,0);var center=parking.LocalPosition+facing*(Vector3.left*(parking.HalfX+2+spec.HalfX)+Vector3.forward*advance);var lot=new TownLot{Prefab=name,External=true,Yaw=yaw,LocalPosition=center-rot*new Vector3(spec.Center.x,0,spec.Center.z),BoundsCenter=spec.Center,HalfX=spec.HalfX,HalfZ=spec.HalfZ,Entry=spec.Entry,HasEntry=true,Role=TownRole.GasStation};lots.Add(lot);advance+=spec.HalfZ*2+4;plan.Radius=Mathf.Max(plan.Radius,lot.LocalPosition.magnitude+Mathf.Sqrt(spec.HalfX*spec.HalfX+spec.HalfZ*spec.HalfZ)+3);}plan.Lots=lots.ToArray();}
        /// <summary>Checks the large-building and camp-count constraints of a candidate.</summary>
        private static bool Fits(BuildingSpec c, bool big, int camps) { return !c.Camp || camps < 2 && (!big || !c.Big); }
        /// <summary>Computes the measured lot entrance in the town-local frame.</summary>
        internal static Vector3 Entrance(TownLot lot)
        { return lot.LocalPosition + Quaternion.Euler(0, lot.Yaw, 0) * lot.Entry; }
        /// <summary>Measures numeric geometry without passing Unity objects to terrain workers.</summary>
        internal static void Measure(MatrixWorld h, float scale, float x, float z, float radius,
            out float lo, out float hi, out float grade)
        {
            lo = float.PositiveInfinity; hi = float.NegativeInfinity; grade = 0;
            float sx = h.worldSize.x / (h.rect.size.x - 1), sz = h.worldSize.z / (h.rect.size.z - 1);
            int x0 = Mathf.Max(h.rect.offset.x, Mathf.FloorToInt((x - radius - h.worldPos.x) / sx) + h.rect.offset.x);
            int z0 = Mathf.Max(h.rect.offset.z, Mathf.FloorToInt((z - radius - h.worldPos.z) / sz) + h.rect.offset.z);
            int x1 = Mathf.Min(h.rect.offset.x + h.rect.size.x - 1, Mathf.CeilToInt((x + radius - h.worldPos.x) / sx) + h.rect.offset.x);
            int z1 = Mathf.Min(h.rect.offset.z + h.rect.size.z - 1, Mathf.CeilToInt((z + radius - h.worldPos.z) / sz) + h.rect.offset.z);
            for (int iz = z0; iz <= z1; iz++) for (int ix = x0; ix <= x1; ix++)
            {
                var p = h.PixelToWorld(ix, iz);
                if ((new Vector2(p.x - x, p.z - z)).sqrMagnitude > radius * radius) continue;
                int index = (iz - h.rect.offset.z) * h.rect.size.x + ix - h.rect.offset.x;
                float v = h.arr[index] * scale;
                lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v);
                if (ix < x1) grade = Mathf.Max(grade, Mathf.Abs(h.arr[index + 1] * scale - v) / sx);
                if (iz < z1) grade = Mathf.Max(grade, Mathf.Abs(h.arr[index + h.rect.size.x] * scale - v) / sz);
            }
        }
    }
}
