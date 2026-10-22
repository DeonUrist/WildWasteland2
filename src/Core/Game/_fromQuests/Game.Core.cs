using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Quests world helpers, Core part (Wave A, from Quests/Game.cs; B01 merges into Origin/PlayerState/ItemSpawner/Clock/FsmIndex/Names): origin-relative positions, player, distances, save registration of spawned objects, money variable, game clock, FSM variable lookups, ground probe, object base names.</summary>
    internal static partial class Game
    {
        // ---- world / player ----------------------------------------------------------

        // The game shifts the whole world as you travel; the MapMagic root moves with it.
        public static Vector3 Origin() { return Core.Origin.Anchor; }

        public static Vector3 Rel(Vector3 world) { return world - Origin(); }

        public static Vector3 Abs(Vector3 rel) { return rel + Origin(); }

        public static Transform Player() { var p = GameRefs.Player; return p != null ? p.transform : null; }

        public static float HDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // Makes a spawned object part of the game's own save: gives it the next `<name>(Clone)<N>` name from the
        // itemNameID counter and adds it to ArrayList_Items on NewGO_ArrayList, the list the game saves with ES3.
        // Same thing the game's spawners (and Apocaspawner) do. Returns a short status text for the log.
        public static string RegisterSpawned(GameObject go, string baseName)
        {
            try
            {
                GameObject counter = GameRefs.ItemNameId;
                FsmInt n = null;
                if (counter != null)
                {
                    PlayMakerFSM[] fsms = counter.GetComponents<PlayMakerFSM>();
                    for (int i = 0; i < fsms.Length && n == null; i++)
                        if (fsms[i].FsmName == "itemNameID") n = fsms[i].FsmVariables.GetFsmInt("intName");
                }
                if (n == null) return "itemNameID counter not found";

                System.Collections.ArrayList list = SavedItemList();
                if (list == null) return "ArrayList_Items not found on NewGO_ArrayList";

                go.name = baseName + "(Clone)" + n.Value;
                n.Value = n.Value + 1;
                list.Add(go);
                InvalidateLiveNames();
                return "registered as " + go.name;
            }
            catch (Exception e) { return "registration failed: " + e.Message; }
        }

        // The game's list of things it saves (ArrayList_Items on NewGO_ArrayList). Looked up fresh each time (this is
        // rare), because the game may give the list a new instance when a save is loaded.
        static System.Collections.ArrayList SavedItemList()
        {
            GameObject reg = GameRefs.NewGOArrayList;
            if (reg == null) return null;
            foreach (Component c in reg.GetComponents<Component>())
            {
                if (c == null || c.GetType().Name != "PlayMakerArrayListProxy") continue;
                Type t = c.GetType();
                FieldInfo rn = t.GetField("referenceName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (rn == null || (rn.GetValue(c) as string) != "ArrayList_Items") continue;
                System.Collections.ArrayList list = null;
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    if (f.FieldType == typeof(System.Collections.ArrayList)) { list = f.GetValue(c) as System.Collections.ArrayList; if (list != null) break; }
                if (list == null)
                {
                    PropertyInfo p = t.GetProperty("arrayList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null) list = p.GetValue(c, null) as System.Collections.ArrayList;
                }
                return list;
            }
            return null;
        }

        // Ground height below a world x/z; false when no collider is loaded there.
        public static bool GroundAt(float x, float z, out Vector3 hit)
        {
            RaycastHit h;
            if (Physics.Raycast(new Vector3(x, 1500f, z), Vector3.down, out h, 4000f, ~0, QueryTriggerInteraction.Ignore))
            {
                hit = h.point;
                return true;
            }
            hit = Vector3.zero;
            return false;
        }

        // ---- money ("nuts") ---------------------------------------------------------

        static FsmInt money;

        static string moneyKey = "\u0001";

        static bool listedGlobals;

        // configured = exact global variable name, or "" to detect it.
        public static FsmInt MoneyVar(string configured)
        {
            if (configured == null) configured = "";
            if (money != null && moneyKey == configured) return money;

            money = null;
            moneyKey = configured;
            PlayMakerGlobals g = PlayMakerGlobals.Instance;
            if (g == null || g.Variables == null || g.Variables.IntVariables == null) return null;
            FsmInt[] ints = g.Variables.IntVariables;

            if (!listedGlobals)
            {
                listedGlobals = true;
                var names = new List<string>();
                for (int i = 0; i < ints.Length; i++) if (ints[i] != null) names.Add(ints[i].Name + "=" + ints[i].Value);
                Plugin.Info("PlayMaker global ints: " + string.Join(", ", names.ToArray()));
            }

            if (configured.Length > 0)
            {
                for (int i = 0; i < ints.Length; i++)
                    if (ints[i] != null && string.Equals(ints[i].Name, configured, StringComparison.OrdinalIgnoreCase)) { money = ints[i]; break; }
                if (money == null) Plugin.Warn("MoneyVariable '" + configured + "' is not a PlayMaker global int.");
                return money;
            }

            string[] exact = { "nuts", "money", "cash", "coins", "currency" };
            for (int e = 0; e < exact.Length && money == null; e++)
                for (int i = 0; i < ints.Length; i++)
                    if (ints[i] != null && string.Equals(ints[i].Name, exact[e], StringComparison.OrdinalIgnoreCase)) { money = ints[i]; break; }
            if (money == null)
            {
                string[] part = { "nut", "money", "cash", "coin" };
                for (int e = 0; e < part.Length && money == null; e++)
                    for (int i = 0; i < ints.Length; i++)
                        if (ints[i] != null && ints[i].Name != null && ints[i].Name.ToLowerInvariant().Contains(part[e])) { money = ints[i]; break; }
            }
            if (money != null) Plugin.Info("Using money variable: " + money.Name);
            else Plugin.Warn("Could not find the money variable among the PlayMaker globals (see the list above). Set MoneyVariable in the config.");
            return money;
        }

        // The nuts you hold; -1 when the money variable can't be found.
        public static int GetMoney(string configured)
        {
            FsmInt m = MoneyVar(configured);
            return m == null ? -1 : m.Value;
        }

        public static bool AddMoney(int amount, string configured)
        {
            FsmInt m = MoneyVar(configured);
            if (m == null) return false;
            m.Value = m.Value + amount;
            return true;
        }

        // ---- items ------------------------------------------------------------------

        static bool UnderPlayer(Transform t)
        {
            for (int i = 0; t != null && i < 16; i++, t = t.parent)
            {
                string n = t.name;
                if (n == "PlayerCamera" || n == "PlayerCameraHolder" || n == "Player") return true;
            }
            return false;
        }

        public static FsmInt FindInt(PlayMakerFSM f, string name)
        {
            if (f == null || f.FsmVariables == null) return null;
            FsmInt[] a = f.FsmVariables.IntVariables;
            if (a == null) return null;
            for (int i = 0; i < a.Length; i++) if (a[i] != null && a[i].Name == name) return a[i];
            return null;
        }

        public static FsmFloat FindFloat(PlayMakerFSM f, string name)
        {
            if (f == null || f.FsmVariables == null) return null;
            FsmFloat[] a = f.FsmVariables.FloatVariables;
            if (a == null) return null;
            for (int i = 0; i < a.Length; i++) if (a[i] != null && a[i].Name == name) return a[i];
            return null;
        }

        // In-game clock in hours since day 0 (Day * 24 + hour of day), from the game manager's DayTime FSM.
        // Returns -1 when the clock cannot be found. Sleeping and time skips count; pausing does not.
        static PlayMakerFSM dayFsm;

        public static double GameHours()
        {
            if (dayFsm == null) dayFsm = FsmUtil.Find(GameRefs.GameManager, "DayTime");
            if (dayFsm == null)
            {
                var all = QuestWorld.All;
                if (all == null) return -1.0;
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null && all[i].FsmName == "DayTime") { dayFsm = all[i]; break; }
                if (dayFsm == null) return -1.0;
            }
            FsmInt day = FindInt(dayFsm, "Day");
            FsmFloat time = FindFloat(dayFsm, "Time");
            if (day == null || time == null) return -1.0;
            return day.Value * 24.0 + time.Value;
        }

        public static string FindString(PlayMakerFSM f, string name)
        {
            if (f == null || f.FsmVariables == null) return "";
            FsmString[] a = f.FsmVariables.StringVariables;
            if (a == null) return "";
            for (int i = 0; i < a.Length; i++) if (a[i] != null && a[i].Name == name) return a[i].Value ?? "";
            return "";
        }

        public static string BaseName(string goName)
        {
            if (string.IsNullOrEmpty(goName)) return "";
            string s = goName;
            int i = s.IndexOf("(Clone)", StringComparison.Ordinal);
            if (i >= 0) s = s.Substring(0, i);
            return s.Trim();
        }
    }
}
