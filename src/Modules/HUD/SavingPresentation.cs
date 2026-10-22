using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.HUD
{
    /// <summary>Draws save screens and countdowns from the active Saves provider and typed presentation updates.</summary>
    internal static class SavingPresentation
    {
        static string _warning;
        /// <summary>Registers HUD-owned presentation subscription and predicate-gated drawing.</summary>
        internal static void Install(IModule owner)
        {
            ModEvents.Subscribe<Core.Events.SavePresentation>(owner, Presentation);
            Scheduler.Gui(owner, "Save presentation", Draw, Wants);
        }
        /// <summary>Forgets transient countdown text when the HUD stops.</summary>
        internal static void Clear() { _warning = null; }
        /// <summary>Idle presentation updates carry the autosave warning; serialization uses ISaves state.</summary>
        static void Presentation(ref Core.Events.SavePresentation update)
        { if (update.Operation == SavePhase.Idle) _warning = update.Phase == Core.Events.SavePresentationPhase.End ? null : update.Text; }
        /// <summary>Requests GUI only while a save is preparing/serializing or an autosave warning is visible.</summary>
        static bool Wants()
        {
            var saves = ModApi.Get<ISaves>();
            return saves != null && (saves.Phase == SavePhase.Saving && saves.Progress >= .1f || _warning != null);
        }
        /// <summary>Preserves the saving fallback and compass warning layout without reaching into the save engine.</summary>
        static void Draw()
        {
            var saves = ModApi.Get<ISaves>();
            if (saves == null || Event.current.type != EventType.Repaint) return;
            if (saves.ActiveFile == null) { Clear(); return; }
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            int depth = GUI.depth;
            if (saves.Phase == SavePhase.Saving && saves.Progress >= .1f && !LoadingProgress.ScreenVisible)
            {
                GUI.depth = -10000; var old = GUI.color;
                GUI.color = Color.black; GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = old;
                GlyphFont.DrawGame(new Rect(0, Screen.height * .42f, Screen.width, 90), saves.Kind == SaveKind.Auto || saves.Kind == SaveKind.Exit ? "AUTOSAVING" : "SAVING", 48 * scale, new Color(.886f, .749f, .424f), TextAnchor.MiddleCenter);
            }
            else if (_warning != null && !LoadingProgress.MenusBlocked && GameRefs.InGame && !EditorSession.Busy)
            {
                Vector2 anchor = CleanHUD.Hud.SaveWarningAnchor();
                var rect = new Rect(anchor.x - 410 * scale, anchor.y + 8 * scale, 820 * scale, 58 * scale);
                GUI.depth = -100;
                bool urgent = _warning == "The game will save now" || !saves.DeferredByCombat && saves.AutosaveDueIn >= 0 && saves.AutosaveDueIn <= 15;
                GlyphFont.DrawGame(rect, _warning, 24 * scale, urgent ? new Color(1f, .78f, .35f) : new Color(.96f, .91f, .79f), TextAnchor.MiddleCenter);
            }
            GUI.depth = depth;
        }
    }
}
