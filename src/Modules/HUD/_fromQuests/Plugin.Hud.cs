using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.HUD
{
    /// <summary>Quests engine, HUD part (Wave A, from Quests/Plugin.cs; B10 IQuestPresentation fallback): the IMGUI tracker, quest log window (Talk window skin) and toasts drawn when CleanHUD does not style them, and the pause check they use.</summary>
    internal sealed partial class QuestFallbackView
    {
        private IQuestApi Api { get { return ModApi.Get<IQuestApi>(); } }
        private IQuestFallbackData Data { get { return ModApi.Get<IQuestFallbackData>(); } }
        private IQuestPresentation Styled { get { return ModApi.Get<IQuestPresentation>(); } }
        private bool TrackerStyled { get { return Styled != null && Styled.Tracker; } }
        private bool JournalStyled { get { return Styled != null && Styled.Journal; } }
        private bool DialogueStyled { get { return Styled != null && Styled.Conversation; } }
        internal bool Wanted() { var api = Api; var data = Data; return api != null && api.Ready && (api.JournalOpen || api.TrackerEnabled || api.Conversation.Open || !string.IsNullOrEmpty(api.Conversation.Prompt) || (data != null && data.NoticeCount > 0)); }
        internal void Render() { if (!Wanted()) return; Draw(); DrawConversation(); }
        static bool GamePaused() { return Time.timeScale < 0.01f || (Cursor.visible && Cursor.lockState != CursorLockMode.Locked); }

        GUIStyle trackStyle, toastStyle, panelStyle, titleStyle;

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------
        void EnsureStyles()
        {
            if (trackStyle != null) return;
            trackStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperRight, wordWrap = false, richText = false };
            toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = false };
            panelStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.UpperLeft, richText = false };
        }

        GUIStyle windowStyle;

        float nextSkinTry;

        // the quest log's background: the same image (and 9-slice edge) as Talk's dialogue window, or Talk's own
        // plain colour when it has no image
        void DrawWindow(Rect box)
        {
            Texture2D tex;
            RectOffset border;
            if (windowStyle == null && Time.unscaledTime >= nextSkinTry)
            {
                nextSkinTry = Time.unscaledTime + 2f;
                if (Data != null && Data.WindowSkin(out tex, out border))
                {
                    windowStyle = new GUIStyle();
                    windowStyle.normal.background = tex;
                    windowStyle.border = border;
                }
            }
            Color old = GUI.color;
            if (windowStyle != null)
            {
                GUI.color = Color.white;
                if (Event.current.type == EventType.Repaint) windowStyle.Draw(box, false, false, false, false);
            }
            else
            {
                GUI.color = new Color(.26f, .19f, .14f, .96f);
                GUI.DrawTexture(box, Texture2D.whiteTexture);
            }
            GUI.color = old;
        }

        static void Shadowed(Rect r, string text, GUIStyle st)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, st);
            GUI.color = old;
            GUI.Label(r, text, st);
        }

        internal void Draw()
        {
            var api = Api; if (api == null || !api.Ready) return;
            EnsureStyles();
            Matrix4x4 oldMatrix = GUI.matrix;
            try
            {
                float s = Screen.height / 1080f;
                GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
                float vw = Screen.width / s;

                bool paused = GamePaused();              // the pause menu (and the talk window) stop the clock
                if (!TrackerStyled && api.TrackerEnabled && api.Rows.Count > 0 && !api.JournalOpen && !paused)
                {
                    float y = 24f;
                    for (int i = 0; i < api.Rows.Count && i < 5; i++, y += 26f)
                        Shadowed(new Rect(vw - 760f, y, 740f, 26f), api.Rows[i].Text, trackStyle);
                }

                if (api.JournalOpen && !JournalStyled && !paused)
                {
                    var box = new Rect(vw / 2f - 400f, 150f, 800f, 620f);
                    DrawWindow(box);
                    panelStyle.normal.textColor = Color.white;
                    titleStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(box.x + 36f, box.y + 28f, 730f, 40f), "Quests   (" + api.Completed + " completed)", titleStyle);
                    GUI.Label(new Rect(box.x + 36f, box.y + 78f, 730f, 510f), api.JournalText, panelStyle);
                }

                float ty = 120f;
                var data = Data;
                if (data != null) for (int i = 0; i < data.NoticeCount; i++)
                {
                    if (Time.unscaledTime > data.NoticeUntil(i)) continue;
                    Shadowed(new Rect(vw / 2f - 500f, ty, 1000f, 34f), data.NoticeText(i), toastStyle); ty += 34f;
                }
            }
            finally { GUI.matrix = oldMatrix; }
        }
    }
}
