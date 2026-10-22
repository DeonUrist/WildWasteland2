using System;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    // (WW) "Display navigation data": GL lines after the main camera rendered (no objects, nothing saved). Surfaces get one colour
    // each (their walks and node ticks), ladders cyan (foot -> rung line -> platform), climbs orange, drops red; editor links are drawn
    // brighter and switched-off links grey. The editor draws its draft through the same code with the selection highlighted.
    internal static class PoiNavView
    {
        private static Material _mat;
        private static bool _hooked;
        internal static PoiNavData EditData;      // the editor's draft (drawn instead of the saved data of its POI)
        internal static Transform EditRoot;
        internal static int Selected = -1;
        internal static bool ShowSurfaces = true, ShowLadders = true, ShowClimbs = true, ShowDrops = true, ShowOff = true;   // editor filters
        internal static bool Visible(PoiNavLink l)
        {
            if (l.Off && !ShowOff) return false;
            return l.Kind == PoiLinkKind.Ladder ? ShowLadders : l.Kind == PoiLinkKind.Climb ? ShowClimbs : ShowDrops;
        }

        internal static void Tick()
        {
            PoiNav.LearnLadderSide();
            PoiNav.WalkerTick();
            bool want = PoiNav.Display != null && PoiNav.Display.Value && Plugin.EnableDebug.Value || EditData != null;
            if (want && PoiNav.Display.Value) PoiNav.Rescan();
            if (want == _hooked) return;
            if (want) Camera.onPostRender += Draw; else Camera.onPostRender -= Draw;
            _hooked = want;
        }
        internal static void Unhook() { if (_hooked) Camera.onPostRender -= Draw; _hooked = false; EditData = null; EditRoot = null; }

        static bool Material()
        {
            if (_mat != null) return true;
            var shader = Shader.Find("Hidden/Internal-Colored"); if (shader == null) return false;
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _mat.SetInt("_ZWrite", 0);
            _mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            return true;
        }

        static void Draw(Camera cam)
        {
            try
            {
                if (cam == null || cam != GameRefs.MainCamera || !Material()) return;
                Vector3 eye = cam.transform.position;
                _mat.SetPass(0);
                GL.PushMatrix(); GL.MultMatrix(Matrix4x4.identity);
                GL.Begin(GL.LINES);
                if (EditData != null && EditRoot != null) DrawData(EditRoot, EditData, null, Selected);
                if (PoiNav.Display != null && PoiNav.Display.Value)
                    foreach (var i in PoiNav.Instances)
                    {
                        if (!i.Ready || i.Root == null || i.Root == EditRoot || (i.Box.center - eye).sqrMagnitude > 250f * 250f) continue;
                        DrawData(i.Root, i.Data, i.World, -1);
                    }
                GL.End(); GL.PopMatrix();
            }
            catch (Exception e) { Log.Warn("[NPC] POI navigation view: " + e.Message); }
        }

        internal static Color SurfaceColor(int s) { var c = Color.HSVToRGB(Mathf.Repeat(s * .618f, 1f), .65f, 1f); c.a = .85f; return c; }

        static void DrawData(Transform root, PoiNavData d, Vector3[] world, int selected)
        {
            Vector3 up = Vector3.up * .05f;
            for (int i = 0; ShowSurfaces && i + 1 < d.Edges.Count; i += 2)
            {
                int a = d.Edges[i], b = d.Edges[i + 1];
                GL.Color(SurfaceColor(d.Nodes[a].Surface));
                GL.Vertex(N(root, d, world, a) + up); GL.Vertex(N(root, d, world, b) + up);
            }
            for (int i = 0; ShowSurfaces && i < d.Nodes.Count; i++)
            {
                Vector3 p = N(root, d, world, i);
                GL.Color(SurfaceColor(d.Nodes[i].Surface)); GL.Vertex(p); GL.Vertex(p + Vector3.up * .35f);
            }
            for (int i = 0; i < d.Links.Count; i++)
            {
                var l = d.Links[i];
                if (!Visible(l) && i != selected) continue;
                Color c = l.Kind == PoiLinkKind.Ladder ? new Color(0f, .9f, 1f) : l.Kind == PoiLinkKind.Climb ? new Color(1f, .55f, 0f) : new Color(1f, .15f, .1f);
                if (l.Manual) c = Color.Lerp(c, Color.white, .35f);
                if (l.Off) c = new Color(.45f, .45f, .45f);
                if (i == selected) c = Color.white;
                c.a = 1f; GL.Color(c);
                Vector3 f = root.TransformPoint(l.From), t = root.TransformPoint(l.To);
                if (l.Kind == PoiLinkKind.Ladder)
                {
                    Vector3 b = root.TransformPoint(l.LineBottom), top = root.TransformPoint(l.LineTop);
                    if (b.y - f.y > .7f)
                    {
                        // a hanging ladder: the jump up to its bottom rung (yellow arc)
                        GL.Color(new Color(1f, .9f, .1f, 1f));
                        Vector3 under = new Vector3(b.x, f.y, b.z); Line(f, under);
                        Vector3 prev = under;
                        for (int k = 1; k <= 8; k++) { float u = k / 8f; Vector3 p = Vector3.Lerp(under, b, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * .2f; Line(prev, p); prev = p; }
                        GL.Color(c);
                        Line(b, top); Line(top, t);
                    }
                    else { Line(f, b); Line(b, top); Line(top, t); }
                    Vector3 side = root.rotation * Quaternion.Euler(0f, l.Yaw + 90f, 0f) * Vector3.forward * .25f;
                    Line(b + side, top + side); Line(b - side, top - side);
                    for (float y = 0f; y <= 1f; y += .1f) { Vector3 p = Vector3.Lerp(b, top, y); Line(p - side, p + side); }
                }
                else
                {
                    Vector3 mid = new Vector3(f.x, Mathf.Max(f.y, t.y) + .2f, f.z);
                    Line(f, mid); Line(mid, t);
                    // arrow head at the landing
                    Vector3 dir = (t - mid); dir.y = 0f; if (dir.sqrMagnitude > 1e-4f) dir.Normalize(); else dir = Vector3.forward;
                    Vector3 side = Vector3.Cross(Vector3.up, dir) * .15f;
                    Line(t, t - dir * .3f + side + Vector3.up * .1f); Line(t, t - dir * .3f - side + Vector3.up * .1f);
                }
                Cross(f, .15f); Cross(t, .15f);
            }
        }
        static Vector3 N(Transform root, PoiNavData d, Vector3[] world, int i) { return world != null && i < world.Length ? world[i] : root.TransformPoint(d.Nodes[i].Pos); }
        static void Line(Vector3 a, Vector3 b) { GL.Vertex(a); GL.Vertex(b); }
        static void Cross(Vector3 p, float s) { Line(p - Vector3.right * s, p + Vector3.right * s); Line(p - Vector3.forward * s, p + Vector3.forward * s); Line(p, p + Vector3.up * s * 2f); }
    }
}
