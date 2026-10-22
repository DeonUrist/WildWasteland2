using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Quests world helpers, NPCAI part (Wave A, from Quests/Game.cs; B07 turns these into population queries): living people/enemies/bodies near a point, the shared live-name index and the game's PlayerIsEnemy switch.</summary>
    internal static partial class Game
    {
        // Base names of the living people (Health FSM in state "health") that carry this faction tag.
        public static List<string> FindNpcNames(string faction)
        {
            var names = new List<string>();
            var all = QuestWorld.People;
            if (all == null) return names;
            string want = Util.Norm(faction);
            for (int i = 0; i < all.Count; i++)
            {
                PlayMakerFSM f = all[i];
                if (f == null || f.FsmName != "Health" || f.ActiveStateName != "health") continue;
                GameObject go = f.gameObject;
                if (go == null || !go.activeInHierarchy || Util.Norm(go.tag) != want) continue;
                names.Add(BaseName(go.name));
            }
            return names;
        }

        // Turns the game's own hostility switch on or off for one NPC. Returns true when it changed something.
        public static bool SetPlayerEnemy(GameObject go, bool enemy)
        {
            if (go == null) return false;
            foreach (PlayMakerFSM f in go.GetComponents<PlayMakerFSM>())
            {
                if (f == null || f.FsmName != "PlayerIsEnemy") continue;
                string want = enemy ? "PlayerEnemy" : "PlayerFriendly";
                if (f.ActiveStateName == want) return false;
                f.Fsm.SetState(want);
                return true;
            }
            return false;
        }

        // The game's own "is this one hostile to the player?" switch: FSM PlayerIsEnemy, state PlayerEnemy or PlayerFriendly.
        public static bool IsPlayerEnemy(GameObject go)
        {
            if (go == null) return false;
            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
                if (fsms[i] != null && fsms[i].FsmName == "PlayerIsEnemy") return fsms[i].ActiveStateName == "PlayerEnemy";
            return false;
        }

        // Living enemies (objects whose Health FSM is still in its "health" state) within radius of center.
        public static List<EnemyInfo> FindEnemies(Vector3 center, float radius)
        {
            var list = new List<EnemyInfo>();
            var all = QuestWorld.People;
            if (all == null) return list;
            for (int i = 0; i < all.Count; i++)
            {
                PlayMakerFSM f = all[i];
                if (f == null || f.FsmName != "Health") continue;
                GameObject go = f.gameObject;
                if (go == null || !go.activeInHierarchy) continue;
                string name = BaseName(go.name);
                if (name.StartsWith("Player", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_Dead", StringComparison.OrdinalIgnoreCase)) continue;
                if (f.ActiveStateName != "health") continue;
                if (HDist(go.transform.position, center) > radius) continue;
                var e = new EnemyInfo();
                e.Name = name;
                e.Tag = go.tag;
                e.Pos = go.transform.position;
                e.Go = go;
                list.Add(e);
            }
            return list;
        }

        // Loaded NPCs and enemies (anything with a Health FSM) by object name, rebuilt at most once a second and shared
        // by everything that asks "is this NPC here?", so the big PlayMaker FSM list is not walked again for every question.
        static readonly Dictionary<string, Transform> liveNames = new Dictionary<string, Transform>();

        static float liveNamesAt = -10f;

        public static Dictionary<string, Transform> LiveNames()
        {
            float now = Time.unscaledTime;
            if (now - liveNamesAt < 1f && now >= liveNamesAt) return liveNames;
            liveNamesAt = now;
            liveNames.Clear();
            var all = QuestWorld.People;
            if (all == null) return liveNames;
            for (int i = 0; i < all.Count; i++)
            {
                PlayMakerFSM f = all[i];
                if (f == null || f.FsmName != "Health") continue;
                GameObject go = f.gameObject;
                if (go == null) continue;
                string n = go.name;
                if (!liveNames.ContainsKey(n)) liveNames[n] = go.transform;
            }
            return liveNames;
        }

        // The Transform of a loaded NPC by object name, or null.
        public static Transform FindLive(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Transform t;
            return LiveNames().TryGetValue(name, out t) && t != null ? t : null;
        }

        // Forget the shared list (after something was spawned or renamed), so the next question sees the change.
        public static void InvalidateLiveNames() { liveNamesAt = -10f; }

        // Everything alive and named like one of the keywords (NPCs and people-type enemies), found live through the
        // PlayMaker FSM list so NPCs that spawn later are picked up the next time this is called.
        public static List<EnemyInfo> FindPeople(string[] keywords)
        {
            var list = new List<EnemyInfo>();
            var all = QuestWorld.People;
            if (all == null || keywords == null) return list;
            var seen = new HashSet<int>();
            for (int i = 0; i < all.Count; i++)
            {
                PlayMakerFSM f = all[i];
                if (f == null) continue;
                GameObject go = f.gameObject;
                if (go == null || !go.activeInHierarchy) continue;
                string name = BaseName(go.name);
                if (name.EndsWith("_Dead", StringComparison.OrdinalIgnoreCase)) continue;
                string n = Util.Norm(name);
                bool match = false;
                for (int k = 0; k < keywords.Length && !match; k++) if (keywords[k].Length > 0 && n.Contains(keywords[k])) match = true;
                if (!match || !seen.Add(go.GetInstanceID())) continue;
                var e = new EnemyInfo();
                e.Name = name;
                e.Tag = go.tag;
                e.Pos = go.transform.position;
                e.Go = go;
                list.Add(e);
            }
            return list;
        }

        // ---- bodies of killed enemies ---------------------------------------------------------------
        // A dead enemy leaves a "<Name>_Dead" object (an item with an ID FSM, saved with the game) that can be carried.
        // Bodies within radius of center, nearest first.
        public static List<GameObject> FindBodies(Vector3 center, float radius)
        {
            var list = new List<GameObject>();
            var all = QuestWorld.People;
            if (all == null) return list;
            var seen = new HashSet<int>();
            for (int i = 0; i < all.Count; i++)
            {
                PlayMakerFSM f = all[i];
                if (f == null || f.FsmName != "ID") continue;
                GameObject go = f.gameObject;
                if (go == null || !BaseName(go.name).EndsWith("_Dead", StringComparison.OrdinalIgnoreCase)) continue;
                if (HDist(go.transform.position, center) > radius) continue;
                if (seen.Add(go.GetInstanceID())) list.Add(go);
            }
            list.Sort(delegate (GameObject a, GameObject b) { return HDist(a.transform.position, center).CompareTo(HDist(b.transform.position, center)); });
            return list;
        }
    }
}
