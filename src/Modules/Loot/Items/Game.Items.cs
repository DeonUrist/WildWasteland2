using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Quests world helpers, Loot part (Wave A, from Quests/Game.cs; B15 item query): finding, counting and taking items by token (ground, carried, reserve ammo), liquid containers and merchant stock exclusion.</summary>
    internal static partial class Game
    {
        // Containers (water cans, fuel cans) have a LiquidAmount FSM with Liquid and LiquidCapacity. An item that
        // holds a liquid only counts when it is at least `minFraction` full, so an empty can is not a water can.
        public static float MinLiquidFraction = 0.5f;

        static bool TooEmpty(GameObject go)
        {
            if (MinLiquidFraction <= 0f) return false;
            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
            {
                if (fsms[i] == null || fsms[i].FsmName != "LiquidAmount") continue;
                FsmFloat liq = FindFloat(fsms[i], "Liquid");
                FsmFloat cap = FindFloat(fsms[i], "LiquidCapacity");
                if (liq == null) return false;
                float full = cap != null && cap.Value > 0f ? cap.Value : 0f;
                if (full <= 0f) return liq.Value <= 0.01f;
                return liq.Value < full * MinLiquidFraction;
            }
            return false;
        }

        // Capacity (litres) of the first loaded container matching the item token, or 0 if it is not a container
        // or none is loaded. Also gives the fullest matching container's current amount.
        public static float ContainerCapacity(string token, out float bestHave)
        {
            bestHave = 0f;
            float cap = 0f;
            var all = QuestWorld.Items;
            if (all == null) return 0f;
            for (int i = 0; i < all.Count; i++)
            {
                PlayMakerFSM f = all[i];
                if (f == null || f.FsmName != "ItemName") continue;
                GameObject go = f.gameObject;
                if (go == null) continue;
                string display;
                if (!Matches(go, f, token, out display) || ForSale(go)) continue;
                PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
                for (int k = 0; k < fsms.Length; k++)
                {
                    if (fsms[k] == null || fsms[k].FsmName != "LiquidAmount") continue;
                    FsmFloat c = FindFloat(fsms[k], "LiquidCapacity");
                    FsmFloat l = FindFloat(fsms[k], "Liquid");
                    if (c != null && c.Value > cap) cap = c.Value;
                    if (l != null && UnderPlayer(go.transform) && l.Value > bestHave) bestHave = l.Value;
                }
            }
            return cap;
        }

        static bool ForSale(GameObject go)
        {
            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
                if (fsms[i] != null && fsms[i].FsmName == "Buyable" && fsms[i].enabled) return true;
            return false;
        }

        // token matches the prefab name, the item's display name or its ID category (case/space/underscore-insensitive).
        // A trailing * means "starts with". A leading = means the prefab name only (alcohol cans have the ID dogfood_can).
        static bool MatchUncached(GameObject go, PlayMakerFSM itemNameFsm, string token, out string display)
        {
            display = Game.FindString(itemNameFsm, "ItemName");
            string t = Util.Norm(token);
            bool exact = false, prefix = false;
            if (t.StartsWith("=")) { exact = true; t = t.Substring(1); }     // "=prefab": that prefab only, no ID category
            if (t.EndsWith("*")) { prefix = true; t = t.Substring(0, t.Length - 1); }
            if (t.Length == 0) return false;

            string b = Util.Norm(BaseName(go.name));
            if (Eq(b, t, prefix)) return true;
            if (exact) return false;
            string d = Util.Norm(display);
            if (Eq(d, t, prefix)) return true;

            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
            {
                if (fsms[i] == null || fsms[i].FsmName != "ID") continue;
                string id = Util.Norm(FindString(fsms[i], "ID"));
                if (Eq(id, t, prefix)) return true;
            }
            return false;
        }

        static bool Eq(string a, string t, bool prefix)
        {
            return prefix ? a.StartsWith(t, StringComparison.Ordinal) : a == t;
        }

        // The player's spare ammo lives as plain counters on the game manager's "Ammo" FSM (ammo_9mm, ammo_556 ...),
        // not as objects: picking a box up empties it into the counter. A quest that asks for ammo boxes can be paid
        // from that counter, one box = the usual box size.
        static readonly string[][] AmmoTypes = new string[][]
        {
            // prefab,           counter,        rounds per box
            new[] { "ammo_box_9mm",     "ammo_9mm",      "14" },
            new[] { "ammo_box_22",      "ammo_22caliber","13" },
            new[] { "ammo_box_12gauge", "ammo_12gauge",  "9"  },
            new[] { "ammo_box_20gauge", "ammo_20gauge",  "9"  },
            new[] { "ammo_box_556mm",   "ammo_556",      "14" },
            new[] { "ammo_box_762mm",   "ammo_762",      "9"  },
            new[] { "ammo_box_3006",    "ammo_3006",     "14" },
            new[] { "ammo_arrow",       "ammo_arrow",    "5"  },
        };

        static PlayMakerFSM ammoFsm;

        static PlayMakerFSM AmmoFsm()
        {
            if (ammoFsm != null) return ammoFsm;
            var all = QuestWorld.All;
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
            {
                PlayMakerFSM f = all[i];
                if (f == null || f.FsmName != "Ammo" || f.gameObject == null) continue;
                if (f.gameObject.name == "__GameManager__" && FindInt(f, "ammo_9mm") != null) { ammoFsm = f; break; }
            }
            return ammoFsm;
        }

        static void AddReserveAmmo(string token, List<ItemHit> hits)
        {
            string t = Util.Norm(token);
            if (t.StartsWith("=")) t = t.Substring(1);
            bool prefix = t.EndsWith("*");
            if (prefix) t = t.Substring(0, t.Length - 1);
            if (t.Length == 0) return;
            PlayMakerFSM fsm = null;
            for (int i = 0; i < AmmoTypes.Length; i++)
            {
                string prefab = Util.Norm(AmmoTypes[i][0]);
                bool hit = t == "ammo" || (prefix ? prefab.StartsWith(t, StringComparison.Ordinal) : prefab == t);
                if (!hit) continue;
                if (fsm == null) fsm = AmmoFsm();
                if (fsm == null) return;
                FsmInt counter = FindInt(fsm, AmmoTypes[i][1]);
                int per = int.Parse(AmmoTypes[i][2]);
                if (counter == null || counter.Value < per) continue;
                var h = new ItemHit();
                h.Reserve = true; h.Held = true; h.Dist = 0f;
                h.Qty = counter; h.PerBox = per; h.Stack = counter.Value / per;
                h.Display = "ammo";
                hits.Add(h);
            }
        }

        // Items that are on the ground within radius of the player, or carried by the player. Items that are
        // currently for sale at a merchant are ignored.
        public static List<ItemHit> FindItems(string token, Vector3 playerPos, float radius, bool countCarried)
        {
            var hits = new List<ItemHit>();
            var all = QuestWorld.Items;
            if (all == null) return hits;

            for (int i = 0; i < all.Count; i++)
            {
                PlayMakerFSM f = all[i];
                if (f == null || f.FsmName != "ItemName") continue;
                GameObject go = f.gameObject;
                if (go == null) continue;

                // cheap distance test first: carried items move with the player, so anything far away is neither carried
                // nor close enough, and the slower "is it under the player?" walk is only done for nearby items
                float dist = HDist(go.transform.position, playerPos);
                if (radius <= 100f && dist > radius && dist > 30f) continue;
                string display;
                if (!Matches(go, f, token, out display)) continue;      // with a big radius most items reach this point: match before the slower walk
                bool held = UnderPlayer(go.transform);
                if (held) { if (!countCarried) continue; }
                else if (dist > radius) continue;
                if (ForSale(go)) continue;
                if (TooEmpty(go)) continue;

                var h = new ItemHit();
                h.Go = go;
                h.Held = held;
                h.Dist = dist;
                h.Display = display;
                PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
                for (int k = 0; k < fsms.Length; k++)
                {
                    if (fsms[k] != null && fsms[k].FsmName == "Quantity")
                    {
                        FsmInt q = FindInt(fsms[k], "Quantity");
                        if (q != null) { h.Qty = q; h.Stack = Math.Max(0, q.Value); }
                        break;
                    }
                }
                if (h.Stack > 0) hits.Add(h);
            }
            if (countCarried) AddReserveAmmo(token, hits);
            return hits;
        }

        public static int CountStacks(List<ItemHit> hits)
        {
            int n = 0;
            for (int i = 0; i < hits.Count; i++) n += hits[i].Stack;
            return n;
        }

        // Removes `count` items: ground items first, then carried ones. Ground items are taken nearest first, measured from
        // `from` when given (the person you are handing them to: the thing you put down in front of them goes before
        // something that just happens to lie nearer to you), otherwise from where you stood when the items were counted.
        public static void TakeItems(List<ItemHit> hits, int count, Vector3? from = null)
        {
            if (from.HasValue)
                for (int i = 0; i < hits.Count; i++)
                    if (hits[i].Go != null && !hits[i].Held) hits[i].Dist = HDist(hits[i].Go.transform.position, from.Value);
            hits.Sort(delegate (ItemHit a, ItemHit b)
            {
                if (a.Held != b.Held) return a.Held ? 1 : -1;
                return a.Dist.CompareTo(b.Dist);
            });
            int remaining = count;
            for (int i = 0; i < hits.Count && remaining > 0; i++)
            {
                ItemHit h = hits[i];
                if (h.Reserve)
                {
                    int boxes = Math.Min(h.Stack, remaining);
                    if (h.Qty != null) h.Qty.Value = Math.Max(0, h.Qty.Value - boxes * h.PerBox);
                    remaining -= boxes;
                    continue;
                }
                if (h.Go == null) continue;
                int take = Math.Min(h.Stack, remaining);
                if (h.Qty != null && h.Stack > take)
                {
                    h.Qty.Value = h.Stack - take;       // partial stack: keep the rest
                }
                else
                {
                    if (h.Qty != null) h.Qty.Value = 0;
                    Plugin.Info("Handed in " + h.Go.name + (h.Held ? " (carried)" : " (" + h.Dist.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m away)"));
                    UnityEngine.Object.Destroy(h.Go);       // the game's DeleteDestroyedItems FSM drops it from the save list
                }
                remaining -= take;
            }
        }
    }
}
