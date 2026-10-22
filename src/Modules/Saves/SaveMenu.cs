using WildWasteland.Core.UI;
namespace WildWasteland.Modules.Saves
{
    /// <summary>Menu adapter for the live save framework; keeps native slot presentation out of Saves.</summary>
    internal sealed class SaveMenu : ISaveMenu
    {
        string _latest, _caption;
        float _next;
        /// <summary>Recovery message from the active framework.</summary>
        public string RecoveryError { get { return SaveSystem.RecoveryError; } }
        /// <summary>Refreshes disk selection at most once per second while a menu queries it.</summary>
        void Refresh()
        {
            if (UnityEngine.Time.unscaledTime < _next) return;
            _next = UnityEngine.Time.unscaledTime + 1;
            _latest = SaveRuntime.Latest();
            _caption = _latest == null ? "" : SaveSystem.Info(_latest).Name;
        }
        /// <summary>Caption of the cached latest published snapshot.</summary>
        public string LatestCaption { get { Refresh(); return _caption ?? ""; } }
        /// <summary>True when the latest snapshot selection is nonempty.</summary>
        public bool HasLatest { get { Refresh(); return _latest != null; } }
        /// <summary>Opens the actual library only when save/load barriers allow menu work.</summary>
        public bool TryOpen(bool saving)
        {
            if (!SaveSystem.Installed || SaveSystem.Busy || RecoveryError != null) return false;
            SaveLibraryPage.Open(saving); return true;
        }
        /// <summary>Loads the actual selected snapshot through normal save admission.</summary>
        public bool TryContinue()
        {
            Refresh();
            if (!SaveSystem.Installed || !SaveSystem.Ready || SaveSystem.Busy || SaveSystem.Requested || SinglePlayerSaving.Serializing || SinglePlayerSaving.Loading || RecoveryError != null || _latest == null) return false;
            if (!System.IO.File.Exists(SaveFiles.WorldPath(_latest))) return false;
            if (SaveRuntime.InMainMenu) return SaveRuntime.Launch(_latest);
            if (!SaveSystem.LocalReady) return false;
            SinglePlayerSaving.Load(_latest); return true;
        }
    }
}
