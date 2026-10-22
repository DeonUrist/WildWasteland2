using WildWasteland.Core;
namespace WildWasteland.Modules.HUD
{
    internal sealed class QuestNotifications : IQuestPresentation
    {
        public bool Tracker { get { return QuestTracker.Owned; } }
        public bool Journal { get { return QuestJournal.Shown; } }
        public bool Conversation { get { return QuestDialogue.Shown; } }
        public bool Notify(string text)
        {
            if (!HudModule.Running || !HudModule.CleanHudEnabled.Value || string.IsNullOrEmpty(text)) return false;
            MessageHook.Push(text); return true;
        }
        internal static void Install(HudModule owner) { Services.QuestUi = new QuestNotifications(); }
        internal static void Clear() { if (Services.QuestUi is QuestNotifications) Services.QuestUi = null; }
    }
}
