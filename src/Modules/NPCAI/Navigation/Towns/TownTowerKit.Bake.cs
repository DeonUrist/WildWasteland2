using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.NPC;

namespace WildWasteland.Modules.POI
{
    /// <summary>
    /// Isolated navigation bake of the extracted small tower: one <see cref="Nav.IsolatedBake"/> of the kit's prefab (ex reflection into
    /// Nav Make/BeginBake/BakeStep/CloseMap). Wave A (A6): partial split of Towns/TownTowerKit.cs; the POI part loads the kit.
    /// </summary>
    internal sealed partial class TownTowerKit
    {
        private Nav.IsolatedBake _bake;

        /// <summary>The tower's reusable map is published (or navigation is off).</summary>
        internal bool Ready { get; private set; }

        /// <summary>Bake progress 0..1.</summary>
        internal float Progress { get { return Ready ? 1 : _bake == null ? 0 : _bake.Progress; } }

        /// <summary>One slice of the isolated bake; false when nothing (more) to do. Throws like the baker when the tower can't be mapped.</summary>
        internal bool Step()
        {
            if (Ready || !Available) return false;
            if (!TownBakeBridge.Enabled) { Ready = true; return true; }
            if (_bake == null) _bake = new Nav.IsolatedBake(Prefab);
            _bake.Step();
            if (_bake.Ready)
            {
                Ready = true;
                if (Log.L1) Log.Main(NavRuntime.Owner, "isolated small-tower navigation ready; shared reusable map: " + _bake.MapFile);
                _bake = null;
            }
            return true;
        }

        /// <summary>Drops a running isolated bake (kit disposed).</summary>
        private void ReleaseBake()
        {
            if (_bake != null) _bake.Release();
            _bake = null;
        }
    }
}
