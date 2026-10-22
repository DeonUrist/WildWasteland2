using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Loot
{
    /// <summary>
    /// Spawn-profile catalog of POI layout markers (Loot, README 6.9): named item/monster lists built from the native weighted
    /// selectors of the asset ItemSpawner templates (ItemSpawn_V1_All and the four wild/scrapyard/coyote EnemySpawn variants).
    /// Native weighted selectors remain the source of truth: profiles filter their existing indices, not item names supplied by
    /// a user. Built by a sliced POI job (<see cref="Step"/>, one asset FSM per step) over the shared Core prefab index
    /// (<see cref="Core.PrefabCatalog.AssetFsms"/>) instead of its own Resources.FindObjectsOfTypeAll scan. Monster profiles are
    /// consumed by the marker builder for NPC groups. Namespace kept for Wave C.
    /// </summary>
    internal sealed class PoiSpawnCatalog : INativeSpawnCatalog
    {
        /// <summary>Built profiles (sorted by name when ready).</summary>
        public List<NativeSpawnProfile> Profiles { get; } = new List<NativeSpawnProfile>();
        private PlayMakerFSM[] _scan;
        private int _next;
        private PlayMakerArrayListProxy _itemList;
        private bool _itemListSearched;
        private readonly HashSet<string> _names = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<GameObject,bool> _safe = new Dictionary<GameObject,bool>();
        /// <summary>True when every asset FSM has been visited.</summary>
        public bool Ready { get; private set; }

        /// <summary>Starts a scan (no-op while one runs or when ready): snapshot of the Core asset FSM list sorted by object name.</summary>
        public void Start()
        {
            if (_scan != null || Ready) return;
            _scan = Core.PrefabCatalog.AssetFsms.ToArray();
            Array.Sort(_scan, (a,b) => string.CompareOrdinal(a == null ? "" : a.name, b == null ? "" : b.name));
            SpawnerProgress.ScanProgress = 0f;
        }
        /// <summary>NativeSpawnProfile of a kind by exact name, or null.</summary>
        public NativeSpawnProfile Find(string kind, string name)
        { foreach (var p in Profiles) if (p.Kind == kind && p.Name == name) return p; return null; }
        /// <summary>Every profile of a kind (new list; editor use).</summary>
        public List<NativeSpawnProfile> For(string kind)
        { var result = new List<NativeSpawnProfile>(); foreach (var p in Profiles) if (p.Kind == kind) result.Add(p); return result; }
        /// <summary>Sliced step: visits one asset FSM; false when idle or finished (then sorts the profiles and sets <see cref="Ready"/>).</summary>
        public bool Step()
        {
            if (_scan == null) return false;
            if (_next >= _scan.Length) { _scan = null; Ready = true; SpawnerProgress.ScanProgress = 1f; Profiles.Sort((a,b) => string.CompareOrdinal(a.Name,b.Name)); return false; }
            var f = _scan[_next++];
            SpawnerProgress.ScanProgress = (float)_next / _scan.Length;
            if (f == null || f.gameObject.scene.IsValid() || f.FsmName != "ItemSpawner") return true;
            bool enemy = f.name == "EnemySpawn_Wild_V1" || f.name == "EnemySpawn_Wild_V2" || f.name == "EnemySpawn_Scrapyard_V1" || f.name == "EnemySpawn_Coyotes_V1";
            bool loot = f.name == "ItemSpawn_V1_All";
            if (!enemy && !loot) return true;
            SelectRandomInt selector = null; ArrayListGet getter = null;
            foreach (var state in f.FsmStates) foreach (var action in state.Actions)
            {
                var select = action as SelectRandomInt;
                if (select != null && select.storeInt != null && select.storeInt.Name == "index") selector = select;
                var get = action as ArrayListGet;
                if (get != null && get.result != null && get.result.variableName == "Item") getter = get;
            }
            if (selector == null || selector.ints == null || getter == null) return true;
            if (enemy)
            {
                foreach (var proxy in f.GetComponents<PlayMakerArrayListProxy>())
                {
                    var list = proxy.preFillGameObjectList; if (proxy.referenceName != "EnemySpawn" || list == null) continue;
                    Add(f, selector, "Monsters", f.name, i => SafeCreature(list[i]), list.Count);
                    Add(f, selector, "Monsters", "Animals", i => SafeCreature(list[i]) && (list[i].CompareTag("Carnivore") || list[i].CompareTag("Herbivore")), list.Count);
                    Add(f, selector, "Monsters", "Mutants", i => SafeCreature(list[i]) && list[i].CompareTag("Mutant"), list.Count);
                    Add(f, selector, "Monsters", "Scrapyard", i => SafeCreature(list[i]) && list[i].CompareTag("Scrapyard"), list.Count);
                    Add(f, selector, "Monsters", "Coyotes", i => SafeCreature(list[i]) && list[i].CompareTag("Coyotes"), list.Count);
                }
            }
            else
            {
                var items = ItemList();
                if (items == null || items.preFillGameObjectList == null) return true;
                var list = items.preFillGameObjectList;
                Add(f, selector, "Items", "All loot", i => list[i] != null, list.Count);
                Add(f, selector, "Items", "Supplies", i => ItemCategory(list[i]) == "Items & supplies", list.Count);
                Add(f, selector, "Items", "Weapons & ammo", i => ItemCategory(list[i]) == "Weapons & equipment", list.Count);
                Add(f, selector, "CarParts", "Car parts", i => ItemCategory(list[i]) == "Car parts", list.Count);
            }
            return true;
        }
        /// <summary>The native "ItemList" array proxy (game item list), looked up once per scan generation.</summary>
        private PlayMakerArrayListProxy ItemList()
        {
            if (_itemListSearched && _itemList != null) return _itemList;
            _itemListSearched = true; _itemList = null;
            foreach (var proxy in Resources.FindObjectsOfTypeAll<PlayMakerArrayListProxy>()) if (proxy.referenceName == "ItemList") { _itemList = proxy; break; }
            return _itemList;
        }
        /// <summary>Adds a profile once per kind:name from the selector rows <paramref name="accept"/> keeps (skipped when empty).</summary>
        private void Add(PlayMakerFSM f, SelectRandomInt select, string kind, string name, Func<int,bool> accept, int count)
        {
            if (_names.Contains(kind + ":" + name)) return;
            var indices = new List<int>(); var weights = new List<float>();
            for (int i = 0; i < select.ints.Length; i++)
            {
                int at = select.ints[i].Value;
                float weight = select.weights != null && i < select.weights.Length ? select.weights[i].Value : 1;
                if (at < 0 || at >= count || weight <= 0 || accept != null && !accept(at)) continue;
                indices.Add(at); weights.Add(weight);
            }
            if (indices.Count == 0) return;
            _names.Add(kind + ":" + name);
            Profiles.Add(new NativeSpawnProfile { Template = f, Kind = kind, Name = name, Indices = indices.ToArray(), Weights = weights.ToArray(), Count = indices.Count });
        }
        /// <summary>Coarse category of an item prefab (tags, layer and name words): Car parts, Weapons &amp; equipment, Items &amp; supplies.</summary>
        internal static string ItemCategory(GameObject root)
        {
            if (root == null) return "Other scenery";
            string name = root.name.ToLowerInvariant();
            if (root.CompareTag("vehPart") || root.CompareTag("vehPartRemoved") || root.CompareTag("Wheel") || root.CompareTag("Hinge")
                || Contains(name, "bumper", "radiator", "exhaust", "gearlever", "headlight", "hood_", "wheel_", "door_car", "door_truck", "junker_door")
                || name.Contains("hp") && name.Contains("nm")) return "Car parts";
            if (root.layer == 12 || Contains(name, "ammo_", "grenade", "rifle", "revolver", "shotgun", "crossbow", "m16", "akm", "rochester", "slamberg", "redmark", "blastlance")) return "Weapons & equipment";
            return "Items & supplies";
        }
        /// <summary>True when <paramref name="text"/> contains any of the words (case-insensitive).</summary>
        internal static bool Contains(string text, params string[] words)
        { foreach (string word in words) if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }
        /// <summary>Creature prefab allowed in marker groups: no boss/merchant/mechanic name and no BossUI FSM (cached per prefab).</summary>
        private bool SafeCreature(GameObject prefab)
        {
            if (prefab == null) return false; bool safe; if (_safe.TryGetValue(prefab, out safe)) return safe;
            safe = !Contains(prefab.name, "boss", "merchant", "mechanic");
            if (safe) foreach (var f in prefab.GetComponentsInChildren<PlayMakerFSM>(true)) if (f.FsmName == "BossUI") { safe = false; break; }
            _safe[prefab] = safe; return safe;
        }
        /// <summary>Forgets profiles and any running scan (new world / unload).</summary>
        public void Clear() { Profiles.Clear(); _names.Clear(); _safe.Clear(); _scan = null; _next = 0; Ready = false; _itemList = null; _itemListSearched = false; if (SpawnerProgress.ScanProgress < 1f) SpawnerProgress.ScanProgress = 1f; }
    }
}
