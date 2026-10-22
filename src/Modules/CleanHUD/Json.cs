using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WildWasteland.Modules.CleanHUD
{
    // Minimal JSON: objects -> Dictionary<string, object>, arrays -> List<object>, numbers -> double, strings, bools, null.
    internal static class Json
    {
        internal static object Parse(string s)
        {
            int i = 0;
            var v = Value(s, ref i);
            Ws(s, ref i);
            if (i < s.Length) throw new FormatException("text after the end at " + At(s, i));
            return v;
        }
        private static string At(string s, int i)
        {
            int line = 1; for (int k = 0; k < i && k < s.Length; k++) if (s[k] == '\n') line++;
            return "line " + line;
        }
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
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            double num;
            if (i > start && double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out num)) return num;
            throw new FormatException("unexpected '" + c + "' at " + At(s, start));
        }
        private static string Str(string s, ref int i)
        {
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
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
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("unterminated string");
        }
        internal static string Text(Dictionary<string, object> d, string k, string def)
        { object o; return d != null && d.TryGetValue(k, out o) && o is string ? (string)o : def; }
        internal static float Num(Dictionary<string, object> d, string k, float def)
        { object o; return d != null && d.TryGetValue(k, out o) && o is double ? (float)(double)o : def; }
        internal static bool Bool(Dictionary<string, object> d, string k, bool def)
        { object o; return d != null && d.TryGetValue(k, out o) && o is bool ? (bool)o : def; }
        internal static Dictionary<string, object> Obj(Dictionary<string, object> d, string k)
        { object o; return d != null && d.TryGetValue(k, out o) ? o as Dictionary<string, object> : null; }
        internal static List<object> Arr(Dictionary<string, object> d, string k)
        { object o; return d != null && d.TryGetValue(k, out o) ? o as List<object> : null; }
    }
}
