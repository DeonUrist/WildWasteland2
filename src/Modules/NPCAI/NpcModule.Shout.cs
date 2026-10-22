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
            new PlayerShoutInput(this, ShoutKey, ShoutModifier, ShoutBlocksButtons, ShoutCooldown, PlayerShoutRange, () => Running);
            Core.ModEvents.Subscribe<Core.Events.PlayerShout>(this, Senses.OnPlayerShout);
        }

        /// <summary>GetButtonDown / GetButton / GetButtonUp prefixes: the shout modifier blocks the game's Kick / ShiftDown buttons.</summary>
        partial void InstallHooksCore()
        {
            // Input ownership and button guards are installed together by PlayerShoutInput.
        }
    }
}
