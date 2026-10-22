using System;
using System.Globalization;

namespace WildWasteland.Core
{
    /// <summary>
    /// GameObject name helpers around Unity's "(Clone)" suffix and the game's "&lt;prefab&gt;(Clone)&lt;N&gt;" save names, replacing
    /// 6 strippers: Quests Game.BaseName (first "(Clone)", Trim) = <see cref="Base"/>; Core SaveCleanup:42 (first "(Clone)", no
    /// trim) = <see cref="StripClone"/>; Performance RenderShared.Key/RootHash (first "(Clone)" when not at index 0, TrimEnd)
    /// = <see cref="RenderBase"/>; NPC Nav debug names (Replace all) = <see cref="RemoveClone"/>; Quests CrewHistory and
    /// SaveCleanup:145 (LastIndexOf + int.TryParse of the rest) = <see cref="CloneNumber"/>. <see cref="BaseEquals"/> and
    /// <see cref="BaseStartsWith"/> compare without allocating (for per-node walks).
    /// </summary>
    public static class Names
    {
        static readonly System.Text.RegularExpressions.Regex InstanceSuffix = new System.Text.RegularExpressions.Regex(@"(\(Clone\)\d*|\s\(\d+\))+$");
        /// <summary>Prefab label used by vehicle templates: removes trailing clone numbers and Unity numbered-copy suffixes, then trims.</summary>
        public static string Prefab(string instance) { return InstanceSuffix.Replace(instance ?? "", "").Trim(); }

        /// <summary>The Unity instantiate suffix.</summary>
        public const string Clone = "(Clone)";

        /// <summary>Quests BaseName: text before the first "(Clone)", trimmed; "" for null/empty. Allocates when it changes the name.</summary>
        public static string Base(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string s = name;
            int i = s.IndexOf(Clone, StringComparison.Ordinal);
            if (i >= 0) s = s.Substring(0, i);
            return s.Trim();
        }

        /// <summary>SaveCleanup rule: text before the first "(Clone)" without trimming (null stays null).</summary>
        public static string StripClone(string name)
        {
            if (name == null) return null;
            int at = name.IndexOf(Clone, StringComparison.Ordinal);
            return at >= 0 ? name.Substring(0, at) : name;
        }

        /// <summary>RenderShared rule: text before the first "(Clone)" when it is not at index 0, TrimEnd; otherwise the name unchanged.</summary>
        public static string RenderBase(string name)
        {
            if (name == null) return null;
            int k = name.IndexOf(Clone, StringComparison.Ordinal);
            return k > 0 ? name.Substring(0, k).TrimEnd() : name;
        }

        /// <summary>Every "(Clone)" removed (NPC Nav debug file names).</summary>
        public static string RemoveClone(string name) { return name == null ? null : name.Replace(Clone, ""); }

        /// <summary>
        /// Save-name number: true when name contains "(Clone)" and the text after the LAST one parses as an int (int.TryParse rules)
        /// - the game's "&lt;prefab&gt;(Clone)&lt;N&gt;" registered objects (SaveCleanup/CrewHistory; CrewHistory also required N &gt;= 0).
        /// </summary>
        public static bool CloneNumber(string name, out int number)
        {
            number = 0;
            if (name == null) return false;
            int at = name.LastIndexOf(Clone, StringComparison.Ordinal);
            return at >= 0 && int.TryParse(name.Substring(at + Clone.Length), NumberStyles.Integer, CultureInfo.CurrentCulture, out number);
        }

        /// <summary>Length of <see cref="Base"/>(name) region before trimming: index of the first "(Clone)" or the name length.</summary>
        static int BaseEnd(string name)
        {
            int i = name.IndexOf(Clone, StringComparison.Ordinal);
            return i >= 0 ? i : name.Length;
        }

        /// <summary>True when Base(name) equals baseName (ordinal), without allocating.</summary>
        public static bool BaseEquals(string name, string baseName) { return BaseEquals(name, baseName, StringComparison.Ordinal); }

        /// <summary>True when Base(name) equals baseName under comparison, without allocating (whitespace around the base is ignored like Trim).</summary>
        public static bool BaseEquals(string name, string baseName, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(name)) return string.IsNullOrEmpty(baseName);
            if (baseName == null) return false;
            int start = 0, end = BaseEnd(name);
            while (start < end && char.IsWhiteSpace(name[start])) start++;
            while (end > start && char.IsWhiteSpace(name[end - 1])) end--;
            int len = end - start;
            return len == baseName.Length && string.Compare(name, start, baseName, 0, len, comparison) == 0;
        }

        /// <summary>True when Base(name) starts with prefix (ordinal), without allocating.</summary>
        public static bool BaseStartsWith(string name, string prefix)
        {
            if (string.IsNullOrEmpty(name) || prefix == null) return false;
            int start = 0, end = BaseEnd(name);
            while (start < end && char.IsWhiteSpace(name[start])) start++;
            return end - start >= prefix.Length && string.CompareOrdinal(name, start, prefix, 0, prefix.Length) == 0;
        }
    }
}
