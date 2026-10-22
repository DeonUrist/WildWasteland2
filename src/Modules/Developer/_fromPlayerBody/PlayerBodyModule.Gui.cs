using WildWasteland.Core;
namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>Retains the existing pose editor hint registration until the B16 editor entry consolidation.</summary>
    public sealed partial class PlayerBodyModule
    {
        private void InitDeveloperHints() { Scheduler.Gui(this, "Pose editor hints", BodyRunner.Gui, BodyRunner.GuiWanted); }
    }
}
