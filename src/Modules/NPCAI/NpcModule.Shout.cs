namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// Former Core (Hotkeys) part of the old NPC entry: the shout input is created by NPCAIModule since B07 (PlayerShoutInput + the
    /// PlayerShout event); the partial methods stay empty until Wave C deletes the shell.
    /// </summary>
    public sealed partial class NpcModule
    {
        /// <summary>Empty (B07): NPCAIModule creates the shout input and subscribes Senses.OnPlayerShout.</summary>
        partial void InitCoreShout() { }

        /// <summary>Empty: PlayerShoutInput installs the button guards together with the input.</summary>
        partial void InstallHooksCore() { }
    }
}
