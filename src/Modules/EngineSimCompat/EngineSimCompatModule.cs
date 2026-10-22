using UnityEngine.SceneManagement;
using WildWasteland.Core;

namespace WildWasteland.Modules.EngineSimCompat
{
    /// <summary>
    /// Registered entry of the Engine Sim compatibility bridge (Wave A: the entry stays registered here; the bridge itself lives in
    /// Compatibility/EngineSim/_fromEngineSimCompat as the other partial part of this class and is taken over by B17).
    /// </summary>
    public sealed partial class EngineSimCompatModule : ModuleBase
    {
        /// <summary>Module id (config section, Harmony id suffix).</summary>
        public override string Id { get { return "EngineSimCompat"; } }
        /// <summary>Name shown on the Modules page.</summary>
        public override string DisplayName { get { return "Engine Sim compatibility"; } }
        /// <summary>Binds nothing of its own; the Compatibility part may add entries.</summary>
        public override void BindConfig(ModuleConfig cfg) { }
        /// <summary>Starts the Engine Sim bridge (Compatibility part).</summary>
        public override void Init() { InitCompatibility(); }
        /// <summary>Forwards scene loads to the bridge.</summary>
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { SceneLoadedCompatibility(scene, mode); }
        /// <summary>Stops the bridge and withdraws the ignition provider.</summary>
        public override void Shutdown() { ShutdownCompatibility(); }

        /// <summary>Compatibility part: Engine Sim detection, patches and jobs.</summary>
        partial void InitCompatibility();
        /// <summary>Compatibility part: reseeds the discovery cache and clears start requests.</summary>
        partial void SceneLoadedCompatibility(Scene scene, LoadSceneMode mode);
        /// <summary>Compatibility part: undoes the bridge.</summary>
        partial void ShutdownCompatibility();
    }
}
