        // TownsModule.BuildStep before the 2026-10-20 performance pass (Init registered only the 700 us "Build and connect" slice).
        private bool BuildStep()
        {
            if (!_running || _blocked || _sites.Count == 0 || SaveSystem.Busy || SaveBus.Saving) return false;
            if (_builtFrame == Time.frameCount || Time.unscaledTime < _buildAfter) return false;
            // Native prefab instantiation is indivisible; never stack two of those costs in one frame.
            // Applied neighbouring main tiles are built immediately, before the player approaches them.
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
                if (site.NextLot < site.Plan.LotCount)
                {
                    _builtFrame = Time.frameCount; _buildAfter = Time.unscaledTime + .04f;
                    try
                    {
                        if (_staging == null) { _staging = new GameObject("Towns inactive staging"); _staging.SetActive(false); }
                        TownFactory.Building(site, site.NextLot, live.Registration.Assets, _settings, _records[site.Plan.Key], _staging.transform, live.Tile.objectsPool.transform);
                        site.NextLot++; live.LotFailures = 0;
                    }
                    catch (Exception e)
                    {
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
                        if (site.Walls == null) { site.Walls = new TownWalls(site, live.Registration.Assets, live.Tile.objectsPool.transform); live.Finished = false; }
                        if (!site.Walls.Ready) { _builtFrame = Time.frameCount; _buildAfter = Time.unscaledTime + .04f; }
                        site.Walls.Step();
                        if (site.Walls.Prepared) { site.Navigation = new TownNavigation(site); live.PreparedAt = Time.unscaledTime; }
                        return true;
                    }
                    if (site.Streets == null)
                    {
                        live.PreparedAt = Time.unscaledTime;
                        site.Streets = TownFactory.Streets(site, live.Registration.Assets, live.Tile.objectsPool.transform);
                        site.Overlap = new TownOverlap(site); site.Navigation = new TownNavigation(site);
                        return true;
                    }
                    if (!TownBakeBridge.Enabled || Time.unscaledTime - live.PreparedAt >= 8)
                    { Finish(live, "preparation budget reached; placed objects and individual maps retained"); return true; }
                    if (site.Walls != null && site.Navigation != null && !site.Navigation.Ready && site.Navigation.Step()) return true;
                    if (site.Overlap != null && !site.Overlap.Ready)
                    { if (site.Overlap.Step()) return true; continue; }
                    if (site.Navigation != null && !site.Navigation.Ready)
                    {
                        if (site.Navigation.Step()) return true;
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
