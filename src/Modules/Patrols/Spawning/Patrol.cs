using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ES3Internal;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // Runner on the hidden plugin object: the car builder (template spawner + convoy), the in-game check, the persistence tick.
    internal partial class Patrol : MonoBehaviour
    {
        internal static void ResetForScene() { BuildQueue.Reset(); VehiclePreparation.Reset(); PatrolLoadPreparation.Reset(); Prefabs.Invalidate(); Register.Invalidate(); DayLight.Reset(); _lightState = -1; SceneGen++; WorldTraffic.ResetForScene(); }

        // bumped on every scene load: a build or a convoy that waited through a load does not continue in the new world
        internal static int SceneGen;

        // WW: was Update (registered as the module's first Update job)
        internal void Tick()
        {
            if (!InGame()) return;
            Ram.Tick();
            ExitSpeed.Tick();
            if (Time.unscaledTime >= _nextLights) { _nextLights = Time.unscaledTime + 5f; LightsTick(); }
            if (Time.unscaledTime >= _nextPrune) { _nextPrune = Time.unscaledTime + 30f; PruneIgnoreSignatures(); }
        }

        // Raider cars drive with their headlights on only while it is dark (DayLight): at dusk / dawn every car with a live crew that the
        // player never took is switched over. New builds and restored cars pick the current state themselves.
        private static float _nextLights;
        private static int _lightState = -1;   // -1 = not read yet in this scene, 0 = day, 1 = dark
        private static void LightsTick()
        {
            int state = DayLight.Dark ? 1 : 0;
            if (state == _lightState) return;
            bool first = _lightState < 0;
            _lightState = state;
            if (first) return;   // the baseline: cars already got the right lights when they were built / restored
            int n = 0;
            foreach (var m in PatrolMarker.All.ToArray())
            {
                if (m == null || m.Exploded || m.PlayerEntered || Register.Pending(m.gameObject)) continue;
                var crew = m.GetComponent<Crew>();
                if (crew == null || !crew.CrewAlive) continue;
                Headlights(m.gameObject, state == 1);
                n++;
            }
            PatrolsModule.Verbose("Lights: it is " + (state == 1 ? "dark" : "light") + " now, " + n + " raider car(s) switched");
        }

        // The game is saving or loading (SaveLoadGame anywhere but isPlay). Builds wait for it: nothing is created or added to the
        // game's save lists while the save walks them.
        internal bool SaveBusy()
        {
            var sl = Core.GameRefs.SaveLoadGame;
            return Alive(sl) && sl.Fsm.Initialized && sl.ActiveStateName != "isPlay";
        }

        // Waits while a save runs. False = give the build up: a load ran (or the scene changed) meanwhile, or 60 s passed.
        private IEnumerator WaitForSave(int gen, bool[] ok)
        {
            ok[0] = true;
            if (!SaveBusy()) yield break;
            float until = Time.realtimeSinceStartup + 60f;
            PatrolsModule.Verbose("Build: waiting for the save to finish");
            while (SaveBusy())
            {
                string st = Core.GameRefs.SaveLoadState ?? "";
                if (SceneGen != gen || st.IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0 || st.IndexOf("Seed", StringComparison.OrdinalIgnoreCase) >= 0
                    || st.IndexOf("Terrain", StringComparison.OrdinalIgnoreCase) >= 0 || Time.realtimeSinceStartup > until) { ok[0] = false; yield break; }
                yield return null;
            }
            if (SceneGen != gen) ok[0] = false;
        }

        // occupants of destroyed cars (the game deletes cars beyond 5 km, the cleanup removes more) leave their signature behind
        private static float _nextPrune;
        private static readonly List<GameObject> _deadKeys = new List<GameObject>();
        private static void PruneIgnoreSignatures()
        {
            _deadKeys.Clear();
            foreach (var k in _ignoreSignature.Keys) if (k == null) _deadKeys.Add(k);
            foreach (var k in _deadKeys) _ignoreSignature.Remove(k);
        }

        // [Debug] AiOverlay: one OnGUI for every pilot (an OnGUI on each car would be dispatched per IMGUI event whether or not it draws)
        // WW: was OnGUI (Gui job); GuiWanted = its gate (the Gui job's predicate)
        /// <summary>Copies the existing pilot overlay lines; Debug owns their GUI rendering and visibility setting.</summary>
        internal static void CollectDiagnosticLines(List<string> into)
        {
            var all = Pilot.All; int shown = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var pilot = all[i];
                into.Add(pilot != null ? pilot.OverlayLine() : null);
                if (pilot != null) shown++;
            }
            if (shown == 0) { into.Clear(); into.Add(Convoy.OverlayLine()); }
        }

        // builds a template (from the F8 menu); one at a time
        internal void Spawn(CarTemplate t)
        {
            if (!InGame()) return;
            BuildQueue.Add(this, t, null, Quaternion.identity, true, false, null);
        }

        // Builds a template at a given place (convoy spawner). hold = the crew waits for Crew.Release() before driving off;
        // onDone receives the car (or null) when the build has finished.
        internal void SpawnAt(CarTemplate t, Vector3 pos, Quaternion rot, bool hold, Action<GameObject> onDone)
        {
            if (!InGame()) { if (onDone != null) onDone(null); return; }
            BuildQueue.Add(this, t, pos, rot, false, hold, onDone);
        }
        internal void CancelBuild(bool menu, Action<GameObject> done) { if (done != null) done(null); }

        // WW: Core.GameRefs.InGame (Menu "play" && SaveLoadGame "isPlay" && timeScale > 0, evaluated once per frame, Fsm.Initialized checked)
        internal bool InGame() { return Core.GameRefs.InGame; }

        // =============================================================== build

        // at == null: SpawnDistance in front of the player, facing the player's way; otherwise exactly there.
        // The work is spread over several frames (one Instantiate group per frame) so a build never stalls a single frame for long.
        internal IEnumerator Build(CarTemplate tpl, Vector3? at, Quaternion rot, bool menu, bool hold, Action<GameObject> onDone)
        {
            GameObject car = null;
            GameObject trailer = null;
            int gen = SceneGen;
            var trafficSpawn = at.HasValue ? TrafficSpace.World(at.Value) : Vector3.zero;
            var saveOk = new bool[1];
            string eventId=string.IsNullOrEmpty(tpl.TrafficId)?(string.IsNullOrEmpty(tpl.SpawnEvent)?Guid.NewGuid().ToString("N"):tpl.SpawnEvent):tpl.TrafficId;
            bool reserved=false;
            try
            {
                yield return WaitForSave(gen, saveOk);   // the game is saving: spawn after it, never half-built into the save
                if(!CrewFactions.Compatible(tpl,tpl.Faction)){Core.Log.Warn("[Patrols] " + "Template "+tpl.Name+" has crew from different factions; correct its driver, passenger or turret choices");yield break;}
                if (!saveOk[0]) { PatrolsModule.Verbose("Build of " + tpl.Name + " dropped: the game loaded or changed scene meanwhile"); yield break; }
                if(!menu&&tpl.Origin!="POI"&&!EventBudget.Reserve(eventId))yield break;
                reserved=!menu&&tpl.Origin!="POI";
                string missing = tpl.MissingPrefab;
                if (missing.Length > 0) { Core.Log.Warn("[Patrols] " + "Template " + tpl.Name + " unavailable: prefab not loaded: " + missing); yield break; }
                Vector3 pos;
                if (at.HasValue) pos = string.IsNullOrEmpty(tpl.TrafficId) ? at.Value : TrafficSpace.Scene(trafficSpawn);
                else
                {
                    Vector3 p, fwd;
                    if (!PlayerPose(out p, out fwd)) { Core.Log.Warn("[Patrols] " + "No player found"); yield break; }
                    pos = p + fwd * PatrolsModule.SpawnDistance + Vector3.up * 1.0f;
                    rot = Quaternion.LookRotation(fwd);
                }
                if (Core.Log.Verbose) PatrolsModule.Verbose("Building template " + tpl.Name + ": " + tpl.Describe());

                var body = Prefabs.Exact(tpl.Body);
                if (body == null) { Core.Log.Warn("[Patrols] " + "Body prefab not found: " + tpl.Body); yield break; }
                GameObject trailerBody = null;
                if (tpl.Trailer != null)
                {
                    trailerBody = Prefabs.Exact(tpl.Trailer.Body);
                    if (trailerBody == null || !TrailerSupport.IsTrailer(trailerBody) || FindChild(body.transform, "hitch") == null || FindChild(trailerBody.transform, "attacher") == null)
                    { Core.Log.Warn("[Patrols] " + "Cannot build " + tpl.Name + ": trailer prefab or towing hitch is missing"); yield break; }
                    if (WarRig.WorldSpawning.IsTemplate(trailerBody))
                    {
                        var hitchOffset = body.transform.InverseTransformPoint(FindChild(body.transform, "hitch").position);
                        var attacherOffset = trailerBody.transform.InverseTransformPoint(FindChild(trailerBody.transform, "attacher").position);
                        var trailerPos = pos + rot * (hitchOffset - attacherOffset);
                        float trailerY = trailerPos.y;
                        if (tpl.Origin != "POI" && !WarRig.WorldSpawning.CanSpawnAt(ref trailerPos, rot))
                        { Core.Log.Info("[Patrols] War Rig patrol spawn rejected: blocked clearance or unsuitable ground."); yield break; }
                        pos.y += trailerPos.y - trailerY;
                    }
                }

                var packed = VehiclePreparation.Require(tpl, tpl.RollBumper(), false);
                var packedTrailer = tpl.Trailer != null ? VehiclePreparation.Require(tpl.Trailer, "", true) : null;
                while (!packed.Ready || (packedTrailer != null && !packedTrailer.Ready))
                {
                    if (gen != SceneGen || packed.Failed || (packedTrailer != null && packedTrailer.Failed)) yield break;
                    yield return null;
                }
                yield return WaitForSave(gen, saveOk);
                if (!saveOk[0]) yield break;
                // An origin jump can occur while a custom build warms.
                if (at.HasValue) pos = TrafficSpace.Scene(trafficSpawn);
                car = VehiclePreparation.Spawn(packed, pos, rot);
                if(!menu&&tpl.Origin!="POI")EncounterCar.Attach(car,eventId,tpl.ConvoyMember,true);
                car.SetActive(true);
                Paint.Apply(car, body.name,tpl.Faction);
                // The car, its parts and its cargo go into the game's save lists only once the car is complete (Register.Commit below):
                // a save during the build (autosave, the player) does not store a half-built car
                // Spawn has already named/deferred the packed frame and each fitted part before activation.
                PatrolsModule.Verbose("Car frame " + car.name + " at " + pos);

                yield return null;   // let the frame's FSMs start (hinges, getEngine, START ...)
                if (car == null) yield break;

                if (tpl.Trailer != null)
                {
                    var hitch = FindChild(car.transform, "hitch");
                    var attacher = FindChild(trailerBody.transform, "attacher");
                    var trailerPos = hitch.position - car.transform.rotation * trailerBody.transform.InverseTransformPoint(attacher.position);
                    trailer = VehiclePreparation.Spawn(packedTrailer, trailerPos, car.transform.rotation);
                    TrailerSupport.Connect(car, trailer); // before the first frame: native TrailerLoad must not add a second joint
                    yield return null;
                    if (car == null || trailer == null) yield break;
                }

                if (tpl.FillFuel)
                {
                    if (tpl.FuelLitres >= 0)
                    {
                        var f = FindFsm(car, "Fuel", "LiquidAmount");
                        var liquid = f != null ? f.FsmVariables.GetFsmFloat("Liquid") : null;
                        if (liquid != null) liquid.Value = tpl.FuelLitres;
                    }
                    else Fuel(car, PatrolsModule.RollPartFill());
                }
                if (tpl.ReleaseHandbrake) Handbrake(car, false);
                List<GameObject> cargo = null;
                List<GameObject> trailerCargo = null;
                string lootKey = null;
                // a truck: its fixed cargo option (template "E" in the editor) when it has one, else a roll among the convoy's allowed cargo
                var loot = tpl.IsTruck ? (tpl.SpawnLoot == null ? (EditorStore.Loot(true, tpl.LootPreset) ?? EditorStore.RollTruckLoot(() => UnityEngine.Random.value, tpl.SpawnCargoOptions)) : null) : EditorStore.Loot(false, tpl.LootPreset);
                var rolled = tpl.SpawnLoot ?? (loot != null ? loot.Roll(() => UnityEngine.Random.value) : new Dictionary<string, int>());
                lootKey = tpl.SpawnCargoKey ?? EditorStore.CargoTexture(rolled, loot != null ? loot.TextureKey : "");
                if (rolled.Count > 0)
                {
                    cargo = new List<GameObject>();
                    yield return Cargo.Load(car, rolled, tpl.IsTruck, cargo);
                    if (car == null) yield break;
                }
                if (trailer != null && tpl.Trailer.SpawnLoot != null)
                {
                    trailerCargo = new List<GameObject>();
                    yield return Cargo.Load(trailer, tpl.Trailer.SpawnLoot, true, trailerCargo);
                    if (car == null || trailer == null) yield break;
                }

                yield return null;
                if (car == null) yield break;
                GameObject driver = null, passenger = null;
                if (!string.IsNullOrEmpty(tpl.Driver))
                {
                    if (tpl.Driver.Trim().EndsWith("_Dead", StringComparison.OrdinalIgnoreCase)) driver = SeatDriver(car, tpl.Driver, !tpl.RestoreCrewAppearance);
                    else driver = SeatLiveDriver(car, tpl.Driver, -1f, CrewPhase.Waiting, 0f, hold, !tpl.RestoreCrewAppearance);
                }
                yield return null;
                if (car == null) yield break;
                if (!string.IsNullOrEmpty(tpl.Passenger)) passenger = SeatPassenger(car, tpl.Passenger, -1f, !tpl.RestoreCrewAppearance);
                var marker = PatrolMarker.Attach(car, body.name, tpl.Driver, driver, tpl.Passenger, passenger, tpl.Rams);
                marker.CargoKey = lootKey ?? "";
                marker.TemplateName = tpl.Name;
                marker.SetupOnly=tpl.SetupOnly;if(tpl.SetupOnly){var setup=car.GetComponent<Crew>();if(setup!=null)setup.Hold=true;Core.ModApi.Get<Core.IVehicleDrive>().StopEngine(car);Handbrake(car,true);foreach(var person in new[]{driver,passenger})if(person!=null)Crew.MuteAi(person);}
                MerchantRuntime.Attach(car,tpl);
                marker.Faction=TrafficFactions.Normalize(tpl.Faction);marker.ExplodeChance=tpl.ExplodeChance;
                TrafficFactions.Crew(car,marker);
                WorldTraffic.AttachPending(car, tpl.TrafficId, tpl.TrafficSlot);
                marker.Trailer = trailer;
                // human turrets: up to 3 on a car's roof, 1 on a motorcycle, none on a truck
                int turretSlots = Rider.Slots(tpl.Body, tpl.IsTruck, tpl.IsMotorcycle);
                for (int ti = 0; ti < turretSlots; ti++)
                {
                    if (string.IsNullOrEmpty(tpl.Turrets[ti])) continue;
                    yield return null;
                    if (car == null) yield break;
                    Rider.Seat(car, marker, ti, tpl.Turrets[ti], tpl.TurretPos[ti],standing:tpl.TurretStanding[ti],randomize:!tpl.RestoreCrewAppearance);
                }
                if (trailer != null)
                    for (int ti = 0; ti < PatrolMarker.TrailerTurretSlots; ti++)
                    {
                        if (string.IsNullOrEmpty(tpl.Trailer.Turrets[ti])) continue;
                        yield return null;
                        if (car == null || trailer == null) yield break;
                        Rider.Seat(car, marker, PatrolMarker.VehicleTurretSlots + ti, tpl.Trailer.Turrets[ti], tpl.Trailer.TurretPos[ti], trailer,tpl.Trailer.TurretStanding[ti],!tpl.RestoreCrewAppearance);
                    }
                Paint.ApplyCargo(car, lootKey);   // the container's texture by load (Textures/cargo_*.png)

                yield return new WaitForSeconds(1f);
                if (car == null) yield break;
                if (cargo != null) Cargo.SettleFsms(cargo);
                if (trailerCargo != null) Cargo.SettleFsms(trailerCargo);
                if (Explode.IsWreck(car)) yield break;   // crew killed during the build: it already blew up
                var carFsms = car.GetComponentsInChildren<PlayMakerFSM>(true);
                SetPartConditions(car, carFsms);
                FillParts(car);
                if (trailer != null) { SetPartConditions(trailer); FillParts(trailer); }
                // the lift kit's adjustment from the template (the kit's own FSM has set its lifted mounts by now)
                if (tpl.SuspensionWidth != 1f || tpl.SuspensionHeight != 0f) Suspension.Apply(car, tpl.SuspensionWidth, tpl.SuspensionHeight, "template");
                marker.SuspensionWidth = tpl.SuspensionWidth; marker.SuspensionHeight = tpl.SuspensionHeight;
                if (Core.Log.Verbose) PatrolsModule.Verbose("Running gear of " + car.name + ": " + Suspension.Describe(car));
                if (DayLight.Dark) Headlights(car, true, carFsms);   // raiders drive with their lights on at night
                RaiderMusic(car, carFsms);                            // a cassette in the template's radio: on, full volume ([General] Raider music)
                yield return WaitForSave(gen, saveOk);
                if (car == null) yield break;
                int committed = Register.Commit(car);
                if (Core.Log.Verbose) PatrolsModule.Verbose("Registered " + car.name + " for saving: " + committed + " object(s)");
                if (trailer != null) Register.Commit(trailer);

                yield return BuildQueue.Assembled;
                if(!tpl.SetupOnly&&(car.GetComponent<MerchantHost>()==null||car.GetComponent<MerchantHost>().Data?.travelling==true))yield return StartUp(car);
            }
            finally
            {
                if(reserved){EventBudget.Release(eventId);if(car!=null)EncounterCar.Attach(car,eventId,tpl.ConvoyMember);}
                // stopped early (a wreck, an error): what exists is registered as before; a destroyed car's list is dropped
                if (car != null) { if (Register.Pending(car)) Register.Commit(car); }
                else Register.Discard(car);
                if (trailer != null)
                {
                    if (car != null && TrailerSupport.Connected(car, trailer)) { if (Register.Pending(trailer)) Register.Commit(trailer); }
                    else { Register.Discard(trailer); Register.RemoveTree(trailer); UnityEngine.Object.Destroy(trailer); }
                }
                else Register.Discard(trailer);
                if (onDone != null) onDone(car);
            }
        }

        // A radio with a cassette in it (the cassette's CheckTag FSM has set the radio's Music.music clip and started it, the OnOff FSM
        // holds the pitch at 0 = "off"): full volume, then switched on the way the player does it - "useDoor" on the PlayStop [Play]
        // FSM (click sound, OnOff Activate = pitch 1, Play/Stop swap, so one click of the player stops it).
        internal static void RaiderMusic(GameObject car, PlayMakerFSM[] snapshot = null)
        {
            if (car == null || PatrolsModule.RaiderMusic == null || !PatrolsModule.RaiderMusic.Value) return;
            int n = 0;
            foreach (var music in snapshot ?? car.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (music == null || music.FsmName != "Music" || music.Fsm == null || !music.Fsm.Initialized) continue;
                var radio = music.gameObject;
                var src = radio.GetComponent<AudioSource>();
                if (src == null) continue;
                var clipVar = music.FsmVariables.GetFsmObject("music");
                var clip = (clipVar != null ? clipVar.Value as AudioClip : null) ?? src.clip;
                if (clip == null) continue;   // no cassette in this radio
                if (src.clip != clip) src.clip = clip;
                src.loop = true; src.volume = 1f;
                if (!src.isPlaying) src.Play();
                var knob = FindChild(radio.transform, "Volume");
                if (knob != null) foreach (var f in knob.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Volume") { var v = f.FsmVariables.GetFsmFloat("audioVolume"); if (v != null) v.Value = 1f; }
                PlayMakerFSM play = null, onOff = null;
                var ps = FindChild(radio.transform, "PlayStop");
                if (ps != null) foreach (var f in ps.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Play") { play = f; break; }
                foreach (var f in radio.GetComponents<PlayMakerFSM>()) if (f.FsmName == "OnOff") { onOff = f; break; }
                if (play != null && play.enabled && play.Fsm.Initialized) play.SendEvent("useDoor");
                else if (onOff != null) { onOff.enabled = true; onOff.SendEvent("Activate"); src.pitch = 1f; }
                else src.pitch = 1f;
                n++;
            }
            if (n > 0) PatrolsModule.Verbose("Raider music: " + car.name + " plays " + n + " cassette(s)");
        }

        // A ragdoll on the driver seat: the *_Dead prefab's root Rigidbody is made kinematic and parented to the car's sitPos
        // (where the player sits), so it rides along exactly; the bones hang off it through their CharacterJoints and keep
        // flopping. Collisions between the ragdoll and the car are ignored so it never gets thrown out by the car's colliders.
        internal static GameObject SeatDriver(GameObject car, string prefabName, bool randomize = true)
        {
            var prefab = Prefabs.FindAny(prefabName);
            if (prefab == null) { Core.Log.Warn("[Patrols] " + "Driver prefab not found: " + prefabName); return null; }
            var sit = FindChild(car.transform, "sitPos") ?? FindChild(car.transform, "hinge_seat_driver");
            if (sit == null) { Core.Log.Warn("[Patrols] " + "No sitPos on " + car.name); return null; }

            var off = new Vector3(PatrolsModule.DriverOffsetX.Value, PatrolsModule.DriverOffsetY.Value, PatrolsModule.DriverOffsetZ.Value);
            var pos = sit.position + car.transform.right * off.x + car.transform.up * off.y + car.transform.forward * off.z;
            var rot = Quaternion.LookRotation(car.transform.forward, car.transform.up);
            var drv = UnityEngine.Object.Instantiate(prefab, pos, rot);
            drv.SetActive(true);
            drv.name = prefab.name + "(Driver)";
            CrewLooks.Dress(drv, prefabName, randomize);

            var drvCols = drv.GetComponentsInChildren<Collider>(true);

            var root = drv.GetComponent<Rigidbody>();
            if (root == null) root = drv.AddComponent<Rigidbody>();
            root.isKinematic = true;
            root.interpolation = RigidbodyInterpolation.None;
            Rider.NoContacts(drv);   // no contacts while seated, only bullet hits (was: every collider pair vs the car ignored)
            drv.transform.SetParent(sit, true);

            int bones = 0;
            foreach (var rb in drv.GetComponentsInChildren<Rigidbody>(true))
                if (rb != root) { bones++; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            PatrolsModule.Verbose("Driver " + drv.name + " on " + sit.name + " at " + pos + " (" + bones + " ragdoll bones, " + drvCols.Length + " colliders)");
            return drv;
        }

        // A live enemy at the wheel with its AI off: instantiated at sitPos, the mover/AI FSMs disabled before they Start,
        // root Rigidbody kinematic and parented to the seat, collisions with the car ignored. Health/Damage/Bodypart stay
        // vanilla so it can be shot; the Crew component drives the car and reacts to its death.
        internal static GameObject SeatLiveDriver(GameObject car, string prefabName, float health = -1f, CrewPhase phase = CrewPhase.Waiting, float seated = 0f, bool hold = false, bool randomize = true)
        {
            var sit = FindChild(car.transform, "sitPos") ?? FindChild(car.transform, "hinge_seat_driver");
            if (sit == null) { Core.Log.Warn("[Patrols] " + "No sitPos on " + car.name); return null; }
            var drv = SeatOccupant(car, prefabName, sit, "Driver", false, health, randomize);
            if (drv == null) return null;
            SimpleBanditBridge.Mark(drv);
            if (PatrolsModule.RangedCombat.Value && PassengerGuard.IsRangedHuman(drv)) PassengerGuard.AttachDriver(drv, car, sit);
            var crew = phase == CrewPhase.Waiting && seated <= 0f ? Crew.Attach(car, drv) : Crew.Restore(car, drv, phase, seated);
            crew.Hold = hold;
            crew.MuteAi();
            return drv;
        }

        // The passenger sits where the driver would if the driver seat hinge were the passenger seat hinge: sitPos shifted
        // by the offset between the two seat hinges (the game has no passenger sit point of its own).
        internal static GameObject SeatPassenger(GameObject car, string prefabName, float health = -1f, bool randomize = true)
        {
            var sit = FindChild(car.transform, "sitPos");
            var hd = FindChild(car.transform, "hinge_seat_driver");
            var hp = FindChild(car.transform, "hinge_seat_passenger");
            if (sit == null || hd == null || hp == null) { Core.Log.Warn("[Patrols] " + "No sitPos / seat hinges for a passenger on " + car.name); return null; }
            var anchor = new GameObject("Apocapatrol.PassengerPos").transform;
            anchor.SetParent(car.transform, false);
            anchor.position = sit.position + (hp.position - hd.position);
            anchor.rotation = sit.rotation;
            // the Rustliner bus (2.3.6): no passenger seat part - he sits on the small broken chair behind the driver's side door, turned
            // 90 degrees left (facing across the aisle): the anchor goes over the chair's seat collider, the hips end up just above it
            var chair = FindChild(car.transform, "chair_2_broken");
            if (chair != null)
            {
                var col = chair.GetComponentInChildren<Collider>(true);
                var top = col != null ? new Vector3(col.bounds.center.x, col.bounds.max.y, col.bounds.center.z) : chair.position;
                anchor.position = top + car.transform.up * (0.08f - PatrolsModule.DriverOffsetY.Value);   // Pose puts the hips at anchor + DriverOffset
                anchor.rotation = car.transform.rotation * Quaternion.Euler(0f, -90f, 0f);
                PatrolsModule.Verbose("Passenger on the bus chair at local " + car.transform.InverseTransformPoint(anchor.position).ToString("F2"));
            }
            var pax = SeatOccupant(car, prefabName, anchor, "Passenger", true, health, randomize);
            if (pax == null) { UnityEngine.Object.Destroy(anchor.gameObject); return null; }
            if(chair!=null||string.Equals(TemplateExporter.PrefabName(car.name),"Rustliner",System.StringComparison.OrdinalIgnoreCase))
                pax.GetComponent<Pose>()?.ConfigureBusPassenger();
            PassengerGuard.Attach(pax, car, anchor);
            return pax;
        }

        // A live enemy in a seat with its AI off: instantiated at the anchor, the mover/AI FSMs disabled by the Crew before
        // they Start, root Rigidbody kinematic and parented to the anchor, collisions with the car ignored, seated pose.
        // Health/Damage/Bodypart stay vanilla so it can be shot.
        private static GameObject SeatOccupant(GameObject car, string prefabName, Transform anchor, string role, bool passenger, float health = -1f, bool randomize = true)
        {
            var prefab = Prefabs.FindAny(prefabName);
            if (prefab == null) { Core.Log.Warn("[Patrols] " + role + " prefab not found: " + prefabName); return null; }

            // along the anchor's axes (= the car's for every seat but the bus chair, which faces sideways)
            var off = new Vector3(PatrolsModule.DriverOffsetX.Value, PatrolsModule.DriverOffsetY.Value, PatrolsModule.DriverOffsetZ.Value);
            var pos = anchor.position + anchor.right * off.x + anchor.up * off.y + anchor.forward * off.z;
            var rot = Quaternion.LookRotation(anchor.forward, anchor.up);
            var go = UnityEngine.Object.Instantiate(prefab, pos, rot);
            SimpleBanditBridge.Mark(go);
            go.SetActive(true);
            if (passenger) PassengerGuard.Prepare(go, PatrolsModule.RangedCombat.Value);
            else if (PatrolsModule.RangedCombat.Value) PassengerGuard.Prepare(go, true);   // ranged-only rewiring + mute, before the FSMs' Start
            else Crew.MuteAi(go);
            go.name = prefab.name + "(" + role + ")";
            CrewLooks.Dress(go, prefabName, randomize);

            var root = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
            root.isKinematic = true;
            root.interpolation = RigidbodyInterpolation.None;
            Rider.NoContacts(go);   // no contacts while seated, only bullet hits (was: every collider pair vs the car ignored)
            go.transform.SetParent(anchor, true);
            if (PatrolsModule.PoseEnabled.Value)
            {
                var pose = Pose.Apply(go, anchor, prefab.name);
                if (!passenger) pose.SetShooting(false);   // a driver sits with its hands on the wheel; PassengerGuard raises the gun only while it fires
            }
            if (health >= 0f) { SetHealth(go, health); go.AddComponent<LateHealth>().Value = health; }
            PatrolsModule.Verbose(role + " " + go.name + " on " + anchor.name + " at " + pos);
            return go;
        }

        // Every collider of an occupant vs every collider of the car. Unity drops an ignore pair when either collider is
        // disabled or its object deactivated (weapon props toggled by WeaponType/Attack, the FireDamage hitbox...), and an
        // occupant's root is kinematic, so a single live pair shoves the car around unopposed. Cheap; re-applied periodically.
        private static readonly Dictionary<GameObject, long> _ignoreSignature = new Dictionary<GameObject, long>();

        // Cheap signature of the occupant's + car's enabled colliders; the pairs are re-applied only when it changes
        // (a toggled prop, a part fallen off), not every second - each IgnoreCollision call makes PhysX rebuild pair filters.
        internal static int IgnoreCollisionsIfChanged(GameObject who, GameObject car)
        {
            if (who == null || car == null) return 0;
            return IgnoreCollisionsIfChanged(who, car, car.GetComponentsInChildren<Collider>(true));
        }

        // carCols = the car's colliders, fetched by the caller once for every occupant it checks
        // Perf: the occupant's colliders go to a reused buffer (was a GetComponentsInChildren array per check, 1-2 Hz per occupant)
        private static readonly List<Collider> _whoCols = new List<Collider>();
        internal static int IgnoreCollisionsIfChanged(GameObject who, GameObject car, IList<Collider> carCols)
        {
            if (who == null || car == null) return 0;
            _whoCols.Clear(); who.GetComponentsInChildren(true, _whoCols);
            long sig = 17;
            for (int i = 0; i < _whoCols.Count; i++)
            { var a = _whoCols[i]; if (a != null && a.enabled && a.gameObject.activeInHierarchy) sig = sig * 31 + a.GetInstanceID(); }
            for (int i = 0; i < carCols.Count; i++)
            { var b = carCols[i]; if (b != null && b.enabled && b.gameObject.activeInHierarchy) sig = sig * 31 + b.GetInstanceID(); }
            long old;
            if (_ignoreSignature.TryGetValue(who, out old) && old == sig) { _whoCols.Clear(); return 0; }
            _ignoreSignature[who] = sig;
            int n = IgnoreCore(who, _whoCols, carCols); _whoCols.Clear();
            if (Core.Log.Verbose) PatrolsModule.Verbose("Collision ignores re-applied for " + who.name + ": " + n + " pairs");
            return n;
        }

        internal static int IgnoreCollisions(GameObject who, GameObject car)
        {
            if (who == null || car == null) return 0;
            return IgnoreCollisions(who, car, car.GetComponentsInChildren<Collider>(true));
        }

        internal static int IgnoreCollisions(GameObject who, GameObject car, IList<Collider> carCols)
        {
            if (who == null || car == null) return 0;
            return IgnoreCore(who, who.GetComponentsInChildren<Collider>(true), carCols);
        }

        private static int IgnoreCore(GameObject who, IList<Collider> whoCols, IList<Collider> carCols)
        {
            int n = 0;
            for (int i = 0; i < whoCols.Count; i++)
            {
                var a = whoCols[i];
                if (a == null || !a.enabled || !a.gameObject.activeInHierarchy) continue;
                for (int k = 0; k < carCols.Count; k++)
                { var b = carCols[k]; if (b != null && b.enabled && b.gameObject.activeInHierarchy && !b.transform.IsChildOf(who.transform)) { Physics.IgnoreCollision(a, b, true); n++; } }
            }
            return n;
        }

        // Before the passenger climbs over, the dead driver's carcass (the <X>_Dead ragdoll its Health FSM dropped on the seat)
        // is thrown out of the car by physics: its colliders are told to ignore the car and the passenger first (so it cannot
        // shove the car), joints to the car are cut, and every bone gets a sideways+up velocity. The returned CorpseEject is
        // Done once the carcass is EjectDistance from the seat; if it has not got there after 3 s it is put down there once.
        // Returns null when there is nothing to eject.
        internal static CorpseEject EjectCorpses(GameObject car, GameObject pax)
        {
            var sit = FindChild(car.transform, "sitPos") ?? FindChild(car.transform, "hinge_seat_driver");
            if (sit == null) return null;
            var roots = new List<Transform>();
            foreach (var c in Physics.OverlapSphere(sit.position, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                if (c == null) continue;
                var root = c.transform.root;
                if (root == car.transform || (pax != null && root == pax.transform.root)) continue;
                if (root.name.IndexOf("_Dead", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!roots.Contains(root)) roots.Add(root);
            }
            if (roots.Count == 0) { PatrolsModule.Verbose("Eject: no carcass near the driver seat"); return null; }
            var carCols = car.GetComponentsInChildren<Collider>(true);
            var left = -car.transform.right;
            foreach (var root in roots)
            {
                int joints = 0, jointsToCar = 0;
                foreach (var j in root.GetComponentsInChildren<Joint>(true))
                {
                    joints++;
                    if (j.connectedBody != null && j.connectedBody.transform.IsChildOf(car.transform)) { jointsToCar++; j.connectedBody = null; }
                }
                foreach (var a in root.GetComponentsInChildren<Collider>(true))
                {
                    if (a == null) continue;
                    foreach (var b in carCols) if (b != null) Physics.IgnoreCollision(a, b, true);   // the carcass is solid; the passenger is all triggers (no pairs needed)
                }
                var velocity = left * PatrolsModule.EjectSpeed + Vector3.up * (PatrolsModule.EjectSpeed * 0.5f) - car.transform.forward * 0.5f;
                Launch(root, velocity);
                PatrolsModule.Verbose("Eject: " + root.name + " thrown out of " + car.name + " at " + velocity.magnitude.ToString("0.0") + " m/s ("
                    + joints + " joints, " + jointsToCar + " were attached to the car)");
            }
            var eject = car.AddComponent<CorpseEject>();
            eject.Init(sit, roots, left);
            return eject;
        }

        // The surviving passenger climbs over to the driver seat: same placement as SeatOccupant, AI fully muted, pose re-anchored.
        internal static GameObject MoveToDriverSeat(GameObject car, GameObject pax)
        {
            var sit = FindChild(car.transform, "sitPos") ?? FindChild(car.transform, "hinge_seat_driver");
            if (sit == null || pax == null) return null;
            var guard = pax.GetComponent<PassengerGuard>();
            var oldAnchor = pax.transform.parent;
            if (guard != null && PatrolsModule.RangedCombat.Value) Crew.MuteAi(pax, PassengerGuard.CombatFsms);
            else { if (guard != null) UnityEngine.Object.Destroy(guard); guard = null; Crew.MuteAi(pax); }
            var off = new Vector3(PatrolsModule.DriverOffsetX.Value, PatrolsModule.DriverOffsetY.Value, PatrolsModule.DriverOffsetZ.Value);
            pax.transform.SetParent(sit, true);
            pax.transform.position = sit.position + car.transform.right * off.x + car.transform.up * off.y + car.transform.forward * off.z;
            pax.transform.rotation = Quaternion.LookRotation(car.transform.forward, car.transform.up);
            var pose = pax.GetComponent<Pose>();
            if (pose != null) pose.Reseat(sit);
            pax.name = pax.name.Replace("(Passenger)", "(Driver)");
            if (guard != null) guard.SetDriverMode(sit);
            Rider.KeepNoContacts(pax);   // already contact-free from its seat; covers props it picked up since
            if (Core.Log.Verbose) PatrolsModule.Verbose("Takeover: " + pax.name + " on " + sit.name);
            if (oldAnchor != null && oldAnchor.name == "Apocapatrol.PassengerPos") UnityEngine.Object.Destroy(oldAnchor.gameObject);
            return pax;
        }

        // The passenger gets out: a fresh instance of its prefab (fully vanilla AI, registered like a spawned enemy so it is
        // saved by the game) appears beside the car with the passenger's remaining health; the seated copy is destroyed.
        // A fresh instance rather than the seated one because the seat setup rewires its FSM actions (ranged-only, melee off).
        internal static void BailOut(GameObject car, GameObject pax, PatrolMarker marker)
        {
            marker.PassengerLeft();
            BailOut(car, pax, marker.PassengerPrefab, 0f);
        }

        // Any occupant gets out on the given side (+1 right, -1 left, 0 = the side its seat is on relative to sitPos).
        internal static GameObject BailOut(GameObject car, GameObject pax, string prefabName, float side,bool record=true)
        {
            float health = GetHealth(pax);
            var prefab = Prefabs.FindAny(prefabName);
            var anchor = pax.transform.parent;
            var sit = FindChild(car.transform, "sitPos");
            if (side == 0f)
            {
                side = 1f;
                if (anchor != null && sit != null) side = Vector3.Dot(anchor.position - sit.position, car.transform.right) >= 0f ? 1f : -1f;
            }
            var from = (anchor != null ? anchor.position : car.transform.position) + car.transform.right * side * VehicleRules.ExitDistance(car.name) + Vector3.up * 1.5f;
            var facing = Vector3.ProjectOnPlane(car.transform.right * side, Vector3.up);
            // the Rustliner bus (2.3.8): everybody leaves through the door in the back (hinge_door_parent/hinge_door_L, the rear door), the
            // truck's exit distance behind it - a sideways exit lands them inside the bus
            var rearDoor = TemplateExporter.PrefabName(car.name).Equals("Rustliner", StringComparison.OrdinalIgnoreCase) ? FindChild(car.transform, "hinge_door_parent") : null;
            var frontDoor=CrewReturn.FrontDoor(car);
            if(frontDoor!=null){var crew=car.GetComponent<Crew>();from=CrewReturn.DoorOutside(car,frontDoor)+Vector3.up*1.5f+(crew!=null?crew.FrontExitOffset():Vector3.zero);facing=car.transform.right;}
            else if (rearDoor != null)
            {
                from = rearDoor.position - car.transform.forward * VehicleRules.ExitDistance(car.name) + Vector3.up * 1.5f;
                facing = Vector3.ProjectOnPlane(-car.transform.forward, Vector3.up);
            }
            var pos = from + Vector3.down * 1.2f;
            var hits = Physics.RaycastAll(from, Vector3.down, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
                if (h.collider != null && !h.collider.transform.IsChildOf(car.transform) && !h.collider.transform.IsChildOf(pax.transform) && h.distance < best)
                { best = h.distance; pos = h.point; }
            float groundY = pos.y;
            var rot = Quaternion.LookRotation(facing.sqrMagnitude > 1e-4f ? facing.normalized : car.transform.forward, Vector3.up);

            if (prefab == null)
            {
                Core.Log.Warn("[Patrols] " + "Bail-out: prefab not found: " + prefabName + "; " + pax.name + " just disappears");
                UnityEngine.Object.Destroy(pax);
                if (anchor != null && anchor.name == "Apocapatrol.PassengerPos") UnityEngine.Object.Destroy(anchor.gameObject);
                Core.Services.RaiseCrewBailedOut(car, pax, null);   // WW: ex a Harmony postfix other mods put on this method
                return null;
            }
            var mob = UnityEngine.Object.Instantiate(prefab, pos + Vector3.up * 0.5f, rot);
            SimpleBanditBridge.Fight(mob);
            TrafficFactions.Dismount(mob,car);
            mob.SetActive(true);
            // stand it on the ground: the prefab pivot may sit mid-body, so lift it by its collider's lowest point
            var rootCol = mob.GetComponent<Collider>();
            if (rootCol != null)
            {
                float bottom = rootCol.bounds.min.y;
                mob.transform.position += Vector3.up * (groundY + 0.15f - bottom);
            }
            pos = mob.transform.position;
            Register.Name(mob, prefab.name); CrewLooks.Transfer(pax, mob); Register.Add(mob, false);
            if (health > 0f) { SetHealth(mob, health); mob.AddComponent<LateHealth>().Value = health; }
            UnityEngine.Object.Destroy(pax);
            if (anchor != null && anchor.name == "Apocapatrol.PassengerPos") UnityEngine.Object.Destroy(anchor.gameObject);
            PatrolsModule.Verbose("Bail-out: " + mob.name + " got out of " + car.name + " with " + (health > 0f ? health.ToString("0") : "full") + " health at " + pos);
            Core.Services.RaiseCrewBailedOut(car, pax, mob);   // WW: the NPC module subscribes (ex its Harmony postfix on Apocapatrol.Patrol.BailOut)
            // awareness handover + return-to-car membership (pax is destroyed at the end of the frame; its name still tells the seat)
            if(record)CrewReturn.Dismounted(car, mob, pax.name.IndexOf("(Driver)", StringComparison.Ordinal) >= 0 ? CrewReturn.RoleDriver : CrewReturn.RolePassenger, -1,
                CrewLooks.Identity(mob, prefabName), null, null, false);
            return mob;
        }

    // Follows a thrown carcass: Done once it is EjectDistance from the driver seat (the passenger only climbs over then);
    // if it has not got there after 3 s (caught on something) it is put down at that distance once. Removes itself when done.
    internal class CorpseEject : MonoBehaviour
    {
        private Transform _sit;
        private List<Transform> _roots;
        private Vector3 _left;
        private float _t, _nextCheck;
        internal bool Done { get; private set; }

        internal void Init(Transform sit, List<Transform> roots, Vector3 left) { _sit = sit; _roots = roots; _left = left; }

        private float Distance(Transform root)
        {
            var rb = root.GetComponentInChildren<Rigidbody>();
            var p = rb != null ? rb.position : root.position;
            var d = p - _sit.position; d.y = 0f;
            return d.magnitude;
        }

        private void FixedUpdate()
        { if (!PatrolsModule.Running) return;
            if (Done) { Destroy(this); return; }
            if (_sit == null || _roots == null) { Done = true; return; }
            _t += Time.fixedDeltaTime;
            if (_t < _nextCheck) return;
            _nextCheck = _t + 0.1f;
            bool clear = true;
            foreach (var root in _roots)
                if (root != null && Distance(root) < PatrolsModule.EjectDistance) clear = false;
            if (clear) { Done = true; PatrolsModule.Verbose("Eject: carcass clear of the seat after " + _t.ToString("0.0") + " s"); return; }
            if (_t < 3f) return;
            foreach (var root in _roots)
            {
                if (root == null || Distance(root) >= PatrolsModule.EjectDistance) continue;
                var target = _sit.position + _left * PatrolsModule.EjectDistance + Vector3.up * 2f;
                var ground = target + Vector3.down * 1.5f;
                foreach (var h in Physics.RaycastAll(target, Vector3.down, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (h.collider != null && !h.collider.transform.IsChildOf(transform) && !h.collider.transform.IsChildOf(root) && h.point.y > ground.y) ground = h.point;
                Patrol.Throw(root, ground + Vector3.up * 0.4f, Vector3.zero);
                PatrolsModule.Verbose("Eject: " + root.name + " did not get clear in 3 s, put down " + PatrolsModule.EjectDistance + " m left of the seat");
            }
            Done = true;
        }
    }

    // Re-applies a health value for a few frames after Instantiate, in case the Health FSM's start state resets it.
    internal class LateHealth : MonoBehaviour
    {
        internal float Value;
        private int _frames;
        private void Update()
        { if (!PatrolsModule.Running) return;
            Patrol.SetHealth(gameObject, Value);
            if (++_frames >= 3) Destroy(this);
        }
    }
    }
}
