using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    /// <summary>
    /// Compatibility part of the CleanHUD entry (Wave A split, B17): ChaseCamera, Apocapocket, Vistas World Gen engine heat,
    /// Apocasaver, Apocatalker and Weapons Pack bridges. Each method runs at the exact position of the original call.
    /// </summary>
    public sealed partial class CleanHudModule
    {
        /// <summary>Installs the ChaseCamera telemetry bridge and the Apocapocket slot reader.</summary>
        partial void InitCompatibilityEarly()
        {
            ChaseCameraHud.Clear(); ChaseCameraHud.Install(this);
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

        /// <summary>From the HUD attachment job: lazy Weapons Pack icon override and ChaseCamera install.</summary>
        partial void AttachCompatibility()
        {
            WeaponsPackIcons.Ensure(this);
            ChaseCameraHud.Install(this);
        }

        /// <summary>Hides the Apocatalker window on scene load.</summary>
        partial void SceneLoadedCompatibility() { TalkDialogue.Hide(); }

        /// <summary>Removes the Weapons Pack icon override.</summary>
        partial void ShutdownCompatibilityEarly() { WeaponsPackIcons.Shutdown(); }

        /// <summary>Clears the Vistas engine heat overlay state.</summary>
        partial void ShutdownCompatibilityVistas() { VistasEngineHud.Clear(); }

        /// <summary>Clears the ChaseCamera bridge and disposes the Apocatalker window.</summary>
        partial void ShutdownCompatibilityChase()
        {
            ChaseCameraHud.Clear();
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
