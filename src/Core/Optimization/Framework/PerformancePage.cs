using WildWasteland.Core.UI;

namespace WildWasteland.Core.Optimization
{
    /// <summary>
    /// The separate "Performance" tab of the settings window (between Modules and Debug; registered by Core's CorePages).
    /// The Core optimization master stays here. Debug hosts the Developer controls.
    /// </summary>
    public sealed class PerformancePage : ToolPage
    {
        static PerformancePage _instance;
        /// <summary>The single Performance tab (created on first use by CorePages.Register).</summary>
        public static PerformancePage Instance { get { return _instance ?? (_instance = new PerformancePage()); } }

        /// <summary>Tab in the General group, visible while the module is off (it holds the module switch).</summary>
        PerformancePage() { Group = ToolGroups.General; Title = "Performance"; ShowWhenModuleOff = true; }

        /// <summary>Draws the single Core optimization switch.</summary>
        public override void Draw(ToolLayout l)
        {
            if (OptimizationRuntime.EnabledEntry != null) l.Toggle(OptimizationRuntime.EnabledEntry, "Performance optimizations");
            l.Text("Limits incremental garbage collection time per frame. Generated mesh LODs work in both modes.");
        }
    }
}
