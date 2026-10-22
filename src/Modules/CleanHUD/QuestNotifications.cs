using WildWasteland.Core;
namespace WildWasteland.Modules.CleanHUD
{
    internal sealed class QuestNotifications : IQuestPresentation
    {
        public bool Tracker { get { return QuestTracker.Owned; } }
        public bool Journal { get { return QuestJournal.Shown; } }
        public bool Conversation { get { return QuestDialogue.Shown; } }
        public bool Notify(string text)
        {
            if (!CleanHudModule.Running || !CleanHudModule.Enabled.Value || string.IsNullOrEmpty(text)) return false;
            MessageHook.Push(text); return true;
        }
        internal static void Install(CleanHudModule owner) { Services.QuestUi = new QuestNotifications(); }
        internal static void Clear() { if (Services.QuestUi is QuestNotifications) Services.QuestUi = null; }
    }
}
