using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    internal static class CrewHistory
    {
        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal);
        private static bool _fresh;
        internal static void Clear() { Names.Clear(); _fresh = false; }
        internal static void Restore(string[] names) { if (names != null) foreach (string name in names) Names.Add(name); }
        internal static void Begin(bool fresh) { Clear(); _fresh = fresh; }
        internal static void Observe(GameObject npc)
        {
            var vehicles = Services.FollowerVehicles;
            if (npc != null && npc.transform.parent == null && vehicles != null && vehicles.IsCrew(npc)) Remember(npc);
        }
        private static void Remember(GameObject npc)
        {
            string name = npc.name;
            int clone = name.LastIndexOf("(Clone)", StringComparison.Ordinal), id;
            if (clone >= 0 && int.TryParse(name.Substring(clone + 7), out id) && id >= 0) Names.Add(name);
        }
        internal static void Bailed(GameObject car, GameObject previous, GameObject npc) { if (npc != null) Remember(npc); }
        internal static bool Blocked(Transform npc)
        {
            if (npc == null) return false;
            var vehicles = Services.FollowerVehicles;
            if (vehicles != null && vehicles.IsCrew(npc.gameObject)) { Observe(npc.gameObject); return true; }
            return Names.Contains(npc.name);
        }
        internal static string[] Snapshot()
        {
            var fsms = PlayMakerFSM.FsmList; // Save-time capture also includes crew created since the last published slice.
            if (fsms != null) foreach (var f in fsms) if (f != null && f.FsmName == "Health") Observe(f.gameObject);
            var names = new string[Names.Count]; Names.CopyTo(names); return names;
        }
        private static string FileName(string slot)
        { return Path.Combine(Path.Combine(Assets.ModuleConfigDir("Quests"), "Saves"), slot + ".crew.txt"); }
        internal static void Load(bool fresh)
        {
            fresh |= _fresh; Clear(); string slot = SaveBus.Slot; if (fresh || slot == null) return;
            try
            {
                string path = FileName(slot);
                if (!File.Exists(path)) path = Path.Combine(Path.Combine(Assets.ModuleConfigDir("QuestsCompat"), "Saves"), slot + ".crew.txt");
                if (File.Exists(path)) foreach (string name in File.ReadAllLines(path)) if (!string.IsNullOrEmpty(name)) Names.Add(name);
            }
            catch (Exception e) { Log.Warn("[Quests] Could not load former crew: " + e.Message); }
        }
        internal static void Save(string slot, string[] names)
        {
            if (slot == null) return;
            try
            {
                string path = FileName(slot); Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path + ".tmp", names);
                if (File.Exists(path)) File.Delete(path);
                File.Move(path + ".tmp", path);
            }
            catch (Exception e) { Log.Warn("[Quests] Could not save former crew: " + e.Message); }
        }
    }
}
