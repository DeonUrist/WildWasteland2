using System;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>
    /// Lifecycle of NPCAI navigation, driven by the NPCAI module entry (NPCAIModule.Navigation.cs): registers the navigation jobs
    /// (grid discovery/bake, passthrough, POI graph build, POI traversals, POI walkers - the last one is its own job, README 6.2), resets
    /// every registry on scene load and start/stop, and shifts scene-space data on floating-origin jumps. While <see cref="Active"/> the old
    /// NPC-entry calls (Nav.Tick, PoiNav.Reset, ...) are no-ops, so nothing runs twice while both entries are registered (Wave B hand-off).
    /// </summary>
    internal static class NavRuntime
    {
        /// <summary>True while the NPCAI entry owns navigation (between <see cref="Start"/> and <see cref="Stop"/>).</summary>
        internal static bool Active { get; private set; }

        /// <summary>The module that registered the jobs (null while stopped).</summary>
        internal static IModule Owner { get; private set; }

        /// <summary>Module start: clean registries, then the jobs in the old runner order (Nav, Passthrough, PoiNav.Build, PoiNav.Traverse, PoiNav.Walkers).</summary>
        internal static void Start(IModule owner)
        {
            Owner = owner;
            Active = true;
            ResetAll();
            Scheduler.EveryFrame(owner, "Nav", Nav.Update);
            Scheduler.Interval(owner, "Passthrough", 0.25f, Passthrough.Update, Phase.Update, true);
            Scheduler.Sliced(owner, "PoiNav.Build", 500, PoiNav.BuildSlice);
            Scheduler.EveryFrame(owner, "PoiNav.Traverse", PoiNavTraverse.Update, Phase.EarlyFixedUpdate);
            Scheduler.EveryFrame(owner, "PoiNav.Walkers", PoiNav.UpdateWalkers);
        }

        /// <summary>Module stop: POI walkers/traversals released, passthrough pairs collide again, every registry cleared (Core removes the jobs).</summary>
        internal static void Stop()
        {
            Step("POI navigation", PoiNav.ResetCore);
            Step("Passthrough", Passthrough.ShutdownCore);
            Step("Nav", Nav.ResetCore);
            Active = false;
            Owner = null;
        }

        /// <summary>Scene load / start: every navigation registry and cache starts empty (the old NpcModule.ResetAll navigation part).</summary>
        internal static void ResetAll()
        {
            Step("Nav reset", Nav.ResetCore);
            Step("Passthrough reset", Passthrough.ResetCore);
            Step("PoiNav reset", PoiNav.ResetCore);
        }

        /// <summary>Floating origin (delta = old - new offset): grid maps, then POI instances/routes/traversals.</summary>
        internal static void Shift(Vector3 delta)
        {
            try { Nav.ShiftCore(delta); } catch (Exception e) { Log.Error(Owner, "Nav origin shift failed", e); }
            try { PoiNav.ShiftCore(delta); } catch (Exception e) { Log.Error(Owner, "PoiNav origin shift failed", e); }
        }

        /// <summary>Runs one lifecycle step; an exception is logged and the next step still runs.</summary>
        static void Step(string what, Action step)
        {
            try { step(); } catch (Exception e) { Log.Error(Owner, "navigation " + what + " failed", e); }
        }
    }
}
