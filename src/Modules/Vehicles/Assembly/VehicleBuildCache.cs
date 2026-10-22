using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// Vehicle build cache: packed, uninitialised template structures (frame + fitted parts) and preprocessed reserve copies, so a template
    /// spawn consumes a prepared instance (never clones it: native components initialise only on the independently owned spawned copy).
    /// Arbitrated by Core WorldWork (Vehicles owner); while loading a save it spends its own bounded 12 ms slice per frame. Exactly one driver
    /// steps it per frame: the Vehicles jobs while the Vehicles module runs, else the old Patrols jobs.
    /// </summary>
    internal static class VehicleBuildCache
    {
        /// <summary>One structural variant (template body/parts, bumper, trailer flag) with its packed root and ready reserves.</summary>
        internal sealed class Entry : IPreparedVehicle
        {
            internal VehicleStructure Template;
            internal string Bumper;
            internal bool Trailer, Queued;
            public bool Ready { get; set; }
            public bool Failed { get; set; }
            internal GameObject Root;
            internal GameObject Preparing;
            internal PlayMakerFSM[] Fsms;
            internal int FsmIndex;
            internal readonly Queue<GameObject> Reserves = new Queue<GameObject>();
            internal IEnumerator Build;
            internal readonly List<Item> Items = new List<Item>();
        }
        /// <summary>A fitted part of a packed structure: child-index path and prefab name (saved-name registration on spawn).</summary>
        internal sealed class Item { internal int[] Path; internal string Prefab; }
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static readonly Queue<Entry> Pending = new Queue<Entry>();
        private static Entry _active;
        private static GameObject _holder;
        private static bool _reported;
        private const int ReserveCount = 2;
        private const double LoadingBudgetMs = 12;
        private static readonly System.Diagnostics.Stopwatch LoadTime = new System.Diagnostics.Stopwatch(), LoadSliceTime = new System.Diagnostics.Stopwatch();
        private static double _loadWorkMs;
        /// <summary>True while a save is loading (not saving): the cache then runs its own load slice.</summary>
        private static bool Loading
        {
            get
            {
                if (!SaveBus.Busy || SaveBus.Saving) return false;
                var saves = ModApi.Get<ISaves>();
                return saves == null || saves.Phase != SavePhase.Saving;
            }
        }
        /// <summary>True while warming is requested or variants are queued / being prepared.</summary>
        internal static bool Warming { get { return _active != null || Pending.Count > 0; } }

        /// <summary>Prepared fraction 0..1 of all reserves (failed variants count as done).</summary>
        internal static float LoadFraction
        {
            get
            {
                if (Entries.Count == 0) return 1f;
                float done = 0f;
                foreach (var entry in Entries.Values)
                {
                    done += entry.Failed ? ReserveCount : entry.Reserves.Count;
                    if (!entry.Failed && entry.Preparing != null && entry.Fsms != null)
                        done += entry.FsmIndex / (float)Math.Max(1, entry.Fsms.Length);
                }
                return Mathf.Clamp01(done / (Entries.Count * ReserveCount));
            }
        }
        /// <summary>
        /// Old Patrols loading-progress source (registered by PatrolsModule): while the Vehicles module runs it reports only the Patrols restore
        /// part (the cache reports itself through <see cref="PreparationWork"/>), otherwise the old combined line.
        /// </summary>
        /// <summary>Loading-progress source of the Vehicles module: vehicle preparation while the cache warms (null otherwise).</summary>
        internal static LoadingProgress.Work? PreparationWork()
        {
            if (!Warming) return null;
            return new LoadingProgress.Work(LoadFraction, Detail());
        }

        /// <summary>Loading-screen line of the cache.</summary>
        private static string Detail()
        {
            return _active != null ? "preparing vehicles - " + _active.Template.Name + " (" + Mathf.RoundToInt(LoadFraction * 100f) + "%)" : "preparing vehicle templates";
        }

        /// <summary>Exact binary definition of a variant, independent of template name, crew/cargo rolls and traffic-instance metadata.</summary>
        private static string Key(VehicleStructure t, string bumper, bool trailer)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(t.Body); writer.Write(bumper); writer.Write(trailer);
                writer.Write(t.Parts != null);
                if (t.Parts != null)
                {
                    writer.Write(t.Parts.Length);
                    foreach (var p in t.Parts)
                    {
                        writer.Write(p.prefab ?? ""); writer.Write(p.hinge ?? ""); writer.Write(p.parent);
                        Floats(writer, p.hingePos); Floats(writer, p.hingeRot); Floats(writer, p.pos); Floats(writer, p.rot);
                    }
                }
                else foreach (string part in new[] { t.Wheel, t.RearWheel, t.Engine, t.Radiator, t.SteeringWheel, t.Exhaust, t.Seat, t.PassengerSeat }) writer.Write(part ?? "");
                writer.Flush(); return Convert.ToBase64String(stream.ToArray());
            }
        }
        /// <summary>Writes a float array (length + values) into the variant key.</summary>
        private static void Floats(BinaryWriter writer, float[] values)
        { writer.Write(values != null ? values.Length : 0); if (values != null) foreach (float v in values) writer.Write(v); }

        /// <summary>The variant entry for a template/bumper/trailer, created and queued when new.</summary>
        internal static Entry Require(VehicleStructure t, string bumper, bool trailer)
        {
            string key = Key(t, bumper, trailer); Entry entry;
            if (Entries.TryGetValue(key, out entry)) return entry;
            entry = new Entry { Template = t, Bumper = bumper, Trailer = trailer };
            Entries.Add(key, entry); Enqueue(entry);
            return entry;
        }
        /// <summary>Queues a variant for preparation (once).</summary>
        private static void Enqueue(Entry entry)
        { if (entry.Queued || ReferenceEquals(_active, entry)) return; entry.Queued = true; Pending.Enqueue(entry); _reported = false; }

        /// <summary>Sliced job "Vehicles.Build cache" (0.5 ms): steps while the Vehicles module runs.</summary>
        internal static bool DriveFromVehicles() { return VehiclesModule.Running && Step(); }

        /// <summary>One gameplay step: not while loading (the load slice runs then) and within the shared frame budget.</summary>
        private static bool Step()
        {
            if (Loading || Scheduler.FrameMs >= Scheduler.FrameBudgetMs) return false;
            return StepCore();
        }

        /// <summary>Every-frame job "Vehicles.Build cache loading": the bounded load slice while the Vehicles module runs.</summary>
        internal static void LoadBurstFromVehicles() { if (VehiclesModule.Running) LoadSlice(); }

        /// <summary>
        /// While a save loads and the cache warms: steps the cache for up to 12 ms this frame (its own bounded slice; normal reserve refills keep
        /// the shared gameplay budget). Early-out: Patrols off (the park is Patrols' data), not loading, nothing to warm.
        /// </summary>
        private static void LoadSlice()
        {
            if (!Loading || !Warming) return;
            // Loading spends its own bounded slice; normal reserve refills retain the shared gameplay budget.
            if (!LoadTime.IsRunning)
            {
                LoadTime.Start();
                if (Log.L1) Log.Main(VehiclesModule.Instance, "Vehicle load preparation started: " + LoadingBudgetMs + " ms/frame budget; " + ReserveCount + " reserves per variant.");
            }
            LoadSliceTime.Restart();
            while (LoadSliceTime.Elapsed.TotalMilliseconds < LoadingBudgetMs && StepCore()) { }
            _loadWorkMs += LoadSliceTime.Elapsed.TotalMilliseconds;
            if (!Warming)
            {
                if (Log.L1) Log.Main(VehiclesModule.Instance, "Vehicle load preparation finished: " + Entries.Count + " structural variant(s); " + LoadTime.ElapsedMilliseconds + " ms elapsed, " + (long)_loadWorkMs + " ms work.");
                LoadTime.Reset(); _loadWorkMs = 0;
            }
        }

        /// <summary>
        /// One preparation step under WorldWork's Vehicles owner: warm the park on request, pack the next variant's structure (parts on hinges),
        /// then clone a reserve and preprocess its FSMs one per step; a failing variant is marked failed and dropped.
        /// </summary>
        private static bool StepCore()
        {
            if (!Warming || !WorldWork.Try(WorldWork.Vehicles)) return false;
            if (_active == null)
            {
                if (Pending.Count == 0)
                {
                    if (!_reported) { _reported = true; if (Log.L1) Log.Main(VehiclesModule.Instance, "Vehicle build cache ready: " + Entries.Count + " structural variant(s)."); }
                    return false;
                }
                _active = Pending.Dequeue();
                _active.Queued = false;
            }
            var entry = _active;
            try
            {
                if (entry.Root == null)
                {
                    var source = entry.Template.Resolve(entry.Template.Body);
                    if (source == null) throw new InvalidOperationException("Missing frame " + entry.Template.Body);
                    if (_holder == null)
                    {
                        _holder = new GameObject("WW.Vehicles.BuildCache") { hideFlags = HideFlags.HideAndDontSave };
                        _holder.SetActive(false); UnityEngine.Object.DontDestroyOnLoad(_holder);
                    }
                    entry.Root = UnityEngine.Object.Instantiate(source, _holder.transform, false);
                    entry.Root.name = source.name; entry.Root.transform.localPosition = Vector3.zero; entry.Root.transform.localRotation = Quaternion.identity;
                    entry.Root.SetActive(true); // inactive hierarchy: no native Awake, save registration, physics or AI
                    entry.Build = VehicleAssembler.PackStructure(entry.Root, entry.Template, entry.Bumper, entry.Trailer);
                    return true;
                }
                if (entry.Build != null)
                {
                    if (entry.Build.MoveNext()) return true;
                    ((IDisposable)entry.Build).Dispose(); entry.Build = null;
                    if (Log.L2) Log.Deep(VehiclesModule.Instance, "Vehicle build cached: " + entry.Template.Name + " / " + entry.Items.Count + " fitted parts / bumper " + entry.Bumper);
                    return true;
                }
                if (entry.Preparing == null)
                {
                    entry.Preparing = UnityEngine.Object.Instantiate(entry.Root, _holder.transform, false);
                    entry.Fsms = entry.Preparing.GetComponentsInChildren<PlayMakerFSM>(true); entry.FsmIndex = 0;
                    return true;
                }
                if (entry.FsmIndex < entry.Fsms.Length)
                {
                    // Native preprocessing decodes/binds actions and installs event handlers while the hierarchy is dormant.
                    // No state entry, native physics or save-list membership; consume this exact instance, never clone its runtime FSM data.
                    var fsm = entry.Fsms[entry.FsmIndex++];
                    fsm.Preprocess();
                    return true;
                }
                entry.Reserves.Enqueue(entry.Preparing); entry.Preparing = null; entry.Fsms = null; entry.Ready = true;
                if (entry.Reserves.Count >= ReserveCount) _active = null;
                return true;
            }
            catch (Exception e)
            {
                entry.Failed = true; _active = null;
                if (entry.Build != null) ((IDisposable)entry.Build).Dispose();
                if (entry.Root != null) UnityEngine.Object.Destroy(entry.Root);
                Log.Warn(VehiclesModule.Instance, "Vehicle build cache failed for " + entry.Template.Name + ": " + e);
                return false;
            }
        }

        /// <summary>Records a part fitted on the variant being packed (path from the root + prefab name).</summary>
        internal static void Record(GameObject part, string prefab)
        {
            var path = new List<int>();
            for (var node = part.transform; node != _active.Root.transform; node = node.parent) path.Add(node.GetSiblingIndex());
            path.Reverse(); _active.Items.Add(new Item { Path = path.ToArray(), Prefab = prefab });
        }

        /// <summary>Clones a part prefab under the dormant cache holder at the hinge pose (no Awake, no save registration).</summary>
        internal static GameObject ClonePart(GameObject prefab, Transform hinge)
        {
            var part = UnityEngine.Object.Instantiate(prefab, _holder.transform, false);
            part.transform.SetPositionAndRotation(hinge.position, hinge.rotation);
            return part;
        }

        /// <summary>Consumes a ready reserve: saved names for the car and its parts, moved into the active scene at the pose, then activated; refills the reserve.</summary>
        internal static GameObject Spawn(Entry entry, Vector3 position, Quaternion rotation)
        {
            // Clone while dormant, assign unique native save names, then activate the entire independent hierarchy.
            var car = entry.Reserves.Dequeue(); entry.Ready = entry.Reserves.Count > 0; Enqueue(entry);
            try
            {
                car.SetActive(false);
                ItemSpawner.Defer(car); ItemSpawner.Name(car, entry.Template.Body); ItemSpawner.AddExisting(car, true);
                foreach (var item in entry.Items)
                {
                    var node = car.transform; foreach (int index in item.Path) node = node.GetChild(index);
                    ItemSpawner.Name(node.gameObject, item.Prefab); ItemSpawner.AddExisting(node.gameObject, false);
                }
                car.transform.SetParent(null, false);
                SceneManager.MoveGameObjectToScene(car, SceneManager.GetActiveScene());
                car.transform.SetPositionAndRotation(position, rotation);
                car.SetActive(true);
                if (Log.L2) Log.Deep(VehiclesModule.Instance, "Vehicle build cache hit: " + entry.Template.Name + " -> " + car.name);
                return car;
            }
            catch { ItemSpawner.Discard(car); UnityEngine.Object.Destroy(car); throw; }
        }

        /// <summary>Drops every variant and the holder (scene change, Patrols shutdown); the park is warmed again on the next step.</summary>
        internal static void Reset()
        {
            LoadTime.Reset(); LoadSliceTime.Reset(); _loadWorkMs = 0;
            if (_active != null && _active.Build != null) ((IDisposable)_active.Build).Dispose();
            foreach (var entry in Entries.Values) { entry.Ready=false; entry.Failed=true; }
            _active = null; Pending.Clear(); Entries.Clear();
            if (_holder != null) UnityEngine.Object.Destroy(_holder);
            _holder = null; _reported = false; VehicleAssembler.Clear();
        }
    }
}
