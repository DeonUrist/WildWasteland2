using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>Armor overflow: a smaller body armor no longer deletes armor; the excess drops as plates (one per 50 points).</summary>
    internal static class ArmorOverflow
    {
        // (WW) per FloatClamp action: is it __GameManager__'s ArmorBackpack armor <= armor_capacity clamp (decided once; GameObject.name
        // allocates) + its transfer state
        /// <summary>Per FloatClamp action: whether it is the ArmorBackpack armor clamp, and its transfer state.</summary>
        private sealed class Clamp { public bool Ours; public OverflowTransfer State; }
        private static readonly Dictionary<FloatClamp, Clamp> _clamps = new Dictionary<FloatClamp, Clamp>();
        /// <summary>Forgets every classified clamp (scene change, load, shutdown).</summary>
        internal static void Reset() { _clamps.Clear(); }
        internal static int Tracked { get { int n = 0; foreach (var c in _clamps.Values) if (c.Ours && c.State != null) n++; return n; } }

        /// <summary>True for __GameManager__'s ArmorBackpack "armor &lt;= armor_capacity" clamp (decided once per action).</summary>
        private static bool Classify(FloatClamp action, Fsm fsm)
        {
            if (fsm.GameObject.name != "__GameManager__") return false;
            var armor = action.floatVariable;
            var max = action.maxValue;
            return armor != null && max != null && armor.Name == "armor" && max.Name == "armor_capacity";
        }

        /// <summary>Armor clamp prefix body: on a capacity drop the excess armor becomes plates on the ground; false while a drop is pending (vanilla clamp suppressed).</summary>
        internal static bool BeforeClamp(FloatClamp action)
        {
            var fsm = action.Fsm;
            if (fsm == null) return true;
            string fn = fsm.Name;                              // cheap first filter: FloatClamp runs every frame in many FSMs
            if (fn == null || fn.Length != 13 || fn != "ArmorBackpack" || fsm.GameObject == null) return true;
            Clamp c;
            if (!_clamps.TryGetValue(action, out c)) { c = new Clamp { Ours = Classify(action, fsm) }; _clamps[action] = c; }
            if (!c.Ours) return true;
            var armor = action.floatVariable;
            var max = action.maxValue;
            if (!AmmoSettings.DropArmorOverflow.Value) { c.State = null; return true; }
            if (float.IsNaN(armor.Value) || float.IsInfinity(armor.Value) || float.IsNaN(max.Value) || float.IsInfinity(max.Value)) return true;
            float capacity = Math.Max(0f, max.Value);
            OverflowTransfer transfer = c.State;
            if (transfer == null)
            {
                c.State = new OverflowTransfer(capacity);
                return true; // Initial observation/load establishes the native baseline.
            }
            if (transfer.Idle(capacity)) return true;
            float retained;
            bool allowClamp = Transfer(transfer, armor.Value, capacity, fsm.GameObject.transform, out retained);
            if (retained != armor.Value)
            {
                float returned = armor.Value - retained;
                armor.Value = retained;
                if (Log.L2) GunplayLog.Deep("Armor capacity reduced: returned " + returned + " armor as " + (returned / OverflowTransfer.ArmorPerPlate) + " plate(s)");
            }
            return allowClamp;
        }

        // (WW) closure outside BeforeClamp (see BackpackOverflow.Transfer)
        /// <summary>Runs the transfer with a plate-dropping callback (kept out of BeforeClamp so its closure is only built on this path).</summary>
        private static bool Transfer(OverflowTransfer t, float armor, float capacity, UnityEngine.Transform at, out float retained)
        {
            return t.BeforeClamp(armor, capacity, UnityEngine.Time.unscaledTime, plates => AmmoBox.Drop("armorplate", plates, at), out retained);
        }
    }
}
