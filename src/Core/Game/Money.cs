using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
namespace WildWasteland.Core
{
    /// <summary>Shared native currency lookup and changes; configuration supplies an optional exact variable name.</summary>
    public static class Money
    {
        /// <summary>Forgets the scene-specific native global handle after load.</summary>
        internal static void Reset() { money = null; moneyKey = "\u0001"; listedGlobals = false; }
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
                Log.Main(null, "PlayMaker global ints: " + string.Join(", ", names.ToArray()));
            }

            if (configured.Length > 0)
            {
                for (int i = 0; i < ints.Length; i++)
                    if (ints[i] != null && string.Equals(ints[i].Name, configured, StringComparison.OrdinalIgnoreCase)) { money = ints[i]; break; }
                if (money == null) Log.Warn(null, "MoneyVariable '" + configured + "' is not a PlayMaker global int.");
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
            if (money != null) Log.Main(null, "Using money variable: " + money.Name);
            else Log.Warn(null, "Could not find the money variable among the PlayMaker globals (see the list above). Set MoneyVariable in the config.");
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
            int before = m.Value;
            m.Value = before + amount;
            if (ModEvents.HasListeners<Events.MoneyChanged>())
            {
                var changed = new Events.MoneyChanged { Old = before, New = m.Value, Reason = "quest" };
                ModEvents.Publish(ref changed);
            }
            return true;
        }

    }
}
