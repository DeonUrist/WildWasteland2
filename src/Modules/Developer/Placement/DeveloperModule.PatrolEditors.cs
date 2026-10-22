using System.Collections.Generic;
using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.Developer
{
    /// <summary>Connects placement tools to their actual Developer module lifecycle.</summary>
    public sealed partial class DeveloperModule
    {
        private PatrolEditorRuntime _patrolEditors;
        /// <summary>Binds Developer placement tool configuration before any scene objects are created.</summary>
        partial void BindPatrolEditors(ModuleConfig cfg) {_patrolEditors=new PatrolEditorRuntime(this,cfg);}
        /// <summary>Starts the actual Developer-owned placement runtime.</summary>
        partial void InitPatrolEditors() {_patrolEditors.Start();}
        /// <summary>Releases Developer placement targets, markers and native visual edit ownership.</summary>
        partial void ShutdownPatrolEditors() {_patrolEditors?.Stop();}
        /// <summary>Discards previous-scene targets and temporary marker state.</summary>
        partial void ScenePatrolEditors() {_patrolEditors?.Scene();}
        /// <summary>Returns pages owned by Developer placement tools.</summary>
        private IEnumerable<ToolPage> PatrolEditorPages() {return _patrolEditors!=null?_patrolEditors.Pages():new ToolPage[0];}
    }
}
