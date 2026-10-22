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
    /// <summary>
    /// (WW) Pre-recorded POI navigation, one file per POI type, all positions POI-local (root.InverseTransformPoint):
    /// nodes  - walkable points (~2 m apart) on every standable surface; Surface = connected walkable patch ("level"), Terrain = ground
    /// nodes that are re-projected onto the real terrain per instance
    /// edges  - straight walks between nodes of one surface
    /// links  - traversals between surfaces: Ladder (From = foot of the ladder, To = platform, LineBottom/LineTop = the rung line),
    /// Climb (From = below the ledge, To = on top) and Drop (jump down). Manual links come from the editor and survive rebakes;
    /// Off links (also baked ones switched off in the editor) are kept but never used.
    /// </summary>
    internal enum PoiLinkKind { Ladder, Climb, Drop }

    /// <summary>One walkable node: POI-local position, surface (level) index, terrain node flag (re-projected per instance).</summary>
    internal sealed class PoiNavNode { public Vector3 Pos; public int Surface; public bool Terrain; }
    internal sealed class PoiNavLink
    {
        public PoiLinkKind Kind; public bool Manual, Off;
        public Vector3 From, To, LineBottom, LineTop; public float Yaw;   // Yaw: POI-local facing while traversing (towards the ladder / wall)
        public int FromSurface = -1, ToSurface = -1;                      // the surfaces its ends stand on (-1 = the nearest node decides)
        /// <summary>Shallow copy of the link (all value fields).</summary>
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

        /// <summary>Deep copy of the data (editor drafts).</summary>
        internal PoiNavData Copy()
        {
            var d = new PoiNavData { Poi = Poi, Signature = Signature, Box = Box, Surfaces = Surfaces };
            foreach (var n in Nodes) d.Nodes.Add(new PoiNavNode { Pos = n.Pos, Surface = n.Surface, Terrain = n.Terrain });
            d.Edges.AddRange(Edges);
            foreach (var l in Links) d.Links.Add(l.Copy());
            return d;
        }

        /// <summary>Recomputes the local bounds (nodes and link ends, +2 m) and the surface count.</summary>
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
        /// <summary>Invariant short float text.</summary>
        static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        /// <summary>Invariant vector text (three floats).</summary>
        static string V(Vector3 v) { return F(v.x) + " " + F(v.y) + " " + F(v.z); }

        /// <summary>The hand-editable text form (header, nodes, edges, links).</summary>
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

        /// <summary>Invariant float parse.</summary>
        static float P(string s) { return float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture); }
        /// <summary>Vector parse from three words starting at i.</summary>
        static Vector3 PV(string[] w, int i) { return new Vector3(P(w[i]), P(w[i + 1]), P(w[i + 2])); }

        /// <summary>Parses the text form; throws on bad data (edges to missing nodes).</summary>
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

}
