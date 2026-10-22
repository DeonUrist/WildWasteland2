using HutongGames.PlayMaker;

namespace WildWasteland.Core
{
    /// <summary>
    /// Mouse dead for ~2 s in TAB / F3 after closing the ESC menu (vanilla timing, not the menu skin).
    /// __GameManager__ "Menu" FSM (Sources fsm/scene1_game/__GameManager__.md): "pause" runs EnableGUI true and SetMouseCursor
    /// unlocked; "play" (ESC closed) runs SetMouseCursor locked+hidden and only turns PlayMakerGUI off again 2 s later
    /// (SendEvent "Deactivate" to the "GUI" FSM, delay=2 -> EnableGUI false). For those 2 s PlayMakerGUI.OnGUI re-applies
    /// Cursor.lockState = Locked / visible = false on every Repaint (controlMouseCursor), so any window that unlocks the
    /// cursor itself (WW F3 EditorSession, inventory) is re-locked every frame until the delayed event lands.
    /// Fix: on entering "play" PlayMakerGUI is switched off at once - the state it reaches 2 s later anyway (the cursor
    /// stays locked: SetMouseCursor already set Cursor.lockState directly in its OnEnter). The late "Deactivate" stays harmless.
    /// </summary>
    internal static class PauseCursorFix
    {
        static readonly FixOwner Owner = new FixOwner("PauseCursorFix");
        static bool _installed;
        /// <summary>Installs the native pause-menu cursor fix independently of HUD availability.</summary>
        internal static void Install()
        { if (_installed) return; _installed = true; Hooks.EnterState.Postfix(Owner, AfterEnterState); }
        /// <summary>Removes the native state observer.</summary>
        internal static void Remove() { Owner.Remove(); _installed = false; }
        /// <summary>Stops the delayed native GUI cursor override immediately when the pause menu returns to play.</summary>
        internal static void AfterEnterState(Fsm fsm, FsmState state)
        {
            if (fsm == null || state == null) return;
            var n = fsm.Name;
            if (n == null || n.Length != 4 || n != "Menu" || state.Name != "play") return;
            var go = fsm.GameObject;
            if (go == null || go.name != "__GameManager__") return;
            var gui = PlayMakerGUI.Instance;
            if (gui != null && gui.enabled) gui.enabled = false;
        }
    }
}
