using System;
using HutongGames.PlayMaker;
namespace WildWasteland.Core
{
    /// <summary>Native world progression flags shared by encounter and quest consumers.</summary>
    public static class Progression
    {
        /// <summary>Counts completed native Boss_* global flags without changing their values.</summary>
        public static int BossKills()
        {
            int n = 0;
            try
            {
                foreach (var b in FsmVariables.GlobalVariables.BoolVariables)
                    if (b != null && b.Name != null && b.Name.StartsWith("Boss_") && b.Value) n++;
            }
            catch (Exception) { }
            return n;
        }
    }
}
