using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Gunplay
{
    /// Preserve ammo before the vanilla per-frame clamp discards it after a capacity reduction.
    internal static class BackpackOverflow
    {
        // (WW) Per IntClamp action: is it one of the __GameManager__ Ammo FSM's ammo_X <= ammo_X_capacity clamps (decided once: the
        // variable names and the owner never change; GameObject.name and the "_capacity" concatenation allocate) + its transfer state.
        /// <summary>Per IntClamp action: whether it is an Ammo "ammo_X &lt;= ammo_X_capacity" clamp, and its transfer state.</summary>
        private sealed class Clamp { public bool Ours; public OverflowTransfer State; }
        private static readonly Dictionary<IntClamp, Clamp> _clamps = new Dictionary<IntClamp, Clamp>();
        private static Fsm _emptySlot;
        private static int _removalFrame;
        /// <summary>[Gunplay] BackpackOverflow.</summary>
        internal static bool Enabled { get { return AmmoSettings.DropBackpackOverflow.Value; } }
        internal static int Tracked { get { int n = 0; foreach (var c in _clamps.Values) if (c.Ours && c.State != null) n++; return n; } }

        /// <summary>Forgets the classified clamps and a pending slot removal.</summary>
        internal static void Reset() { _clamps.Clear(); _emptySlot = null; }

        /// <summary>True for __GameManager__'s Ammo FSM clamps of ammo_X to ammo_X_capacity (decided once per action).</summary>
        private static bool Classify(IntClamp action, Fsm fsm)
        {
            if (fsm.GameObject.name != "__GameManager__") return false;
            var count = action.intVariable;
            var max = action.maxValue;
            return count != null && max != null && count.Name.StartsWith("ammo_", StringComparison.Ordinal) && max.Name == count.Name + "_capacity";
        }

        /// <summary>Ammo clamp prefix body: on a capacity drop the excess rounds become an ammo box; false while a drop is pending (vanilla clamp suppressed).</summary>
        internal static bool BeforeClamp(IntClamp action)
        {
            var fsm = action.Fsm;
            if (fsm == null) return true;
            string fn = fsm.Name;                          // cheap first filter: IntClamp runs every frame in many FSMs
            if (fn == null || fn.Length != 4 || fn != "Ammo" || fsm.GameObject == null) return true;
            Clamp c;
            if (!_clamps.TryGetValue(action, out c)) { c = new Clamp { Ours = Classify(action, fsm) }; _clamps[action] = c; }
            if (!c.Ours) return true;
            var count = action.intVariable;
            var max = action.maxValue;
            if (!Enabled) { c.State = null; return true; }

            int capacity = Math.Max(0, max.Value);
            OverflowTransfer state = c.State;
            if (state == null)
            {
                // Establish a baseline without spilling ammo during scene initialization/save loading.
                c.State = new OverflowTransfer(capacity);
                return true;
            }
            if (state.Idle(capacity)) return true;
            int retained;
            bool allowClamp = Transfer(state, count.Value, capacity, count.Name, fsm.GameObject.transform, out retained);
            if (retained != count.Value)
            {
                int excess = count.Value - retained;
                count.Value = retained;
                if (Log.L2) GunplayLog.Deep("Backpack capacity reduced: dropped " + excess + " x " + count.Name + "; retained " + count.Value + "/" + capacity);
            }
            // Letting vanilla run here would delete the ammo we could not put into a world item.
            return allowClamp;
        }

        // (WW) the drop closure lives here, not in BeforeClamp: a lambda capturing BeforeClamp's locals made the compiler allocate
        // its closure on every IntClamp call game-wide, before the first filter
        /// <summary>Runs the transfer with a box-dropping callback (closure built only on this path).</summary>
        private static bool Transfer(OverflowTransfer state, int count, int capacity, string caliber, Transform at, out int retained)
        {
            return state.BeforeClamp(count, capacity, Time.unscaledTime, excess => AmmoBox.Drop(caliber, excess, at), out retained);
        }

        /// <summary>FsmState.OnEnter postfix body: notes the Backpack_Item slot going empty with an item (first filter: FSM name length 13).</summary>
        internal static void OnSlotEnter(FsmState state)
        {
            var fsm = state.Fsm;
            if (fsm == null) return;
            string fn = fsm.Name;                           // cheap first filter: every state entry of every FSM lands here
            if (fn == null || fn.Length != 13 || !Enabled || fn != "SlotEmptyFull" || fsm.GameObject == null || fsm.GameObject.name != "Backpack_Item") return;
            if (state.Name == "full") { _emptySlot = null; return; }
            var item = fsm.Variables.GetFsmGameObject("item");
            if (state.Name != "empty" || item == null || item.Value == null) return;
            _emptySlot = fsm;
            _removalFrame = Time.frameCount;
        }

        /// <summary>Per frame (Ammo job): once a removed backpack's slot stays empty, restores the base ammo capacities the Backpack FSM's "set" state defines.</summary>
        internal static void Tick()
        {
            // (old: Reset() every frame while off; BeforeClamp already drops each clamp's state while off)
            if (!Enabled) { _emptySlot = null; return; }
            if (_emptySlot == null || Time.frameCount <= _removalFrame) return;
            var slot = _emptySlot.GameObject;
            if (slot == null || _emptySlot.ActiveStateName != "empty" || slot.transform.childCount != 0) { _emptySlot = null; return; }
            var take = FsmUtil.Find(slot, "TakeItem");
            // Swapping packs temporarily empties the slot while dropCurrent waits one frame.
            if (take != null && (take.ActiveStateName == "dropCurrent" || take.ActiveStateName == "takeWeapon")) return;

            var gm = GameRefs.GameManager;
            var backpack = gm != null ? FsmUtil.Find(gm, "Backpack") : null;
            var ammo = gm != null ? FsmUtil.Find(gm, "Ammo") : null;
            var armor = gm != null ? FsmUtil.Find(gm, "ArmorBackpack") : null;
            if (backpack == null || ammo == null || armor == null) return;
            FsmState set = null;
            foreach (var st in backpack.FsmStates) if (st.Name == "set") { set = st; break; }
            if (set == null) return;
            // Read vanilla's base capacities instead of duplicating its caliber table.
            foreach (var a in set.Actions)
            {
                var action = a as SetIntValue;
                if (action == null) continue;
                if (!action.Enabled || action.intVariable == null || action.intValue == null) continue;
                string name = action.intVariable.Name;
                if (!name.StartsWith("ammo_", StringComparison.Ordinal) || !name.EndsWith("_capacity", StringComparison.Ordinal)) continue;
                var capacity = ammo.FsmVariables.GetFsmInt(name);
                if (capacity != null) capacity.Value = action.intValue.Value;
            }
            var packCapacity = armor.FsmVariables.GetFsmFloat("backpack_capacity");
            var multiply = backpack.FsmVariables.GetFsmFloat("multiply");
            if (packCapacity != null) packCapacity.Value = 0f;
            if (multiply != null) multiply.Value = 1f;
            _emptySlot = null;
            AmmoSettings.V("Backpack removed: restored base ammo capacities");
        }
    }
}
