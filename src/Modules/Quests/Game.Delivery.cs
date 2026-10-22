using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    // One stack of a matching item found in the world or on the player.
    internal class ItemHit
    {
        public GameObject Go;
        public int Stack = 1;
        public FsmInt Qty;       // the item's own "Quantity" variable, if it is a stack
        public bool Held;        // in the hand / a quick slot (under the player camera)
        public bool Reserve;     // not an object: spare rounds in the player's ammo pouch (Qty = that ammo counter)
        public int PerBox = 1;   // rounds that make one "box" for a reserve hit
        public float Dist;
        public string Display = "";
    }

    // A point of interest (camp, cave, building, wreck) in the loaded part of the world.
    internal class Poi
    {
        public string Kind = "";      // "Camp", "Cave", "Building", "Wreck"
        public string Name = "";      // object name without (Clone), e.g. Camp_11
        public string Faction = "";   // from a child object named after a human faction (EnemySpawn_Coyotes_V1, CoyotesMerchantSpawn_V1, ...) ("" = none)
        public Transform Obj;         // the camp object itself
        public Vector3 Pos;           // world position
    }

    // A living enemy near somewhere.
    internal class EnemyInfo
    {
        public string Name = "";      // e.g. Grimhound
        public string Tag = "";       // Unity tag; the game uses it as the faction (Scrapyard, Coyotes)
        public Vector3 Pos;           // world position
        public GameObject Go;         // the object itself (so the quest giver can be left out of its own targets)
    }

    // Everything that touches the game's own objects lives here, so a game update only needs fixes in one file.
    /// <summary>
    /// Quests world helpers, Quests part (Wave A split of Quests/Game.cs): delivery goods (prefab lookup, spawning in front of the
    /// giver, watching them for a few seconds) and the per-world reset. The other parts: Core (Core/Game/_fromQuests/Game.Core.cs),
    /// NPCAI (Game.Npcs.cs), POI (Game.Pois.cs), Loot (Game.Items.cs, Game.Matches.cs).
    /// </summary>
    internal static partial class Game
    {
        // ---- spawning items (delivery goods) ----------------------------------------------
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        // An item prefab by name (e.g. dogfood_can): the loaded asset, not an object in the world.
        public static GameObject FindPrefab(string name)
        {
            string want = Util.Norm(name.TrimStart('='));
            GameObject g;
            if (prefabs.TryGetValue(want, out g) && g != null) return g;
            foreach (GameObject go in QuestWorld.Objects)
            {
                if (go == null || go.scene.IsValid() || go.transform.parent != null) continue;
                if (Util.Norm(go.name) != want) continue;
                bool item = false;
                foreach (PlayMakerFSM f in go.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "ItemName") { item = true; break; }
                if (!item) continue;
                prefabs[want] = go;
                return go;
            }
            return null;
        }

        // Finds a spot on the ground level with the giver's feet, in front of them. Looks only a little above the feet so a roof,
        // tank or truck body next to the giver is never picked, and tries other directions when the spot is blocked.
        static Vector3 GroundNear(Vector3 feet, Vector3 fwd, Vector3 side)
        {
            float[] turns = { 0f, 40f, -40f, 80f, -80f, 130f, -130f, 180f };
            for (int t = 0; t < turns.Length; t++)
            {
                Vector3 dir = Quaternion.Euler(0f, turns[t], 0f) * fwd;
                Vector3 p = feet + dir * 1.4f + side;
                RaycastHit[] hits = QuestWorld.RaycastAll(new Vector3(p.x, feet.y + 0.8f, p.z), Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, delegate (RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });
                for (int k = 0; k < hits.Length; k++)
                {
                    Collider c = hits[k].collider;
                    if (c == null || hits[k].distance <= 0.001f) continue;
                    if (c.GetComponentInParent<PlayMakerFSM>() != null && c.attachedRigidbody != null) continue;   // a person or an item, not ground
                    if (hits[k].point.y > feet.y + 0.5f) break;
                    Vector3 g = hits[k].point;
                    if (!Physics.CheckSphere(g + Vector3.up * 0.45f, 0.2f, ~0, QueryTriggerInteraction.Ignore)) return g;
                    break;
                }
            }
            return feet + fwd * 0.6f;
        }

        // Goods that were just handed out are watched for a few seconds: one that falls through the ground, is deleted by another
        // mod, or is moved far away without the player carrying it is put back at its spot.
        class Watched { public GameObject Go; public Vector3 Home; public GameObject Prefab; public string BaseName; public float Until; public Action<string> Log; public int Fixes; }
        static readonly List<Watched> watched = new List<Watched>();
        static float nextWatch;

        public static void WatchSpawns()
        {
            if (watched.Count == 0 || Time.time < nextWatch) return;
            nextWatch = Time.time + 0.4f;
            for (int i = watched.Count - 1; i >= 0; i--)
            {
                Watched w = watched[i];
                if (Time.time > w.Until || w.Fixes >= 3) { watched.RemoveAt(i); continue; }
                try
                {
                    bool lost = w.Go == null;
                    if (!lost && w.Go.transform.parent == null)
                    {
                        Vector3 pos = w.Go.transform.position;
                        if (pos.y < w.Home.y - 2.5f) lost = true;                                             // fell through the world
                        else if (new Vector2(pos.x - w.Home.x, pos.z - w.Home.z).magnitude > 40f) lost = true;   // thrown far away
                    }
                    if (!lost) continue;
                    if (w.Go != null) UnityEngine.Object.Destroy(w.Go);
                    w.Fixes++;
                    w.Go = UnityEngine.Object.Instantiate(w.Prefab, w.Home + Vector3.up * 0.2f, Quaternion.identity);
                    w.Go.SetActive(true);
                    w.Log("Delivery: " + w.BaseName + " was lost; put back (" + RegisterSpawned(w.Go, w.BaseName) + ").");
                }
                catch (Exception) { watched.RemoveAt(i); }
            }
        }

        // Puts `count` copies of an item on the ground in front of `who`, registered in the game's save like the
        // game's own spawns. Returns how many were made.
        public static int SpawnItemsInFront(string name, int count, Transform who, Action<string> log)
        {
            GameObject prefab = FindPrefab(name);
            if (prefab == null || who == null) { log("Delivery: no prefab named " + name); return 0; }
            Vector3 fwd = who.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            string baseName = BaseName(prefab.name);
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = GroundNear(who.position, fwd, right * ((i - (count - 1) * 0.5f) * 0.5f));
                GameObject go = UnityEngine.Object.Instantiate(prefab, p, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                go.SetActive(true);
                log("Delivery: spawned " + baseName + ", " + RegisterSpawned(go, baseName));
                watched.Add(new Watched { Go = go, Home = p, Prefab = prefab, BaseName = baseName, Until = Time.time + 12f, Log = log });
                made++;
            }
            return made;
        }

        internal static void ResetWorld()
        {
            Clock.Reset(); Money.Reset(); ammoFsm = null;
            liveNames.Clear(); liveNamesAt = -10; MatchesByToken.Clear(); _matches = 0; prefabs.Clear(); watched.Clear();
        }
    }
}
