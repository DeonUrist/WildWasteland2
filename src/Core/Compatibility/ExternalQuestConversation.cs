using System;
using System.Collections.Generic;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>One current quest choice with a callback owned by its quest session.</summary>
    public sealed class QuestDialogueButton { public string Key,Text; public Action Click; }
    /// <summary>Compatibility-owned external dialogue adapter; Quests owns all decisions and native conversation state.</summary>
    public interface IExternalQuestConversation
    {
        bool TalkingNow { get; }
        string NpcName { get; }
        Transform Npc { get; }
        void Bind(object host);
        void Say(string text);
        void You(string text);
        bool WindowSkin(out Texture2D texture,out RectOffset border);
        bool NeedsSync(List<QuestDialogueButton> choices);
        void SyncButtons(List<QuestDialogueButton> choices);
        void ClearButtons();
        void Subscribe(IModule owner,Action<object> opened,Action closed,Action<string,string> action);
        void Unsubscribe(IModule owner);
    }
}
