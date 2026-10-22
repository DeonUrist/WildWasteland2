using System.Collections.Generic;

namespace WildWasteland.Modules.Tools
{
    internal static class EditorSelection
    {
        /// <summary>Retains exactly the requested expanded group and clears other ledger expansion state.</summary>
        internal static string Select(HashSet<string> expanded, string id, bool open)
        {
            expanded.Clear();
            if (open && !string.IsNullOrEmpty(id)) { expanded.Add(id); return id; }
            return "";
        }
    }
}
