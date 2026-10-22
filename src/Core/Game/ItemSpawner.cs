using System;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// The game's own save registration for spawned objects (what its spawners, Apocaspawner and Apocatremors do): name the
    /// object "&lt;base&gt;(Clone)&lt;N&gt;" from the itemNameID counter (FSM "itemNameID", int "intName" on GameRefs.ItemNameId)
    /// and add it to an ArrayList proxy on NewGO_ArrayList ("ArrayList_Items" or "ArrayList_Cars"), which ES3 saves.
    /// Replaces 6 copies: Core InventorySupplies.Drop, Quests Game.RegisterSpawned (reflection), Patrols WarRigFeature,
    /// Patrols Patrol Counter/Registry/Name, NPC Gungirl.Register, Guns AmmoMechanics.Drop.
    /// Differences between copies (kept selectable): numbering - five copies increment the counter and then use it
    /// (<see cref="Numbering.IncrementFirst"/>, default), Quests used the current value and then incremented
    /// (<see cref="Numbering.UseThenIncrement"/>); list lookup - exact "ArrayList_Items"/"ArrayList_Cars" (InventorySupplies,
    /// Quests, WarRig) vs. first proxy whose name contains "item"/"car" (Patrols, Gungirl, Guns): here exact name first, then
    /// the contains rule, which picks the same proxy in the vanilla scene. Lookups are cached per NewGO_ArrayList / itemNameID
    /// instance (GameRefs clears those on scene load), so a registration does not allocate beyond the name string.
    /// </summary>
    public static partial class ItemSpawner
    {
        /// <summary>Which saved list.</summary>
        public enum SaveList { Items, Cars }
        /// <summary>How the itemNameID counter is consumed.</summary>
        public enum Numbering { IncrementFirst, UseThenIncrement }

        static GameObject _counterGo, _registry;
        static FsmInt _counter;
        static PlayMakerArrayListProxy _items, _cars;

        /// <summary>The itemNameID "intName" counter, or null while the game objects are not there.</summary>
        public static FsmInt Counter()
        {
            var go = GameRefs.ItemNameId;
            if (go == null) { _counterGo = null; _counter = null; return null; }
            if (ReferenceEquals(go, _counterGo) && _counter != null) return _counter;
            _counterGo = go; _counter = null;
            var f = FsmUtil.Find(go, "itemNameID");
            if (f != null && f.FsmVariables != null) _counter = f.FsmVariables.GetFsmInt("intName");
            return _counter;
        }

        /// <summary>The saved list proxy of the given kind on NewGO_ArrayList, or null.</summary>
        public static PlayMakerArrayListProxy List(SaveList which)
        {
            var reg = GameRefs.NewGOArrayList;
            if (reg == null) { _registry = null; _items = _cars = null; return null; }
            if (!ReferenceEquals(reg, _registry) || (which == SaveList.Items ? _items == null : _cars == null))
            {
                _registry = reg; _items = _cars = null;
                PlayMakerArrayListProxy looseItems = null, looseCars = null;
                foreach (var p in reg.GetComponents<PlayMakerArrayListProxy>())
                {
                    if (p == null) continue;
                    string n = p.referenceName ?? "";
                    if (_items == null && string.Equals(n, "ArrayList_Items", StringComparison.OrdinalIgnoreCase)) _items = p;
                    if (_cars == null && string.Equals(n, "ArrayList_Cars", StringComparison.OrdinalIgnoreCase)) _cars = p;
                    if (looseItems == null && n.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0) looseItems = p;
                    if (looseCars == null && n.IndexOf("car", StringComparison.OrdinalIgnoreCase) >= 0) looseCars = p;
                }
                if (_items == null) _items = looseItems;
                if (_cars == null) _cars = looseCars;
            }
            return which == SaveList.Items ? _items : _cars;
        }

        /// <summary>True when both the counter and the list exist (spawning would be saved).</summary>
        public static bool Available(SaveList which = SaveList.Items) { return Counter() != null && List(which) != null; }

        /// <summary>Consumes one id from the counter (see <see cref="Numbering"/>); -1 when the counter is missing.</summary>
        public static int NextId(Numbering numbering = Numbering.IncrementFirst)
        {
            var c = Counter();
            if (c == null) return -1;
            if (numbering == Numbering.IncrementFirst) { c.Value += 1; return c.Value; }
            int id = c.Value; c.Value = id + 1; return id;
        }

        /// <summary>
        /// Names go "&lt;baseName&gt;(Clone)&lt;id&gt;" with a fresh id (Patrols Prefabs.Name); without a counter the name is
        /// "&lt;baseName&gt;(Clone)" (Patrols/Gungirl behaviour). Returns the id or -1.
        /// </summary>
        public static int Name(GameObject go, string baseName, Numbering numbering = Numbering.IncrementFirst)
        {
            int id = NextId(numbering);
            go.name = baseName + "(Clone)" + (id >= 0 ? id.ToString() : "");
            return id;
        }

        /// <summary>
        /// Names go from the counter and adds it to the saved list. Returns false and changes nothing when the counter or the
        /// list is missing (InventorySupplies/Quests/Guns/WarRig behaviour; Gungirl used to rename even then).
        /// </summary>
        public static bool RegisterSaved(GameObject go, string baseName, SaveList which = SaveList.Items, Numbering numbering = Numbering.IncrementFirst)
        {
            if (go == null) return false;
            var list = List(which);
            if (list == null || list.arrayList == null || Counter() == null) return false;
            Name(go, baseName, numbering);
            list.arrayList.Add(go);
            return true;
        }

        /// <summary>Removes go from the saved list (undo of a failed spawn). True when it was listed.</summary>
        public static bool Unregister(GameObject go, SaveList which = SaveList.Items)
        {
            var list = List(which);
            if (go == null || list == null || list.arrayList == null) return false;
            int at = list.arrayList.IndexOf(go);
            if (at < 0) return false;
            list.arrayList.RemoveAt(at);
            return true;
        }
    }
}
