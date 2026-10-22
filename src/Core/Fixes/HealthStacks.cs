using System;
using BepInEx.Bootstrap;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Core
{
    // HealthStacks 1.0.0 (a326d8d): always-on quick-slot top-up, with no config or module switch.
    internal static class HealthStacks
    {
        internal const string GUID = "com.denis.apocalypter.healthstacks";
        internal const int MaxCharges = 3;

        internal static void Init()
        {
            BepInEx.PluginInfo info;
            if (Chainloader.PluginInfos.TryGetValue(GUID, out info) && !ReferenceEquals(info.Instance, null))
            {
                Log.Warn("[Core] HealthStacks: " + ReplacementGuard.Status(GUID) + ". Built-in fix waits until restart.");
                return;
            }
            Hooks.GetButtonDownUpdate.Prefix(null, TakeItemUsePatch.Prefix);
            Log.Always("[Core] HealthStacks ready (always on, max 3 charges)");
        }
    }

    internal static class TakeItemUsePatch
    {
        internal static bool Prefix(GetButtonDown __instance)
        {
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Name.Length != 8) return true;
            try
            {
                if (fsm.Name != "TakeItem" || __instance.buttonName == null || __instance.buttonName.Value != "Use") return true;
                if (fsm.ActiveStateName != "over") return true;
                var slot = fsm.GameObject;
                if (slot == null || (slot.name != "Bandage_Item" && slot.name != "FirstAid_Item")) return true;
                if (ApocasetterBridge.InputBlocked || Time.timeScale <= 0f || !Application.isFocused) return true;
                if (!Input.GetButtonDown("Use")) return true;
                if (!Merge(fsm, slot)) return true;
                if (__instance.storeResult != null) __instance.storeResult.Value = false;
                return false; // handled: the vanilla "FULL" branch never runs
            }
            catch (Exception e)
            {
                Log.Error("[Core] HealthStacks TakeItem patch", e);
                return true;
            }
        }

        static bool Merge(Fsm takeFsm, GameObject slot)
        {
            var itemVar = takeFsm.Variables.GetFsmGameObject("Item");
            var ground = itemVar != null ? itemVar.Value : null;
            if (ground == null) return false;

            GameObject held = null;
            for (int i = 0; i < slot.transform.childCount; i++)
            {
                var c = slot.transform.GetChild(i).gameObject;
                if (QuantityVar(c) != null) { held = c; break; }
            }
            if (held == null || held == ground) return false; // empty slot -> vanilla pickup

            string id = ItemId(ground);
            if (id == null || id != ItemId(held)) return false;

            var hq = QuantityVar(held);
            var gq = QuantityVar(ground);
            if (gq == null) return false;
            int need = HealthStacks.MaxCharges - hq.Value;
            if (need <= 0 || gq.Value <= 0) return false; // slot already full -> vanilla "FULL"

            int moved = Math.Min(need, gq.Value);
            hq.Value += moved;
            gq.Value -= moved;
            if (Log.Verbose) Log.Info(string.Format("[Core] HealthStacks {0}: took {1} charge(s), slot {2}/{3}, ground item {4}", slot.name, moved, hq.Value,
                HealthStacks.MaxCharges, gq.Value > 0 ? gq.Value + " left" : "used up"));

            if (gq.Value <= 0)
            {
                itemVar.Value = null;
                UnityEngine.Object.Destroy(ground); // its own Quantity FSM would do the same (Quantity < 1 -> DestroySelf)
            }
            PlayPickupSound(takeFsm, slot);
            RefreshSlotUi(slot, hq.Value);
            return true;
        }

        static FsmInt QuantityVar(GameObject go)
        {
            var f = FsmUtil.Find(go, "Quantity");
            return f != null ? f.FsmVariables.GetFsmInt("Quantity") : null;
        }

        static string ItemId(GameObject go)
        {
            var f = FsmUtil.Find(go, "ID");
            var s = f != null ? f.FsmVariables.GetFsmString("ID") : null;
            return s != null ? s.Value : null;
        }

        // SlotEmptyFull writes the counter only when it enters "full": write it directly and keep its cached var in sync.
        static void RefreshSlotUi(GameObject slot, int quantity)
        {
            var sef = FsmUtil.Find(slot, "SlotEmptyFull");
            if (sef == null) return;
            var q = sef.FsmVariables.GetFsmInt("Quantity");
            if (q != null) q.Value = quantity;
            var full = FindState(sef.Fsm, "full");
            if (full == null) return;
            foreach (var a in full.Actions)
            {
                var t = a as UiTextSetText;
                if (t == null) continue;
                var go = sef.Fsm.GetOwnerDefaultTarget(t.gameObject);
                var txt = go != null ? go.GetComponent<UnityEngine.UI.Text>() : null;
                if (txt != null) txt.text = quantity.ToString();
            }
        }

        static void PlayPickupSound(Fsm takeFsm, GameObject slot)
        {
            var st = FindState(takeFsm, "takeWeapon");
            if (st == null) return;
            foreach (var a in st.Actions)
            {
                var p = a as PlaySound;
                if (p == null || p.clip == null) continue;
                var clip = p.clip.Value as AudioClip;
                if (clip != null) AudioSource.PlayClipAtPoint(clip, slot.transform.position, p.volume != null ? p.volume.Value : 1f);
                return;
            }
        }

        static FsmState FindState(Fsm fsm, string name)
        {
            foreach (var s in fsm.States)
                if (s.Name == name) return s;
            return null;
        }
    }
}
