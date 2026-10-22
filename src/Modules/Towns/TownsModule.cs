using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using Den.Tools;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Nodes.ObjectsGenerators;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Towns
{
    public sealed class TownsModule : ModuleBase
    {
        private sealed class Registration
        {
            internal Graph Graph;
            internal IOutlet<TransitionsList>[] Avoid;
            internal TownAssets Assets;
        }
        private sealed class Capture
        {
            internal Registration Registration;
            internal TownPlan Plan;
            internal GenerationInput Input;
            internal int Epoch;
            internal List<TownGasStation.Request> GasStation;

        }
        private sealed class Live
        {
            internal TerrainTile Tile;
            internal TownSite Site;
            internal Registration Registration;
            internal bool Finished;
            internal bool WallFailed;
            internal int LotFailures, MapFailures;
            internal float PreparedAt;
        }
        private readonly TownSettings _settings = new TownSettings();
        private readonly TownNames _names = new TownNames();
        private TownGasStation _gasStation;
        private PoiDressing _dressing;
        private PoiDresser _dresser;
        private readonly Dictionary<Graph, Registration> _graphs = new Dictionary<Graph, Registration>();
        private readonly ConcurrentDictionary<TileData, Capture> _captures = new ConcurrentDictionary<TileData, Capture>();
        private readonly ConcurrentQueue<Capture> _planned = new ConcurrentQueue<Capture>();
        private readonly List<Live> _sites = new List<Live>();
        private readonly List<Live> _resume = new List<Live>();
        private string _startingTerrainState;

        private Dictionary<string, TownRecord> _records = new Dictionary<string, TownRecord>(StringComparer.Ordinal);
        private bool _running, _blocked, _waitingForTowns, _loading;
        private int _epoch, _next;
        private int _builtFrame = -1;
        private float _buildAfter;
        private GameObject _staging;
        private readonly System.Diagnostics.Stopwatch _burst = new System.Diagnostics.Stopwatch();
        private const int PrepStepLimit = 60000;   // overlay/connection steps per town before preparation is cut short (work, not wall clock)
        public override string Id { get { return "Towns"; } }
        public override string DisplayName { get { return "Towns"; } }
        public int SiteCount { get { return _sites.Count; } }
        public TownSite GetSite(int index) { return _sites[index].Site; }
        internal TownSite MarkerTown(Transform root, out List<GameObject> pool)
        {
            pool = null;
            foreach (var live in _sites) foreach (var building in live.Site.Buildings)
                if (building != null && building.transform == root)
                {
                    if (!live.Registration.Assets.Population.TryGetValue(live.Site.Plan.Faction, out pool) || pool.Count == 0)
                        throw new InvalidOperationException("No native " + live.Site.Plan.Faction + " population for town POI markers.");
                    return live.Site;
                }
            return null;
        }
        public override void BindConfig(ModuleConfig cfg) { _settings.Bind(cfg); }
        public void BindStandalone(ConfigFile file) { _settings.Bind(file); }
        public override void Init()
        {
            if (_running) return;
            _running = true; TownsApi.Provider = this;
            _gasStation = new TownGasStation(_settings);
            _names.Load(); _names.Begin(_records);
            TownBakeBridge.Install(this);
            _dressing = new PoiDressing(); _dressing.Install(this);
            _dresser = new PoiDresser(_dressing); _dresser.Install(this, _settings);
            InteriorPopulation.Install(this);
            TownMarkers.Install(this);


            TownPoiSettings.Install(this, _settings);
            SaveBus.StateChanged += StateChanged;
            Hooks.WaitUpdate.Prefix(this, BeforeHandoff);
            LoadingProgress.Register(this, LoadProgress);

            _settings.PopulationMinPercent.SettingChanged += PopulationChanged; _settings.PopulationMaxPercent.SettingChanged += PopulationChanged;
            _settings.LootMinPercent.SettingChanged += PopulationChanged; _settings.LootMaxPercent.SettingChanged += PopulationChanged;
            TerrainTile.OnBeforeTilePrepare += Prepare;
            TerrainTile.OnTileFinalized += Finalized; TerrainTile.OnTileApplied += Applied; TerrainTile.OnTileMoved += Moved;
            VistasWorldGen.TerrainEdited += TownTerrainEdited;
            if (VistasWorldGen.Present) Log.Always("[Towns] Vistas compatibility: planned town foundations flattened before buildings; no access roads.");
            Scheduler.Sliced(this, "Build and connect", 700, BuildStep);
            // Behind a loading/generating screen this tick runs construction far beyond the shared frame budget; the predicate is one field read.
            Scheduler.EveryFrame(this, "Load construction", LoadBurst).Wants = LoadWanted;
            Scheduler.Interval(this, "Spawner state", .5f, Refresh, Phase.Update, true);
            Scheduler.Interval(this, "Town discovery", .25f, TownMarkers.Refresh, Phase.Update, true);
            Scheduler.Interval(this, "Gas stations", .25f, _gasStation.Tick, Phase.Update, true);
            foreach (var old in _resume)
            {
                if (old.Tile == null || old.Tile.mapMagic == null || old.Tile.mapMagic.graph == null) continue;
                var plan = old.Site.Plan;
                string key = ChunkKey(old.Tile.mapMagic.graph.random.Seed, old.Tile.coord.x, old.Tile.coord.z);
                if (key != plan.Key) continue;
                Registration registration;
                if (!_graphs.TryGetValue(old.Tile.mapMagic.graph, out registration)) registration = Register(old.Tile.mapMagic.graph);
                var resumed = NewSite(plan, old.Tile.mapMagic.transform, old.Tile.main.terrain, registration.Assets.Specs); resumed.Record = _records[key]; resumed.Record.Plan = resumed.Plan;
                TownPopulation.Budget(resumed, registration.Assets, _settings);
                _sites.Add(new Live { Tile = old.Tile, Registration = registration, Site = resumed });
            }
            _resume.Clear();
            Log.Always("[Towns] ready: " + _settings.Min.Value + "-" + _settings.Max.Value + " buildings, " + _settings.Chance.Value + "% per main chunk; ensure selected towns=" + _settings.EnsureTown.Value
                + "; preferred terrain " + _settings.Relief.Value + " m relief / " + _settings.Earthwork.Value + " m cut-fill.");
        }
        private static bool NativePoi(ObjectsOutput output)
        {
            foreach (var p in output.prefabs)
                if (p != null && (p.name.StartsWith("Building_", StringComparison.Ordinal) || p.name.StartsWith("Camp_", StringComparison.Ordinal)
                    || p.name.StartsWith("Cave_", StringComparison.Ordinal) || p.name.StartsWith("Wreck_", StringComparison.Ordinal))) return true;
            return false;
        }
        private Registration Register(Graph graph)
        {
            var registration = new Registration { Graph = graph, Assets = new TownAssets() };
            try
            {
                registration.Assets.Load(graph, _settings);
                var avoid = new List<IOutlet<TransitionsList>>();
                foreach (var gen in graph.generators)
                {
                    var output = gen as ObjectsOutput;
                    if (output != null)
                    {
                        IOutlet<object> source;
                        if (NativePoi(output) && graph.links.TryGetValue(output, out source)) avoid.Add((IOutlet<TransitionsList>)source);
                    }
                    if (gen.GetType().Name == "PoiLocations" && gen is IOutlet<Den.Tools.TransitionsList>) avoid.Add((IOutlet<Den.Tools.TransitionsList>)gen);
                }
                registration.Avoid = avoid.ToArray();
                _graphs.Add(graph, registration);
                Log.Always("[Towns] catalog registered: " + registration.Assets.Specs.Length + " building templates; native graph unchanged; towns and access roads follow native road capture.");
                return registration;
            }
            catch { Undo(registration); throw; }
        }
        private void Prepare(TerrainTile tile, TileData data)
        {
            if (!_running || tile == null || data == null || tile.mapMagic == null || tile.mapMagic.graph == null) return;
            TownPoiSettings.Capture(tile, data);
            if (_blocked) return;
            try
            {
                Registration registration;
                if (!_graphs.TryGetValue(tile.mapMagic.graph, out registration)) registration = Register(tile.mapMagic.graph);
                int seed = registration.Graph.random.Seed;
                string key = ChunkKey(seed, tile.coord.x, tile.coord.z);
                TownRecord record; _records.TryGetValue(key, out record);
                var input = _settings.Snapshot(key, seed, registration.Assets.Specs, record == null ? null : record.Plan);
                input.ExcludeStartingChunk = tile.coord.x == 0 && tile.coord.z == 0 && !_settings.PoiNearSpawn.Value;
                if (input.ExcludeStartingChunk && (input.Saved == null || !input.Saved.HasTown)) input.Chance = 0;

                // Draft and main must agree: draft only uses an existing saved/previously generated plan.
                // An unseen draft stays natural; it never invents a differently positioned town.
                if (data.isDraft && input.Saved == null) input.Chance = 0;
                if (Services.Poi == null) input.Chance = 0;
                if (Services.PoiNavSetup) input.Chance = 0;   // NPC POI pathfinding setup: no new towns while it runs
                if (input.Saved != null && input.Saved.HasTown)
                    foreach (var lot in input.Saved.Lots) if (!lot.IsParking && !lot.Skipped && !registration.Assets.Buildings.ContainsKey(lot.Prefab))
                        throw new InvalidOperationException("Saved town requires unavailable prefab " + lot.Prefab + "; restore its catalog before loading.");
                bool startingChunk = tile.coord.x == 0 && tile.coord.z == 0 && !_settings.PoiNearSpawn.Value;
                _captures[data] = new Capture { Registration = registration, Input = input, Epoch = _epoch, GasStation = _gasStation.Capture(tile, data, key, startingChunk, registration.Assets) };
            }
            catch (Exception e) { Log.Warn("[Towns] skipped terrain preparation: " + e.Message); }
        }
        private void Finalized(TerrainTile tile, TileData data, StopToken stop)
        {
            Capture capture;
            if (data == null || (stop != null && stop.stop) || !_captures.TryGetValue(data, out capture)) return;
            if (!_running || capture.Epoch != _epoch) return;
            // WW loads first: Patrols has already captured the completed native road splines.
            // Only now plan towns and access roads; nothing is linked back into the native graph.
            try
            {
                if (data.heights == null) throw new InvalidOperationException("Final native terrain heights are missing.");
                if (!data.isDraft && (capture.Input.Chance > 0 || capture.Input.Saved != null && capture.Input.Saved.HasTown))
                    capture.Input.Progress = TownLoading.Begin(capture.Input.Key);
                var avoid = new List<Vector3>();
                foreach (var source in capture.Registration.Avoid)
                {
                    var points = data.ReadOutletProduct(source);
                    if (points != null) for (int i = 0; i < points.count; i++) avoid.Add(points.arr[i].pos);
                }
                var assets = capture.Registration.Assets;
                var roads = new TownWorldRoads(assets.Roads == null ? null : data.ReadOutletProduct(assets.Roads), data);
                TownGasStation.Plan(capture.GasStation, data, avoid, assets, capture.Input.Saved, roads, stop);
                capture.Plan = TownPlanner.Make(capture.Input, data, data.heights, avoid, roads, stop);
                if (capture.Plan == null || !capture.Plan.HasTown || (stop != null && stop.stop))
                { if (capture.Input.Progress != null) capture.Input.Progress.Finish(capture.Plan == null ? "Cancelled" : capture.Plan.Decision == "chance" ? "No town selected for this block" : capture.Plan.Decision); return; }
                if (capture.Input.Progress != null) capture.Input.Progress.Report(.58f, "Shaping town ground and access track");
                TownLateTerrain.Apply(data, capture.Plan, assets, stop);
                if (!data.isDraft)
                {
                    // Building-map overlay (neighbour walls, wreck garden, towers, border props) from the read-only templates, off the main thread.
                    // A failure here only loses the shortcut: the runtime projection remains the fallback.
                    try { if (!VistasWorldGen.Present) capture.Plan.Overlay = TownOverlayPlan.Build(capture.Plan, assets.Specs, assets.WallKit.Dressing, assets.ShellBounds, assets.NavMaxStep, stop); }
                    catch (Exception e) { capture.Plan.Overlay = null; Log.Always("[Towns] WARNING: overlay plan skipped for " + capture.Plan.Key + ": " + e.Message); }
                    _planned.Enqueue(capture);
                }
            }
            catch (Exception e)
            { capture.Input.Error = "Late town generation: " + e.Message; if (capture.Input.Progress != null) capture.Input.Progress.Finish("Failed: " + e.Message); }
        }
        private void Applied(TerrainTile tile, TileData data, StopToken stop)
        {
            TraceStartingTerrain(tile, "applied");
            Capture capture;
            if (data != null) TownPoiSettings.Forget(data);
            if (data == null || !_captures.TryRemove(data, out capture)) return;
            if (!_running || _blocked || data.isDraft || tile == null || capture.Epoch != _epoch || (stop != null && stop.stop)) return;
            try { _gasStation.Applied(capture.GasStation, tile, capture.Registration.Assets, _dressing); }
            catch (Exception e) { Log.Warn("[Towns] gas station placement stopped: " + e.Message); }
            if (capture.Input.Error != null) { Log.Warn("[Towns] skipped generation: " + capture.Input.Error); return; }
            if (capture.Plan == null) return;
            try
            {
                Remove(tile, true);
                TownRecord record;
                if (!_records.TryGetValue(capture.Plan.Key, out record)) _records.Add(capture.Plan.Key, record = new TownRecord { Plan = capture.Plan });
                else if (TownSettings.RejectedDoor(record.Plan) || !record.Plan.HasTown && (capture.Plan.HasTown || capture.Plan.Decision != "chance")
                    || capture.Plan.PathsVersion > record.Plan.PathsVersion) record.Plan = capture.Plan;
                var plan = record.Plan;
                if (plan != capture.Plan && plan.Overlay == null && capture.Plan.Overlay != null && TownOverlayPlan.SameLayout(plan, capture.Plan)) plan.Overlay = capture.Plan.Overlay;
                _names.Assign(plan);
                Log.Always("[Towns] " + plan.Key + ": " + plan.Decision + (plan.HasTown ? "; " + plan.LotCount + " lots, " + plan.Condition + ", relief " + plan.OriginalRelief.ToString("0.00", CultureInfo.InvariantCulture) + " m, max cut/fill " + plan.MaximumEarthwork.ToString("0.00", CultureInfo.InvariantCulture) + " m at " + plan.WorldCenter : ""));
                if (!plan.HasTown)
                {
                    if (plan.Decision.StartsWith("town placement pending:", StringComparison.Ordinal))
                    {
                        Log.Warn("[Towns] unmet town request " + plan.Key + ": " + plan.Decision + "; retained for retry, no existing town moved.");

                    }
                    return;
                }
                if (plan.GradeAccess) Log.Always("[Towns] graded access corridor " + plan.Key + ": road approach retained.");

                var site = NewSite(plan, tile.mapMagic.transform, tile.main.terrain, capture.Registration.Assets.Specs);
                record.Plan = site.Plan;
                site.Record = record;
                TownPopulation.Budget(site, capture.Registration.Assets, _settings);
                site.Loading = capture.Input.Progress;
                if (site.Loading != null) site.Loading.Report(.7f, "Placing 0/" + plan.LotCount + " buildings");
                _sites.Add(new Live { Tile = tile, Registration = capture.Registration, Site = site });
            }
            catch (Exception e) { Log.Warn("[Towns] stopped applying town; existing objects retained: " + e.Message); }
        }
        private static string ChunkKey(int seed, int x, int z)
        { return "v1/" + seed.ToString(CultureInfo.InvariantCulture) + "/" + x.ToString(CultureInfo.InvariantCulture) + "/" + z.ToString(CultureInfo.InvariantCulture); }
        private static TownSite NewSite(TownPlan plan, Transform anchor, Terrain terrain, BuildingSpec[] specs)
        {
            var site = new TownSite { Plan = plan, Anchor = anchor, GroundTerrain = null, Buildings = new GameObject[plan.LotCount], Spawners = new PlayMakerFSM[plan.LotCount][] };
            if (VistasWorldGen.Present)
            {
                if (terrain == null) throw new InvalidOperationException("Town foundation terrain is missing.");
                site.Plan = plan = plan.CopyPaths(); plan.Overlay = null; plan.DressingOnly = null; plan.AccessPath = new Vector3[0]; plan.GradeAccess = false;
                var center = site.Scene(Vector3.zero); center.y = terrain.SampleHeight(center) + terrain.transform.position.y;
                plan.WorldCenter = anchor.InverseTransformPoint(center);
                foreach (var lot in plan.Lots)
                {
                    var p = lot.LocalPosition; p.y = -lot.Entry.y; lot.LocalPosition = p;
                }
                TownWalkGraph.Build(plan, specs, null);
                site.GroundPad = TownGroundPad.Prepare(site, terrain);
            }
            site.Approach = new TownApproach(plan);
            TownMarkers.Add(site); return site;
        }
        private void TownTerrainEdited(TerrainTile tile)
        {
            if (!_running || tile == null) return;
            foreach (var live in _sites) if (live.Tile == tile && live.Site.GroundPad != null) live.Site.GroundPad.Apply();
        }
        private bool Loading { get { return _waitingForTowns || (SaveBus.Busy && !SaveBus.Saving && !SaveSystem.Busy); } }
        private bool LoadWanted() { return _loading; }
        private void LoadBurst()
        {
            // Behind the loading screen nobody sees frame pacing: construction and connection run up to the configured
            // milliseconds per frame instead of the shared 2 ms scheduler budget. One prefab activation per frame still holds.
            float budget = _settings.LoadingBudgetMs.Value; if (budget <= 0 || !_loading) return;
            _burst.Reset(); _burst.Start();
            while (_burst.Elapsed.TotalMilliseconds < budget && BuildStep()) { }
        }
        private bool BuildStep()
        {
            _loading = Loading;
            if (!_running || _blocked || _sites.Count == 0 || SaveSystem.Busy || SaveBus.Saving) return false;
            if (_builtFrame == Time.frameCount || Time.unscaledTime < _buildAfter) return false;
            // Native prefab activation is indivisible; never stack two of those costs in one frame, and leave 40 ms between
            // them while someone is playing. Everything else (wall pieces, planned props, map overlays, link checks) is
            // microsecond work and runs inside the slice. Applied neighbouring main tiles are built immediately.
            float spacing = _loading ? 0 : .04f;
            var player = GameRefs.Player;
            if (player != null)
            {
                float best = float.MaxValue;
                for (int i = 0; i < _sites.Count; i++)
                {
                    var candidate = _sites[i]; if (candidate.Finished && !NeedsWalls(candidate) || candidate.Site.Anchor == null) continue;
                    float distance = (candidate.Site.Scene(Vector3.zero) - player.transform.position).sqrMagnitude;
                    if (distance < best) { best = distance; _next = i; }
                }
            }
            if (_next >= _sites.Count) _next = 0;
            for (int n = 0; n < _sites.Count; n++)
            {
                var live = _sites[_next++ % _sites.Count]; var site = live.Site;
                if (live.Finished && !NeedsWalls(live) || live.Tile == null || site.Anchor == null) continue;
                if (!WorldWork.Try(WorldWork.Towns)) return false;
                if (site.NextLot < site.Plan.LotCount)
                {
                    _builtFrame = Time.frameCount; _buildAfter = Time.unscaledTime + spacing;
                    try
                    {
                        if (_staging == null) { _staging = new GameObject("Towns inactive staging"); _staging.SetActive(false); }
                        if (site.Staged == null && TownFactory.NeedsStage(site, site.NextLot))
                        {
                            // Half one: deserialize the clone under the inactive root. Half two (next slot) activates it.
                            site.Staged = TownFactory.Stage(site, site.NextLot, live.Registration.Assets, _staging.transform);
                            _buildAfter = Time.unscaledTime + spacing * .5f;
                            return true;
                        }
                        var staged = site.Staged; site.Staged = null;
                        TownFactory.Building(site, site.NextLot, live.Registration.Assets, _settings, _records[site.Plan.Key], _staging.transform, live.Tile.objectsPool.transform, staged);
                        site.NextLot++; live.LotFailures = 0;
                    }
                    catch (Exception e)
                    {
                        if (site.Staged != null) { UnityEngine.Object.Destroy(site.Staged); site.Staged = null; }
                        Log.Warn("[Towns] lot " + site.Plan.Key + "/" + site.NextLot + " attempt " + (++live.LotFailures) + ": " + e.Message);
                        if (live.LotFailures >= 2)
                        {
                            try { TownFactory.KeepGeometry(site, site.NextLot, live.Tile.objectsPool.transform); }
                            catch (Exception keepError) { Log.Warn("[Towns] retained partial lot: " + keepError.Message); }
                            site.NextLot++; live.LotFailures = 0; site.PopulationCounts = site.LootCounts = null;
                        }
                    }
                    return true;
                }
                try
                {
                    if (NeedsWalls(live))
                    {
                        if (site.Walls == null)
                        {
                            site.Walls = new TownWalls(site, live.Registration.Assets, live.Tile.objectsPool.transform); live.Finished = false; site.PrepSteps = 0;
                            // Ownership confirmed after the first overlay: project the planned towers/props into the building maps too.
                            if (site.Overlap != null) site.Overlap.Reapply(true);
                        }
                        bool spawned;
                        site.Walls.Step(out spawned);
                        if (spawned) { _builtFrame = Time.frameCount; _buildAfter = Time.unscaledTime + spacing; }
                        if (site.Walls.Prepared) { site.Navigation = new TownNavigation(site); live.PreparedAt = Time.unscaledTime; site.PrepSteps = 0; }
                        return true;
                    }
                    if (site.Streets == null)
                    {
                        live.PreparedAt = Time.unscaledTime; site.PrepSteps = 0;
                        site.Streets = TownFactory.Streets(site, live.Registration.Assets, live.Tile.objectsPool.transform);
                        site.Overlap = new TownOverlap(site, site.Walls != null); site.Navigation = new TownNavigation(site);
                        return true;
                    }
                    if (!TownBakeBridge.Enabled) { Finish(live, "NPC navigation off; placed objects and individual maps retained"); return true; }
                    if (site.PrepSteps > PrepStepLimit || Time.unscaledTime - live.PreparedAt >= 60)
                    {
                        Log.Warn("[Towns] preparation budget reached " + site.Plan.Key + " after " + site.PrepSteps + " steps / " + (Time.unscaledTime - live.PreparedAt).ToString("0", CultureInfo.InvariantCulture)
                            + " s; overlay " + (site.Overlap != null && site.Overlap.Ready ? "ready" : "partial") + ", street routing " + (site.Navigation != null && site.Navigation.Ready ? "ready" : "incomplete") + ".");
                        Finish(live, "preparation budget reached; placed objects and individual maps retained"); return true;
                    }
                    // Overlay first: entrance portals and routes must read the projected maps, with or without walls.
                    if (site.Overlap != null && !site.Overlap.Ready)
                    { if (site.Overlap.Step()) { site.PrepSteps++; return true; } continue; }
                    if (site.Navigation != null && !site.Navigation.Ready)
                    {
                        if (site.Navigation.Step()) { site.PrepSteps++; return true; }
                        if (!site.Navigation.Ready && !site.Navigation.Failed) continue;
                    }
                    Finish(live, "compact town ready; no road painting or loading wait"); return true;
                }
                catch (Exception e)
                {
                    Log.Warn("[Towns] preparation " + site.Plan.Key + " attempt " + (++live.MapFailures) + ": " + e.Message);
                    if (live.MapFailures >= 2)
                    {
                        if (NeedsWalls(live)) { live.WallFailed = true; if (site.Walls != null) site.Walls.Dispose(); site.Walls = null; }
                        Finish(live, "connection attempts stopped; placed objects retained");
                    }
                    return true;
                }
            }
            return false;
        }
        private bool NeedsWalls(Live live)
        { return !live.WallFailed && _settings.Walls.Value && TownPerimeter.Allegiance(live.Site.Plan) && live.Registration.Assets.WallKit.Available
            && (live.Site.Walls == null || !live.Site.Walls.Prepared); }
        private static void Finish(Live live, string reason)
        {
            live.Finished = true; live.Site.NavigationStopped = true;
            if (live.Site.Overlap != null && !live.Site.Overlap.Ready)
                try { live.Site.Overlap.FinishPartial(); } catch (Exception e) { Log.Warn("[Towns] stopped partial overlap: " + e.Message); }
            if (live.Site.Loading != null) live.Site.Loading.Finish(reason);
        }
        private void Refresh()
        {
            if (!_running || _blocked || SaveSystem.Busy || SaveBus.Saving) return;
            Capture pending;
            for (int i = 0; i < 4 && _planned.TryDequeue(out pending); i++)
                if (pending.Epoch == _epoch && pending.Plan != null && pending.Plan.HasTown && !_records.ContainsKey(pending.Plan.Key))
                { _records.Add(pending.Plan.Key, new TownRecord { Plan = pending.Plan }); _names.Assign(pending.Plan); }
            for (int i = _sites.Count - 1; i >= 0; i--)
            {
                var live = _sites[i];
                if (live.Tile == null || live.Site.Anchor == null) { TownFactory.Destroy(live.Site); _sites.RemoveAt(i); continue; }
                if (GameRefs.InGame) TownFactory.Resume(live.Site);
                if (!live.Site.MerchantReady && live.Site.Plan.Owned && live.Site.Plan.Faction == TownFaction.Coyotes)
                {
                    for (int lot = 0; lot < live.Site.Plan.LotCount; lot++)
                        if (live.Site.Plan.Lots[lot].IsMerchant && live.Site.Spawners[lot] != null)
                            TownPopulation.Configure(live.Site, lot, live.Registration.Assets, _settings);
                    live.Site.MerchantReady = true;
                }
            }
        }
        private void PopulationChanged(object sender, EventArgs args)
        {
            if (!_running || _blocked) return;
            try
            {
                foreach (var live in _sites) { live.Site.PopulationCounts = live.Site.LootCounts = null; live.Site.PopulationReady = false; }
                foreach (var live in _sites) for (int i = 0; i < live.Site.Spawners.Length; i++)
                    if (!live.Site.Plan.Lots[i].IsParking && !live.Site.Plan.Lots[i].Skipped && live.Site.Spawners[i] != null)
                    { TownPopulation.Configure(live.Site, i, live.Registration.Assets, _settings); InteriorPopulation.OwnTown(live.Site, i); }
                Log.Always("[Towns] DEBUG ranges: population=" + _settings.PopulationMinPercent.Value + "-" + _settings.PopulationMaxPercent.Value + "%, loot=" + _settings.LootMinPercent.Value + "-" + _settings.LootMaxPercent.Value + "% of native totals per town (balanced pending spawns; existing objects retained).");
            }
            catch (Exception e) { Log.Warn("[Towns] population update stopped; existing actors/items retained: " + e.Message); }
        }
        private void Moved(TerrainTile tile)
        {
            if (_gasStation != null) _gasStation.Moved(tile);
            Remove(tile, true);
            string key = ChunkKey(tile.mapMagic.graph.random.Seed, tile.coord.x, tile.coord.z);
            if (tile.main != null) ForgetMoved(tile.main.data, key);
            if (tile.draft != null) ForgetMoved(tile.draft.data, key);
        }
        private void TraceStartingTerrain(TerrainTile applied, string source)
        {
            if (!VistasWorldGen.Present || applied == null || applied.mapMagic == null || Mathf.Abs(applied.coord.x) + Mathf.Abs(applied.coord.z) > 1) return;
            try
            {
                var map = applied.mapMagic; var start = map.tiles[new Coord(0, 0)];
                var terrain = start == null ? null : start.ActiveTerrain;
                if (terrain == null || terrain.terrainData == null) return;
                var data = terrain.terrainData; int res = data.holesResolution;
                string state = "seed=" + map.graph.random.Seed + "; active=" + (start.main != null && terrain == start.main.terrain ? "main" : "draft")
                    + "; size=" + data.size.ToString("F1") + "; heightRes=" + data.heightmapResolution + "; globalsHeight=" + map.globals.height
                    + "; terrainWrites=off; tests=" + _settings.TestGasStation.Value + "/" + _settings.TestCamp10.Value + "/" + Services.PoiNavSetup;
                int[] dx = { -1, 1, 0, 0 }, dz = { 0, 0, -1, 1 };
                for (int side = 0; side < 4; side++)
                {
                    var holes = data.GetHoles(side == 1 ? res - 1 : 0, side == 3 ? res - 1 : 0, side < 2 ? 1 : res, side < 2 ? res : 1);
                    int missing = 0; foreach (bool solid in holes) if (!solid) missing++;
                    state += "; edge" + side + " holes=" + missing + "/" + res;
                    var neighbour = map.tiles[new Coord(dx[side], dz[side])]; var other = neighbour == null ? null : neighbour.ActiveTerrain;
                    if (other == null || other.terrainData == null) { state += " neighbour=absent"; continue; }
                    var next = other.terrainData; float step = 0;
                    for (int s = 0; s <= 8; s++)
                    {
                        float u = side == 0 ? 0 : side == 1 ? 1 : s / 8f, v = side == 2 ? 0 : side == 3 ? 1 : s / 8f;
                        var p = terrain.transform.position + new Vector3(u * data.size.x, 0, v * data.size.z);
                        float a = data.GetInterpolatedHeight(u, v) + terrain.transform.position.y;
                        float b = next.GetInterpolatedHeight((p.x - other.transform.position.x) / next.size.x, (p.z - other.transform.position.z) / next.size.z) + other.transform.position.y;
                        step = Mathf.Max(step, Mathf.Abs(a - b));
                    }
                    state += " neighbourHeight=" + next.size.y.ToString("F1", CultureInfo.InvariantCulture) + " maxStep=" + step.ToString("F1", CultureInfo.InvariantCulture) + "m";
                }
                if (state == _startingTerrainState) return;
                _startingTerrainState = state; Log.Always("[Towns] Starting terrain (" + source + "): " + state);
            }
            catch (Exception e) { Log.Warn("[Towns] Starting terrain trace failed: " + e.Message); }
        }
        private void ForgetMoved(TileData data, string key)
        {
            if (data == null) return;
            Capture ignored;
            // Move calls Dist/Prepare before OnTileMoved: retain the new coordinate's capture.
            if (_captures.TryGetValue(data, out ignored) && ignored.Epoch == _epoch && ignored.Input.Key == key) return;
            TownPoiSettings.Forget(data);
            if (_captures.TryRemove(data, out ignored) && ignored.Input.Progress != null) ignored.Input.Progress.Finish("Cancelled — terrain streamed out");
        }
        private LoadingProgress.Work? LoadProgress()
        {
            if (!_running || _blocked) return null;
            float done = 0, total = 0; int ready = 0, count = 0; string stage = null;
            foreach (var live in _sites)
            {
                var site = live.Site; if (live.Tile == null || site.Anchor == null) continue;
                bool walls = NeedsWalls(live), finished = live.Finished && !walls, population = InteriorPopulation.TownReady(site);
                int lots = site.Plan.LotCount; count++; total += lots + 3;
                float border = site.NextLot < lots ? 0 : site.Walls != null ? site.Walls.Progress : !walls && population ? 1 : 0;
                done += Mathf.Min(site.NextLot, lots) + border + (!walls && population ? finished ? 1
                    : .5f * ((site.Overlap != null ? site.Overlap.Progress : 0) + (site.Navigation != null ? site.Navigation.Progress : 0)) : 0) + (finished && population ? 1 : 0);
                if (finished && population) { ready++; continue; }
                string current = site.NextLot < lots ? "Placing buildings " + site.NextLot + "/" + lots
                    : walls ? site.Walls != null ? site.Walls.Stage : "Building town walls"
                    : !finished ? "Connecting town paths" : "Preparing town residents";
                if (stage == null || stage == "Preparing town residents") stage = current;
            }
            if (count == ready) return null;
            float fraction = Mathf.Clamp01(done / Mathf.Max(1, total));
            // The shared screen reserves 70–98% of overall loading for module preparation.
            int remaining = Mathf.CeilToInt((.3f - .28f * fraction) * 100);
            return new LoadingProgress.Work(fraction, stage + " | " + ready + "/" + count + " towns ready | " + remaining + "% of load remaining");
        }
        private bool BeforeHandoff(HutongGames.PlayMaker.Actions.Wait action)
        {
            if (!_running || action.Fsm.Name != "SaveLoadGame" || action.State.Name != "Wait for Terrain Gen") return true;
            bool pending = false;
            foreach (var live in _sites) if (live.Tile != null && live.Site.Anchor != null && (!live.Finished || NeedsWalls(live) || !InteriorPopulation.TownReady(live.Site))) { pending = true; break; }
            if (pending && !_waitingForTowns) Log.Always("[Towns] Load handoff waiting: restoring town buildings and population before play.");
            if (!pending && _waitingForTowns) Log.Always("[Towns] Load handoff ready: town buildings and population restored before play.");
            _waitingForTowns = pending;
            return !pending;
        }
        private void Remove(TerrainTile tile, bool capture)
        {
            for (int i = _sites.Count - 1; i >= 0; i--) if (_sites[i].Tile == tile)
            {
                var live = _sites[i]; TownRecord record;
                if (capture && _records.TryGetValue(live.Site.Plan.Key, out record)) TownFactory.Capture(live.Site, record, live.Registration.Assets);
                if (live.Site.Loading != null) live.Site.Loading.Finish("Cancelled — terrain streamed out");
                TownFactory.Destroy(live.Site); _sites.RemoveAt(i);
            }
        }
        public bool SetRole(string key, int lot, TownRole role)
        {
            TownRecord record;
            if (!_records.TryGetValue(key, out record) || lot < 0 || lot >= record.Plan.LotCount || ((int)role & ~63) != 0) return false;
            record.Plan.Lots[lot].Role = role;
            Log.Always("[Towns] lot role " + key + "/" + lot + " = " + role + " (persists with next world save)"); return true;
        }
        internal void TravelStops(List<Patrols.TownStop> into)
        {
            foreach (var record in _records.Values)
            {
                var plan = record.Plan;
                // (WW) a Coyotes town always takes part (its merchant bus must be there), others once their faction holds it
                if (!plan.HasTown || plan.PathsVersion < 3) continue;
                if (plan.Faction != TownFaction.Coyotes && (!plan.Owned || plan.Condition != TownCondition.Inhabited)) continue;
                foreach (var lot in plan.Lots) if (lot.IsParking && !lot.Skipped)
                {
                    into.Add(new Patrols.TownStop { Id = plan.Key, Name = plan.Name, Faction = plan.Faction.ToString(),
                        Parking = plan.World(lot.LocalPosition), Heading = Quaternion.Euler(0, plan.Yaw + lot.Yaw, 0) * Vector3.forward,
                        Center = plan.WorldCenter, Radius = plan.Radius, MerchantSeeded = plan.MerchantSeeded, CanTravel = plan.AccessPath.Length > 1, Lane = false,
                        RoadApproach = plan.AccessPath.Length > 1 ? (Vector3?)plan.World(plan.AccessPath[plan.AccessPath.Length - 1]) : null }); break;
                }
            }
        }
        internal void MerchantSeeded(string key)
        { TownRecord record; if (_records.TryGetValue(key, out record)) record.Plan.MerchantSeeded = true; }
        internal int TravelRevision
        { get { int count = 0; foreach (var record in _records.Values) if (record.Plan.HasTown) count++; return unchecked(_epoch * 397 ^ count); } }
        internal bool PatrolBase(string id)
        {
            TownRecord record;
            if (id == null || !_records.TryGetValue(id, out record) || !record.Plan.HasTown || record.Plan.Condition != TownCondition.Inhabited) return false;
            foreach (var lot in record.Plan.Lots) if (lot.IsParking && !lot.Skipped) return true;
            return false;
        }
        internal int PatrolCount(Vector3 player, float range)
        {
            int count = 0;
            foreach (var record in _records.Values)
            {
                var plan = record.Plan; if (!plan.HasTown || plan.Condition != TownCondition.Inhabited) continue;
                foreach (var lot in plan.Lots) if (lot.IsParking && !lot.Skipped)
                { var d = plan.World(lot.LocalPosition) - player; d.y = 0; if (d.sqrMagnitude <= range * range) count++; break; }
            }
            return count;
        }
        internal void PatrolBases(List<Patrols.TrafficBase> into)
        {
            foreach (var record in _records.Values)
            {
                var plan = record.Plan;
                if (!plan.HasTown || plan.Condition != TownCondition.Inhabited) continue;
                foreach (var lot in plan.Lots) if (lot.IsParking && !lot.Skipped)
                { into.Add(new Patrols.TrafficBase { id = plan.Key, name = plan.Name, faction = plan.Faction.ToString(),
                    center = plan.World(lot.LocalPosition), apron = plan.World(lot.LocalPosition), gate = plan.World(lot.LocalPosition), size = new Vector3(24, 0, 40) }); break; }
            }
        }
        internal bool ParkingOutside(Vector3 world, out Vector3 parking)
        {
            parking = world;
            foreach (var record in _records.Values) if (record.Plan.HasTown)
            {
                var plan = record.Plan; var p = world - plan.WorldCenter; p.y = 0;
                if (p.sqrMagnitude > plan.Radius * plan.Radius) continue;
                var frame = TownPerimeter.Frame(plan);
                if (!frame.Contains(p)) continue;
                foreach (var lot in plan.Lots) if (lot.IsParking && !lot.Skipped) { parking = plan.World(lot.LocalPosition); return true; }
            }
            return false;
        }
        internal void TravelBlocks(List<Bounds> into)
        {
            foreach (var record in _records.Values) if (record.Plan.HasTown)
            { var plan = record.Plan; var frame = TownPerimeter.Frame(plan); frame.center += new Vector3(plan.WorldCenter.x, 0, plan.WorldCenter.z);
                var size = frame.size; size.y = 10000; frame.size = size; frame.Expand(new Vector3(28, 0, 28)); into.Add(frame); }
        }
        public bool SetCondition(string key, TownCondition condition)
        {
            TownRecord record;
            if (!_records.TryGetValue(key, out record) || !Enum.IsDefined(typeof(TownCondition), condition)) return false;
            record.Plan.Condition = condition;
            Log.Always("[Towns] condition " + key + " = " + condition + " (persists with next world save)"); return true;
        }
        internal bool Waypoint(GameObject owner, Vector3 position, Vector3 goal, out Vector3 waypoint, out bool approach)
        {
            waypoint = goal; approach = false;
            for (int i = 0; i < _sites.Count; i++)
            {
                var site = _sites[i].Site;
                if (site.Anchor == null || site.Approach == null) continue;
                var center = site.Anchor.TransformPoint(site.Plan.WorldCenter);
                float radius = site.Plan.Radius + site.Plan.Feather + 30f;
                float px = position.x - center.x, pz = position.z - center.z, gx = goal.x - center.x, gz = goal.z - center.z;
                if (px * px + pz * pz > radius * radius && gx * gx + gz * gz > radius * radius)
                {
                    float dx = gx - px, dz = gz - pz, t = Mathf.Clamp01(-(px * dx + pz * dz) / Mathf.Max(.0001f, dx * dx + dz * dz));
                    float x = px + dx * t, z = pz + dz * t; if (x * x + z * z > radius * radius) continue;
                }
                var local = site.Anchor.InverseTransformPoint(position) - site.Plan.WorldCenter;
                if (!site.Approach.Inside(local))
                {
                    var target = site.Anchor.InverseTransformPoint(goal) - site.Plan.WorldCenter;
                    if (!site.Approach.Inside(target))
                    {
                        Vector3 detour; if (site.Walls == null || !site.Approach.Around(owner, local, target, out detour)) continue;
                        waypoint = site.Ground(site.Anchor.TransformPoint(site.Plan.WorldCenter + detour));
                        approach = true; return true;
                    }
                    waypoint = site.Ground(site.Anchor.TransformPoint(site.Plan.WorldCenter + site.Approach.Entrance(local)));
                    approach = true; return true;
                }
                if (site.Navigation != null && site.Navigation.Ready && site.Navigation.Waypoint(position, goal, out waypoint)) return true;
                bool hold; Vector3 next;
                if (site.Walls != null && TownBakeBridge.Root(position) == null && site.Walls.Walkers.Waypoint(site.Anchor.InverseTransformPoint(position) - site.Plan.WorldCenter,
                    site.Anchor.InverseTransformPoint(goal) - site.Plan.WorldCenter, out next, out hold))
                { waypoint = site.Ground(site.Anchor.TransformPoint(site.Plan.WorldCenter + next)); return true; }
            }
            return false;
        }
        internal bool VehicleWaypoint(Vector3 position, Vector3 goal, out Vector3 waypoint, out bool hold)
        {
            Vector3 parking;
            bool redirected = ParkingOutside(Patrols.TrafficSpace.World(goal), out parking);
            if (redirected) goal = Patrols.TrafficSpace.Scene(parking);
            waypoint = goal; hold = false;
            foreach (var live in _sites)
            {
                var site = live.Site; if (site.Anchor == null || site.Walls == null) continue;
                Vector3 next;
                if (!site.Walls.Vehicles.Waypoint(site.Anchor.InverseTransformPoint(position) - site.Plan.WorldCenter,
                    site.Anchor.InverseTransformPoint(goal) - site.Plan.WorldCenter, out next, out hold)) continue;
                waypoint = site.Ground(site.Anchor.TransformPoint(site.Plan.WorldCenter + next)); return true;
            }
            return redirected;
        }
        public override void OnSaveBegin(string slot)
        {
            if (!_running || string.IsNullOrEmpty(slot)) return;
            if (_blocked) { SaveBus.ReportFailure("Towns is blocked; its world data was not saved. See [Towns] error."); return; }
            try
            {
                foreach (var live in _sites) TownFactory.Capture(live.Site, _records[live.Site.Plan.Key], live.Registration.Assets);
                try { _gasStation.Save(slot); } catch (Exception e) { Log.Warn("[Towns] gas station spawner states not saved: " + e.Message); }
                TownSaveRecord.Save(slot, _records);
                if (_dressing != null) _dressing.Markers.Save(slot);
            }
            catch (Exception e) { SaveBus.ReportFailure("Towns save failed: " + e.Message); Block("saving", e); }
        }
        private string SavePath(string slot) { return Path.Combine(Assets.ModuleConfigDir(Id), "Saves", Path.GetFileName(slot) + ".towns.bin"); }
        public override void OnLoadBegin(string slot)
        {
            ClearSites(); _resume.Clear(); _epoch++; _blocked = false;
            if (_gasStation != null) _gasStation.Load(slot);
            try { if (_dressing != null) _dressing.Markers.Load(slot); _records = string.IsNullOrEmpty(slot) ? new Dictionary<string, TownRecord>(StringComparer.Ordinal) : TownSaveRecord.Load(slot, SavePath(slot)); _names.Begin(_records); InteriorPopulation.DiscoverExisting(); }
            catch (Exception e) { _records.Clear(); Block("loading (saved data retained on disk)", e); }
        }
        public void NewWorld()
        { ClearSites(); if (_dressing != null) _dressing.Markers.Load(null); if (_gasStation != null) _gasStation.NewWorld(); _resume.Clear(); _epoch++; _blocked = false;  _records.Clear(); _names.Begin(_records); InteriorPopulation.DiscoverExisting(); }
        private void StateChanged(string previous, string current)
        { if (current == "setSeed" || current == "Start") NewWorld(); }
        public override void OnWorldReady() { Refresh(); }
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            ClearSites(); _resume.Clear(); _epoch++; _captures.Clear();

            // Keep graph registrations until shutdown: the native generator graph can survive scene changes.
        }
        private void ClearSites()
        { _startingTerrainState = null; if (_dressing != null) _dressing.Clear(); if (_gasStation != null) _gasStation.Clear(true); foreach (var live in _sites) TownFactory.Destroy(live.Site); _sites.Clear(); _captures.Clear();  TownPoiSettings.Clear(); InteriorPopulation.Clear(); _next = 0; _waitingForTowns = false; TownRoadBridge.Clear(); TownLoading.Clear("World changed"); }
        private void Block(string stage, Exception e)
        { _blocked = true; TownLoading.Failed(e.Message); Log.Error("[Towns] blocked during " + stage + "; no replacement layout will be invented", e); }
        private static void Undo(Registration registration)
        {
            if (registration.Assets != null) registration.Assets.Dispose();
        }
        public override void Shutdown()
        {
            if (!_running) return;
            _running = false; _epoch++;
            InteriorPopulation.ReleaseDeferred();
            LoadingProgress.Remove(this);
            Patrols.WorldTraffic.TownsStopped();
            SaveBus.StateChanged -= StateChanged;
            _settings.PopulationMinPercent.SettingChanged -= PopulationChanged; _settings.PopulationMaxPercent.SettingChanged -= PopulationChanged;
            _settings.LootMinPercent.SettingChanged -= PopulationChanged; _settings.LootMaxPercent.SettingChanged -= PopulationChanged;
            TerrainTile.OnBeforeTilePrepare -= Prepare; TerrainTile.OnTileFinalized -= Finalized;
            TerrainTile.OnTileApplied -= Applied; TerrainTile.OnTileMoved -= Moved;
            VistasWorldGen.TerrainEdited -= TownTerrainEdited;
            _resume.Clear();
            foreach (var live in _sites)
            {
                TownRecord record;
                if (_records.TryGetValue(live.Site.Plan.Key, out record)) TownFactory.Capture(live.Site, record, live.Registration.Assets);
                _resume.Add(live);
            }
            ClearSites(); _captures.Clear();
            if (_dressing != null) _dressing.Dispose(); _dressing = null; _dresser = null;

            foreach (var r in _graphs.Values) Undo(r); _graphs.Clear();
            if (_staging != null) UnityEngine.Object.Destroy(_staging); _staging = null;
            Scheduler.RemoveAll(this); Hotkeys.RemoveAll(this); Hooks.RemoveAll(this); TownBakeBridge.Uninstall(this);
            if (ReferenceEquals(TownsApi.Provider, this)) TownsApi.Provider = null;
        }
        public override IEnumerable<ToolPage> ToolPages()
        { return new ToolPage[] { new ActionPage(this, ToolGroups.Debug, "Gas stations", DrawGasStation) { DebugOnly = true, IsTool = true },
            new ActionPage(this, ToolGroups.Debug, "POI dresser", DrawDresser) { DebugOnly = true, IsTool = true } }; }
        private void DrawDresser(ToolLayout l)
        { if (_dresser != null) _dresser.DrawPage(l, _settings); else l.Entry(_settings.PoiDresserKey); }
        private void DrawGasStation(ToolLayout l)
        {
            if (_gasStation != null) _gasStation.Draw(l);
            else { l.Entry(_settings.DesertGasStationChance); l.Entry(_settings.TestGasStation); l.Entry(_settings.GasStationScale); }
        }
        public override void OnOriginShift(Vector3 delta) { if (_gasStation != null) _gasStation.Shift(delta); if (_dresser != null) _dresser.Shift(delta); }
        internal void Draw(ToolLayout layout)
        {
            layout.Value("Towns", _sites.Count.ToString()); layout.Value("Saved chunk decisions", _records.Count.ToString());
            layout.Value("Generation", _blocked ? "BLOCKED - see [Towns] log" : "ready");
            foreach (var live in _sites) layout.Value(live.Site.Plan.Key,
                live.Site.Plan.Condition + ", " + live.Site.NextLot + "/" + live.Site.Plan.LotCount + " lots, " + (live.Site.Navigation != null && live.Site.Navigation.Ready ? "connected" : "waiting for bakes"));
        }
    }
}
