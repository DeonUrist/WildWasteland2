using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// The Apocatalker bridge: the HUD-styled dialogue mirror (<see cref="TalkDialogue"/>, its patches and its per-frame job) under the
    /// Compatibility.Apocatalker scope. The Quests conversation adapter (TalkBridge partial: Talk Open/Close hooks, TalkApi link) stays
    /// installed by Quests, which owns the conversation session (REFACTOR-REQUEST B17-12).
    /// </summary>
    internal static class ApocatalkerBridge
    {
        private static ExternalQuestConversation _quests;
        /// <summary>Installs the mirror and registers job "Compatibility.Apocatalker.Dialogue" (LateUpdateLast, as the HUD job was).</summary>
        internal static void Start(IModule scope)
        {
            _quests=ExternalQuestConversation.Start(scope);
            TalkDialogue.Install(scope);
        }

        /// <summary>Scene change: hides the mirror (was the HUD's scene step).</summary>
        internal static void OnSceneLoaded() { TalkDialogue.Hide(); }

        /// <summary>Destroys the mirror window.</summary>
        internal static void Shutdown() { try{_quests?.Stop();}finally{_quests=null;TalkDialogue.Dispose();} }
    }
}
