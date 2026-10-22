using WildWasteland.Core;

namespace WildWasteland.Modules.Developer
{
    /// <summary>
    /// Developer module entry (W0 skeleton, README 3.1): pose and placement editors (README 6.11, D7).
    /// Registered after the existing modules; Init is empty until its Wave B owner moves the features in. Its [Developer] Enabled entry is
    /// new and nothing reads it yet. Assets: plugins/WildWasteland/Developer/, data: config/WildWasteland/Developer/ (D13).
    /// </summary>
    public sealed partial class DeveloperModule : ModuleBase
    {
        /// <summary>Module id = config section prefix, Harmony id suffix and asset folder name.</summary>
        public override string Id { get { return "Developer"; } }

        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Developer"; } }

        /// <summary>No entries yet (Core binds [Developer] Enabled).</summary>
        public override void BindConfig(ModuleConfig cfg) {BindPatrolEditors(cfg);}

        /// <summary>Nothing to start yet.</summary>
        public override void Init() {InitPatrolEditors();}
        public override void Shutdown() {ShutdownPatrolEditors();}
        public override void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,UnityEngine.SceneManagement.LoadSceneMode mode) {ScenePatrolEditors();}
        public override System.Collections.Generic.IEnumerable<WildWasteland.Core.UI.ToolPage> ToolPages() {return PatrolEditorPages();}
        partial void BindPatrolEditors(ModuleConfig cfg);
        partial void InitPatrolEditors();
        partial void ShutdownPatrolEditors();
        partial void ScenePatrolEditors();
    }
}
