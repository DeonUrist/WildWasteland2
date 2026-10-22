using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// NpcModule part owned by Core (Hotkeys) (Wave A split): the player's shout key and the GetButton* prefixes that block the game's
    /// buttons while the shout modifier is held. Partial methods are called from the NPC entry where the moved lines were.
    /// </summary>
    public sealed partial class NpcModule
    {
        /// <summary>Registers [Keys] Shout (fires Senses.ShoutPressed when ShoutAllowed: module on and the modifier held).</summary>
        partial void InitCoreShout()
        {
            Hotkeys.Register(this, ShoutKey, Senses.ShoutPressed, Senses.ShoutAllowed);
        }

        /// <summary>GetButtonDown / GetButton / GetButtonUp prefixes: the shout modifier blocks the game's Kick / ShiftDown buttons.</summary>
        partial void InstallHooksCore()
        {
            Hooks.GetButtonDownUpdate.Prefix(this, Senses.BeforeGetButtonDown);
            Hooks.GetButtonDo.Prefix(this, Senses.BeforeGetButton);
            Hooks.GetButtonUpUpdate.Prefix(this, Senses.BeforeGetButtonUp);
        }
    }
}
