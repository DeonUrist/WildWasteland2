using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>
    /// Core part of the CleanHUD entry (Wave A split, B01): theme (accent, embedded textures, glyph font), scene cache, pause cursor
    /// fix and the release check. Each method runs at the exact position of the original call in HudModule.
    /// </summary>
    public sealed partial class HudModule
    {
        private Coroutine _updateCheck;

        /// <summary>Subscribes the accent theme to its setting.</summary>
        partial void InitCoreTheme() { }

        /// <summary>Preloads the embedded theme textures, map icons and glyph font, then resets the scene cache.</summary>
        partial void InitCoreAssets()
        {
            Assets.Preload();
            SceneCache.Reset();
        }

        /// <summary>Starts the GitHub release check on the Core runner (once per session).</summary>
        partial void InitCoreUpdateCheck() { _updateCheck = Scheduler.StartCoroutine(Menu.CheckUpdates()); }

        /// <summary>Subscribes PauseCursorFix to Core.Hooks.EnterState (postfix).</summary>
        partial void InitCoreHooks() { }

        /// <summary>Registers the sliced "Scene key discovery" job (500 us).</summary>
        partial void InitCoreSceneJob() { Scheduler.Sliced(this, "Scene key discovery", 500, SceneCache.Step); }

        /// <summary>Registers the sliced "Accent panels" job (500 us).</summary>
        partial void InitCoreAccentJob() { }

        /// <summary>Forgets cached scene paths after a scene load.</summary>
        partial void SceneResetCore() { SceneCache.Reset(); }

        /// <summary>Resets the scene cache and starts key-FSM discovery when the world is ready.</summary>
        partial void WorldReadyCore() { SceneCache.Reset(); SceneCache.BeginKeys(); }

        /// <summary>Stops the release check coroutine.</summary>
        partial void ShutdownCoreUpdateCheck() { Scheduler.StopCoroutine(_updateCheck); _updateCheck = null; }

        /// <summary>Clears the scene cache and destroys the theme/icon textures.</summary>
        partial void ShutdownCoreAssets() { SceneCache.Clear(); Assets.Dispose(); }

        /// <summary>Resets the glyph font and unsubscribes the accent theme.</summary>
        partial void ShutdownCoreTheme()
        {
            // Shared Core theme remains alive while HUD is disabled.
        }
    }
}
