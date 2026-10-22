using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Loot
{
    /// <summary>Loot-owned native item discovery and consumption. Quests supplies liquid policy through IQuestItems; Core supplies FSM, player and geometry indexes.</summary>
    internal sealed partial class QuestItems : IQuestItems
    {
        /// <summary>Containers (water cans, fuel cans) have a LiquidAmount FSM with Liquid and LiquidCapacity. An item that holds a
        /// liquid only counts when it is at least this full, so an empty can is not a water can ([Quests / Items] MinLiquidFraction).</summary>
        private float MinLiquidFraction = 0.5f;

        /// <summary>True when the item is a liquid container below <see cref="MinLiquidFraction"/> of its capacity (empty when capacity is unknown).</summary>
        bool TooEmpty(GameObject go)
        {
            if (MinLiquidFraction <= 0f) return false;
            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
            {
                if (fsms[i] == null || fsms[i].FsmName != "LiquidAmount") continue;
                FsmFloat liq = FsmIndex.Float(fsms[i], "Liquid");
                FsmFloat cap = FsmIndex.Float(fsms[i], "LiquidCapacity");
                if (liq == null) return false;
                float full = cap != null && cap.Value > 0f ? cap.Value : 0f;
                if (full <= 0f) return liq.Value <= 0.01f;
                return liq.Value < full * MinLiquidFraction;
            }
            return false;
        }

        /// <summary>Largest capacity (litres) of the loaded containers matching the item token (0 when it is not a container or none
        /// is loaded); <paramref name="bestHave"/> = the fullest matching container the player carries.</summary>
        public float ContainerCapacity(string token, out float bestHave)
        {
            bestHave = 0f;
            float cap = 0f;
            var all = FsmIndex.ByName("ItemName");
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
                    FsmFloat c = FsmIndex.Float(fsms[k], "LiquidCapacity");
                    FsmFloat l = FsmIndex.Float(fsms[k], "Liquid");
                    if (c != null && c.Value > cap) cap = c.Value;
                    if (l != null && PlayerState.UnderPlayer(go.transform) && l.Value > bestHave) bestHave = l.Value;
                }
            }
            return cap;
        }

        /// <summary>True for merchant stock (an enabled "Buyable" FSM).</summary>
        bool ForSale(GameObject go)
        {
            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
                if (fsms[i] != null && fsms[i].FsmName == "Buyable" && fsms[i].enabled) return true;
            return false;
        }

        /// <summary>Token match without the memo: the prefab name, the item's display name or its ID category
        /// (case/space/underscore/dash/dot-insensitive). A trailing * means "starts with"; a leading = means the prefab name only
        /// (alcohol cans have the ID dogfood_can).</summary>
        bool MatchUncached(GameObject go, PlayMakerFSM itemNameFsm, string token, out string display)
        {
            display = ItemText(itemNameFsm, "ItemName");
            string t = ItemNorm(token);
            bool exact = false, prefix = false;
            if (t.StartsWith("=")) { exact = true; t = t.Substring(1); }     // "=prefab": that prefab only, no ID category
            if (t.EndsWith("*")) { prefix = true; t = t.Substring(0, t.Length - 1); }
            if (t.Length == 0) return false;

            string b = ItemNorm(Names.Base(go.name));
            if (Eq(b, t, prefix)) return true;
            if (exact) return false;
            string d = ItemNorm(display);
            if (Eq(d, t, prefix)) return true;

            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
            {
                if (fsms[i] == null || fsms[i].FsmName != "ID") continue;
                string id = ItemNorm(ItemText(fsms[i], "ID"));
                if (Eq(id, t, prefix)) return true;
            }
            return false;
        }

        /// <summary>Equality, or prefix match when <paramref name="prefix"/>.</summary>
        bool Eq(string a, string t, bool prefix)
        {
            return prefix ? a.StartsWith(t, StringComparison.Ordinal) : a == t;
        }

        /// <summary>Lower-cased token without spaces, underscores, dashes and dots (same rule as Quests Util.Norm).</summary>
        string ItemNorm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ' || c == '_' || c == '-' || c == '.') continue;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>Named string variable of an FSM through the Core FSM index, "" when missing.</summary>
        string ItemText(PlayMakerFSM fsm, string name) { var value = FsmIndex.String(fsm, name); return value != null ? value.Value ?? "" : ""; }

        /// <summary>The player's spare ammo lives as plain counters on the game manager's "Ammo" FSM (ammo_9mm, ammo_556 ...), not
        /// as objects: picking a box up empties it into the counter. A quest that asks for ammo boxes can be paid from that counter,
        /// one box = the usual box size. Rows: prefab, counter, rounds per box.</summary>
        readonly string[][] AmmoTypes = new string[][]
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

        PlayMakerFSM ammoFsm;

        /// <summary>The game manager's "Ammo" FSM (cached once found).</summary>
        PlayMakerFSM AmmoFsm()
        {
            if (ammoFsm != null) return ammoFsm;
            var all = FsmIndex.All;
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
            {
                PlayMakerFSM f = all[i];
                if (f == null || f.FsmName != "Ammo" || f.gameObject == null) continue;
                if (f.gameObject.name == "__GameManager__" && FsmIndex.Int(f, "ammo_9mm") != null) { ammoFsm = f; break; }
            }
            return ammoFsm;
        }

        /// <summary>Adds one reserve hit per ammo counter matching the token ("ammo" = every type) that holds at least one box.</summary>
        void AddReserveAmmo(string token, List<QuestItemHit> hits)
        {
            string t = ItemNorm(token);
            if (t.StartsWith("=")) t = t.Substring(1);
            bool prefix = t.EndsWith("*");
            if (prefix) t = t.Substring(0, t.Length - 1);
            if (t.Length == 0) return;
            PlayMakerFSM fsm = null;
            for (int i = 0; i < AmmoTypes.Length; i++)
            {
                string prefab = ItemNorm(AmmoTypes[i][0]);
                bool hit = t == "ammo" || (prefix ? prefab.StartsWith(t, StringComparison.Ordinal) : prefab == t);
                if (!hit) continue;
                if (fsm == null) fsm = AmmoFsm();
                if (fsm == null) return;
                FsmInt counter = FsmIndex.Int(fsm, AmmoTypes[i][1]);
                int per = int.Parse(AmmoTypes[i][2]);
                if (counter == null || counter.Value < per) continue;
                var h = new QuestItemHit();
                h.Reserve = true; h.Held = true; h.Dist = 0f;
                h.Qty = counter; h.PerBox = per; h.Stack = counter.Value / per;
                h.Display = "ammo";
                hits.Add(h);
            }
        }

        /// <summary>Items matching the token on the ground within <paramref name="radius"/> of the player or carried by the player
        /// (plus reserve ammo when counting carried items); merchant stock and too-empty containers are ignored.</summary>
        public List<QuestItemHit> FindItems(string token, Vector3 playerPos, float radius, bool countCarried,float minimumLiquidFraction)
        {
            MinLiquidFraction=minimumLiquidFraction;
            var hits = new List<QuestItemHit>();
            var all = FsmIndex.ByName("ItemName");
            if (all == null) return hits;

            for (int i = 0; i < all.Count; i++)
            {
                PlayMakerFSM f = all[i];
                if (f == null || f.FsmName != "ItemName") continue;
                GameObject go = f.gameObject;
                if (go == null) continue;

                // cheap distance test first: carried items move with the player, so anything far away is neither carried
                // nor close enough, and the slower "is it under the player?" walk is only done for nearby items
                float dist = WorldGeometry.HorizontalDistance(go.transform.position, playerPos);
                if (radius <= 100f && dist > radius && dist > 30f) continue;
                string display;
                if (!Matches(go, f, token, out display)) continue;      // with a big radius most items reach this point: match before the slower walk
                bool held = PlayerState.UnderPlayer(go.transform);
                if (held) { if (!countCarried) continue; }
                else if (dist > radius) continue;
                if (ForSale(go)) continue;
                if (TooEmpty(go)) continue;

                var h = new QuestItemHit();
                h.Go = go;
                h.Held = held;
                h.Dist = dist;
                h.Display = display;
                PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
                for (int k = 0; k < fsms.Length; k++)
                {
                    if (fsms[k] != null && fsms[k].FsmName == "Quantity")
                    {
                        FsmInt q = FsmIndex.Int(fsms[k], "Quantity");
                        if (q != null) { h.Qty = q; h.Stack = Math.Max(0, q.Value); }
                        break;
                    }
                }
                if (h.Stack > 0) hits.Add(h);
            }
            if (countCarried) AddReserveAmmo(token, hits);
            return hits;
        }

        /// <summary>Removes <paramref name="count"/> items: ground items first, then carried ones, then reserve ammo boxes. Ground
        /// items are taken nearest first, measured from <paramref name="from"/> when given (the person you are handing them to),
        /// otherwise from where you stood when the items were counted. Partial stacks keep the rest; whole items are destroyed.</summary>
        public void TakeItems(List<QuestItemHit> hits, int count, Vector3? from = null)
        {
            if (from.HasValue)
                for (int i = 0; i < hits.Count; i++)
                    if (hits[i].Go != null && !hits[i].Held) hits[i].Dist = WorldGeometry.HorizontalDistance(hits[i].Go.transform.position, from.Value);
            hits.Sort(delegate (QuestItemHit a, QuestItemHit b)
            {
                if (a.Held != b.Held) return a.Held ? 1 : -1;
                return a.Dist.CompareTo(b.Dist);
            });
            int remaining = count;
            for (int i = 0; i < hits.Count && remaining > 0; i++)
            {
                QuestItemHit h = hits[i];
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
                    if (Log.L1) Log.Info("[Quests] Handed in " + h.Go.name + (h.Held ? " (carried)" : " (" + h.Dist.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m away)"));
                    UnityEngine.Object.Destroy(h.Go);       // the game's DeleteDestroyedItems FSM drops it from the save list
                }
                remaining -= take;
            }
        }
    }
}
