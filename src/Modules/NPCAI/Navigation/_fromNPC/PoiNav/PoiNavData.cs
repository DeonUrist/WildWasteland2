using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    // (WW) Pre-recorded POI navigation, one file per POI type, all positions POI-local (root.InverseTransformPoint):
    //   nodes  - walkable points (~2 m apart) on every standable surface; Surface = connected walkable patch ("level"), Terrain = ground
    //            nodes that are re-projected onto the real terrain per instance
    //   edges  - straight walks between nodes of one surface
    //   links  - traversals between surfaces: Ladder (From = foot of the ladder, To = platform, LineBottom/LineTop = the rung line),
    //            Climb (From = below the ledge, To = on top) and Drop (jump down). Manual links come from the editor and survive rebakes;
    //            Off links (also baked ones switched off in the editor) are kept but never used.
    internal enum PoiLinkKind { Ladder, Climb, Drop }

    internal sealed class PoiNavNode { public Vector3 Pos; public int Surface; public bool Terrain; }
    internal sealed class PoiNavLink
    {
        public PoiLinkKind Kind; public bool Manual, Off;
        public Vector3 From, To, LineBottom, LineTop; public float Yaw;   // Yaw: POI-local facing while traversing (towards the ladder / wall)
        public int FromSurface = -1, ToSurface = -1;                      // the surfaces its ends stand on (-1 = the nearest node decides)
        public PoiNavLink Copy() { return (PoiNavLink)MemberwiseClone(); }
    }

    internal sealed class PoiNavData
    {
        internal const int Version = 1;
        internal string Poi = "", Signature = "";
        internal readonly List<PoiNavNode> Nodes = new List<PoiNavNode>();
        internal readonly List<int> Edges = new List<int>();            // pairs a, b
        internal readonly List<PoiNavLink> Links = new List<PoiNavLink>();
        internal Bounds Box;                                            // local bounds of the nodes (computed)
        internal int Surfaces;

        internal PoiNavData Copy()
        {
            var d = new PoiNavData { Poi = Poi, Signature = Signature, Box = Box, Surfaces = Surfaces };
            foreach (var n in Nodes) d.Nodes.Add(new PoiNavNode { Pos = n.Pos, Surface = n.Surface, Terrain = n.Terrain });
            d.Edges.AddRange(Edges);
            foreach (var l in Links) d.Links.Add(l.Copy());
            return d;
        }

        internal void Recompute()
        {
            Surfaces = 0;
            if (Nodes.Count == 0) { Box = new Bounds(); return; }
            var b = new Bounds(Nodes[0].Pos, Vector3.zero);
            foreach (var n in Nodes) { b.Encapsulate(n.Pos); if (n.Surface + 1 > Surfaces) Surfaces = n.Surface + 1; }
            foreach (var l in Links) { b.Encapsulate(l.From); b.Encapsulate(l.To); }
            b.Expand(new Vector3(2f, 2f, 2f));
            Box = b;
        }

        // ---------- text format (hand editable, diff friendly) ----------
        static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        static string V(Vector3 v) { return F(v.x) + " " + F(v.y) + " " + F(v.z); }

        internal string Write()
        {
            var sb = new StringBuilder();
            sb.Append("WWPOINAV ").Append(Version).Append('\n');
            sb.Append("poi ").Append(Poi).Append('\n');
            sb.Append("sig ").Append(Signature).Append('\n');
            foreach (var n in Nodes) sb.Append("node ").Append(V(n.Pos)).Append(' ').Append(n.Surface).Append(' ').Append(n.Terrain ? 1 : 0).Append('\n');
            for (int i = 0; i + 1 < Edges.Count; i += 2) sb.Append("edge ").Append(Edges[i]).Append(' ').Append(Edges[i + 1]).Append('\n');
            foreach (var l in Links)
                sb.Append("link ").Append(l.Kind).Append(' ').Append(l.Manual ? 1 : 0).Append(' ').Append(l.Off ? 1 : 0).Append(' ')
                  .Append(V(l.From)).Append(' ').Append(V(l.To)).Append(' ').Append(V(l.LineBottom)).Append(' ').Append(V(l.LineTop)).Append(' ').Append(F(l.Yaw)).Append(' ').Append(l.FromSurface).Append(' ').Append(l.ToSurface).Append('\n');
            return sb.ToString();
        }

        static float P(string s) { return float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture); }
        static Vector3 PV(string[] w, int i) { return new Vector3(P(w[i]), P(w[i + 1]), P(w[i + 2])); }

        internal static PoiNavData Read(string text)
        {
            var d = new PoiNavData();
            var lines = text.Split('\n');
            if (lines.Length == 0 || !lines[0].Trim().StartsWith("WWPOINAV ", StringComparison.Ordinal)) throw new InvalidDataException("not a POI navigation file");
            if (int.Parse(lines[0].Trim().Substring(9), CultureInfo.InvariantCulture) != Version) throw new InvalidDataException("unsupported version");
            for (int li = 1; li < lines.Length; li++)
            {
                string line = lines[li].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var w = line.Split(' ');
                switch (w[0])
                {
                    case "poi": d.Poi = line.Substring(4); break;
                    case "sig": d.Signature = line.Length > 4 ? line.Substring(4) : ""; break;
                    case "node": d.Nodes.Add(new PoiNavNode { Pos = PV(w, 1), Surface = int.Parse(w[4], CultureInfo.InvariantCulture), Terrain = w[5] == "1" }); break;
                    case "edge":
                        int a = int.Parse(w[1], CultureInfo.InvariantCulture), b = int.Parse(w[2], CultureInfo.InvariantCulture);
                        d.Edges.Add(a); d.Edges.Add(b); break;
                    case "link":
                        d.Links.Add(new PoiNavLink
                        {
                            Kind = (PoiLinkKind)Enum.Parse(typeof(PoiLinkKind), w[1]), Manual = w[2] == "1", Off = w[3] == "1",
                            From = PV(w, 4), To = PV(w, 7), LineBottom = PV(w, 10), LineTop = PV(w, 13), Yaw = P(w[16]),
                            FromSurface = w.Length > 18 ? int.Parse(w[17], CultureInfo.InvariantCulture) : -1,
                            ToSurface = w.Length > 18 ? int.Parse(w[18], CultureInfo.InvariantCulture) : -1
                        });
                        break;
                }
            }
            for (int i = 0; i < d.Edges.Count; i++) if (d.Edges[i] < 0 || d.Edges[i] >= d.Nodes.Count) throw new InvalidDataException("edge to a missing node");
            d.Recompute();
            return d;
        }
    }

    // where the files live: the editor / baker writes config\WildWasteland\NPC\PoiNav\<type>.txt; a shipped pack may sit in
    // plugins\WildWasteland\NPC\PoiNav\ (read when no config file exists)
    internal static class PoiNavStore
    {
        internal static string ConfigDir { get { return Path.Combine(Assets.ModuleConfigDir("NPC"), "PoiNav"); } }
        internal static string ShippedDir { get { return Path.Combine(Assets.ModuleDir("NPC"), "PoiNav"); } }
        static string FileName(string poi)
        {
            var sb = new StringBuilder();
            foreach (char c in poi) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.ToString() + ".txt";
        }
        internal static string ConfigPath(string poi) { return Path.Combine(ConfigDir, FileName(poi)); }

        private static readonly Dictionary<string, PoiNavData> _cache = new Dictionary<string, PoiNavData>(StringComparer.Ordinal);
        private sealed class Loading
        {
            internal Task<PoiNavData> Work;
            internal readonly CancellationTokenSource Cancel = new CancellationTokenSource();
            internal readonly List<string> Errors = new List<string>();
        }
        private static readonly Dictionary<string, Loading> _loading = new Dictionary<string, Loading>(StringComparer.Ordinal);
        private static readonly object _readLock = new object();
        internal static void Forget() { foreach (var job in _loading.Values) job.Cancel.Cancel(); _loading.Clear(); _cache.Clear(); }
        internal static void Forget(string poi) { Loading job; if (_loading.TryGetValue(poi, out job)) { job.Cancel.Cancel(); _loading.Remove(poi); } _cache.Remove(poi); }

        internal static PoiNavData GetAsync(string poi)
        {
            if (string.IsNullOrEmpty(poi)) return null;
            PoiNavData data; if (_cache.TryGetValue(poi, out data)) return data;
            Loading job;
            if (!_loading.TryGetValue(poi, out job))
            {
                var paths = new[] { ConfigPath(poi), Path.Combine(ShippedDir, FileName(poi)) };
                job = new Loading(); var load = job;
                job.Work = Task.Run(() =>
                {
                    lock (_readLock) foreach (var path in paths)
                    {
                        if (load.Cancel.IsCancellationRequested) return null;
                        try { if (File.Exists(path)) { var parsed = PoiNavData.Read(File.ReadAllText(path)); parsed.Poi = poi; return parsed; } }
                        catch (Exception e) { load.Errors.Add("[NPC] POI navigation file " + path + " ignored: " + e.Message); }
                    }
                    return (PoiNavData)null;
                });
                _loading.Add(poi, job); return null;
            }
            if (!job.Work.IsCompleted) return null;
            _loading.Remove(poi);
            if (job.Work.IsFaulted) { Log.Warn("[NPC] POI navigation load failed: " + poi + ": " + job.Work.Exception.GetBaseException().Message); _cache[poi] = null; return null; }
            foreach (var error in job.Errors) Log.Warn(error);
            data = job.Work.Result; _cache[poi] = data; return data;
        }

        /// <summary>The data for a POI type, or null (cached, also the miss).</summary>
        internal static PoiNavData Get(string poi)
        {
            if (string.IsNullOrEmpty(poi)) return null;
            PoiNavData d;
            if (_cache.TryGetValue(poi, out d)) return d;
            d = null;
            foreach (var path in new[] { ConfigPath(poi), Path.Combine(ShippedDir, FileName(poi)) })
            {
                try { if (File.Exists(path)) { d = PoiNavData.Read(File.ReadAllText(path)); d.Poi = poi; break; } }
                catch (Exception e) { Log.Warn("[NPC] POI navigation file " + path + " ignored: " + e.Message); }
            }
            _cache[poi] = d;
            Loading pending; if (_loading.TryGetValue(poi, out pending)) { pending.Cancel.Cancel(); _loading.Remove(poi); }
            return d;
        }

        internal static void Save(PoiNavData d)
        {
            Directory.CreateDirectory(ConfigDir);
            string path = ConfigPath(d.Poi), tmp = path + ".tmp";
            string text = d.Write();
            PoiNavData.Read(text);   // never publish a file this reader rejects
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) { File.Copy(path, path + ".bak", true); File.Delete(path); }
            File.Move(tmp, path);
            d.Recompute();
            _cache[d.Poi] = d;
            Loading pending; if (_loading.TryGetValue(d.Poi, out pending)) { pending.Cancel.Cancel(); _loading.Remove(d.Poi); }
        }

        /// <summary>"Camp_7(Clone)", "Building_3 (Towns 1A2B/4)", "Camp_7 (WW POI nav setup)" -> the POI type.</summary>
        internal static string TypeOf(string rootName)
        {
            if (string.IsNullOrEmpty(rootName)) return "";
            int cut = rootName.Length;
            int p = rootName.IndexOf('('); if (p >= 0 && p < cut) cut = p;
            int s = rootName.IndexOf(' '); if (s >= 0 && s < cut) cut = s;
            return rootName.Substring(0, cut);
        }
    }
}
