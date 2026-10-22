using WildWasteland.Core;
using WildWasteland.Core.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.Tools
{
    /// <summary>
    /// Owns authoring tools and their scheduled work. LOD export belongs here independently of optimization presets;
    /// the remaining editor integrations are supplied by B16 (README 6.10).
    /// </summary>
    public sealed class ToolsModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Tools"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Tools"; } }

        /// <summary>No entries yet (Core binds [Tools] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) { }

        /// <summary>Authoring work starts only from an explicit tool action.</summary>
        public override void Init() { }

        /// <summary>Stops an in-progress export when Tools is disabled.</summary>
        public override void Shutdown() { GeneratedLodExport.Stop(); }

        /// <summary>Releases captured mesh references before a different save is loaded.</summary>
        public override void OnLoadBegin(string slot) { GeneratedLodExport.Stop(); }

        /// <summary>Stops an export whose source scene has been replaced.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { GeneratedLodExport.Stop(); }

        /// <summary>Registers the explicit loaded-mesh export authoring page.</summary>
        public override IEnumerable<ToolPage> ToolPages()
        {
            yield return new ActionPage(this, ToolGroups.Debug, "Generated LOD export", DrawLodExport) { IsTool = true };
        }

        /// <summary>Shows capture progress and explicit start/stop actions without changing optimization settings.</summary>
        private void DrawLodExport(ToolLayout layout)
        {
            layout.Text(GeneratedLodExport.Status);
            layout.Text("Exports loaded, readable mesh originals for the offline LOD generator. Wait for completion, close the game, then run Generate.cmd.");
            if (GeneratedLodExport.Running)
            {
                if (layout.Button("Stop loaded-model export")) GeneratedLodExport.Stop();
            }
            else if (layout.Button("Export loaded models for LOD generator")) GeneratedLodExport.Start(this);
        }
    }
}
