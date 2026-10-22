using WildWasteland.Core;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// Debug part of the Performance entry (Wave A split): binds and drives <see cref="Telemetry"/> (frame ring, census,
    /// lifecycle tags). B03 takes this over; the calls run at the same points as before the split.
    /// </summary>
    public sealed partial class PerformanceModule
    {
        /// <summary>Binds the Telemetry entries ([Performance / Telemetry]) after the feature entries (BindConfig tail).</summary>
        partial void BindDebug(ModuleConfig cfg) { Telemetry.Bind(cfg); }

        /// <summary>Starts Telemetry jobs (Init) and routes the Access compile queue's first batch to the "compile" spike tag.</summary>
        partial void InitDebug()
        {
            Telemetry.Init(this);
            Access.Queue.OnFirstBatch = MarkCompile;
        }

        /// <summary>Stops Telemetry (Shutdown) and detaches the compile-queue tag callback if it is still ours.</summary>
        partial void ShutdownDebug()
        {
            Telemetry.Shutdown();
            if (Access.Queue.OnFirstBatch == (System.Action)MarkCompile) Access.Queue.OnFirstBatch = null;
        }

        /// <summary>Tags the next frame as "scene" (OnSceneLoaded, before features react).</summary>
        partial void SceneLoadedDebug() { Telemetry.OnSceneLoaded(); }

        /// <summary>Tags the current frame with a lifecycle event (save, load, tile) for spike attribution.</summary>
        partial void MarkDebug(string tag) { Telemetry.Mark(tag); }

        /// <summary>Access.Queue first-batch callback: the former CompileQueue Telemetry.Mark("compile") (request W0B-6).</summary>
        static void MarkCompile() { Telemetry.Mark("compile"); }
    }
}
