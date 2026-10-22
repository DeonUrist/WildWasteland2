using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// Item list of the loot editor: every pickable item prefab (asset root with an "ID" FSM and a Rigidbody, no vehicles/trailers,
    /// no tool sets or explosion helpers) with its display name (Apocaspawner name, else the ItemName FSM, else the prefab name).
    /// Built on demand from the shared Core prefab index (<see cref="Core.PrefabCatalog.AssetFsms"/>) instead of its own
    /// Resources.FindObjectsOfTypeAll scan; invalidated by Patrols on scene changes. Loot (Wave B); the Apocaspawner name part
    /// lives in Compatibility (PickupCatalog.Apocaspawner.cs). Namespace kept for Wave C.
    /// </summary>
    internal static partial class PickupCatalog
    {
        private static List<PickupItem> _items;

        /// <summary>Drops the cached list (scene change); the next <see cref="All"/> rebuilds it.</summary>
        internal static void Invalidate() { _items = null; }

        /// <summary>Every pickable item sorted by display name then id (editor use only, built once per invalidation).</summary>
        internal static List<PickupItem> All()
        {
            if (_items != null) return _items;
            var names = ApocaspawnerNames();   // Compatibility part (PickupCatalog.Apocaspawner.cs)
            var items = new Dictionary<string, PickupItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var fsm in Core.PrefabCatalog.AssetFsms)
            {
                if (fsm == null || fsm.FsmName != "ID") continue;
                var go = fsm.gameObject;
                if (go.scene.IsValid() || go.transform.parent != null || go.GetComponent<Rigidbody>() == null) continue;
                var fsms = go.GetComponents<PlayMakerFSM>();
                if (fsms.Any(f => f.FsmName == "RpmGear" || f.FsmName == "CrashDamage" || f.FsmName == "TrailerAttached")) continue;
                string id = fsm.FsmVariables.GetFsmString("ID") != null ? fsm.FsmVariables.GetFsmString("ID").Value : "";
                if (id == "PartAdjusterTools" || id == "grenadeexplode" || id == "blastlanceexplode" || go.name.StartsWith("toolset_", StringComparison.OrdinalIgnoreCase)) continue;
                string name;
                if (!names.TryGetValue(go.name, out name) || string.IsNullOrEmpty(name))
                {
                    var label = fsms.FirstOrDefault(f => f.FsmName == "ItemName");
                    var value = label != null ? label.FsmVariables.GetFsmString("ItemName") : null;
                    name = value != null && !string.IsNullOrEmpty(value.Value) ? value.Value : go.name.Replace('_', ' ');
                }
                items[go.name] = new PickupItem { Id = go.name, Name = name };
            }
            _items = items.Values.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Id).ToList(); return _items;
        }

        /// <summary>Display name of an item id from the built list, else the id with underscores as spaces.</summary>
        internal static string Name(string id) { var item = _items != null ? _items.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) : null; return item != null ? item.Name : id.Replace('_', ' '); }
    }
}
