using System;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Old Patrols entry part of the War Rig page (Wave A split of PatrolsPages.cs). Wave B: the Vehicles module shows the page; Wave C deletes this file.</summary>
    public sealed partial class PatrolsModule
    {
        /// <summary>Placeholder kept while PatrolsModule still registers the page: points to the Vehicles module's War Rig page.</summary>
        [Obsolete("refactor: the War Rig page belongs to the Vehicles module (VehiclesPages.DrawWarRig)")]
        void WarRigPage(ToolLayout l)
        {
            l.Text("The War Rig tools moved to the Vehicles module: open its \"War Rig\" page (Patrols & vehicles group).");
        }
    }
}
