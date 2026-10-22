using System.Collections.Generic;
using UnityEngine;

// Quest contracts (core-modapi.md 8), moved unchanged from QuestServices.cs. Provider: Quests. Consumers: HUD, Compatibility.
// IQuestPresentation (HUD provides) lives in Hud.cs. FROZEN after W0.
namespace WildWasteland.Core
{
    /// <summary>One tracker row.</summary>
    public sealed class QuestRow
    {
        /// <summary>Quest id and row text.</summary>
        public string Id, Text;
        /// <summary>Row colour.</summary>
        public Color Color;
    }

    /// <summary>One conversation choice.</summary>
    public sealed class QuestChoice
    {
        /// <summary>Choice key and text.</summary>
        public string Key, Text;
    }

    /// <summary>The open quest conversation (Talk-style dialogue).</summary>
    public interface IQuestConversation
    {
        /// <summary>A conversation is open.</summary>
        bool Open { get; }
        /// <summary>Speaker name.</summary>
        string Name { get; }
        /// <summary>Current prompt.</summary>
        string Prompt { get; }
        /// <summary>Increments on every change.</summary>
        int Revision { get; }
        /// <summary>Chat lines so far.</summary>
        IList<string> Chat { get; }
        /// <summary>Current choices.</summary>
        IList<QuestChoice> Choices { get; }
        /// <summary>Selects a choice.</summary>
        void Choose(string key);
        /// <summary>Closes the conversation.</summary>
        void Close();
    }

    /// <summary>Quest state for HUD widgets and the Talk bridge.</summary>
    public interface IQuestApi
    {
        /// <summary>Quest content loaded.</summary>
        bool Ready { get; }
        /// <summary>Tracker enabled.</summary>
        bool TrackerEnabled { get; }
        /// <summary>Journal open.</summary>
        bool JournalOpen { get; }
        /// <summary>Journal text.</summary>
        string JournalText { get; }
        /// <summary>Journal key (changes with content).</summary>
        string JournalKey { get; }
        /// <summary>Completed quest count.</summary>
        int Completed { get; }
        /// <summary>Tracker rows.</summary>
        IList<QuestRow> Rows { get; }
        /// <summary>Open conversation (or a closed one).</summary>
        IQuestConversation Conversation { get; }
        /// <summary>An external dialogue host opened (Talk).</summary>
        void ExternalOpened(object host);
        /// <summary>The external dialogue host closed.</summary>
        void ExternalClosed();
    }
}
