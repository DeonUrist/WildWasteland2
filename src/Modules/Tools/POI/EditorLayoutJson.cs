using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Tools
{
    internal sealed class EditorProp
    {
        internal long Asset;
        internal string Mesh, Source;
        internal Vector3 Position, Rotation, Scale = Vector3.one;
        internal bool Collision;
        internal string Kind, Id, Profile;
        internal int Minimum = 1, Maximum = 3, Chance = 100;
        internal bool Marker { get { return !string.IsNullOrEmpty(Kind); } }
        internal EditorProp Copy() { return (EditorProp)MemberwiseClone(); }
    }
    internal sealed class EditorLayout
    {
        internal const int Limit = 512;
        internal string Type;
        internal readonly List<EditorProp> Props = new List<EditorProp>();
        internal EditorLayout Copy()
        { var copy = new EditorLayout { Type = Type }; foreach (var prop in Props) copy.Props.Add(prop.Copy()); return copy; }
    }
    // These are portable POI authoring assets, shared by every instance; never world/save-slot sidecars.
    internal static class EditorLayoutJson
    {
        internal static EditorLayout Parse(string text, string type)
        {
            var root = Json.Parse(text) as Dictionary<string, object>;
            if (root == null || Number(root, "version") != 2 || String(root, "poi") != type) throw new InvalidDataException("Invalid POI layout version/type");
            object value; var rows = root.TryGetValue("props", out value) ? value as List<object> : null;
            if (rows == null || rows.Count > EditorLayout.Limit) throw new InvalidDataException("Invalid prop list (maximum 512)");
            var layout = new EditorLayout { Type = type };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var data = row as Dictionary<string, object>; long id;
                if (data == null || !long.TryParse(String(data, "asset"), NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) throw new InvalidDataException("Invalid native prop reference");
                string mesh = String(data, "mesh");
                if (string.IsNullOrEmpty(mesh) || mesh.Length > 256) throw new InvalidDataException("Invalid mesh name");
                var prop = new EditorProp { Asset = id, Mesh = mesh, Position = Vector(data, "position", -500, 500),
                    Rotation = Vector(data, "rotation", -3600, 3600), Scale = Vector(data, "scale", .01f, 20) };
                prop.Kind = String(data, "kind");
                prop.Source = String(data, "source"); if (prop.Source != null && prop.Source.Length > 4096) throw new InvalidDataException("Prop source path too long");
                if (prop.Marker)
                {
                    prop.Id = String(data, "id"); prop.Profile = String(data, "profile");
                    Guid guid;
                    if (Array.IndexOf(EditorPalette.Kinds, prop.Kind) < 0 || !Guid.TryParseExact(prop.Id, "N", out guid) || !ids.Add(prop.Id)
                        || prop.Profile == null || prop.Profile.Length > 160) throw new InvalidDataException("Invalid marker identity/profile");
                    prop.Minimum = Integer(data, "min", 0, 32); prop.Maximum = Integer(data, "max", prop.Minimum, 32); prop.Chance = Integer(data, "chance", 0, 100);
                }
                if (!data.TryGetValue("collision", out value) || !(value is bool)) throw new InvalidDataException("Missing collision flag");
                prop.Collision = (bool)value; layout.Props.Add(prop);
            }
            return layout;
        }
        private static string String(Dictionary<string, object> data, string key)
        { object value; return data.TryGetValue(key, out value) ? value as string : null; }
        private static double Number(Dictionary<string, object> data, string key)
        { object value; if (!data.TryGetValue(key, out value) || !(value is double)) throw new InvalidDataException("Invalid " + key); return (double)value; }
        private static int Integer(Dictionary<string, object> data, string key, int min, int max)
        { double value = Number(data, key); if (value < min || value > max || value != Math.Floor(value)) throw new InvalidDataException("Invalid marker " + key); return (int)value; }
        private static Vector3 Vector(Dictionary<string, object> data, string key, float min, float max)
        {
            object value; var array = data.TryGetValue(key, out value) ? value as List<object> : null;
            if (array == null || array.Count != 3) throw new InvalidDataException("Invalid " + key);
            var vector = new Vector3();
            for (int i = 0; i < 3; i++)
            {
                if (!(array[i] is double)) throw new InvalidDataException("Invalid " + key);
                double n = (double)array[i];
                if (double.IsNaN(n) || double.IsInfinity(n) || n < min || n > max) throw new InvalidDataException("Out-of-range " + key);
                vector[i] = (float)n;
            }
            return vector;
        }
        private static void Quote(StringBuilder text, string value)
        {
            text.Append('"');
            foreach (char c in value)
                if (c == '"' || c == '\\') text.Append('\\').Append(c);
                else if (c < 32) text.Append("\\u").Append(((int)c).ToString("x4"));
                else text.Append(c);
            text.Append('"');
        }
        private static void WriteVector(StringBuilder text, Vector3 value)
        { text.Append('[').Append(value.x.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(value.y.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(value.z.ToString("R", CultureInfo.InvariantCulture)).Append(']'); }
        internal static string Serialize(EditorLayout layout)
        {
            var text = new StringBuilder("{\n  \"version\":2,\n  \"poi\":"); Quote(text, layout.Type); text.Append(",\n  \"props\":[\n");
            for (int i = 0; i < layout.Props.Count; i++)
            {
                var p = layout.Props[i]; if (i > 0) text.Append(",\n");
                text.Append("    {\"asset\":"); Quote(text, p.Asset.ToString(CultureInfo.InvariantCulture)); text.Append(",\"mesh\":"); Quote(text, p.Mesh);
                text.Append(",\"position\":"); WriteVector(text, p.Position); text.Append(",\"rotation\":"); WriteVector(text, p.Rotation);
                text.Append(",\"scale\":"); WriteVector(text, p.Scale); text.Append(",\"collision\":").Append(p.Collision ? "true" : "false");
                if (!string.IsNullOrEmpty(p.Source)) { text.Append(",\"source\":"); Quote(text, p.Source); }
                if (p.Marker)
                {
                    text.Append(",\"kind\":"); Quote(text, p.Kind); text.Append(",\"id\":"); Quote(text, p.Id); text.Append(",\"profile\":"); Quote(text, p.Profile ?? "");
                    text.Append(",\"min\":").Append(p.Minimum).Append(",\"max\":").Append(p.Maximum).Append(",\"chance\":").Append(p.Chance);
                }
                text.Append('}');
            }
            string content = text.Append("\n  ]\n}\n").ToString(); Parse(content, layout.Type);
            return content;
        }
    }
}
