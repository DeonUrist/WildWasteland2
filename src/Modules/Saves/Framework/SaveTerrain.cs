using WildWasteland.Core;
using UI = WildWasteland.Core.UI;
using Events = WildWasteland.Core.Events;
using System;
using System.Collections;
using System.IO;
using Den.Tools.Tasks;
using MapMagic.Core;
using UnityEngine;

namespace WildWasteland.Modules.Saves
{
    internal static class SaveTerrain
    {
        static bool _held, _draining;
        /// <summary>Installs the module-owned terrain admission barrier used during save publication.</summary>
        internal static void Install(IModule owner)
        {
            if (!Patches.Patch(owner, Patches.Method(typeof(MapMagicObject), "Update"), prefix: Patches.Method(typeof(SaveTerrain), "MapUpdate"))
                || !Patches.Patch(owner, Patches.Method(typeof(CoroutineManager), "Update"), prefix: Patches.Method(typeof(SaveTerrain), "ApplyUpdate")))
                throw new InvalidOperationException("Terrain save barrier could not be installed");
            Log.Always("[System] Terrain save barrier ready: drain pending work, hold streaming until publication completes");
        }
        /// <summary>Holds new terrain admission while allowing queued applications to drain.</summary>
        static bool MapUpdate()
        {
            if (!_held) return true;
            // Stop new tile moves/admission, but let already queued applications finish before Begin.
            if (_draining) CoroutineManager.Update();
            return false;
        }
        /// <summary>Allows terrain applications only before the publication barrier fully closes.</summary>
        static bool ApplyUpdate() { return !_held || _draining; }
        /// <summary>Drains terrain to two quiet observations with timeout and generation checks.</summary>
        internal static IEnumerator Prepare()
        {
            _held = _draining = true;
            int generation = SaveBus.Generation, quiet = 0; float deadline = Time.unscaledTime + 120f;
            while (true)
            {
                if (generation != SaveBus.Generation) throw new IOException("World changed before terrain could settle for saving");
                if (Time.unscaledTime >= deadline) throw new IOException("Terrain is still loading; save deferred before writing. Try again after loading finishes");
                quiet = ThreadManager.IsWorking || CoroutineManager.IsWorking ? 0 : quiet + 1;
                if (quiet >= 2)
                {
                    _draining = false;
                    Log.Always("[System] Terrain settled; streaming held for world save");
                    yield break;
                }
                yield return null;
            }
        }
        /// <summary>Releases terrain admission and queued-application barriers.</summary>
        internal static void Release() { _held = _draining = false; }
    }
}
