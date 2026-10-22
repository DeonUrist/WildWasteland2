using System;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (split from PlayerBody/PlayerBodyModule.cs in Wave A): the player driver - BodyRunner state and jobs
    /// (build/rebuild the body, view routing, end of the old Runner) and the Low Crawl input layer. Target: Presentation player (B05).
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        /// <summary>Init (first): a clean BodyRunner state, also after a runtime off -> on.</summary>
        partial void InitPlayer()
        {
            BodyRunner.ResetAll();
        }

        /// <summary>Init: Low Crawl hooks and its EarlyUpdate job (after the third-person GetButtonDown prefix).</summary>
        partial void InitPlayerCrawling()
        {
            Crawling.Init(this);
        }

        /// <summary>Init: the old Runner Update / LateUpdate as jobs.</summary>
        partial void InitPlayerJobs()
        {
            Scheduler.EveryFrame(this, "Body update", BodyRunner.Tick, Phase.Update);
            Scheduler.EveryFrame(this, "Body late", BodyRunner.LateTick, Phase.LateUpdate);
        }

        /// <summary>Shutdown: the body is destroyed and the game's own player restored.</summary>
        partial void ShutdownPlayer()
        {
            try { BodyRunner.TearDown(); } catch (Exception e) { Error(e); }
        }

        /// <summary>Scene load: BodyRunner drops its scene references.</summary>
        partial void ScenePlayer()
        {
            BodyRunner.OnSceneLoaded();
        }
    }
}
