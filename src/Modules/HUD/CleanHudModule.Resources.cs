using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>
    /// HUD resource preload, scene cache and release check lifecycle.
    /// </summary>
    public sealed partial class HudModule
    {
        private Coroutine _updateCheck;

        /// <summary>Preloads the embedded theme textures, map icons and glyph font, then resets the scene cache.</summary>
        partial void InitCoreAssets()
        {
            Assets.Preload();
            SceneCache.Reset();
        }

        /// <summary>Starts the GitHub release check on the Core runner (once per session).</summary>
        partial void InitCoreUpdateCheck() { _updateCheck = Scheduler.StartCoroutine(Menu.CheckUpdates()); }

        /// <summary>Registers the sliced "Scene key discovery" job (500 us).</summary>
        partial void InitCoreSceneJob() { Scheduler.Sliced(this, "Scene key discovery", 500, SceneCache.Step); }

        /// <summary>Forgets cached scene paths after a scene load.</summary>
        partial void SceneResetCore() { SceneCache.Reset(); }

        /// <summary>Resets the scene cache and starts key-FSM discovery when the world is ready.</summary>
        partial void WorldReadyCore() { SceneCache.Reset(); SceneCache.BeginKeys(); }

        /// <summary>Stops the release check coroutine.</summary>
        partial void ShutdownCoreUpdateCheck() { Scheduler.StopCoroutine(_updateCheck); _updateCheck = null; }

        /// <summary>Clears the scene cache and destroys the theme/icon textures.</summary>
        partial void ShutdownCoreAssets() { SceneCache.Clear(); Assets.Dispose(); }
    }
}
