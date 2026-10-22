using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Loot
{
    /// <summary>
    /// Per-world spawner progress (README 6.9; ex Towns PoiDressMarkers progress fields). Two owners:
    /// <list type="bullet">
    /// <item>POI layout marker groups (owner key <see cref="MarkerOwner"/>, spawner path = marker identity): one-shot progress saved
    /// in the slot under ES3 key <c>WildWasteland.Towns.PoiDressMarkers.v1</c> (WWPM1 text: identity, OnOff, number, numberEnd per
    /// line; key, format and identity formula unchanged). Live FSMs are tracked and captured before every save/unload.</item>
    /// <item>Any other owner key reported through <see cref="ILootSpawning.ReportProgress"/>: kept in memory for the session
    /// (cleared on load/new world); the reporting module persists its own records (towns, gas stations).</item>
    /// </list>
    /// Static so the POI marker builder keeps working while the Loot module is switched off (the old call sites forward here).
    /// Main thread only.
    /// </summary>
    internal static class SpawnerProgress
    {
        /// <summary>Owner key of the POI layout marker groups.</summary>
        internal const string MarkerOwner = "PoiDressMarkers";
        private const string MarkerKey = "WildWasteland.Towns.PoiDressMarkers.v1";

        /// <summary>Saved state of one spawner.</summary>
        internal sealed class State
        {
            /// <summary>Native OnOff (1 = finished/off), spawned so far, target count.</summary>
            internal int OnOff, Number, NumberEnd;
        }

        /// <summary>A live marker spawner FSM whose variables are captured into its state.</summary>
        private struct Live
        {
            /// <summary>Marker identity.</summary>
            internal string Key;
            /// <summary>Native ItemSpawner FSM.</summary>
            internal PlayMakerFSM Fsm;
        }

        /// <summary>Fraction done of the running POI marker spawn-list scan (PoiSpawnCatalog), 1 when idle.</summary>
        internal static float ScanProgress = 1f;

        private static readonly Dictionary<string, State> markers = new Dictionary<string, State>(StringComparer.Ordinal);
        private static readonly List<Live> live = new List<Live>();
        private static readonly Dictionary<string, Dictionary<string, State>> others = new Dictionary<string, Dictionary<string, State>>(StringComparer.Ordinal);

        /// <summary>Saved marker state of an identity, or null.</summary>
        internal static State Marker(string key)
        {
            State state;
            return markers.TryGetValue(key, out state) ? state : null;
        }

        /// <summary>Adds the first (committed) state of a marker group; later builds reuse it, so layout/list changes never reroll.</summary>
        internal static State AddMarker(string key, int numberEnd, int onOff)
        {
            var state = new State { NumberEnd = numberEnd, OnOff = onOff };
            markers.Add(key, state);
            return state;
        }

        /// <summary>Tracks a live marker spawner FSM (captured before save/unload).</summary>
        internal static void Track(string key, PlayMakerFSM fsm) { live.Add(new Live { Key = key, Fsm = fsm }); }

        /// <summary>Copies the variables of every live marker FSM into its saved state and forgets destroyed FSMs.</summary>
        internal static void CaptureMarkers()
        {
            for (int i = 0; i < live.Count; i++)
            {
                var row = live[i];
                if (row.Fsm == null) continue;
                Set(MarkerOwner, row.Key, markers, NativeSpawners.Read(row.Fsm, "OnOff"), NativeSpawners.Read(row.Fsm, "number"), NativeSpawners.Read(row.Fsm, "numberEnd"));
            }
            live.RemoveAll(IsGone);
        }

        /// <summary>True for a tracked row whose FSM was destroyed.</summary>
        private static bool IsGone(Live row) { return row.Fsm == null; }

        /// <summary>Unload of the dressed sites: captures, stops tracking and (reset) forgets every marker state.</summary>
        internal static void ClearMarkers(bool reset)
        {
            CaptureMarkers(); live.Clear();
            if (reset) markers.Clear();
        }

        /// <summary>Writes the marker progress into the save slot (cache now, re-save + verify after the save; failures fail the save).</summary>
        internal static void SaveMarkers(string slot)
        {
            CaptureMarkers();
            var text = new StringBuilder("WWPM1\n");
            foreach (var row in markers) text.Append(row.Key).Append('\t').Append(row.Value.OnOff).Append('\t').Append(row.Value.Number).Append('\t').Append(row.Value.NumberEnd).Append('\n');
            SaveBus.Key(MarkerKey).Write(LootModule.Instance, slot, text.ToString());
        }

        /// <summary>Reads the marker progress of a slot (null/empty = new world: everything cleared). Throws on an invalid record so
        /// the caller blocks instead of rerolling committed groups. Also clears the session progress of other owners.</summary>
        internal static void LoadMarkers(string slot)
        {
            markers.Clear(); live.Clear(); others.Clear();
            if (string.IsNullOrEmpty(slot)) return;
            var settings = new ES3Settings(slot) { location = ES3.Location.File };
            if (!File.Exists(settings.FullPath) || !ES3.KeyExists(MarkerKey, settings)) return;
            string[] lines = ES3.Load<string>(MarkerKey, settings).Split('\n');
            if (lines.Length == 0 || lines[0] != "WWPM1" || lines.Length > 100000) throw new InvalidDataException("Invalid marker progress header/count");
            foreach (string line in lines)
            {
                if (line.Length == 0 || line == "WWPM1") continue;
                var v = line.Split('\t'); int on, number, end;
                if (v.Length != 4 || !int.TryParse(v[1], out on) || !int.TryParse(v[2], out number) || !int.TryParse(v[3], out end)
                    || on < 0 || on > 1 || number < 0 || end < 0 || end > 32 || number > 32 || markers.ContainsKey(v[0])) throw new InvalidDataException("Invalid marker progress row");
                markers.Add(v[0], new State { OnOff = on, Number = number, NumberEnd = end });
            }
            if (Log.L1) Log.Main(LootModule.Instance, "POI marker progress loaded: " + markers.Count + " group(s)");
        }

        /// <summary>Saved progress of a spawner path under an owner key (marker owner: tracked FSMs are captured first).</summary>
        internal static bool TryGet(string ownerKey, string spawnerPath, out bool on, out int number, out int numberEnd)
        {
            on = false; number = numberEnd = 0;
            if (ownerKey == null || spawnerPath == null) return false;
            State state = null;
            if (ownerKey == MarkerOwner) { CaptureMarkers(); markers.TryGetValue(spawnerPath, out state); }
            else { Dictionary<string, State> paths; if (others.TryGetValue(ownerKey, out paths)) paths.TryGetValue(spawnerPath, out state); }
            if (state == null) return false;
            on = state.OnOff > 0; number = state.Number; numberEnd = state.NumberEnd;
            return true;
        }

        /// <summary>Records progress reported through the API (publishes <see cref="SpawnerProgressChanged"/> on change).</summary>
        internal static void Report(string ownerKey, string spawnerPath, bool on, int number, int numberEnd)
        {
            if (ownerKey == null || spawnerPath == null) return;
            Dictionary<string, State> paths;
            if (ownerKey == MarkerOwner) paths = markers;
            else if (!others.TryGetValue(ownerKey, out paths)) others[ownerKey] = paths = new Dictionary<string, State>(StringComparer.Ordinal);
            Set(ownerKey, spawnerPath, paths, on ? 1 : 0, number, numberEnd);
        }

        /// <summary>Remaining spawns of an owner: sum of numberEnd - number over its unfinished spawners; false when unknown.</summary>
        internal static bool TryBudget(string ownerKey, out int remaining)
        {
            remaining = 0;
            Dictionary<string, State> paths;
            if (ownerKey == MarkerOwner) { CaptureMarkers(); paths = markers; }
            else if (ownerKey == null || !others.TryGetValue(ownerKey, out paths)) return false;
            foreach (var state in paths.Values) if (state.OnOff == 0 && state.NumberEnd > state.Number) remaining += state.NumberEnd - state.Number;
            return true;
        }

        /// <summary>Stores a state and publishes <see cref="SpawnerProgressChanged"/> when it changed and someone listens.</summary>
        private static void Set(string ownerKey, string path, Dictionary<string, State> paths, int onOff, int number, int numberEnd)
        {
            State state;
            if (!paths.TryGetValue(path, out state)) paths[path] = state = new State { OnOff = -1 };
            if (state.OnOff == onOff && state.Number == number && state.NumberEnd == numberEnd) return;
            state.OnOff = onOff; state.Number = number; state.NumberEnd = numberEnd;
            if (!ModEvents.HasListeners<SpawnerProgressChanged>()) return;
            var evt = new SpawnerProgressChanged { OwnerKey = ownerKey, SpawnerPath = path, On = onOff > 0, Number = number, NumberEnd = numberEnd };
            ModEvents.Publish(ref evt);
        }
    }
}
