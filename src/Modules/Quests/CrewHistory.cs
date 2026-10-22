using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    internal static class CrewHistory
    {
        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal);
        private const string SaveKey = "WildWasteland.Quests.CrewHistory";
        private static bool _fresh, _readFailed;
        internal static void Clear() { Names.Clear(); _fresh = false; _readFailed = false; }
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
        /// <summary>Loads former crew identities from the selected world save only.</summary>
        internal static void Load(bool fresh)
        {
            fresh |= _fresh; Clear(); string slot = SaveBus.Slot; if (fresh || slot == null) return;
            try
            {
                string value = SaveBus.Key(SaveKey).Read(slot);
                if (value == null) return;
                if (!value.StartsWith("WW-CREW-1\n", StringComparison.Ordinal)) throw new InvalidOperationException("Invalid crew history");
                foreach (string encoded in value.Substring(10).Split('\n'))
                    if (encoded.Length > 0) Names.Add(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
            }
            catch (Exception e) { _readFailed = true; Log.Warn("[Quests] Could not load former crew; saving blocked: " + e.Message); }
        }
        /// <summary>Stages former crew identities in the world-save transaction.</summary>
        internal static void Save(IModule owner, string slot)
        {
            if (_readFailed) { SaveBus.ReportFailure(owner, "Former crew history is unreadable; preserving the previous save."); return; }
            try
            {
                var names = Snapshot();
                var text = new System.Text.StringBuilder("WW-CREW-1\n");
                Array.Sort(names, StringComparer.Ordinal);
                foreach (string name in names) text.Append(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(name))).Append('\n');
                SaveBus.Key(SaveKey).Write(owner, slot, text.ToString());
            }
            catch (Exception e) { SaveBus.ReportFailure(owner, "Former crew save failed: " + e.Message); Log.Warn("[Quests] Could not save former crew: " + e.Message); }
        }
    }
}
