// Nav equivalence check + micro-benchmark for Wild Wasteland (see run.sh). Loads the BUILT WildWasteland.dll and the previous
// build (renamed WildWasteland_before.dll) side by side on plain mono, feeds both the same synthetic structure grids by reflection
// and compares: (i) full Build distances old vs new (whole array), the new lazy field against the old full field for every
// settled cell and after a forced full expansion, (ii) Nav.Next (result, next cell, path left, TraceInfo) old vs new, cold caches
// (every call alone) and warm (many NPCs, realistic think cadence, caches live). Exit code 1 on any mismatch.
// --patch <dir>: rewrites UnityEngine.CoreModule.dll in dir so Time.time reads a static field (Time.WW_FakeTime) and
// Object.GetInstanceID returns the fake cached pointer (both are engine icalls otherwise), and renames WildWasteland_before.dll's
// assembly. Nothing in the tested paths touches Physics / GameObjects for real (owners and roots are uninitialised objects).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

static class NavCheck
{
    static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--patch") { Patcher.Patch(args[1]); return 0; }
        if (args.Length >= 4 && args[0] == "--run") return Run(args[1], args[2], args[3], args.Length > 4 ? int.Parse(args[4]) : 6, args.Length > 5 ? int.Parse(args[5]) : 400);
        Console.WriteLine("usage: NavCheck --patch <dir> | --run <dir> <new.dll> <old.dll> [seeds] [pairsPerSeed]");
        return 2;
    }

    // ---------- the two assemblies ----------
    sealed class Side
    {
        public string Tag; public Assembly Asm; public Type Nav, Structure, Field, Npc;
        public MethodInfo Next, Build4, Build5, Expand, Components, TraceInfo, SceneLoaded, Add, Clear;
        public FieldInfo Structures, LastReason;
        public object StructList;
        public bool Lazy;

        public Side(string tag, string path, string dir)
        {
            Tag = tag;
            Asm = Assembly.LoadFrom(path);
            Nav = Asm.GetType("WildWasteland.Modules.NPC.Nav", true);
            Structure = Nav.GetNestedType("Structure", BindingFlags.NonPublic | BindingFlags.Public);
            Field = Nav.GetNestedType("Field", BindingFlags.NonPublic | BindingFlags.Public);
            Npc = Asm.GetType("WildWasteland.Modules.NPC.NpcModule", true);
            var all = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            Next = Nav.GetMethod("Next", all);
            foreach (var m in Nav.GetMethods(all)) if (m.Name == "Build") { if (m.GetParameters().Length == 4) Build4 = m; else if (m.GetParameters().Length == 5) Build5 = m; }
            Expand = Nav.GetMethod("Expand", all);
            Components = Nav.GetMethod("Components", all);
            TraceInfo = Nav.GetMethod("TraceInfo", all);
            SceneLoaded = Nav.GetMethod("OnSceneLoaded", all);
            Structures = Nav.GetField("_structures", all);
            LastReason = Nav.GetField("LastReason", all);
            Lazy = Build5 != null && Expand != null;
            StructList = Structures.GetValue(null);
            Add = StructList.GetType().GetMethod("Add"); Clear = StructList.GetType().GetMethod("Clear");

            // config: like tools/config-harness - ModuleConfig + BindConfig of the NPC module on fresh cfg files
            var core = Asm.GetType("WildWasteland.Core.Log", true);
            core.GetField("Source", all).SetValue(null, BepInEx.Logging.Logger.CreateLogSource("WW-" + tag));
            var mc = Asm.GetType("WildWasteland.Core.ModuleConfig", true);
            var mod = Activator.CreateInstance(Npc);
            var main = new BepInEx.Configuration.ConfigFile(Path.Combine(dir, tag + ".main.cfg"), false);
            var tools = new BepInEx.Configuration.ConfigFile(Path.Combine(dir, tag + ".tools.cfg"), false);
            var cfg = Activator.CreateInstance(mc, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { "NPC", main, tools }, null);
            mc.GetProperty("Enabled").SetValue(cfg, main.Bind("NPC", "Enabled", true, "x"), null);
            Asm.GetType("WildWasteland.Core.ModuleBase", true).GetProperty("Config").SetValue(mod, cfg, null);
            Npc.GetMethod("BindConfig").Invoke(mod, new object[] { cfg });
            Npc.GetField("Running", all).SetValue(null, true);
            if (!(bool)Nav.GetProperty("On", all).GetValue(null, null)) throw new Exception(tag + ": Nav.On is false after binding");
        }

        public void Reset() { SceneLoaded.Invoke(null, null); StructList = Structures.GetValue(null); }

        public object MakeStructure(Grid g, int idx)
        {
            var s = Activator.CreateInstance(Structure, true);
            var root = (Transform)FormatterServices.GetUninitializedObject(typeof(Transform));
            typeof(UnityEngine.Object).GetField("m_CachedPtr", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, new IntPtr(1000 + idx));
            Set(s, "Root", root); Set(s, "Name", "Camp_T" + idx + "(Clone)");
            Set(s, "Box", new Bounds(g.Min + new Vector3(g.W * g.Cell * 0.5f, 5f, g.H * g.Cell * 0.5f), new Vector3(g.W * g.Cell, 10f, g.H * g.Cell)));
            Set(s, "Cell", g.Cell); Set(s, "RefY", 0f); Set(s, "W", g.W); Set(s, "H", g.H);
            Set(s, "FloorY", (float[])g.FloorY.Clone()); Set(s, "Why", (byte[])g.Why.Clone()); Set(s, "Open", (bool[])g.Open.Clone());
            Set(s, "Edges", (byte[])g.Edges.Clone()); Set(s, "Phase", 2); Set(s, "Baked", true); Set(s, "Walkable", g.Walkable);
            Set(s, "Near", (bool[])g.Near.Clone());
            if (Structure.GetField("StepAtBake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null) Set(s, "StepAtBake", 0.25f);
            Components.Invoke(null, new object[] { s });
            int ec = 0, ew = 0;
            for (int i = 0; i < g.W * g.H; i++) { int x = i % g.W, z = i / g.W; if (x == 0 || z == 0 || x == g.W - 1 || z == g.H - 1) { ec++; if (!float.IsNaN(g.FloorY[i])) ew++; } }
            Set(s, "EdgeCells", ec); Set(s, "EdgeWalkable", ew);
            Add.Invoke(StructList, new object[] { s });
            return s;
        }
        void Set(object o, string f, object v) { Structure.GetField(f, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(o, v); }
        public object Get(object o, string f) { return o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(o); }

        public string CallNext(object owner, Vector3 pos, Vector3 goal, out bool r, out Vector3 next, out float left)
        {
            var a = new object[] { owner, pos, goal, null, null };
            r = (bool)Next.Invoke(null, a); next = (Vector3)a[3]; left = (float)a[4];
            return (string)TraceInfo.Invoke(null, null) + " | " + (string)LastReason.GetValue(null);
        }
        public float[] FullBuild(object s, Vector3 goal, bool inside, int goalCell)
        {
            var f = Build4.Invoke(null, new object[] { s, goal, inside, goalCell });
            return (float[])Get(f, "Dist");
        }
        public object LazyBuild(object s, Vector3 goal, bool inside, int goalCell, int until) { return Build5.Invoke(null, new object[] { s, goal, inside, goalCell, until }); }
        public void ExpandTo(object s, object f, int until) { Expand.Invoke(null, new object[] { s, f, until }); }
        public void ClearFields(object s) { ((System.Collections.IDictionary)Get(s, "Fields")).Clear(); }
        public void ClearMemos() { foreach (var n in new[] { "_outMemo", "_outPlans", "_exits", "_pickDone", "_pickWait" }) { var f = Nav.GetField(n, BindingFlags.Static | BindingFlags.NonPublic); if (f != null) ((System.Collections.IDictionary)f.GetValue(null)).Clear(); } }
        public void Masks(object s) { var m = Nav.GetMethod("MasksNow", BindingFlags.Static | BindingFlags.NonPublic); if (m != null) { m.Invoke(null, new object[] { s }); MasksMade++; } }
        public int MasksMade;
        public int PickQueued() { return (int)Nav.GetProperty("PickQueued", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null); }
        public void PickStep() { Nav.GetMethod("PickStep", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null); }
    }

    static FieldInfo _fakeTime;
    static void SetTime(float t) { if (_fakeTime == null) _fakeTime = typeof(Time).GetField("WW_FakeTime", BindingFlags.Static | BindingFlags.Public); _fakeTime.SetValue(null, t); }
    static GameObject FakeOwner(int id)
    {
        var go = (GameObject)FormatterServices.GetUninitializedObject(typeof(GameObject));
        typeof(UnityEngine.Object).GetField("m_CachedPtr", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(go, new IntPtr(50000 + id));
        return go;
    }

    // ---------- synthetic grids ----------
    sealed class Grid
    {
        public int W, H, Walkable; public float Cell = 0.5f; public Vector3 Min;
        public float[] FloorY; public byte[] Why; public bool[] Open; public byte[] Edges; public bool[] Near;
        public List<int> Big = new List<int>();     // walkable cells (Comp decides "big" on the DLL side; we only avoid blocked ones)
        public Vector3 Center(int i) { return new Vector3(Min.x + (i % W + 0.5f) * Cell, FloorY[i], Min.z + (i / W + 0.5f) * Cell); }
    }

    static Grid MakeGrid(System.Random rnd, int W, int H, double blocked, int walls, float maxStep, float hopStep)
    {
        var g = new Grid { W = W, H = H, Min = new Vector3((float)rnd.Next(-500, 500) + 0.25f, 0f, (float)rnd.Next(-500, 500) + 0.75f) };
        int n = W * H;
        g.FloorY = new float[n]; g.Why = new byte[n]; g.Open = new bool[n]; g.Edges = new byte[n]; g.Near = new bool[n];
        // heights: a few plateaus (steps of 0-0.6 m between them: walkable, hop-able or a cliff)
        int px = rnd.Next(3, 7); var ph = new float[px * px];
        for (int i = 0; i < ph.Length; i++) ph[i] = rnd.NextDouble() < 0.5 ? 0f : (float)(rnd.NextDouble() * 0.6);
        for (int i = 0; i < n; i++)
        {
            int x = i % W, z = i / W;
            float y = ph[Math.Min(px - 1, x * px / W) + px * Math.Min(px - 1, z * px / H)] + (float)(rnd.NextDouble() * 0.04);
            bool blk = rnd.NextDouble() < blocked;
            g.FloorY[i] = blk ? float.NaN : y;
            g.Why[i] = blk ? (byte)(rnd.NextDouble() < 0.5 ? 2 : rnd.NextDouble() < 0.3 ? 3 : 1) : (byte)0;
            g.Open[i] = rnd.NextDouble() < 0.8;
        }
        // thin walls: edge bits closed along a line (cells on both sides stay walkable), with a gap or two; some seal a room completely
        var closed = new HashSet<long>();
        for (int w = 0; w < walls; w++)
        {
            bool vert = rnd.NextDouble() < 0.5; int at = rnd.Next(2, (vert ? W : H) - 2), a0 = rnd.Next(0, (vert ? H : W) / 2), a1 = rnd.Next(a0 + 5, vert ? H : W);
            int gap = rnd.NextDouble() < 0.7 ? rnd.Next(a0, a1) : -1;
            for (int a = a0; a < a1; a++)
            {
                if (a == gap || a == gap + 1) continue;
                int i = vert ? a * W + at : at * W + a;
                // the wall lies between (at-1) and at along the normal: close +x (or +z) edge of the cell before it, incl. the diagonals that cross it
                int j = vert ? a * W + at - 1 : (at - 1) * W + a;
                closed.Add(((long)j << 32) | (uint)i);
                if (vert) { if (a > 0) closed.Add(((long)((a - 1) * W + at - 1) << 32) | (uint)i); if (a + 1 < H) closed.Add(((long)j << 32) | (uint)((a + 1) * W + at)); }
                else { if (a > 0) closed.Add(((long)((at - 1) * W + a - 1) << 32) | (uint)i); if (a + 1 < W) closed.Add(((long)j << 32) | (uint)(at * W + a + 1)); }
            }
        }
        // a sealed island: a rectangle of walkable cells with every edge across its border closed (its own component, unreachable)
        if (rnd.NextDouble() < 0.8)
        {
            int ix = rnd.Next(5, W - 20), iz = rnd.Next(5, H - 20), iw = rnd.Next(4, 12), ih = rnd.Next(4, 12);
            for (int z = iz - 1; z <= iz + ih; z++) for (int x = ix - 1; x <= ix + iw; x++)
            {
                bool inA = x >= ix && x < ix + iw && z >= iz && z < iz + ih;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = x + dx, cz = z + dz; if ((dx == 0 && dz == 0) || cx < 0 || cz < 0 || cx >= W || cz >= H) continue;
                    bool inB = cx >= ix && cx < ix + iw && cz >= iz && cz < iz + ih;
                    if (inA != inB) { closed.Add(((long)(z * W + x) << 32) | (uint)(cz * W + cx)); closed.Add(((long)(cz * W + cx) << 32) | (uint)(z * W + x)); }
                }
            }
        }
        // (review) a roofed room sealed by a ring of no-floor cells (Why 1): no open sky, no way to the ring -> ExitCell's RELAXED branch
        // (the ring is crossable under _relax) and RelaxedNext; 8 x 8 .. 14 x 14 walkable cells (>= MinArea, so NPCs stand there)
        if (rnd.NextDouble() < 0.7)
        {
            int ix = rnd.Next(20, W - 40), iz = rnd.Next(20, H - 40), iw = rnd.Next(8, 15), ih = rnd.Next(8, 15);
            for (int z = iz - 1; z <= iz + ih; z++) for (int x = ix - 1; x <= ix + iw; x++)
            {
                int i = z * W + x;
                bool ring = x == ix - 1 || x == ix + iw || z == iz - 1 || z == iz + ih;
                if (ring) { g.FloorY[i] = float.NaN; g.Why[i] = 1; g.Open[i] = false; }
                else { if (float.IsNaN(g.FloorY[i])) { g.FloorY[i] = 0.1f; g.Why[i] = 0; } g.Open[i] = false; }
            }
        }
        int[] EdgeDx = { 1, 0, 1, -1 }, EdgeDz = { 0, 1, 1, 1 };
        for (int i = 0; i < n; i++)
        {
            float ya = g.FloorY[i]; if (float.IsNaN(ya)) continue;
            int x = i % W, z = i / W; byte bits = 0;
            for (int k = 0; k < 4; k++)
            {
                int cx = x + EdgeDx[k], cz = z + EdgeDz[k];
                if (cx < 0 || cz < 0 || cx >= W || cz >= H) continue;
                int j = cz * W + cx; float yb = g.FloorY[j]; if (float.IsNaN(yb)) continue;
                if (closed.Contains(((long)i << 32) | (uint)j) || closed.Contains(((long)j << 32) | (uint)i)) continue;
                float dy = Math.Abs(ya - yb);
                if (dy <= maxStep && rnd.NextDouble() > 0.01) { bits |= (byte)(1 << k); continue; }
                if (k > 1 || dy > hopStep) continue;
                if (rnd.NextDouble() < 0.7) bits |= (byte)(1 << (k + 4));
            }
            g.Edges[i] = bits;
        }
        for (int i = 0; i < n; i++)
        {
            if (float.IsNaN(g.FloorY[i])) continue;
            g.Walkable++; g.Big.Add(i);
            int x = i % W, z = i / W;
            for (int dz = -1; dz <= 1 && !g.Near[i]; dz++) for (int dx = -1; dx <= 1; dx++)
            {
                int cx = x + dx, cz = z + dz; if (cx < 0 || cz < 0 || cx >= W || cz >= H) continue;
                byte w = g.Why[cz * W + cx]; if (w == 2 || w == 3) { g.Near[i] = true; break; }
            }
        }
        return g;
    }

    // ---------- the run ----------
    static int tieDiff; static HashSet<int> tied = new HashSet<int>();
    static int slicedTotal, slicedJobs, slicedSteps, warmSliced, warmPending;
    static int _mismatch; static int _checks;
    static void Mismatch(string what) { _mismatch++; if (_mismatch <= 40) Console.WriteLine("MISMATCH " + what); }

    static int Run(string dir, string newDll, string oldDll, int seeds, int pairs)
    {
        Directory.SetCurrentDirectory(dir);
        Console.WriteLine("NavCheck: new " + newDll + ", old " + oldDll);
        string root = Path.Combine(dir, "root");      // BepInEx paths (ConfigFile's static init wants them), as in tools/config-harness
        Directory.CreateDirectory(Path.Combine(root, "BepInEx", "config"));
        File.WriteAllText(Path.Combine(root, "Apocalypter.exe"), "");
        typeof(BepInEx.Paths).GetMethod("SetExecutablePath", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { Path.Combine(root, "Apocalypter.exe"), null, null, null });
        var nw = new Side("new", Path.GetFullPath(newDll), dir);
        var od = new Side("old", Path.GetFullPath(oldDll), dir);
        Console.WriteLine("new build lazy API: " + nw.Lazy + "; old lazy API: " + od.Lazy);
        var pickInline = nw.Nav.GetField("PickInline", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (pickInline != null) pickInline.SetValue(null, true);
        Console.WriteLine("PickInline field: " + (pickInline != null));
        SetTime(10f);
        int pairsTotal = 0, fieldsTotal = 0, coldTotal = 0, warmTotal = 0, unreachable = 0, outside = 0, routed = 0, evictTotal = 0, lowStepTotal = 0, relaxedTotal = 0, evictPeak = 0, evictPeakOld = 0, sharedDiff = 0;
        for (int seed = 1; seed <= seeds; seed++)
        {
            var rnd = new System.Random(seed * 7919);
            int W = rnd.Next(100, 201), H = rnd.Next(100, 201);
            var g = MakeGrid(rnd, W, H, 0.08 + rnd.NextDouble() * 0.12, rnd.Next(2, 8), 0.25f, 0.5f);
            nw.Reset(); od.Reset();
            object sn = nw.MakeStructure(g, seed), so = od.MakeStructure(g, seed);
            nw.Masks(sn);
            var cn = (int[])nw.Get(sn, "Comp"); var co = (int[])od.Get(so, "Comp");
            for (int i = 0; i < cn.Length; i++) if (cn[i] != co[i]) { Mismatch("seed " + seed + " Comp[" + i + "] " + cn[i] + " vs " + co[i]); break; }
            var sizes = (int[])nw.Get(sn, "CompSize"); int bigComp = 0; for (int c = 0; c < sizes.Length; c++) if (sizes[c] > sizes[bigComp]) bigComp = c;
            var stand = new List<int>(); foreach (int i in g.Big) if (sizes[cn[i]] >= 30) stand.Add(i);     // NPCs stand on cells of areas >= MinArea (a small island makes NearestReachable raycast)
            var roofedStand = new List<int>(); foreach (int i in stand) if (!g.Open[i] && cn[i] != bigComp) roofedStand.Add(i);
            int roofed = roofedStand.Count;
            Console.WriteLine("seed " + seed + ": " + W + " x " + H + " cells, " + g.Walkable + " walkable, " + sizes.Length + " areas, largest " + sizes[bigComp] + ", " + roofed + " roofed stand cells off the main area");

            // (i) fields: old full Build vs new full Build, and the lazy field at every stage
            for (int t = 0; t < 12; t++)
            {
                bool inside = t % 3 != 2;
                int gc = g.Big[rnd.Next(g.Big.Count)];
                Vector3 goal = inside ? g.Center(gc) : new Vector3(g.Min.x + (float)(rnd.NextDouble() * 2 - 0.5) * W * g.Cell, 0f, g.Min.z + (float)(rnd.NextDouble() * 2 - 0.5) * H * g.Cell);
                float[] oldD = od.FullBuild(so, goal, inside, gc), newD = nw.FullBuild(sn, goal, inside, gc);
                _checks++; fieldsTotal++;
                for (int i = 0; i < oldD.Length; i++) if (!Same(oldD[i], newD[i])) { Mismatch("seed " + seed + " full field #" + t + " cell " + i + ": old " + oldD[i] + " new " + newD[i]); break; }
                if (!nw.Lazy) continue;
                int u1 = g.Big[rnd.Next(g.Big.Count)], u2 = g.Big[rnd.Next(g.Big.Count)], u3 = g.Big[rnd.Next(g.Big.Count)];
                object f = nw.LazyBuild(sn, goal, inside, gc, u1);
                CheckLazy(nw, sn, f, oldD, "seed " + seed + " lazy #" + t + " stage1 (until " + u1 + ")", u1);
                nw.ExpandTo(sn, f, u2); CheckLazy(nw, sn, f, oldD, "seed " + seed + " lazy #" + t + " stage2 (until " + u2 + ")", u2);
                nw.ExpandTo(sn, f, u3); CheckLazy(nw, sn, f, oldD, "seed " + seed + " lazy #" + t + " stage3 (until " + u3 + ")", u3);
                nw.ExpandTo(sn, f, -1);
                var d = (float[])nw.Get(f, "Dist");
                if (nw.Get(f, "H") != null) Mismatch("seed " + seed + " lazy #" + t + ": heap kept after a full expansion");
                for (int i = 0; i < oldD.Length; i++) if (!Same(oldD[i], d[i])) { Mismatch("seed " + seed + " lazy #" + t + " after full expansion cell " + i + ": old " + oldD[i] + " new " + d[i]); break; }
            }

            // (ii) Next, cold: every call alone (caches emptied, time far apart, no owner)
            for (int p = 0; p < pairs; p++)
            {
                int from = p % 8 == 1 && roofedStand.Count > 0 ? roofedStand[rnd.Next(roofedStand.Count)] : stand[rnd.Next(stand.Count)];   // every 8th pair from the sealed roofed room (relaxed exits)
                Vector3 pos = g.Center(from) + new Vector3((float)(rnd.NextDouble() - 0.5) * 0.4f, 0f, (float)(rnd.NextDouble() - 0.5) * 0.4f);
                Vector3 goal; int kind = p % 4;
                if (kind == 0) { int gx = from % W + rnd.Next(-3, 4), gz = from / W + rnd.Next(-3, 4); gx = Math.Max(0, Math.Min(W - 1, gx)); gz = Math.Max(0, Math.Min(H - 1, gz)); int gc = gz * W + gx; goal = new Vector3(g.Min.x + (gx + 0.5f) * g.Cell, float.IsNaN(g.FloorY[gc]) ? pos.y : g.FloorY[gc], g.Min.z + (gz + 0.5f) * g.Cell); }
                else if (kind == 1) goal = g.Center(g.Big[rnd.Next(g.Big.Count)]);
                else if (kind == 2) { int gc = rnd.Next(W * H); goal = new Vector3(g.Min.x + (gc % W + 0.5f) * g.Cell, 0.2f, g.Min.z + (gc / W + 0.5f) * g.Cell); }
                else { float ang = (float)(rnd.NextDouble() * Math.PI * 2); float r = (W + H) * g.Cell * 0.5f + (float)rnd.NextDouble() * 30f; goal = new Vector3(g.Min.x + W * g.Cell * 0.5f + (float)Math.Cos(ang) * r, 0f, g.Min.z + H * g.Cell * 0.5f + (float)Math.Sin(ang) * r); outside++; }
                SetTime(100f + p * 100f);
                nw.ClearFields(sn); od.ClearFields(so);
                bool r1, r2; Vector3 n1, n2; float l1, l2;
                string t1 = nw.CallNext(null, pos, goal, out r1, out n1, out l1);
                string t2 = od.CallNext(null, pos, goal, out r2, out n2, out l2);
                _checks++; pairsTotal++; coldTotal++;
                if (r1) routed++;
                if (t2.Contains("relaxed")) relaxedTotal++;
                if (t2.Contains("NO ring") || t2.Contains("EXIT")) unreachable++;
                if (pickInline != null && kind == 3)
                {
                    pickInline.SetValue(null, false);
                    nw.ClearFields(sn); nw.ClearMemos(); od.ClearMemos();
                    bool r3; Vector3 n3; float l3;
                    nw.CallNext(null, pos, goal, out r3, out n3, out l3);
                    int steps = 0; bool queued = nw.PickQueued() > 0;
                    while (nw.PickQueued() > 0 && steps < 100000) { nw.PickStep(); steps++; }
                    string t3 = nw.CallNext(null, pos, goal, out r3, out n3, out l3);
                    pickInline.SetValue(null, true);
                    _checks++; slicedTotal++; if (queued) slicedJobs++; slicedSteps += steps;
                    if (r3 != r2 || !Same(n3, n2) || !Same(l3, l2) || t3 != t2)
                        Mismatch("seed " + seed + " SLICED cold pair " + p + " from " + (from % W) + "," + (from / W) + ": new " + r3 + " " + V(n3) + " " + l3 + " [" + t3 + "] / old " + r2 + " " + V(n2) + " " + l2 + " [" + t2 + "]");
                }
                if (r1 != r2 || !Same(n1, n2) || !Same(l1, l2) || t1 != t2)
                    Mismatch("seed " + seed + " cold pair " + p + " kind " + kind + " from " + (from % W) + "," + (from / W) + ": new " + r1 + " " + V(n1) + " " + l1 + " [" + t1 + "] / old " + r2 + " " + V(n2) + " " + l2 + " [" + t2 + "]");
            }

            // (ii) Next, warm: 24 NPCs think every 0.1-0.2 s for 12 s toward moving goals; caches, memos and plans live on both sides
            int npcs = 24; var owners = new GameObject[npcs]; var at = new int[npcs]; var goals = new Vector3[npcs]; var nextThink = new float[npcs];
            for (int k = 0; k < npcs; k++) { owners[k] = FakeOwner(seed * 100 + k); at[k] = stand[rnd.Next(stand.Count)]; goals[k] = g.Center(g.Big[rnd.Next(g.Big.Count)]); nextThink[k] = 1000f + (float)rnd.NextDouble() * 0.2f; }
            Vector3 player = g.Center(g.Big[rnd.Next(g.Big.Count)]);
            for (float now = 1000f; now < 1012f; now += 0.05f)
            {
                SetTime(now);
                if (rnd.NextDouble() < 0.1) player = g.Center(g.Big[rnd.Next(g.Big.Count)]) + new Vector3((float)rnd.NextDouble(), 0f, (float)rnd.NextDouble());
                for (int k = 0; k < npcs; k++)
                {
                    if (now < nextThink[k]) continue;
                    nextThink[k] = now + 0.1f + (float)rnd.NextDouble() * 0.1f;
                    Vector3 goal = k % 3 == 0 ? player : k % 3 == 1 ? goals[k] : new Vector3(g.Min.x - 10f - k, 0f, g.Min.z + k * 3f);   // the player, a fixed cell, a point outside
                    Vector3 pos = g.Center(at[k]);
                    bool r1, r2; Vector3 n1, n2; float l1, l2;
                    string t1 = nw.CallNext(owners[k], pos, goal, out r1, out n1, out l1);
                    string t2 = od.CallNext(owners[k], pos, goal, out r2, out n2, out l2);
                    _checks++; pairsTotal++; warmTotal++;
                    if (t2.Contains("relaxed")) relaxedTotal++;
                    if (r1 != r2 || !Same(n1, n2) || !Same(l1, l2) || t1 != t2)
                        Mismatch("seed " + seed + " warm t=" + now.ToString("0.00") + " npc " + k + " from " + (at[k] % W) + "," + (at[k] / W) + ": new " + r1 + " " + V(n1) + " " + l1 + " [" + t1 + "] / old " + r2 + " " + V(n2) + " " + l2 + " [" + t2 + "]");
                    // walk: toward the waypoint one cell per think (or wander when the map says straight)
                    if (r1) { int wx, wz; if (CellOf(g, n1, out wx, out wz)) { int cx = at[k] % W, cz = at[k] / W; cx += Math.Sign(wx - cx); cz += Math.Sign(wz - cz); int ni = cz * W + cx; if (cn[ni] >= 0 && sizes[cn[ni]] >= 30) at[k] = ni; } }
                    else if (rnd.NextDouble() < 0.3) at[k] = stand[rnd.Next(stand.Count)];
                }
            }

            if (pickInline != null)
            {
                pickInline.SetValue(null, false); nw.ClearMemos(); od.ClearMemos();
                var wat = new int[npcs]; for (int k = 0; k < npcs; k++) { wat[k] = stand[rnd.Next(stand.Count)]; nextThink[k] = 1100f + (float)rnd.NextDouble() * 0.2f; }
                for (float now = 1100f; now < 1112f; now += 0.05f)
                {
                    SetTime(now); nw.PickStep();
                    for (int k = 0; k < npcs; k++)
                    {
                        if (now < nextThink[k]) continue;
                        nextThink[k] = now + 0.1f + (float)rnd.NextDouble() * 0.1f;
                        Vector3 goal = k % 2 == 0 ? new Vector3(g.Min.x - 10f - k, 0f, g.Min.z + k * 3f + (now - 1100f)) : player;
                        bool r1; Vector3 n1; float l1;
                        nw.CallNext(owners[k], g.Center(wat[k]), goal, out r1, out n1, out l1);
                        warmSliced++; if (nw.PickQueued() > 0) warmPending++;
                        if (r1) { int wx, wz; if (CellOf(g, n1, out wx, out wz)) { int cx = wat[k] % W, cz = wat[k] / W; cx += Math.Sign(wx - cx); cz += Math.Sign(wz - cz); int ni = cz * W + cx; if (cn[ni] >= 0 && sizes[cn[ni]] >= 30) wat[k] = ni; } }
                    }
                }
                while (nw.PickQueued() > 0) nw.PickStep();
                pickInline.SetValue(null, true); nw.ClearMemos(); od.ClearMemos();
            }

            // (review) eviction: 48 NPCs of the largest area, each with its own fixed goal cell of that area (48 distinct 4 x 4 buckets),
            // thinking every 0.1 s for 2.5 s (FieldSeconds 1): the per-structure cache passes 16 (expired sweep) and 32 (clear all) on
            // both sides; then the same with goals anywhere (other areas, outside): the exit / NPC-cell fields join the cache
            var bigCells = new List<int>(); foreach (int i in g.Big) if (cn[i] == bigComp) bigCells.Add(i);
            // phase 2: 16 of them chase ONE goal that jitters inside its 4 x 4 bucket (FieldFor's 2.9-cell sharing) while the others churn the
            // cache: the sides' eviction moments differ (key-space-3 entries, the area short cut), so which cached field a think gets can
            // differ - the non-identity fix-NPC-perf.md section 3 declares; counted apart (SHARED), not a mismatch
            for (int phase = 0; phase < 4; phase++)   // phase 3 = phase 2 with every goal in the largest area: the caches evolve alike, a shared-field difference there would be a bug
            {
                int bn = 48; var bo = new GameObject[bn]; var bat = new int[bn]; var bg = new Vector3[bn]; var bt = new float[bn];
                var used = new HashSet<long>();
                int shared = bigCells[rnd.Next(bigCells.Count)];
                for (int k = 0; k < bn; k++)
                {
                    bo[k] = FakeOwner(seed * 100 + 50 + k); bat[k] = bigCells[rnd.Next(bigCells.Count)];
                    int gc = -1;
                    for (int t = 0; t < 200 && gc < 0; t++) { int c = phase == 0 || phase == 3 ? bigCells[rnd.Next(bigCells.Count)] : g.Big[rnd.Next(g.Big.Count)]; long b = ((long)(c % W / 4) << 20) | (long)(c / W / 4); if (used.Add(b)) gc = c; }
                    bg[k] = gc < 0 ? g.Center(bat[k]) : (phase == 1 || phase == 2) && k % 6 == 5 ? new Vector3(g.Min.x - 15f - k, 0f, g.Min.z + k * 2f) : g.Center(gc);
                    bt[k] = 2000f + phase * 10f + (float)rnd.NextDouble() * 0.1f;
                }
                for (float now = 2000f + phase * 10f; now < 2002.5f + phase * 10f; now += 0.05f)
                {
                    SetTime(now);
                    for (int k = 0; k < bn; k++)
                    {
                        if (now < bt[k]) continue;
                        bt[k] = now + 0.1f;
                        if (phase >= 2 && k < 16) { int sx = shared % W / 4 * 4 + rnd.Next(4), sz = shared / W / 4 * 4 + rnd.Next(4); int sc = Math.Min(H - 1, sz) * W + Math.Min(W - 1, sx); bg[k] = float.IsNaN(g.FloorY[sc]) ? g.Center(shared) : g.Center(sc); }
                        bool r1, r2; Vector3 n1, n2; float l1, l2;
                        string t1 = nw.CallNext(bo[k], g.Center(bat[k]), bg[k], out r1, out n1, out l1);
                        string t2 = od.CallNext(bo[k], g.Center(bat[k]), bg[k], out r2, out n2, out l2);
                        _checks++; pairsTotal++; evictTotal++;
                        if (t2.Contains("relaxed")) relaxedTotal++;
                        evictPeak = Math.Max(evictPeak, ((System.Collections.IDictionary)nw.Get(sn, "Fields")).Count); evictPeakOld = Math.Max(evictPeakOld, ((System.Collections.IDictionary)od.Get(so, "Fields")).Count);
                        if (r1 != r2 || !Same(n1, n2) || !Same(l1, l2) || t1 != t2)
                        {
                            if (phase >= 2 && k < 16 && r1 == r2 && t1.Contains("same area") && t2.Contains("same area")) { sharedDiff++; if (sharedDiff <= 3) Console.WriteLine("SHARED-FIELD DIFFERENCE seed " + seed + " t=" + now.ToString("0.00") + " npc " + k + ": new " + V(n1) + " " + l1 + " / old " + V(n2) + " " + l2); }
                            else if (t1.Contains("EXIT") && t2.Contains("EXIT") && (Same(l1, l2) || tied.Contains(seed * 1000 + k)))
                            { tieDiff++; tied.Add(seed * 1000 + k); if (tieDiff <= 3) Console.WriteLine("EXIT TIE seed " + seed + " t=" + now.ToString("0.00") + " npc " + k + ": new [" + t1 + "] / old [" + t2 + "]"); }
                            else
                            Mismatch("seed " + seed + " evict phase " + phase + " t=" + now.ToString("0.00") + " npc " + k + " from " + (bat[k] % W) + "," + (bat[k] / W) + ": new " + r1 + " " + V(n1) + " " + l1 + " [" + t1 + "] / old " + r2 + " " + V(n2) + " " + l2 + " [" + t2 + "]");
                        }
                        if (r1) { int wx, wz; if (CellOf(g, n1, out wx, out wz)) { int cx = bat[k] % W, cz = bat[k] / W; cx += Math.Sign(wx - cx); cz += Math.Sign(wz - cz); int ni = cz * W + cx; if (cn[ni] >= 0 && sizes[cn[ni]] >= 30) bat[k] = ni; } }
                    }
                }
                int fc = ((System.Collections.IDictionary)nw.Get(sn, "Fields")).Count, fo = ((System.Collections.IDictionary)od.Get(so, "Fields")).Count;
                Console.WriteLine("  evict phase " + phase + ": fields cached at the end new " + fc + " / old " + fo + " (peak " + evictPeak + " / " + evictPeakOld + ")");
            }

            // (review) MaxStep LOWERED live after the bake (F7 tuning): the area short cut must switch itself off (StepAtBake), the old
            // side simply builds with the lower step; cold pairs with goals anywhere
            foreach (var side in new[] { nw, od }) ((BepInEx.Configuration.ConfigEntry<float>)side.Npc.GetField("NavMaxStep", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null)).Value = 0.12f;
            for (int p = 0; p < pairs / 4; p++)
            {
                int from = stand[rnd.Next(stand.Count)];
                Vector3 goal = p % 2 == 0 ? g.Center(g.Big[rnd.Next(g.Big.Count)]) : new Vector3(g.Min.x + (float)(rnd.NextDouble() * 2 - 0.5) * W * g.Cell, 0f, g.Min.z + (float)(rnd.NextDouble() * 2 - 0.5) * H * g.Cell);
                SetTime(5000f + p * 100f);
                nw.ClearFields(sn); od.ClearFields(so);
                bool r1, r2; Vector3 n1, n2; float l1, l2;
                string t1 = nw.CallNext(null, g.Center(from), goal, out r1, out n1, out l1);
                string t2 = od.CallNext(null, g.Center(from), goal, out r2, out n2, out l2);
                _checks++; pairsTotal++; lowStepTotal++;
                if (t2.Contains("relaxed")) relaxedTotal++;
                if (r1 != r2 || !Same(n1, n2) || !Same(l1, l2) || t1 != t2)
                    Mismatch("seed " + seed + " lowered MaxStep pair " + p + " from " + (from % W) + "," + (from / W) + ": new " + r1 + " " + V(n1) + " " + l1 + " [" + t1 + "] / old " + r2 + " " + V(n2) + " " + l2 + " [" + t2 + "]");
            }
            foreach (var side in new[] { nw, od }) ((BepInEx.Configuration.ConfigEntry<float>)side.Npc.GetField("NavMaxStep", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null)).Value = 0.25f;
        }
        Console.WriteLine();
        Console.WriteLine("checks " + _checks + ": fields " + fieldsTotal + ", Next pairs " + pairsTotal + " (cold " + coldTotal + " of which " + outside + " outside goals, " + routed + " routed, " + unreachable + " with an exit; warm " + warmTotal + ", eviction " + evictTotal + ", lowered MaxStep " + lowStepTotal + "; " + relaxedTotal + " relaxed exits), mismatches " + _mismatch + ", shared-field differences (declared, section 3) " + sharedDiff);
        Console.WriteLine("move masks made on the new side: " + nw.MasksMade + " structures");
        Console.WriteLine("exit ties (equal cost, lower cell first; and that NPC's later thinks in the phase) " + tieDiff);
        Console.WriteLine("sliced pick checks " + slicedTotal + " (" + slicedJobs + " queued a job, " + slicedSteps + " frames of PickStep), warm sliced thinks " + warmSliced + " (" + warmPending + " with a job pending)");
        Bench(nw, od);
        Console.WriteLine(_mismatch == 0 ? "RESULT: equivalent" : "RESULT: " + _mismatch + " MISMATCHES");
        return _mismatch == 0 ? 0 : 1;
    }

    static bool CellOf(Grid g, Vector3 p, out int x, out int z)
    {
        x = Mathf.FloorToInt((p.x - g.Min.x) / g.Cell); z = Mathf.FloorToInt((p.z - g.Min.z) / g.Cell);
        return x >= 0 && z >= 0 && x < g.W && z < g.H;
    }

    // a lazy field: every cell with Dist <= Settled equals the full field; the asked cell is settled (or unreachable with the heap drained);
    // a tentative cell is never below its final distance
    static void CheckLazy(Side nw, object s, object f, float[] full, string what, int until)
    {
        var d = (float[])nw.Get(f, "Dist"); float settled = (float)nw.Get(f, "Settled"); bool complete = nw.Get(f, "H") == null;
        _checks++;
        if (!(d[until] <= settled) && !(float.IsInfinity(full[until]) && complete)) { Mismatch(what + ": asked cell not settled (dist " + d[until] + ", settled " + settled + ", complete " + complete + ")"); return; }
        if (!Same(d[until], full[until])) { Mismatch(what + ": asked cell " + d[until] + " vs full " + full[until]); return; }
        int set = 0;
        for (int i = 0; i < d.Length; i++)
        {
            if (d[i] <= settled) { set++; if (!Same(d[i], full[i])) { Mismatch(what + ": settled cell " + i + " " + d[i] + " vs full " + full[i]); return; } }
            else if (d[i] < full[i]) { Mismatch(what + ": tentative cell " + i + " " + d[i] + " below full " + full[i]); return; }
        }
        if (complete) for (int i = 0; i < d.Length; i++) if (!Same(d[i], full[i])) { Mismatch(what + ": complete field differs at " + i); return; }
    }

    static string V(Vector3 v) { return v.x.ToString("0.000") + "," + v.y.ToString("0.000") + "," + v.z.ToString("0.000"); }
    static bool Same(float a, float b) { return a == b || (float.IsNaN(a) && float.IsNaN(b)); }
    static bool Same(Vector3 a, Vector3 b) { return Same(a.x, b.x) && Same(a.y, b.y) && Same(a.z, b.z); }

    // ---------- micro-benchmark: 184 x 227 cells, ~29k reachable ----------
    static void Bench(Side nw, Side od)
    {
        var rnd = new System.Random(4242);
        Grid g = null;
        for (int tries = 0; tries < 20; tries++) { g = MakeGrid(rnd, 184, 227, 0.27, 3, 0.25f, 0.5f); if (g.Walkable > 28000 && g.Walkable < 31500) break; }
        nw.Reset(); od.Reset();
        object sn = nw.MakeStructure(g, 99), so = od.MakeStructure(g, 99);
        nw.Masks(sn);
        var comp = (int[])nw.Get(sn, "Comp"); var sizes = (int[])nw.Get(sn, "CompSize"); int big = 0; for (int c = 0; c < sizes.Length; c++) if (sizes[c] > sizes[big]) big = c;
        var cells = new List<int>(); foreach (int i in g.Big) if (comp[i] == big) cells.Add(i);
        Console.WriteLine("bench grid 184 x 227 = " + (184 * 227) + " cells, " + g.Walkable + " walkable, largest area " + sizes[big] + " cells");
        const int N = 100;
        var froms = new int[N]; var nearGoals = new int[N]; var farGoals = new int[N];
        for (int k = 0; k < N; k++)
        {
            froms[k] = cells[rnd.Next(cells.Count)];
            int x = froms[k] % 184, z = froms[k] / 184, gc = -1;
            for (int t = 0; t < 100 && gc < 0; t++) { int gx = x + rnd.Next(-3, 4), gz = z + rnd.Next(-3, 4); if (gx >= 0 && gz >= 0 && gx < 184 && gz < 227 && comp[gz * 184 + gx] == big && Math.Abs(gx - x) + Math.Abs(gz - z) >= 3) gc = gz * 184 + gx; }
            nearGoals[k] = gc < 0 ? froms[k] : gc;
            farGoals[k] = cells[rnd.Next(cells.Count)];
        }
        var sw = new Stopwatch();
        // old full Build from the goal (what every FieldFor miss cost before)
        sw.Restart(); for (int k = 0; k < N; k++) od.FullBuild(so, g.Center(nearGoals[k]), true, nearGoals[k]); sw.Stop();
        Console.WriteLine("old Build (full field)            : " + (sw.Elapsed.TotalMilliseconds / N).ToString("0.000") + " ms per call");
        sw.Restart(); for (int k = 0; k < N; k++) nw.FullBuild(sn, g.Center(nearGoals[k]), true, nearGoals[k]); sw.Stop();
        Console.WriteLine("new Build (full field)            : " + (sw.Elapsed.TotalMilliseconds / N).ToString("0.000") + " ms per call");
        if (nw.Lazy)
        {
            sw.Restart(); for (int k = 0; k < N; k++) nw.LazyBuild(sn, g.Center(nearGoals[k]), true, nearGoals[k], froms[k]); sw.Stop();
            Console.WriteLine("new Build lazy, goal 3 cells away : " + (sw.Elapsed.TotalMilliseconds / N).ToString("0.000") + " ms per call");
            sw.Restart(); for (int k = 0; k < N; k++) nw.LazyBuild(sn, g.Center(farGoals[k]), true, farGoals[k], froms[k]); sw.Stop();
            Console.WriteLine("new Build lazy, random far goal   : " + (sw.Elapsed.TotalMilliseconds / N).ToString("0.000") + " ms per call");
        }
        bool r; Vector3 n; float l;
        foreach (var side in new[] { od, nw })
        {
            object s = side == nw ? sn : so;
            // the line of sight case returns before any field; take pairs where the map had to route (GridSight blocked) for the near set
            sw.Restart(); for (int k = 0; k < N; k++) { SetTime(2000f + k * 100f); side.ClearFields(s); side.CallNext(null, g.Center(froms[k]), g.Center(nearGoals[k]), out r, out n, out l); } sw.Stop();
            Console.WriteLine(side.Tag + " Next, cold cache, goal 3 cells   : " + (sw.Elapsed.TotalMilliseconds / N).ToString("0.000") + " ms per call (incl. straight-line early outs)");
            sw.Restart(); for (int k = 0; k < N; k++) { SetTime(2000f + k * 100f); side.ClearFields(s); side.CallNext(null, g.Center(froms[k]), g.Center(farGoals[k]), out r, out n, out l); } sw.Stop();
            Console.WriteLine(side.Tag + " Next, cold cache, far goal       : " + (sw.Elapsed.TotalMilliseconds / N).ToString("0.000") + " ms per call");
            // the "no map route" case: goal on the sealed island (another area) - old: 2-3 full Builds per call; new: area short cut + cached NPC field
            int isl = -1; for (int i = 0; i < comp.Length && isl < 0; i++) if (comp[i] >= 0 && comp[i] != big && sizes[comp[i]] >= 30) isl = i;
            if (isl >= 0)
            {
                sw.Restart(); for (int k = 0; k < N; k++) { SetTime(2000f + k * 100f); side.ClearFields(s); side.CallNext(null, g.Center(froms[k]), g.Center(isl), out r, out n, out l); } sw.Stop();
                Console.WriteLine(side.Tag + " Next, cold, goal in another area : " + (sw.Elapsed.TotalMilliseconds / N).ToString("0.000") + " ms per call");
                sw.Restart(); SetTime(3000f); side.ClearFields(s); for (int k = 0; k < N; k++) { SetTime(3000f + k * 0.1f); side.CallNext(null, g.Center(froms[k % 4]), g.Center(isl), out r, out n, out l); } sw.Stop();
                Console.WriteLine(side.Tag + " Next, warm, goal in another area : " + (sw.Elapsed.TotalMilliseconds / N).ToString("0.000") + " ms per call (4 NPC cells, 0.1 s apart)");
            }
            sw.Restart(); for (int k = 0; k < N; k++) { SetTime(2000f + k * 100f); side.ClearFields(s); side.CallNext(null, g.Center(froms[k]), new Vector3(g.Min.x - 20f, 0f, g.Min.z + (k * 7 % 227) * 0.5f), out r, out n, out l); } sw.Stop();
            Console.WriteLine(side.Tag + " Next, cold, goal outside         : " + (sw.Elapsed.TotalMilliseconds / N).ToString("0.000") + " ms per call (PickOutExit when the line out is blocked)");
        }
    }
}

// ---------- the Cecil patch (a separate process: the assemblies must not be loaded yet) ----------
static class Patcher
{
    public static void Patch(string dir)
    {
        string core = Path.Combine(dir, "UnityEngine.CoreModule.dll");
        var rp = new Mono.Cecil.ReaderParameters { InMemory = true, AssemblyResolver = new Mono.Cecil.DefaultAssemblyResolver() };
        ((Mono.Cecil.DefaultAssemblyResolver)rp.AssemblyResolver).AddSearchDirectory(dir);
        var ad = Mono.Cecil.AssemblyDefinition.ReadAssembly(core, rp);
        var md = ad.MainModule;
        var time = md.GetType("UnityEngine.Time");
        var fld = new Mono.Cecil.FieldDefinition("WW_FakeTime", Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static, md.TypeSystem.Single);
        time.Fields.Add(fld);
        foreach (var m in time.Methods)
        {
            if (m.Name != "get_time" && m.Name != "get_unscaledTime") continue;
            m.ImplAttributes = Mono.Cecil.MethodImplAttributes.IL | Mono.Cecil.MethodImplAttributes.Managed;
            m.Body = new Mono.Cecil.Cil.MethodBody(m);
            var il = m.Body.GetILProcessor();
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, fld); il.Emit(Mono.Cecil.Cil.OpCodes.Ret);
        }
        var obj = md.GetType("UnityEngine.Object");
        Mono.Cecil.FieldDefinition ptr = null; foreach (var f in obj.Fields) if (f.Name == "m_CachedPtr") ptr = f;
        MethodInfo toInt = null; foreach (var mi in typeof(IntPtr).GetMethods()) if (mi.Name == "op_Explicit" && mi.ReturnType == typeof(int) && mi.GetParameters()[0].ParameterType == typeof(IntPtr)) toInt = mi;
        foreach (var m in obj.Methods)
        {
            if (m.Name != "GetInstanceID") continue;
            m.ImplAttributes = Mono.Cecil.MethodImplAttributes.IL | Mono.Cecil.MethodImplAttributes.Managed;
            m.Body = new Mono.Cecil.Cil.MethodBody(m);
            var il = m.Body.GetILProcessor();
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0); il.Emit(Mono.Cecil.Cil.OpCodes.Ldfld, ptr); il.Emit(Mono.Cecil.Cil.OpCodes.Call, md.ImportReference(toInt)); il.Emit(Mono.Cecil.Cil.OpCodes.Ret);
        }
        ad.Write(core);
        Console.WriteLine("patched " + core + " (Time.time -> Time.WW_FakeTime, Object.GetInstanceID -> m_CachedPtr)");
        string before = Path.Combine(dir, "WildWasteland_before.dll");
        var bd = Mono.Cecil.AssemblyDefinition.ReadAssembly(before, rp);
        bd.Name.Name = "WildWasteland_before"; bd.MainModule.Name = "WildWasteland_before.dll";
        bd.Write(before);
        Console.WriteLine("renamed assembly " + before);
    }
}
