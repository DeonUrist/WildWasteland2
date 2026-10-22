using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Quests entry, HUD part (Wave A, from Quests/QuestsModule.cs; B10): the "Quests.Windows" Gui job drawing the IMGUI fallback UI.</summary>
    public sealed partial class QuestsModule
    {
        /// <summary>Registers the Gui job (same registration position as before: last job of Init).</summary>
        partial void InitHud() { Scheduler.Gui(this, "Quests.Windows", Draw, WantsGui); }
        /// <summary>Draws the tracker/journal/toasts and the fallback dialogue while this engine is the live one.</summary>
        void Draw() { if (Plugin.I == _engine) { _engine.Draw(); _engine.DrawConversation(); } }
        /// <summary>Gui predicate: true only while something of the quest UI must be drawn.</summary>
        bool WantsGui() { return Plugin.I == _engine && _engine.WantsGui; }
    }
}
