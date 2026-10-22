using System;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// Low Crawl, animation side (split from PlayerBody/Animation/Crawling.cs in Wave A): the humanoid "LowCrawl" clip from the embedded
    /// low-crawl.bundle, mixed into the body by CrawlBody. Target: Presentation character (B05).
    /// </summary>
    internal static partial class Crawling
    {
        internal static AnimationClip Clip;
        static AssetBundle _bundle;

        /// <summary>Loads the LowCrawl clip once (module Init); throws when the bundle or a humanoid clip is missing, as before.</summary>
        static void LoadClip()
        {
            if (Clip == null)
            {
                _bundle = AssetBundle.LoadFromFile(DiskResources.FilePath("Presentation.Crawl.low-crawl.bundle"));
                if (_bundle != null) foreach (var clip in _bundle.LoadAllAssets<AnimationClip>()) if (clip.name == "LowCrawl") Clip = clip;
                if (Clip == null || !Clip.humanMotion) throw new InvalidOperationException("Low Crawl humanoid clip unavailable");
            }
        }
    }
}
