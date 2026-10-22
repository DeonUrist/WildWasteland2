namespace WildWasteland.Core.UI
{
    /// <summary>Save library and Continue commands exposed to menu presentation independently of the save framework implementation.</summary>
    public interface ISaveMenu
    {
        /// <summary>Recovery error that blocks save/load admission, or null.</summary>
        string RecoveryError { get; }
        /// <summary>Display caption of the latest usable snapshot, or empty when none exists.</summary>
        string LatestCaption { get; }
        /// <summary>Whether a usable snapshot exists.</summary>
        bool HasLatest { get; }
        /// <summary>Opens the save or load library if framework admission permits it.</summary>
        bool TryOpen(bool saving);
        /// <summary>Loads the newest usable snapshot if framework admission permits it.</summary>
        bool TryContinue();
    }
}
