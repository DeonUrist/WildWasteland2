using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// HUD part of <see cref="SaveRuntime"/> (Wave A split): the full-screen "SAVING"/"AUTOSAVING" fallback overlay drawn while
    /// the offline save coroutine serializes. B10 takes this over (Saves presentation events).
    /// </summary>
    internal static partial class SaveRuntime
    {
        /// <summary>Registers the predicate-gated overlay GUI job (end of Install, as before).</summary>
        static partial void InstallHud() { Scheduler.Gui(null, "Native saving fallback", DrawSaving, WantsSaving); }

        /// <summary>GUI predicate: true only while SinglePlayerSaving serializes.</summary>
        static bool WantsSaving() { return SinglePlayerSaving.Serializing; }
        /// <summary>Black screen with the centred SAVING text (repaint only, hidden behind the loading screen).</summary>
        static void DrawSaving()
        {
            if (LoadingProgress.ScreenVisible || Event.current.type != EventType.Repaint) return;
            int depth = GUI.depth; GUI.depth = -10000; var color = GUI.color;
            GUI.color = Color.black; GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = color;
            Modules.CleanHUD.GlyphFont.DrawGame(new Rect(0, Screen.height * .42f, Screen.width, 90), SaveSystem.AutomaticSaving ? "AUTOSAVING" : "SAVING", 48 * Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), new Color(.886f, .749f, .424f), TextAnchor.MiddleCenter);
            GUI.depth = depth;
        }
    }
}
