using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    /// <summary>
    /// Compatibility part of the CleanHUD entry (Wave A split, B17): ChaseCamera, Apocapocket, Vistas World Gen engine heat,
    /// Apocasaver, Apocatalker and Weapons Pack bridges. Each method runs at the exact position of the original call.
    /// </summary>
    public sealed partial class CleanHudModule
    {
        /// <summary>Installs the Apocapocket slot reader (the ChaseCamera part moved to the Compatibility ChaseCamera bridge, B17).</summary>
        partial void InitCompatibilityEarly()
        {
            ApocapocketInventory.Install(this);
        }

        /// <summary>Installs the Vistas World Gen engine heat overlay replacement.</summary>
        partial void InitCompatibilityVistas() { VistasEngineHud.Install(this); }

        /// <summary>Installs the Apocasaver autosave screen and save-name popup hooks.</summary>
        partial void InitCompatibilityApocasaver() { ApocasaverLoading.Install(this, _loadingScreen); }

        /// <summary>Installs the Apocatalker dialogue mirror.</summary>
        partial void InitCompatibilityTalk() { TalkDialogue.Install(this); }

        /// <summary>Registers the "Dialogue window" job (LateUpdateLast) of the Apocatalker mirror.</summary>
        partial void InitCompatibilityDialogueJob() { Scheduler.EveryFrame(this, "Dialogue window", TalkDialogue.Tick, Phase.LateUpdateLast); }

        /// <summary>Empty (B17): the Weapons Pack icons and the ChaseCamera HUD observer are installed by their Compatibility bridges.</summary>
        partial void AttachCompatibility() { }

        /// <summary>Hides the Apocatalker window on scene load.</summary>
        partial void SceneLoadedCompatibility() { TalkDialogue.Hide(); }

        /// <summary>Empty (B17): the Weapons Pack icon override shuts down with the Compatibility bridge.</summary>
        partial void ShutdownCompatibilityEarly() { }

        /// <summary>Clears the Vistas engine heat overlay state.</summary>
        partial void ShutdownCompatibilityVistas() { VistasEngineHud.Clear(); }

        /// <summary>Disposes the Apocatalker window (the ChaseCamera observer is cleared by its Compatibility bridge, B17).</summary>
        partial void ShutdownCompatibilityChase()
        {
            TalkDialogue.Dispose();
        }

        /// <summary>Clears the Apocasaver and Apocapocket bridges.</summary>
        partial void ShutdownCompatibilityLate()
        {
            ApocasaverLoading.Clear();
            ApocapocketInventory.Clear();
        }
    }
}
