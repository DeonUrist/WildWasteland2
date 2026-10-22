using System;
using System.Collections.Concurrent;
using Den.Tools;
using MapMagic.Products;
using MapMagic.Terrains;

namespace WildWasteland.Core
{
    /// <summary>
    /// The single MapMagic tile subscription of the mod (7 independent ones today: Towns, POI, Performance, Quests, CleanHUD,
    /// Patrols TrafficRoads + VistasWorldGen.TerrainEdited). Modules register typed handlers with an owner; each handler runs in
    /// its own try/catch (a failing handler never stops the others, and is reported once per owner/kind, rate-limited by Log).
    /// Threading (AGENTS immutable handoff rule): Prepare and Applied and Moved run on the main thread; Finalized runs on a
    /// MapMagic worker thread - capture immutable data there, consume it in Applied. Handler lists are copy-on-write arrays
    /// so the worker never sees a list being modified; worker-thread failures are queued and logged on the main thread.
    /// Edited (Vistas terrain snapshot) is raised by whoever owns that hook through <see cref="RaiseEdited"/> (Compatibility
    /// in Wave B; until then a bridge from VistasWorldGen.TerrainEdited can be registered with <see cref="EditedSource"/>).
    /// </summary>
    public static class WorldTiles
    {
        /// <summary>Main thread, before MapMagic prepares a tile's generation (OnBeforeTilePrepare).</summary>
        public delegate void PrepareHandler(TerrainTile tile, TileData data);
        /// <summary>Finalized (worker thread) and Applied (main thread) signature.</summary>
        public delegate void TileHandler(TerrainTile tile, TileData data, StopToken stop);
        /// <summary>Moved / Edited signature (main thread).</summary>
        public delegate void TileEvent(TerrainTile tile);

        /// <summary>One registration (owner + one handler of one kind).</summary>
        private sealed class Entry
        {
            public IModule Owner; public PrepareHandler Prepare; public TileHandler Finalized, Applied; public TileEvent Moved, Edited; public string Name;
        }

        static readonly object _lock = new object();
        static Entry[] _entries = new Entry[0];
        static bool _installed;
        static readonly ConcurrentQueue<string> _workerErrors = new ConcurrentQueue<string>();

        /// <summary>Calls counted per kind (Debug page).</summary>
        public static long Prepared, Finalizations, Applications, Moves, Edits;

        /// <summary>Registers a Prepare handler (main thread, before generation).</summary>
        public static void OnPrepare(IModule owner, PrepareHandler h) { Add(new Entry { Owner = owner, Prepare = h, Name = "Prepare" }); }
        /// <summary>Registers a Finalized handler: WORKER THREAD, no Unity API; capture immutable data for Applied.</summary>
        public static void OnFinalized(IModule owner, TileHandler h) { Add(new Entry { Owner = owner, Finalized = h, Name = "Finalized" }); }
        /// <summary>Registers an Applied handler (main thread, tile objects exist).</summary>
        public static void OnApplied(IModule owner, TileHandler h) { Add(new Entry { Owner = owner, Applied = h, Name = "Applied" }); }
        /// <summary>Registers a Moved handler (main thread, tile moved/re-used).</summary>
        public static void OnMoved(IModule owner, TileEvent h) { Add(new Entry { Owner = owner, Moved = h, Name = "Moved" }); }
        /// <summary>Registers a TerrainEdited handler (main thread; Vistas edited a tile's terrain).</summary>
        public static void OnEdited(IModule owner, TileEvent h) { Add(new Entry { Owner = owner, Edited = h, Name = "Edited" }); }

        /// <summary>Removes every handler of owner (module shutdown).</summary>
        public static void RemoveAll(IModule owner)
        {
            lock (_lock)
            {
                int keep = 0;
                for (int i = 0; i < _entries.Length; i++) if (_entries[i].Owner != owner) keep++;
                if (keep == _entries.Length) return;
                var next = new Entry[keep]; int k = 0;
                for (int i = 0; i < _entries.Length; i++) if (_entries[i].Owner != owner) next[k++] = _entries[i];
                _entries = next;
            }
        }

        /// <summary>Removes one handler delegate (any kind) registered by owner.</summary>
        public static void Remove(IModule owner, Delegate handler)
        {
            lock (_lock)
            {
                int at = -1;
                for (int i = 0; i < _entries.Length && at < 0; i++)
                {
                    var e = _entries[i];
                    if (e.Owner == owner && (Equals(e.Prepare, handler) || Equals(e.Finalized, handler) || Equals(e.Applied, handler) || Equals(e.Moved, handler) || Equals(e.Edited, handler))) at = i;
                }
                if (at < 0) return;
                var next = new Entry[_entries.Length - 1];
                Array.Copy(_entries, 0, next, 0, at); Array.Copy(_entries, at + 1, next, at, _entries.Length - at - 1);
                _entries = next;
            }
        }

