using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>F7 "Climbing" page of the Presentation World part: climb / ladder state and settings.</summary>
    internal static class ClimbingPage
    {
        /// <summary>Draws clip / climb / ladder state rows and the climbing settings.</summary>
        internal static void Draw(ToolLayout l)
        {
            var c = ClimbingController.Instance;
            l.Heading("State");
            l.Value("Climbing clips", ClimbingController.ClipsLoaded ? "loaded" : "MISSING (Models/apocaplayer_climbing.bundle) - climbing off");
            l.Value("Climbing now", YesNo(c != null && c.Climbing));
            l.Value("Can climb here", YesNo(ModAPI.PlayerCanTraverse));
            l.Value("Last climb", string.IsNullOrEmpty(ClimbingController.LastResult) ? "-" : ClimbingController.LastResult);
            l.Value("Last rejection", string.IsNullOrEmpty(ClimbingController.LastReject) ? "-" : ClimbingController.LastReject);
            l.Value("Last ladder", string.IsNullOrEmpty(LadderController.LastResult) ? "-" : LadderController.LastResult);
            l.Heading("Settings");
            l.Toggle(TraversalSettings.ClimbingOn);
            l.Toggle(TraversalSettings.JumpTriggersClimbing);
            l.Toggle(TraversalSettings.Ladders);
            l.Entry(TraversalSettings.LadderSpeed);
            l.Entry(TraversalSettings.LadderOffset);
            l.Entry(TraversalSettings.LadderEyeForward);
            l.Entry(TraversalSettings.LadderNeckBack);
            l.Entry(TraversalSettings.ClimbKey);
        }

        /// <summary>"yes" / "no".</summary>
        static string YesNo(bool b) { return b ? "yes" : "no"; }
    }
}
