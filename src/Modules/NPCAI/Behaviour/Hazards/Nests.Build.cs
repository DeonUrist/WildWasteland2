using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Nests part: building a nest in slices (hanging web spots, ground webs, layout memory).</summary>
    internal static partial class Nests
    {
        // ---------- building, sliced ----------
        /// <summary>0 centre, 1 spot search (rays), 2 choose, 3 hanging webs (one mesh per step), 4 ground webs, 5 ready</summary>
        private static void Build(Nest n, float now)
        {
            float budget = Mathf.Max(0.2f, NpcaiSettings.NestFrameBudgetMs.Value);
            var saved = UnityEngine.Random.state;
            if (!n.RndOn) { UnityEngine.Random.InitState(n.Seed); n.RndOn = true; } else UnityEngine.Random.state = n.Rnd;
            _sw.Reset(); _sw.Start();
            try
            {
                n.Hold = false;
                while (n.Phase < 5 && !n.Hold && _sw.Elapsed.TotalMilliseconds < budget) Step(n);
            }
            catch (Exception e) { NpcaiRuntime.Error("Nests: " + n.Name + ": " + e); n.Phase = 5; }
            finally { n.Rnd = UnityEngine.Random.state; UnityEngine.Random.state = saved; }
            _sw.Stop();
            float ms = (float)_sw.Elapsed.TotalMilliseconds;
            n.BuildMs += ms; n.Frames++; if (ms > n.MaxFrameMs) n.MaxFrameMs = ms;
            if (n.Phase >= 5 && !n.Logged)
            {
                n.Logged = true;
                if (n.FromCache && n.Hanging.Count + n.Ground.Count == 0 && n.Poi != null)
                {
                    // the remembered spots no longer hold (the place changed): forget them and build anew
                    _layouts.Remove(n.Key); SaveLayouts();
                    n.FromCache = false; n.Logged = false; n.Phase = 0; n.Relaxed = true;
                    return;
                }
                if (!n.FromCache) Remember(n);
                // the touch check and the seating reach every web of the nest (a cave's webs may lie farther than its spiders' spread)
                foreach (var w in n.Hanging) n.Radius = Mathf.Max(n.Radius, Flat(w.Center - n.C) + w.R + 2f);
                foreach (var w in n.Ground) n.Radius = Mathf.Max(n.Radius, Flat(w.Center - n.C) + w.R + 2f);
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Nests: " + n.Name + " is a spider nest: " + n.Hanging.Count + " hanging + " + n.Ground.Count + " ground webs, " + n.Spiders.Count + " spider(s), built in " + n.BuildMs.ToString("0.0") + " ms over " + n.Frames + " frames (at most " + n.MaxFrameMs.ToString("0.00") + " ms in one)");
                SeatAll(n);
                PublishBuilt(n);
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Nests: " + n.Name + ": " + n.Sitters.Count + " big spider(s) sit in its webs" + (n.SpiderSpots ? " (webs placed around the spiders' own spots)" : ""));
            }
        }

        /// <summary>One build step of a nest: candidate spots, hanging and ground webs, the layout check and remembering, then seating the sitters.</summary>
        private static void Step(Nest n)
        {
            switch (n.Phase)
            {
                case 0:
                {
                    // the centre: the place itself, unless its spiders are elsewhere in it (a deep cave) - then where they are, on a 2 m grid
                    Vector3 sum = Vector3.zero; int c = 0;
                    foreach (var a in n.Spiders) if (a != null && a.Owner != null) { sum += a.T.position; c++; }
                    Vector3 mean = c > 0 ? sum / c : n.Poi.position;
                    n.C = Flat(mean - n.Poi.position) <= 15f ? n.Poi.position : new Vector3(Mathf.Round(mean.x / 2f) * 2f, mean.y, Mathf.Round(mean.z / 2f) * 2f);
                    n.RefY = c > 0 ? mean.y : n.Poi.position.y;
                    float spread = 0f; foreach (var a in n.Spiders) if (a != null && a.Owner != null) spread = Mathf.Max(spread, Flat(a.T.position - n.C));
                    n.Radius = Mathf.Clamp(Mathf.Ceil((spread + 8f) / 5f) * 5f, 12f, NpcaiSettings.NestMaxRadius.Value);
                    // (1.7.4) built here before: the same webs again, at once
                    if (!n.Relaxed && Recall(n)) { n.FromCache = true; n.Phase = 3; n.Step = 0; break; }
                    // (1.7.2) a camp / cave / building NPCAI maps: its map gives the webs' spots - for a cave only the floor under its roof
                    bool known, baked;
                    bool cave = n.Cave;
                    Nav.FloorCells(n.Poi, cave, n.Floor, 1500, out known, out baked);
                    if (known && !baked)
                    {
                        // (1.7.4) the map is waited for 6 s at the most (it may be busy elsewhere); then the spiders' own surroundings decide
                        if (n.WaitSince <= 0f) n.WaitSince = Time.time;
                        if (Time.time - n.WaitSince < 6f)
                        {
                            if (!n.Waiting) { n.Waiting = true; if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Nests: " + n.Name + " waits for its map"); }
                            n.Hold = true; n.NextTry = Time.time + 1f;      // asked again in a second; other nests build meanwhile
                            return;
                        }
                        n.Floor.Clear();
                    }
                    if (n.Floor.Count == 0)
                    {
                        // (1.7.5) no map (a wreck, or the map was not ready): the floor the spiders stand on is the floor - they are already
                        // where the place has room, often under its roof
                        foreach (var a in n.Spiders)
                        {
                            RaycastHit sg;
                            if (a != null && a.Owner != null && Physics.Raycast(a.T.position + Vector3.up * 0.5f, Vector3.down, out sg, 3f, GroundMask, QueryTriggerInteraction.Ignore)) n.Floor.Add(sg.point);
                        }
                        n.SpiderSpots = n.Floor.Count > 0;
                    }
                    else
                    {
                        // the map decides where webs go: keep the floor points within reach of the middle (a cave: its whole inside, up to 2 x MaxRadius)
                        float lim = cave ? NpcaiSettings.NestMaxRadius.Value * 2f : n.Radius;
                        n.Floor.RemoveAll(f => Flat(f - n.C) > lim);
                    }
                    int big = 0; foreach (var a in n.Spiders) if (CanSit(a)) big++;
                    n.Want = Mathf.Clamp(big + UnityEngine.Random.Range(1, 4), NpcaiSettings.NestMinWebs.Value, Mathf.Max(NpcaiSettings.NestMinWebs.Value, NpcaiSettings.NestMaxWebs.Value));
                    n.GroundWant = UnityEngine.Random.Range(NpcaiSettings.NestGroundMin.Value, Mathf.Max(NpcaiSettings.NestGroundMin.Value, NpcaiSettings.NestGroundMax.Value) + 1);
                    n.Phase = 1; n.Step = 0;
                    break;
                }
                case 1:
                {
                    // one candidate spot: a point 0.9-2.6 m above a floor, free itself, with surfaces around it in a vertical plane (8 rays, 2 planes)
                    if (n.Step++ >= NpcaiSettings.NestSpots.Value) { n.Phase = 2; break; }
                    float h = UnityEngine.Random.Range(0.9f, 2.6f), yaw = UnityEngine.Random.Range(0f, 180f);
                    Vector3 floor;
                    if (n.SpiderSpots)
                    {
                        // a spider's spot, or up to 3 m from it with nothing solid in between
                        Vector3 sp = n.Floor[UnityEngine.Random.Range(0, n.Floor.Count)];
                        Vector2 j = UnityEngine.Random.insideUnitCircle * 3f;
                        Vector3 a0 = sp + Vector3.up * 1f, a1 = a0 + new Vector3(j.x, 0f, j.y);
                        RaycastHit jg;
                        if (!Physics.Linecast(a0, a1, Solid, QueryTriggerInteraction.Ignore) && Physics.Raycast(a1, Vector3.down, out jg, 3f, GroundMask, QueryTriggerInteraction.Ignore)) floor = jg.point;
                        else floor = sp;
                    }
                    else if (n.Floor.Count > 0) floor = n.Floor[UnityEngine.Random.Range(0, n.Floor.Count)];
                    else if (n.Cave) { if (!SpiderFloor(n, 10f, out floor)) break; }
                    else
                    {
                        Vector2 d = UnityEngine.Random.insideUnitCircle * n.Radius;
                        RaycastHit g;
                        Vector3 top = new Vector3(n.C.x + d.x, n.RefY + 4f, n.C.z + d.y);
                        if (!Physics.Raycast(top, Vector3.down, out g, 12f, GroundMask, QueryTriggerInteraction.Ignore)) break;
                        floor = g.point;
                    }
                    Vector3 hub = floor + Vector3.up * h;
                    if (n.Floor.Count > 0 && Physics.Raycast(floor + Vector3.up * 0.1f, Vector3.up, h, Solid, QueryTriggerInteraction.Ignore)) { h *= 0.6f; if (h < 0.7f || Physics.Raycast(floor + Vector3.up * 0.1f, Vector3.up, h + 0.3f, Solid, QueryTriggerInteraction.Ignore)) break; }   // a low roof: lower, or no room
                    if (Physics.CheckSphere(hub, 0.3f, Solid, QueryTriggerInteraction.Ignore)) break;
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 nrm = Quaternion.Euler(0f, yaw + k * 90f, 0f) * Vector3.forward;
                        float gap, mean;
                        int hits = Webs.Probe(hub, nrm, out gap, out mean);
                        if (n.Relaxed ? (hits < 3 || gap > 170f || mean < 0.4f || mean > 2.5f) : (hits < 4 || gap > 135f || mean < 0.6f || mean > 2.4f)) continue;
                        // (1.7.3) enclosed spots first: under a roof / inside a shelter, the share of open sky above counts against it
                        float sky = Webs.SkyOpen(hub);
                        n.Cands.Add(new Cand { Hub = hub, N = nrm, Score = hits + mean * 0.8f - gap / 180f + (1f - sky) * 4f });
                        break;
                    }
                    break;
                }
                case 2:
                {
                    if (n.Cands.Count == 0 && !n.Relaxed) { n.Relaxed = true; n.Phase = 1; n.Step = 0; if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Nests: " + n.Name + ": no spot for a hanging web, looking again less strictly"); break; }   // (1.7.4)
                    n.Cands.Sort((a, b) => b.Score.CompareTo(a.Score));
                    n.Chosen = new List<Cand>();
                    foreach (var c in n.Cands)
                    {
                        if (n.Chosen.Count >= n.Want) break;
                        bool close = false; foreach (var o in n.Chosen) if ((o.Hub - c.Hub).sqrMagnitude < 3.5f * 3.5f) { close = true; break; }
                        if (!close) n.Chosen.Add(c);
                    }
                    n.Cands.Clear(); n.Phase = 3; n.Step = 0;
                    break;
                }
                case 3:
                {
                    if (n.Step >= n.Chosen.Count) { n.Phase = 4; n.Step = 0; n.Tries = 0; break; }
                    var c = n.Chosen[n.Step++];
                    var w = Webs.Orb(c.Hub, c.N);
                    if (w != null) { w.Go.name = "NPCAI_Nest_" + n.Name; n.Hanging.Add(w); }
                    break;
                }
                case 4:
                {
                    if (n.FromCache)
                    {
                        if (n.Step >= n.CachedGround.Count) { n.Phase = 5; break; }
                        var cw = Webs.Sheet(n.CachedGround[n.Step++]);
                        if (cw != null) { cw.Go.name = "NPCAI_Nest_" + n.Name; n.Ground.Add(cw); }
                        break;
                    }
                    if (n.Ground.Count >= n.GroundWant || n.Tries++ >= n.GroundWant * 5)
                    {
                        // (1.7.4) never a nest without webs: whatever is missing goes on the floor under its spiders (one per step)
                        if (n.Ground.Count + n.Hanging.Count < 3 && n.Forced < n.Spiders.Count && n.Forced < 4)
                        {
                            var sp = n.Spiders[n.Forced++];
                            RaycastHit fg;
                            if (sp != null && sp.Owner != null && Physics.Raycast(sp.T.position + Vector3.up * 0.5f, Vector3.down, out fg, 3f, GroundMask, QueryTriggerInteraction.Ignore))
                            {
                                bool near = false; foreach (var o in n.Ground) if ((o.Center - fg.point).sqrMagnitude < 2f * 2f) { near = true; break; }
                                if (!near) { var fw = Webs.Sheet(fg.point); if (fw != null) { fw.Go.name = "NPCAI_Nest_" + n.Name; n.Ground.Add(fw); } }
                            }
                            break;
                        }
                        n.Phase = 5; break;
                    }
                    RaycastHit g;
                    Vector3 at;
                    if (n.Cave && n.Floor.Count == 0)
                    {
                        Vector3 sf; if (!SpiderFloor(n, 10f, out sf)) break;
                        at = sf;
                    }
                    else if (n.Floor.Count > 0)
                    {
                        Vector3 f = n.Floor[UnityEngine.Random.Range(0, n.Floor.Count)];
                        if (!Physics.Raycast(f + Vector3.up * 0.5f, Vector3.down, out g, 1.5f, GroundMask, QueryTriggerInteraction.Ignore) || g.normal.y < 0.75f) break;
                        at = g.point;
                    }
                    else
                    {
                        Vector2 d = UnityEngine.Random.insideUnitCircle * n.Radius * 0.9f;
                        if (!Physics.Raycast(new Vector3(n.C.x + d.x, n.RefY + 4f, n.C.z + d.y), Vector3.down, out g, 12f, GroundMask, QueryTriggerInteraction.Ignore) || g.normal.y < 0.75f) break;
                        at = g.point;
                    }
                    foreach (var o in n.Ground) if ((o.Center - at).sqrMagnitude < 3f * 3f) return;
                    // (1.7.3) enclosed ground first: in the open only after 3 tries per web found nothing sheltered
                    if (n.Tries <= n.GroundWant * 3 && Webs.SkyOpen(at + Vector3.up * 0.3f) > 0.6f) break;
                    var w = Webs.Sheet(at);
                    if (w != null) { w.Go.name = "NPCAI_Nest_" + n.Name; n.Ground.Add(w); }
                    break;
                }
            }
        }

        /// <summary>(1.7.4) a floor point in the same open space as one of the nest's spiders (within r m, nothing solid between): inside a cave even without its map - never in the rock between its walls, never outside</summary>
        private static bool SpiderFloor(Nest n, float r, out Vector3 floor)
        {
            floor = Vector3.zero;
            if (n.Spiders.Count == 0) return false;
            var a = n.Spiders[UnityEngine.Random.Range(0, n.Spiders.Count)];
            if (a == null || a.Owner == null) return false;
            Vector3 from = a.T.position + Vector3.up * 1.2f;
            Vector2 d = UnityEngine.Random.insideUnitCircle * r;
            Vector3 to = from + new Vector3(d.x, 0f, d.y);
            if (Physics.Linecast(from, to, Solid, QueryTriggerInteraction.Ignore)) return false;
            RaycastHit g;
            if (!Physics.Raycast(to, Vector3.down, out g, 5f, GroundMask, QueryTriggerInteraction.Ignore)) return false;
            if (!Physics.Raycast(g.point + Vector3.up * 0.2f, Vector3.up, 40f, Solid, QueryTriggerInteraction.Ignore)) return false;   // a cave: rock above
            floor = g.point; return true;
        }

    }
}
