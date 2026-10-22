using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    public sealed class QuestRow
    {
        public string Id, Text;
        public Color Color;
    }
    public sealed class QuestChoice
    {
        public string Key, Text;
    }
    public interface IQuestConversation
    {
        bool Open { get; }
        string Name { get; }
        string Prompt { get; }
        int Revision { get; }
        IList<string> Chat { get; }
        IList<QuestChoice> Choices { get; }
        void Choose(string key);
        void Close();
    }
    public interface IQuestApi
    {
        bool Ready { get; }
        bool TrackerEnabled { get; }
        bool JournalOpen { get; }
        string JournalText { get; }
        string JournalKey { get; }
        int Completed { get; }
        IList<QuestRow> Rows { get; }
        IQuestConversation Conversation { get; }
        void ExternalOpened(object host);
        void ExternalClosed();
    }
    public interface IQuestPresentation
    {
        bool Tracker { get; }
        bool Journal { get; }
        bool Conversation { get; }
        bool Notify(string text);
    }
}
