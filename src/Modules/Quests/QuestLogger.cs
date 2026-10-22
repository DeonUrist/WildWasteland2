namespace WildWasteland.Modules.Quests
{
    internal sealed class QuestLogger
    {
        internal void LogInfo(object message) { if (Core.Log.Verbose) Core.Log.Info("[Quests] " + message); }
        internal void LogWarning(object message) { Core.Log.Warn("[Quests] " + message); }
        internal void LogError(object message) { Core.Log.Error("[Quests] " + message); }
    }
}
