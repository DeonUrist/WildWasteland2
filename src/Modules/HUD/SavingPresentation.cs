using WildWasteland.Core;
using WildWasteland.Core.Events;
using WildWasteland.Core.UI;
using UnityEngine;
namespace WildWasteland.Modules.HUD
{
    /// <summary>HUD-owned saving overlay and autosave countdown driven by the save presentation protocol.</summary>
    internal static class SavingPresentation
    {
        static string _countdown;
        /// <summary>Subscribes under HUD ownership and registers predicate-gated drawing.</summary>
        internal static void Install(IModule owner)
        {
            _countdown = null;
            ModEvents.Subscribe<SavePresentation>(owner, Changed);
            Scheduler.Gui(owner, "Save presentation", Draw, Wanted);
        }
        /// <summary>Receives the countdown, including its explicit end event.</summary>
        static void Changed(ref SavePresentation update)
        { if (update.Operation == SavePhase.Idle) _countdown = update.Phase == SavePresentationPhase.End ? null : update.Text; }
        /// <summary>Clears a countdown that belongs to a previous world or disabled module.</summary>
        internal static void Clear() { _countdown = null; }
        /// <summary>Queries whether the current state has visible save presentation.</summary>
        static bool Wanted() { var saves = ModApi.Get<ISaves>(); return saves != null && (saves.Phase == SavePhase.Saving && saves.Progress >= .1f || !string.IsNullOrEmpty(_countdown)); }
        /// <summary>Draws serialization fallback or countdown using only public save state.</summary>
        static void Draw()
        {
            if (Event.current.type != EventType.Repaint || LoadingProgress.ScreenVisible) return;
            var saves = ModApi.Get<ISaves>(); if (saves == null) return;
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            int depth = GUI.depth; Color color = GUI.color;
            if (saves.Phase == SavePhase.Saving && saves.Progress >= .1f)
            {
                GUI.depth = -10000; GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = color;
                GlyphFont.DrawGame(new Rect(0, Screen.height * .42f, Screen.width, 90), saves.Kind == SaveKind.Auto || saves.Kind == SaveKind.Exit ? "AUTOSAVING" : "SAVING", 48 * scale, DialogueWidgets.Gold, TextAnchor.MiddleCenter);
            }
            else if (!string.IsNullOrEmpty(_countdown) && GameRefs.InGame && !LoadingProgress.MenusBlocked && !EditorSession.Busy)
            {
                Vector2 anchor = HudView.SaveWarningAnchor(); GUI.depth = -100;
                bool urgent = saves.DeferredByCombat || saves.AutosaveDueIn <= 15;
                GlyphFont.DrawGame(new Rect(anchor.x - 410 * scale, anchor.y + 8 * scale, 820 * scale, 58 * scale), _countdown, 24 * scale, urgent ? new Color(1f, .78f, .35f) : new Color(.96f, .91f, .79f), TextAnchor.MiddleCenter);
            }
            GUI.color = color; GUI.depth = depth;
        }
    }
}
