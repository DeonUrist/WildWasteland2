using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>
    /// Compatibility part of the old CleanHUD entry (Wave A split). Since B17 the bridges belong to the Compatibility module and these
    /// methods are empty, except the Apocasaver presentation install that needs the HUD's own loading screen. Wave C deletes them.
    /// </summary>
    public sealed partial class HudModule
    {
        /// <summary>Empty (B17): the Apocapocket reader and the ChaseCamera observer are installed by their Compatibility bridges.</summary>
        partial void InitCompatibilityEarly() { }

        /// <summary>Empty (B17): the Vistas engine-heat notices are installed by the Compatibility VistasWorldGen bridge.</summary>
        partial void InitCompatibilityVistas() { }

        /// <summary>Installs the Apocasaver autosave screen and save-name popup hooks.</summary>
        partial void InitCompatibilityApocasaver() { ApocasaverLoading.Install(this, _loadingScreen); }

        /// <summary>Empty (B17): the Apocatalker dialogue mirror is installed by the Compatibility Apocatalker bridge.</summary>
        partial void InitCompatibilityTalk() { }

        /// <summary>Empty (B17): the mirror's job is "Compatibility.Apocatalker.Dialogue" (LateUpdateLast) of the Compatibility module.</summary>
        partial void InitCompatibilityDialogueJob() { }

        /// <summary>Empty (B17): the Weapons Pack icons and the ChaseCamera HUD observer are installed by their Compatibility bridges.</summary>
        partial void AttachCompatibility() { }

        /// <summary>Empty (B17): the Compatibility module hides the Apocatalker mirror on scene load.</summary>
        partial void SceneLoadedCompatibility() { }

        /// <summary>Empty (B17): the Weapons Pack icon override shuts down with the Compatibility bridge.</summary>
        partial void ShutdownCompatibilityEarly() { }

        /// <summary>Empty (B17): the Vistas engine-heat notices are cleared by their Compatibility bridge.</summary>
        partial void ShutdownCompatibilityVistas() { }

        /// <summary>Empty (B17): the ChaseCamera observer and the Apocatalker mirror are released by their Compatibility bridges.</summary>
        partial void ShutdownCompatibilityChase() { }

        /// <summary>Clears the Apocasaver loading-screen hooks (Apocapocket is cleared by its Compatibility bridge, B17).</summary>
        partial void ShutdownCompatibilityLate()
        {
            ApocasaverLoading.Clear();
        }
    }
}
