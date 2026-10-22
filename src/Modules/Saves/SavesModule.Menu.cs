using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Saves
{
    public sealed partial class SavesModule
    {
        /// <summary>Registers named menu actions only while the real save engine owns its native adapters.</summary>
        void RegisterMenuActions()
        {
            UiActions.Register(this, "Saves.OpenSave", () => SaveLibraryPage.Open(true));
            UiActions.Register(this, "Saves.OpenLoad", () => SaveLibraryPage.Open(false));
            UiActions.Register(this, "Saves.Continue", ContinueLatest, LatestCaption);
        }
        /// <summary>Loads the newest published snapshot through the normal singleplayer admission path.</summary>
        static void ContinueLatest()
        {
            string file = SaveRuntime.Latest();
            if (file != null) SinglePlayerSaving.Load(file);
        }
        /// <summary>Reads the newest snapshot caption when a menu rebuild asks for it, without leaking the catalogue type.</summary>
        static string LatestCaption()
        {
            string file = SaveRuntime.Latest();
            return file == null ? "" : SaveSystem.Info(file).Name;
        }
    }
}