        /// <summary>Appends a registration (copy-on-write) and installs the MapMagic subscription on first use.</summary>
        static void Add(Entry e)
        {
            lock (_lock)
            {
                var next = new Entry[_entries.Length + 1];
                Array.Copy(_entries, next, _entries.Length); next[_entries.Length] = e;
                _entries = next;
            }
            Install();
        }

        /// <summary>Subscribes the five MapMagic/Vistas sources once (idempotent; no Unity scene API involved).</summary>
        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            TerrainTile.OnBeforeTilePrepare += RaisePrepare;
            TerrainTile.OnTileFinalized += RaiseFinalized;
            TerrainTile.OnTileApplied += RaiseApplied;
            TerrainTile.OnTileMoved += RaiseMoved;
        }

        /// <summary>Unsubscribes from MapMagic (plugin teardown).</summary>
        public static void Uninstall()
        {
            if (!_installed) return;
            _installed = false;
            TerrainTile.OnBeforeTilePrepare -= RaisePrepare;
            TerrainTile.OnTileFinalized -= RaiseFinalized;
            TerrainTile.OnTileApplied -= RaiseApplied;
            TerrainTile.OnTileMoved -= RaiseMoved;
        }

        /// <summary>
        /// Hook point for the TerrainEdited source: the owner of the Vistas hook (VistasWorldGen today, Compatibility later)
        /// subscribes <see cref="RaiseEdited"/> to its event, e.g. VistasWorldGen.TerrainEdited += WorldTiles.EditedSource.
        /// </summary>
        public static Action<TerrainTile> EditedSource { get { return RaiseEdited; } }

        /// <summary>MapMagic OnBeforeTilePrepare (main thread): runs every Prepare handler.</summary>
        static void RaisePrepare(TerrainTile tile, TileData data)
        {
            Prepared++;
            var list = _entries;
            for (int i = 0; i < list.Length; i++)
            {
                var h = list[i].Prepare; if (h == null) continue;
                try { h(tile, data); } catch (Exception ex) { Fail(list[i], ex, false); }
            }
        }

        /// <summary>MapMagic OnTileFinalized (worker thread): runs every Finalized handler; failures are queued for the main thread.</summary>
        static void RaiseFinalized(TerrainTile tile, TileData data, StopToken stop)
        {
            System.Threading.Interlocked.Increment(ref Finalizations);
            var list = _entries;
            for (int i = 0; i < list.Length; i++)
            {
                var h = list[i].Finalized; if (h == null) continue;
                try { h(tile, data, stop); } catch (Exception ex) { Fail(list[i], ex, true); }
            }
        }

        /// <summary>MapMagic OnTileApplied (main thread): logs queued worker failures, then runs every Applied handler.</summary>
        static void RaiseApplied(TerrainTile tile, TileData data, StopToken stop)
        {
            Applications++;
            DrainWorkerErrors();
            var list = _entries;
            for (int i = 0; i < list.Length; i++)
            {
                var h = list[i].Applied; if (h == null) continue;
                try { h(tile, data, stop); } catch (Exception ex) { Fail(list[i], ex, false); }
            }
        }

        /// <summary>MapMagic OnTileMoved (main thread): runs every Moved handler.</summary>
        static void RaiseMoved(TerrainTile tile)
        {
            Moves++;
            var list = _entries;
            for (int i = 0; i < list.Length; i++)
            {
                var h = list[i].Moved; if (h == null) continue;
                try { h(tile); } catch (Exception ex) { Fail(list[i], ex, false); }
            }
        }

        /// <summary>Vistas terrain edit (main thread): runs every Edited handler.</summary>
        public static void RaiseEdited(TerrainTile tile)
        {
            Edits++;
            var list = _entries;
            for (int i = 0; i < list.Length; i++)
            {
                var h = list[i].Edited; if (h == null) continue;
                try { h(tile); } catch (Exception ex) { Fail(list[i], ex, false); }
            }
        }

        /// <summary>Reports a handler failure: logged now on the main thread, queued from the worker thread.</summary>
        static void Fail(Entry e, Exception ex, bool worker)
        {
            string msg = "[" + (e.Owner != null ? e.Owner.Id : "Core") + "] tile " + e.Name + " handler failed: " + ex.GetType().Name + ": " + ex.Message;
            if (worker) { if (_workerErrors.Count < 64) _workerErrors.Enqueue(msg); }
            else Log.Warn(msg);
        }

        /// <summary>Logs worker-thread failures queued since the last main-thread event.</summary>
        static void DrainWorkerErrors()
        {
            string msg;
            while (_workerErrors.TryDequeue(out msg)) Log.Warn(msg);
        }
    }
}
