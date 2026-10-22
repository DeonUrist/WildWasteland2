using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Patches = WildWasteland.Core.Patches;

namespace WildWasteland.Modules.HUD
{
    /// <summary>Quests engine, HUD part (Wave A, from Quests/NativeConversation.cs; B10): the IMGUI fallback dialogue window and talk prompt, drawn only when CleanHUD does not style the conversation.</summary>
    internal sealed partial class QuestFallbackView
    {
        Vector2 _fallbackChat, _fallbackChoices;

        /// <summary>Draws fallback quest chat, choices and prompt when the clean dialogue widget is unavailable.</summary>
        internal void DrawConversation()
        {
            var api = Api; if (api == null || !api.Ready) return;
            var bridge = api.Conversation;
            if (!bridge.Open)
            {
                if (bridge.Prompt.Length > 0 && !DialogueStyled && GameRefs.InGame)
                    GUI.Label(new Rect(Screen.width * .5f - 240, Screen.height * .65f, 480, 40), bridge.Prompt);
                return;
            }
            if (DialogueStyled) return;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 900f); var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            try
            {
                var rect = new Rect(Screen.width / scale * .5f - 450, 45, 900, 800); GUI.Box(rect, bridge.Name);
                GUILayout.BeginArea(new Rect(rect.x + 25, rect.y + 45, 850, 710));
                _fallbackChat = GUILayout.BeginScrollView(_fallbackChat, GUILayout.Height(380));
                foreach (string line in bridge.Chat) GUILayout.Label(line); GUILayout.EndScrollView();
                _fallbackChoices = GUILayout.BeginScrollView(_fallbackChoices, GUILayout.Height(240));
                foreach (var choice in bridge.Choices) if (GUILayout.Button(choice.Text)) bridge.Choose(choice.Key);
                GUILayout.EndScrollView(); if (GUILayout.Button("Close")) bridge.Close(); GUILayout.EndArea();
            }
            finally { GUI.matrix = matrix; }
        }
    }
}
