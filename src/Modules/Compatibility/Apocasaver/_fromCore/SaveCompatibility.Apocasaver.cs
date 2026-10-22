namespace WildWasteland.Core
{
    /// <summary>
    /// Compatibility (Apocasaver) part of <see cref="SaveCompatibility"/> (Wave A split): the HandPose.Set/Get prefixes that
    /// ApocasaverExclusion installs when the standalone Apocasaver is loaded. Key format "apocasaver.pose.&lt;item&gt;" unchanged.
    /// </summary>
    internal static partial class SaveCompatibility
    {
        /// <summary>Prefix replacing Apocasaver HandPose.Set: stores the pose in the session table saved by SavePoses; skips the original.</summary>
        internal static bool SetPose(string itemName, string pose)
        { if (!string.IsNullOrEmpty(itemName)) Poses["apocasaver.pose." + itemName] = pose ?? ""; return false; }
        /// <summary>Prefix replacing Apocasaver HandPose.Get: session table first, then the slot's ES3 cache/file; skips the original.</summary>
        internal static bool GetPose(string itemName, ref string __result)
        {
            __result = null; if (string.IsNullOrEmpty(itemName)) return false;
            string key = "apocasaver.pose." + itemName;
            if (Poses.TryGetValue(key, out __result)) return false;
            string file = SaveBus.Slot; if (file == null) return false;
            foreach (var location in new[] { ES3.Location.Cache, ES3.Location.File })
            {
                var settings = new ES3Settings(file) { location = location };
                if (ES3.KeyExists(key, settings)) { __result = ES3.Load<string>(key, settings); break; }
            }
            return false;
        }
    }
}
