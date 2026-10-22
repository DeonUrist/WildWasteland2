namespace WildWasteland.Modules.Guns
{
    /// <summary>HANDOVER to Gunplay (B09; REFACTOR-REQUEST B04-1). Saves part of the Guns entry (Wave A split, B04): ES3 patches that carry the per-gun magazine ledger (key Apocaunloader.Magazines).</summary>
    public sealed partial class GunsModule
    {
        /// <summary>Applies the SaveAll/LoadAll/StoreCachedFile patches of the magazine ledger.</summary>
        partial void InitSaves()
        {
            Core.Patches.ApplyClass(this, typeof(MagazineSavePatch));
            Core.Patches.ApplyClass(this, typeof(MagazineLoadPatch));
            Core.Patches.ApplyClass(this, typeof(MagazineStorePatch));
        }
    }
}
