using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WildWasteland.Core
{
    /// <summary>
    /// The one hand-written JSON reader/writer of the mod (AGENTS 5.5: Unity JsonUtility drops nested custom arrays).
    /// Reader: objects -> Dictionary&lt;string, object&gt; (insertion order kept), arrays -> List&lt;object&gt;, numbers -> double,
    /// strings, bools, null. It is the lenient superset of the 8 module copies: everything the strict glTF copies accept parses
    /// to the same values, and it additionally accepts // line comments, trailing commas and a truncated \u escape
    /// (Patrols CarTemplateFile behaviour). Writers: <see cref="Str"/> (escaping, as CarTemplateFile), <see cref="Write"/>
    /// (pretty printer, byte-identical to Patrols Editor StoreJson), <see cref="Float"/> ("0.####" invariant, CarTemplateFile).
    /// Not for per-frame use: parsing allocates the result tree.
    /// </summary>
    public static class Json
    {
        [ThreadStatic] static StringBuilder _sb;

        /// <summary>Parses a whole JSON document; throws FormatException (with a line number) on malformed input or trailing text.</summary>
        public static object Parse(string s)
        {
            if (s == null) throw new FormatException("unexpected end of the file");
            int i = 0;
            var v = Value(s, ref i);
            Ws(s, ref i);
            if (i < s.Length) throw new FormatException("text after the end at " + At(s, i));
            return v;
        }

        /// <summary>Parses like <see cref="Parse"/> but returns false (and null) instead of throwing.</summary>
        public static bool TryParse(string s, out object value)
        {
            try { value = Parse(s); return true; }
            catch (FormatException) { value = null; return false; }
            catch (ArgumentException) { value = null; return false; }
        }

        /// <summary>Parses a document whose root must be an object; returns null when it is not one (throws on malformed JSON).</summary>
        public static Dictionary<string, object> ParseObject(string s) { return Parse(s) as Dictionary<string, object>; }

        /// <summary>Human-readable position ("line N") of character index i, used in error messages.</summary>
        private static string At(string s, int i)
        {
            int line = 1; for (int k = 0; k < i && k < s.Length; k++) if (s[k] == '\n') line++;
            return "line " + line;
        }

        /// <summary>Skips whitespace, the UTF-8 BOM and // line comments.</summary>
        private static void Ws(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '﻿') i++;
                else if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') i++; }
                else break;
            }
        }

        /// <summary>Reads one JSON value starting at i (after optional whitespace) and advances i past it.</summary>
        private static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end of the file");
            char c = s[i];
            if (c == '{')
            {
                i++;
                var d = new Dictionary<string, object>(StringComparer.Ordinal);
                Ws(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    if (i >= s.Length || s[i] != '"') throw new FormatException("expected a \"key\" at " + At(s, i));
                    string k = Str(s, ref i);
                    Ws(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw new FormatException("expected ':' at " + At(s, i));
                    i++;
                    d[k] = Value(s, ref i);
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; Ws(s, ref i); if (i < s.Length && s[i] == '}') { i++; return d; } continue; }
                    if (i < s.Length && s[i] == '}') { i++; return d; }
                    throw new FormatException("expected ',' or '}' at " + At(s, i));
                }
            }
            if (c == '[')
            {
                i++;
                var l = new List<object>();
                Ws(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; Ws(s, ref i); if (i < s.Length && s[i] == ']') { i++; return l; } continue; }
                    if (i < s.Length && s[i] == ']') { i++; return l; }
                    throw new FormatException("expected ',' or ']' at " + At(s, i));
                }
            }
            if (c == '"') return Str(s, ref i);
            if (Word(s, ref i, "true")) return true;
            if (Word(s, ref i, "false")) return false;
            if (Word(s, ref i, "null")) return null;
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            double num;
            if (i > start && double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out num)) return num;
            throw new FormatException("unexpected '" + c + "' at " + At(s, start));
        }

        /// <summary>Consumes the literal w at i when present.</summary>
        private static bool Word(string s, ref int i, string w)
        {
            if (s.Length - i >= w.Length && string.CompareOrdinal(s, i, w, 0, w.Length) == 0) { i += w.Length; return true; }
            return false;
        }

        /// <summary>Reads a quoted string at i (s[i] is the opening quote) and unescapes it.</summary>
        private static string Str(string s, ref int i)
        {
            i++;
            var sb = _sb ?? (_sb = new StringBuilder(64));
            sb.Length = 0;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') { string r = sb.ToString(); sb.Length = 0; return r; }
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 <= s.Length) { sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); i += 4; }
                        break;
                    default: sb.Append(e); break;   // \" \\ \/
                }
            }
            sb.Length = 0;
            throw new FormatException("unterminated string");
        }

        // ------------------------------------------------------------------ typed getters (null-safe)

        /// <summary>String member k of d, or def when d is null, k is missing or not a string.</summary>
        public static string Text(Dictionary<string, object> d, string k, string def)
        { object o; return d != null && d.TryGetValue(k, out o) && o is string ? (string)o : def; }

        /// <summary>Number member k of d as float, or def when d is null, k is missing or not a number.</summary>
        public static float Num(Dictionary<string, object> d, string k, float def)
        { object o; return d != null && d.TryGetValue(k, out o) && o is double ? (float)(double)o : def; }

        /// <summary>Number member k of d as double, or def.</summary>
        public static double Double(Dictionary<string, object> d, string k, double def)
        { object o; return d != null && d.TryGetValue(k, out o) && o is double ? (double)o : def; }

        /// <summary>Number member k of d truncated to int (as the module copies' (int)Num casts), or def.</summary>
        public static int Int(Dictionary<string, object> d, string k, int def)
        { object o; return d != null && d.TryGetValue(k, out o) && o is double ? (int)(double)o : def; }

        /// <summary>Bool member k of d, or def.</summary>
        public static bool Bool(Dictionary<string, object> d, string k, bool def)
        { object o; return d != null && d.TryGetValue(k, out o) && o is bool ? (bool)o : def; }

        /// <summary>Object member k of d, or null.</summary>
        public static Dictionary<string, object> Obj(Dictionary<string, object> d, string k)
        { object o; return d != null && d.TryGetValue(k, out o) ? o as Dictionary<string, object> : null; }

        /// <summary>Array member k of d, or null.</summary>
        public static List<object> Arr(Dictionary<string, object> d, string k)
        { object o; return d != null && d.TryGetValue(k, out o) ? o as List<object> : null; }

        /// <summary>Vector-3 member k (CarTemplateFile.Json.Floats): the numbers of the array when there are exactly 3, otherwise an empty array.</summary>
        public static float[] Floats(Dictionary<string, object> d, string k)
        {
            var r = FloatList(d, k);
            return r.Length == 3 ? r : new float[0];
        }

        /// <summary>All numeric elements of array member k as floats (non-numbers skipped); empty when missing.</summary>
        public static float[] FloatList(Dictionary<string, object> d, string k)
        {
            var l = Arr(d, k);
            if (l == null) return new float[0];
            int n = 0;
            for (int i = 0; i < l.Count; i++) if (l[i] is double) n++;
            var r = new float[n]; n = 0;
            for (int i = 0; i < l.Count; i++) if (l[i] is double) r[n++] = (float)(double)l[i];
            return r;
        }

        // ------------------------------------------------------------------ writers

        /// <summary>Appends v as a quoted JSON string (null as ""); escapes \" \\ \n \r \t and other control chars as \u00xx (CarTemplateFile.Json.Str).</summary>
        public static void Str(StringBuilder sb, string v)
        {
            sb.Append('"');
            if (v != null)
                for (int i = 0; i < v.Length; i++)
                {
                    char c = v[i];
                    if (c == '"') sb.Append("\\\"");
                    else if (c == '\\') sb.Append("\\\\");
                    else if (c == '\n') sb.Append("\\n");
                    else if (c == '\r') sb.Append("\\r");
                    else if (c == '\t') sb.Append("\\t");
                    else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                }
            sb.Append('"');
        }

        /// <summary>Returns v as a quoted, escaped JSON string.</summary>
        public static string Quote(string v) { var sb = new StringBuilder((v == null ? 0 : v.Length) + 2); Str(sb, v); return sb.ToString(); }

        /// <summary>Appends a float with "0.####" invariant formatting (CarTemplateFile number style).</summary>
        public static void Float(StringBuilder sb, float v) { sb.Append(v.ToString("0.####", CultureInfo.InvariantCulture)); }

        /// <summary>Builds an ordered object from alternating key/value arguments (Patrols Editor StoreJson.Object).</summary>
        public static Dictionary<string, object> Object(params object[] values)
        {
            var result = new Dictionary<string, object>();
            for (int i = 0; i + 1 < values.Length; i += 2) result[(string)values[i]] = values[i + 1];
            return result;
        }

        /// <summary>
        /// Pretty-prints a value tree exactly like Patrols Editor StoreJson.Write: objects one member per line indented by
        /// 2 spaces per depth, arrays inline with ", " separators, other values via Convert.ToString(invariant), a final newline.
        /// </summary>
        public static string Write(object value) { var b = new StringBuilder(); Append(b, value, 0); return b.Append('\n').ToString(); }

        /// <summary>Appends value at the given depth with the StoreJson layout (see <see cref="Write"/>).</summary>
        public static void Append(StringBuilder b, object value, int depth)
        {
            if (value == null) { b.Append("null"); return; }
            if (value is string) { Str(b, (string)value); return; }
            if (value is bool) { b.Append((bool)value ? "true" : "false"); return; }
            var dict = value as Dictionary<string, object>;
            if (dict != null)
            {
                b.Append("{\n"); int n = 0;
                foreach (var pair in dict) { if (n++ > 0) b.Append(",\n"); b.Append(' ', (depth + 1) * 2); Str(b, pair.Key); b.Append(": "); Append(b, pair.Value, depth + 1); }
                b.Append('\n').Append(' ', depth * 2).Append('}'); return;
            }
            var list = value as IEnumerable;
            if (list != null) { b.Append('['); int n = 0; foreach (var item in list) { if (n++ > 0) b.Append(", "); Append(b, item, depth + 1); } b.Append(']'); return; }
            b.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
    }
}
