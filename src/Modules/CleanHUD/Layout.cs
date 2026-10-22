using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace WildWasteland.Modules.CleanHUD
{
    // One HUD group placed in the 1920x1080 design frame (top-left origin), as exported by the mockup's "Copy layout".
    internal sealed class Placement
    {
        internal string Id;
        internal float X, Y, Scale = 1f;
        internal bool CenterX;
    }

    // layout.json = the mockup's export format. Missing keys fall back to the approved mockup values.
    internal sealed class Layout
    {
        internal readonly Dictionary<string, Placement> Elements = new Dictionary<string, Placement>();
        internal Color IconTint = Hex("#e2bf6c"), HpColor = Hex("#d8492f"), ArmorColor = Hex("#9fb3bf");
        internal bool InactiveTint;          // false = dim white (mockup default)
        internal float VisibleArc = 160f;
        internal bool EightPoint;            // false = N E S W only
        internal bool HeadingCaret;
        internal bool PinOffArc;             // false = hide markers outside the arc

        internal static readonly string[] Ids = { "compass", "vitals", "weapon", "slots", "insanity", "log" };

        internal static Layout Defaults()
        {
            var l = new Layout();
            l.Elements["compass"] = new Placement { Id = "compass", X = 410, Y = 24 };
            l.Elements["vitals"] = new Placement { Id = "vitals", X = 24, Y = 930 };
            l.Elements["weapon"] = new Placement { Id = "weapon", X = 1540, Y = 892 };
            l.Elements["slots"] = new Placement { Id = "slots", X = 627, Y = 972, CenterX = true };
            l.Elements["insanity"] = new Placement { Id = "insanity", X = 1716, Y = 24 }; // right edge 24 px from the screen edge, top level with the compass
            l.Elements["log"] = new Placement { Id = "log", X = 24, Y = 24 };          // message log, top left
            return l;
        }

        internal static Layout Parse(string text)
        {
            var l = Defaults();
            var root = Json.Parse(text) as Dictionary<string, object>;
            if (root == null) throw new FormatException("layout.json must be a JSON object");
            var style = Json.Obj(root, "style");
            if (style != null)
            {
                l.IconTint = Hex(Json.Text(style, "iconTint", "#e2bf6c"));
                l.HpColor = Hex(Json.Text(style, "hpColor", "#d8492f"));
                l.ArmorColor = Hex(Json.Text(style, "armorColor", "#9fb3bf"));
                l.InactiveTint = Json.Text(style, "inactiveSlotIcons", "white") == "tint";
            }
            var cmp = Json.Obj(root, "compass");
            if (cmp != null)
            {
                l.VisibleArc = Mathf.Clamp(Json.Num(cmp, "visibleArcDeg", 160f), 60f, 300f);
                l.EightPoint = Json.Text(cmp, "labels", "cardinal") == "8-point";
                l.HeadingCaret = Json.Bool(cmp, "headingCaret", false);
                l.PinOffArc = Json.Text(cmp, "offArcMarkers", "hide") == "clamp";
            }
            var els = Json.Arr(root, "elements");
            if (els != null)
                foreach (var o in els)
                {
                    var e = o as Dictionary<string, object>;
                    var id = Json.Text(e, "id", "");
                    Placement p;
                    if (e == null || !l.Elements.TryGetValue(id, out p)) continue;
                    p.X = Json.Num(e, "x", p.X);
                    p.Y = Json.Num(e, "y", p.Y);
                    p.Scale = Mathf.Clamp(Json.Num(e, "scale", 1f), 0.4f, 2f);
                    p.CenterX = Json.Bool(e, "centerX", p.CenterX);
                }
            return l;
        }

        internal static Color Hex(string s)
        {
            Color c;
            if (s != null && ColorUtility.TryParseHtmlString(s, out c)) return c;
            return Color.white;
        }
        private static string HexOf(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c).ToLowerInvariant(); }

        // Same shape as the mockup export, so a layout copied from the mockup can be pasted over this file.
        internal static string DefaultJson()
        {
            var l = Defaults();
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            sb.Append("{\n \"version\": 1,\n \"reference\": {\"width\": 1920, \"height\": 1080, \"origin\": \"top-left\", \"scaler\": \"ScaleWithScreenSize/Expand\"},\n");
            sb.Append(" \"style\": {\"iconTint\": \"").Append(HexOf(l.IconTint)).Append("\", \"hpColor\": \"").Append(HexOf(l.HpColor))
              .Append("\", \"armorColor\": \"").Append(HexOf(l.ArmorColor)).Append("\", \"inactiveSlotIcons\": \"white\"},\n");
            sb.Append(" \"compass\": {\"visibleArcDeg\": 160, \"labels\": \"cardinal\", \"headingCaret\": false, \"offArcMarkers\": \"hide\"},\n");
            sb.Append(" \"elements\": [\n");
            for (int i = 0; i < Ids.Length; i++)
            {
                var p = l.Elements[Ids[i]];
                sb.Append("  {\"id\": \"").Append(p.Id).Append("\", \"x\": ").Append(p.X.ToString(ci)).Append(", \"y\": ").Append(p.Y.ToString(ci))
                  .Append(", \"scale\": 1, \"centerX\": ").Append(p.CenterX ? "true" : "false").Append("}").Append(i < Ids.Length - 1 ? ",\n" : "\n");
            }
            sb.Append(" ]\n}\n");
            return sb.ToString();
        }
    }

    // Loads BepInEx\config\CleanHUD\layout.json, writes the default once, reloads when the file changes.
    internal sealed class LayoutFile
    {
        internal Layout Current = Layout.Defaults();
        internal int Revision;
        private readonly string _path;
        private DateTime _stamp = DateTime.MinValue;
        private float _nextCheck;

        internal LayoutFile(string path) { _path = path; }
        internal string Path { get { return _path; } }

        internal void Poll(bool force)
        {
            if (!force && Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 2f;
            try
            {
                if (!File.Exists(_path))
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path));
                    File.WriteAllText(_path, Layout.DefaultJson());
                }
                var stamp = File.GetLastWriteTimeUtc(_path);
                if (stamp == _stamp && !force) return;
                _stamp = stamp;
                Current = Layout.Parse(File.ReadAllText(_path));
                Revision++;
                CleanHudModule.Log("Layout loaded from " + _path);
            }
            catch (Exception e)
            {
                CleanHudModule.Warn("layout.json not usable (" + e.Message + "); keeping the previous layout.");
            }
        }
    }
}
