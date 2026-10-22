using WildWasteland.Core;
namespace WildWasteland.Modules.HUD
{
    internal sealed class QuestNotifications : IQuestPresentation
    {
        /// <summary>Returns tracker for the quest notification provider.</summary>
        public bool Tracker { get { return QuestTracker.Owned; } }
        /// <summary>Returns journal for the quest notification provider.</summary>
        public bool Journal { get { return QuestJournal.Shown; } }
        /// <summary>Returns conversation for the quest notification provider.</summary>
        public bool Conversation { get { return QuestDialogue.Shown; } }
        /// <summary>Resolves notify for the quest notification provider.</summary>
        public bool Notify(string text)
        {
            if (!HudModule.Running || !HudModule.CleanHudEnabled.Value || string.IsNullOrEmpty(text)) return false;
            MessageHook.Push(text); return true;
        }
        /// <summary>Installs the quest notification provider.</summary>
        internal static void Install(HudModule owner) { Core.ModApi.Provide<IQuestPresentation>(owner,new QuestNotifications()); }
        /// <summary>Clears transient quest notification provider state so the next scene or session starts cleanly.</summary>
        internal static void Clear() { if (Core.ModApi.Get<Core.IQuestPresentation>() is QuestNotifications) Core.ModApi.Withdraw<IQuestPresentation>(Core.ModApi.OwnerOf<IQuestPresentation>()); }
    }
}
