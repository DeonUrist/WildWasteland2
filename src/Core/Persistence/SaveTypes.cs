using System;
using System.Collections.Generic;
using System.Reflection;

namespace WildWasteland.Core
{
    /// <summary>
    /// Save compatibility for components the standalone mods left on saved objects. Easy Save 3 stores a component's type as
    /// "Namespace.Type, Assembly" (e.g. "Apocapatrol.Crew, Apocapatrol", "WarRig.WarRigUnit, WarRig"). Those assemblies no longer
    /// exist once the mods live in WildWasteland.dll, so a prefix on ES3Reflection.GetType(string) maps them:
    /// old assembly + old namespace prefix -> new namespace prefix inside this assembly. Results are cached.
    /// Each module keeps ONE namespace (plus documented sub-namespaces) so the mapping stays a pure prefix rename.
    /// </summary>
    public static class SaveTypes
    {
        struct Map { public string Asm, OldNs, NewNs; }
        static readonly List<Map> _maps = new List<Map>();
        static readonly Dictionary<string, Type> _cache = new Dictionary<string, Type>();
        static Assembly _self;
        public static int Resolved;

        /// <summary>Longer (more specific) prefixes must be registered first.</summary>
        public static void Alias(string oldAssembly, string oldNamespace, string newNamespace)
        {
            _maps.Add(new Map { Asm = oldAssembly, OldNs = oldNamespace, NewNs = newNamespace });
        }

        internal static void Install()
        {
            _self = typeof(SaveTypes).Assembly;
            Alias("WarRig", "WarRig", "WildWasteland.Modules.Patrols.WarRig");
            Alias("Apocapatrol", "Apocapatrol", "WildWasteland.Modules.Patrols");
            Alias("Apocaplayer", "Apocaplayer", "WildWasteland.Modules.PlayerBody");
            Alias("NPCAI", "NPCAI", "WildWasteland.Modules.NPC");
            Alias("Gunplay", "Gunplay", "WildWasteland.Modules.Guns");
            Alias("GunplayHUD", "GunplayHUD", "WildWasteland.Modules.HUD");
            var t = typeof(ES3Internal.ES3Reflection);
            Patches.Patch(null, t.GetMethod("GetType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null),
                prefix: typeof(SaveTypes).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic));
        }

        static bool Prefix(string typeString, ref Type __result)
        {
            if (typeString == null || typeString.IndexOf(',') < 0) return true;
            Type cached;
            if (_cache.TryGetValue(typeString, out cached)) { if (cached == null) return true; __result = cached; return false; }
            Type found = Lookup(typeString);
            _cache[typeString] = found;
            if (found == null) return true;
            Resolved++;
            if (Log.Verbose) Log.Info("SaveTypes: " + typeString + " -> " + found.FullName);
            __result = found;
            return false;
        }

        static Type Lookup(string typeString)
        {
            int comma = typeString.IndexOf(',');
            string name = typeString.Substring(0, comma).Trim();
            string asm = typeString.Substring(comma + 1).Trim();
            int c2 = asm.IndexOf(','); if (c2 >= 0) asm = asm.Substring(0, c2).Trim();
            foreach (var m in _maps)
            {
                if (!string.Equals(m.Asm, asm, StringComparison.Ordinal)) continue;
                if (!name.StartsWith(m.OldNs + ".", StringComparison.Ordinal)) continue;
                // the old assembly may still be loaded (standalone mod installed): then leave it to ES3
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) if (a.GetName().Name == asm) return null;
                var t = _self.GetType(m.NewNs + name.Substring(m.OldNs.Length), false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
