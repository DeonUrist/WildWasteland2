using WildWasteland.Core;

namespace WildWasteland.Modules.Saves
{
    public sealed partial class SavesModule : ISaves
    {
        /// <summary>Includes admission, serialization, publication, recovery and native load work.</summary>
        public bool Busy { get { return _recovering || SavesSystem.Busy || SavesSystem.Requested || SavesSystem.Exiting || SinglePlayerSaving.Serializing || SaveBus.Saving || SaveBus.Loading || SaveRoutines.Active; } }
        /// <summary>The framework phase; loading stays distinct so Core world preparation may continue.</summary>
        public SavePhase Phase
        {
            get
            {
                if (_recovering) return SavePhase.Recovering;
                if (SinglePlayerSaving.Loading || SaveBus.Loading) return SavePhase.Loading;
                return Busy ? SavePhase.Saving : SavePhase.Idle;
            }
        }
        /// <summary>Distinguishes exit autosaves from timed autosaves and manual saves.</summary>
        public SaveKind Kind { get { return SavesSystem.Exiting ? SaveKind.Exit : SavesSystem.AutomaticSaving ? SaveKind.Auto : SaveKind.Manual; } }
        /// <summary>Completed milestones of serialization; native loading has no fractional progress here.</summary>
        public float Progress { get { return Phase == SavePhase.Saving ? SinglePlayerSaving.Progress : 0; } }
        /// <summary>Current milestone or failure status without inventing native serializer completion estimates.</summary>
        public string ProgressText { get { return _recovering ? "Recovering interrupted saves" : Phase == SavePhase.Loading ? "Loading world" : SinglePlayerSaving.ProgressText ?? SavesSystem.Status; } }
        /// <summary>Actual countdown deadline, or a negative value when autosave is unavailable.</summary>
        public float AutosaveDueIn { get { return SavesSystem.AutosaveDueIn; } }
        /// <summary>Uses the last combat check; querying UI state never rescans actors.</summary>
        public bool DeferredByCombat { get { return SavesSystem.Autosave.Value && SavesSystem.LocalReady && SaveCombat.Unsafe; } }
        /// <summary>Current save identity, unavailable in menus.</summary>
        public string ActiveFile { get { return SavesSystem.LocalWorld ? SavesSystem.ActiveFile : null; } }
        string _nameFile, _activeName;
        int _nameVersion = -1, _nameGeneration = -1;
        /// <summary>Published save display name, or the new world's name; cached until identity/catalogue changes.</summary>
        public string ActiveName
        {
            get
            {
                string file = ActiveFile; if (file == null) return null;
                if (_nameFile != file || _nameVersion != SavesSystem.CatalogueVersion || _nameGeneration != SaveBus.Generation)
                {
                    _nameFile = file; _nameVersion = SavesSystem.CatalogueVersion; _nameGeneration = SaveBus.Generation;
                    _activeName = SavesSystem.Info(file, SavesSystem.CurrentWorldName).Name;
                }
                return _activeName;
            }
        }
        /// <summary>Manual admission uses existing health/recovery rules; scheduled saves additionally enforce combat safety.</summary>
        public bool CanSaveNow { get { return _running && !Busy && SavesSystem.CanSaveNow; } }
        /// <summary>Asks for the normal warning path soon, retaining all autosave safety checks.</summary>
        public void RequestAutosaveSoon(IModule owner, string reason) { if (_running) SavesSystem.RequestAutosaveSoon(); }
    }
}
