using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// NpcModule part owned by Tools (Wave A split): the POI pathfinding setup mode, the POI navigation data view and its editor (jobs,
    /// hotkey, shutdown, reset, origin shift). Partial methods are called from the NPC entry where the moved lines were.
    /// </summary>
    public sealed partial class NpcModule
    {
        internal static BepInEx.Configuration.ConfigEntry<bool> HighlightUnbakedMapPois;
        /// <summary>Registers the developer setup / bake queue job (before PoiNav.Build, as before).</summary>
        partial void InitToolsPoiNavSetup()
        {
            Scheduler.EveryFrame(this, "PoiNav.Setup", PoiNavSetup.Tick);
        }

        /// <summary>Registers the data view, the editor (tick, camera, Gui) and the editor hotkey (after PoiNav.Traverse / PoiNav.Walkers).</summary>
        partial void InitToolsPoiNavEditor()
        {
            Scheduler.EveryFrame(this, "PoiNav.View", PoiNavView.Tick);
            Scheduler.EveryFrame(this, "PoiNav.Editor", PoiNavEditor.Tick);
            Scheduler.EveryFrame(this, "PoiNav.EditorCamera", PoiNavEditor.CameraTick, Phase.LateUpdateLast);
            Scheduler.Gui(this, "PoiNav.EditorGui", PoiNavEditor.Draw, PoiNavEditor.WantsGui);
            Hotkeys.Register(this, PoiNav.EditorKey, PoiNavEditor.Toggle, PoiNavEditor.CanToggle, true);
        }

        /// <summary>"POI navigation" shutdown step, Tools part: editor closed, view unhooked, setup copies removed.</summary>
        static partial void ShutdownTools()
        {
            PoiNavEditor.Close(true); PoiNavView.Unhook(); PoiNavSetup.Reset();
        }

        /// <summary>ResetAll, Tools part: editor closed, setup reset (before PoiNav.Reset, as before).</summary>
        static partial void ResetTools()
        {
            PoiNavEditor.Close(true); PoiNavSetup.Reset();
        }

        /// <summary>Floating origin: the setup copies and the editor's cached positions.</summary>
        static partial void ShiftTools(Vector3 delta)
        {
            PoiNavSetup.Shift(delta); PoiNavEditor.Shift(delta);
        }
    }
}
