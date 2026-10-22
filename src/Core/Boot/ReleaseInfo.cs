using System;
using System.Collections.Generic;

namespace WildWasteland.Core
{
    /// <summary>Session release metadata shared with menu presentation; no module state is accessed.</summary>
    internal static class ReleaseInfo
    {
        private static bool _updateChecked;
        internal static string UpdateUrl { get; private set; }

        /// <summary>Checks published version tags once per session and records the newest release page.</summary>
        internal static System.Collections.IEnumerator CheckUpdates()
        {
            if (_updateChecked) yield break;
            using (var req = UnityEngine.Networking.UnityWebRequest.Get("https://api.github.com/repos/DeonUrist/WildWasteland/releases?per_page=100"))
            {
                req.timeout = 20;
                req.SetRequestHeader("User-Agent", "WildWasteland/" + Plugin.VERSION);
                req.SetRequestHeader("Cache-Control", "no-cache");
                yield return req.SendWebRequest();
                _updateChecked = true;
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) { Log.Warn(null, "WW update check: " + req.error); yield break; }
                try
                {
                    var releases = Json.Parse(req.downloadHandler.text) as List<object>;
                    if (releases == null) throw new FormatException("expected releases list");
                    var latest = new Version(Plugin.VERSION);
                    foreach (var item in releases)
                    {
                        var release = item as Dictionary<string, object>; Version version;
                        string tag = Json.Text(release, "tag_name", "");
                        if (Json.Bool(release, "draft", false) || !Version.TryParse(tag.TrimStart('v', 'V').Split('-')[0], out version) || version <= latest) continue;
                        latest = version; UpdateUrl = "https://github.com/DeonUrist/WildWasteland/releases/tag/" + Uri.EscapeDataString(tag);
                    }
                    if (Log.L1) Log.Main(null, "WW update check: installed " + Plugin.VERSION + (UpdateUrl == null ? "; no newer release" : "; update available " + latest));
                }
                catch (Exception e) { UpdateUrl = null; Log.Warn(null, "WW update check: " + e.Message); }
            }
        }
    }
}
