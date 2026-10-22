using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
{
    /// <summary>WW release check against GitHub (Wave A split; B01 -> a Core version/release info service). Started once per Init as a Core coroutine; the menu shows the update button from _updateUrl.</summary>
    internal sealed partial class Menu
    {
        private static bool _updateChecked;
        private static string _updateUrl;

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
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) { CleanHudModule.Warn("WW update check: " + req.error); yield break; }
                try
                {
                    var releases = Core.Json.Parse(req.downloadHandler.text) as List<object>;
                    if (releases == null) throw new FormatException("expected releases list");
                    var latest = new Version(Plugin.VERSION);
                    foreach (var item in releases)
                    {
                        var release = item as Dictionary<string, object>; Version version;
                        string tag = Core.Json.Text(release, "tag_name", "");
                        if (Core.Json.Bool(release, "draft", false) || !Version.TryParse(tag.TrimStart('v', 'V').Split('-')[0], out version) || version <= latest) continue;
                        latest = version; _updateUrl = "https://github.com/DeonUrist/WildWasteland/releases/tag/" + Uri.EscapeDataString(tag);
                    }
                    CleanHudModule.Log("WW update check: installed " + Plugin.VERSION + (_updateUrl == null ? "; no newer release" : "; update available " + latest));
                }
                catch (Exception e) { _updateUrl = null; CleanHudModule.Warn("WW update check: " + e.Message); }
            }
        }
    }
}
