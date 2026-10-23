using System.Globalization;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Survival
{
    /// <summary>
    /// Survival part of the Weather entry (Wave A split, owner B14): up to four storm regions (manual previews and natural
    /// encounters), their per-frame movement/presentation tick, the hurricane hazard tick, column layout and asset preparation.
    /// </summary>
    internal sealed partial class WeatherRuntime
    {
        /// <summary>Spawns a manual storm preview ahead of or around the player (not saved); up to four regions.</summary>
        private void Spawn(bool hurricane, bool here)
        {
            if (!_running || !_prepared || !GameRefs.InPlay || GameRefs.Player == null || GameRefs.MainCamera == null) return;
            if (_count == _regions.Length) { Log.Warn(null,"[Weather] Four previews are already active; clear them before spawning more."); return; }
            if (_visuals == null) _visuals = new WeatherVisuals(_audio);
            if (!_visuals.Ready) { Log.Warn(null,"[Weather] Preview assets unavailable. See the earlier Weather asset error."); return; }
            _audio.Load();
            Vector3 forward = GameRefs.MainCamera.transform.forward; forward.y = 0;
            if (forward.sqrMagnitude < .01f) forward = GameRefs.Player.transform.forward;
            forward.y = 0; forward.Normalize();
            Vector3 center = GameRefs.Player.transform.position; center.y -= 1.5f;
            UpdateLayout();
            if (!here) center += forward * (_depth.Value * 550 + 5000f);
            var anchor = _native.Preview(center,Quaternion.LookRotation(-forward));
            if (anchor == null) return;
            _regions[_count] = new WeatherRegion
            {
                WorldCenter = Origin.World(center), Radii = new Vector2(_width.Value*500, _depth.Value*500), Velocity = -forward * 20f,
                Heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, Hurricane = hurricane
            };
            _anchors[_count] = anchor; anchor.Selected = true;
            _criticalLogged[_count] = false; _ages[_count++] = 0; _draw.Enabled = true;
            Log.Always(null,"[Weather] Spawned " + (hurricane ? "critical" : "ordinary") + " rounded-front storm; movement 20 m/s; critical hurricane walls use native lifting/damage with safe eyes. Manual storms are not saved.");
        }

        /// <summary>Turns nearby native storm encounters into WW critical storm regions (natural storms keep native save bookkeeping); drops regions whose anchor vanished.</summary>
        internal void ReconcileNative()
        {
            if (!_running || _native == null || GameRefs.Player == null) return;
            for (int i=_count-1;i>=0;i--)
                if (_anchors[i]!=null && _anchors[i].Root==null) RemoveRegion(i);
            Vector3 player=GameRefs.Player.transform.position;
            for (int n=0;n<_native.Storms.Length;n++)
            {
                var storm=_native.Storms[n];
                if (storm.Root==null || !storm.Ready || storm.Owned || storm.Selected || !storm.Root.activeInHierarchy) continue;
                if (!_native.PrepareEncounter(storm)) continue;
                float distance=(storm.Root.transform.position-player).sqrMagnitude;
                if (distance>Mathf.Pow(_width.Value*500+8000,2)) continue;
                if (_count==_regions.Length)
                {
                    int farthest=-1; float far=distance;
                    for (int i=0;i<_count;i++)
                    {
                        if (_anchors[i]==null || _anchors[i].Owned) continue;
                        float d=(Origin.Scene(_regions[i].WorldCenter)-player).sqrMagnitude;
                        if (d>far) { far=d; farthest=i; }
                    }
                    if (farthest<0) continue;
                    RemoveRegion(farthest);
                }
                if (_visuals==null) _visuals=new WeatherVisuals(_audio);
                if (!_visuals.Ready) return;
                _audio.Load(); UpdateLayout();
                Vector3 forward=storm.Body!=null && storm.Body.velocity.sqrMagnitude>1 ? -storm.Body.velocity : -storm.Root.transform.forward;
                forward.y=0; forward.Normalize();
                _regions[_count]=new WeatherRegion { WorldCenter=Origin.World(storm.Root.transform.position),
                    Radii=new Vector2(_width.Value*500,_depth.Value*500), Heading=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg,
                    Velocity=storm.Body!=null ? storm.Body.velocity : -forward*25, Hurricane=true };
                _anchors[_count]=storm; _ages[_count]=storm.Age; _criticalLogged[_count]=true;
                storm.Selected=true; _count++; _draw.Enabled=true;
                if (_visuals!=null) _visuals.ClearGround();
                Log.Always(null,"[Weather] Natural encounter now drives a WW critical storm: "+storm.Root.name+"; native movement/save list retained.");
            }
        }
        /// <summary>Removes region index (swap with the last) and releases its native anchor.</summary>
        private void RemoveRegion(int index)
        {
            if (_native!=null) _native.Remove(_anchors[index]);
            _count--; _regions[index]=_regions[_count]; _ages[index]=_ages[_count];
            _criticalLogged[index]=_criticalLogged[_count]; _anchors[index]=_anchors[_count]; _anchors[_count]=null;
            if (_visuals!=null) _visuals.ClearGround();
        }
        /// <summary>FixedUpdate job: applies hurricane-wall hazards for every critical region between 30 s and 580 s of age.</summary>
        private void TickHazards()
        {
            if (!_running || _native==null || _native.Saving || !GameRefs.InGame) return;
            for (int i=0;i<_count;i++)
            {
                var anchor=_anchors[i];
                if (anchor==null || anchor.Root==null || !anchor.Root.activeInHierarchy || _ages[i]<30 || _ages[i]>580) continue;
                _hazards.Tick(_regions[i],i,anchor,_columns,_columnCount,_visuals,_native);
            }
        }

        /// <summary>Recomputes hurricane column positions when density/width/depth changed and applies the new radii to every region.</summary>
        private void UpdateLayout()
        {
            if (_lastDensity == _density.Value && _lastWidth == _width.Value && _lastDepth == _depth.Value) return;
            _lastDensity = _density.Value; _lastWidth = _width.Value; _lastDepth = _depth.Value;
            if (_visuals != null) _visuals.ClearGround();
            var radii = new Vector2(_lastWidth*500,_lastDepth*500);
            float area = new WeatherRegion { Radii = radii }.AreaSquareKm;
            int requested = Mathf.Clamp(Mathf.RoundToInt(area*_lastDensity),0,_columns.Length);
            _columnCount = 0;
            float occupied = 0, available = radii.x*1.7f;
            for (int i = 0; i < requested; i++)
            {
                float radius = WeatherVortexSmoke.TopRadius*WeatherVortexSmoke.TopWidth(i), gap = i == 0 ? 0 : 350;
                if (occupied+gap+radius*2 > available) break;
                _columns[_columnCount++] = new Vector3(occupied+gap+radius,0,Mathf.Sin(i*2.4f)*radii.y*.16f);
                occupied += gap+radius*2;
            }
            for (int i = 0; i < _columnCount; i++) _columns[i].x -= occupied*.5f;
            for (int i = 0; i < _count; i++) { var region = _regions[i]; region.Radii = radii; _regions[i] = region; }
            Log.Always(null,"[Weather] Front layout: " + _lastWidth.ToString("0.#",CultureInfo.InvariantCulture) + " x " + _lastDepth.ToString("0.#",CultureInfo.InvariantCulture) + " km; " + area.ToString("0.##",CultureInfo.InvariantCulture) + " km²; " + _columnCount + " hurricanes placed of " + requested + " requested at " + _lastDensity.ToString("0.##",CultureInfo.InvariantCulture) + "/km²; varied widths, minimum top gap 350 m (critical phase only).");
        }

        /// <summary>LateUpdateLast job (enabled while regions exist): moves regions, ages them, drives audio and draws the storm visuals.</summary>
        private void Tick()
        {
            if (!_running || _count == 0) return;
            UpdateLayout();
            float dt = GameRefs.InGame ? Time.deltaTime : 0;
            _clock += dt;
            for (int i = _count - 1; i >= 0; i--)
            {
                var anchor = _anchors[i];
                if (anchor != null && anchor.Root == null) { RemoveRegion(i); continue; }
                var region = _regions[i];
                if (anchor != null && !anchor.Owned)
                {
                    region.WorldCenter = Origin.World(anchor.Root.transform.position);
                    if (anchor.Body != null) region.Velocity = anchor.Body.velocity;
                    Vector3 motion = region.Velocity; motion.y = 0;
                    if (motion.sqrMagnitude > 1)
                    {
                        float heading = Mathf.Atan2(-motion.x,-motion.z)*Mathf.Rad2Deg;
                        if (Mathf.Abs(Mathf.DeltaAngle(region.Heading,heading)) > .1f && _visuals != null) _visuals.ClearGround();
                        region.Heading = heading;
                    }
                }
                else
                {
                    region.WorldCenter += region.Velocity * dt;
                    if (anchor != null) anchor.Root.transform.position = Origin.Scene(region.WorldCenter);
                }
                _regions[i] = region; _ages[i] += dt;
                if (anchor != null) anchor.Age = _ages[i];
                if (region.Hurricane && _ages[i] >= 20 && !_criticalLogged[i])
                {
                    _criticalLogged[i] = true;
                    Log.Always(null,"[Weather] Preview entering critical phase: same-color columns forming inside the dust wall over 20 seconds.");
                }
                if (anchor != null && !anchor.Owned) { _ages[i] = anchor.Age = 40; continue; }
                if (_ages[i] >= 600) RemoveRegion(i);
            }
            if (_count == 0) { Clear(); return; }
            _audio.Tick(_regions,_ages,_count,GameRefs.MainCamera,_clock);
            if (_visuals == null) return;
            if (!GameRefs.InPlay) { _visuals.Suspend(); return; }
            var camera = GameRefs.MainCamera;
            if (camera == null || !camera.isActiveAndEnabled) { _visuals.Suspend(); return; }
            var policy = ModApi.Get<IWeatherVisualPolicy>();
            var detail = policy != null ? policy.WeatherDetail : WeatherVisualDetail.Balanced;
            _visuals.Begin(_clock, camera);
            for (int i = 0; i < _count; i++)
            {
                float life = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_ages[i] / 6f)) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((600 - _ages[i]) / 20f));
                float critical = _regions[i].Hurricane ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((_ages[i] - 20) / 20f)) : 0;
                _visuals.Draw(_regions[i], camera, detail, life, critical, _columns, _columnCount, i);
            }
            _visuals.End(camera, detail);
        }
        /// <summary>Ends existing weather after the native sleep time skip.</summary>
        internal void SleepFinished() { Clear(); }
        /// <summary>Removes every region and anchor, resets hazards/audio/visuals and disables the draw job.</summary>
        private void Clear()
        {
            bool hadPreviews = _count > 0;
            for (int i=0;i<_count;i++) { if (_native!=null) _native.Remove(_anchors[i]); _anchors[i]=null; }
            if (_hazards!=null) _hazards.Reset();
            _count = 0; _clock = 0;
            if (_audio != null) _audio.Stop();
            if (_visuals != null) { _visuals.Suspend(); _visuals.ClearGround(); }
            if (_draw != null) _draw.Enabled = false;
            if (hadPreviews) Log.Always(null,"[Weather] Cleared visual previews.");
        }
        /// <summary>Loads audio, lays out columns and prepares the storm visuals and native dust/debris pools.</summary>
        private void PrepareAssets()
        {
            if (_visuals==null) _visuals=new WeatherVisuals(_audio);
            if (!_visuals.Ready) throw new System.InvalidOperationException("Weather assets are unavailable; native storm behavior is retained.");
            _audio.Load(); UpdateLayout();
            var policy=ModApi.Get<IWeatherVisualPolicy>();
            _visuals.Prepare(new Vector2(_width.Value*500,_depth.Value*500),_columnCount,policy!=null ? policy.WeatherDetail : WeatherVisualDetail.Balanced);
            PrepareNativeAssets();
        }
        /// <summary>Interval job: prepares the native dust/debris pools once they can be built (retained across loads).</summary>
        private void PrepareNativeAssets()
        {
            if (!_nativeAssetsReady && _visuals!=null && _visuals.PrepareNative())
            { _nativeAssetsReady=true; Log.Always(null,"[Weather] Native dust/debris pools ready before encounters; retained across loads."); }
        }
    }
}
