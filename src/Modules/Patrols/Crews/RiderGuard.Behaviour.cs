using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Traffic-owned turret throw, leap, escort, dismount and landing decisions composed with real visual and weapon services.</summary>
    internal sealed partial class RiderGuard
    {
        /// <summary>Per frame: the rider's decisions (face the target, throw the lance within range and angle, the "Witness me!" leap at a close car, bail off a bike that cannot turn).</summary>
        private void Update()
        { if (!PatrolsModule.Running) return;
            if (_mode == Mode.Done) return;
            if(!_gunner && _warboyPrefab)EnsureVisual();
            if (Time.timeScale <= 0f) return;
            if (_mode == Mode.Flying) { Fly(); return; }
            if (_mode == Mode.Dismount) { Hop(); return; }
            if (_car == null || _anchor == null) { Destroy(this); return; }
            if (transform.parent == null || !Alive()) { Die(); return; }
            if (!_gunner && Time.time >= _nextHide) { _nextHide = Time.time + 0.5f; HideOwnWeapons(); }
            // a collider the game adds or enables later (weapon prop) is made a trigger too - no contacts at all while seated
            if (Time.time >= _nextIgnore) { _nextIgnore = Time.time + 1f; Rider.KeepNoContacts(gameObject); _carMarker = _car.GetComponent<PatrolMarker>(); }

            // the player took this car, or it blew up: off the roof
            if(_marker?.SetupOnly==true)return;
            if (Core.PlayerState.Car == _car || (_marker != null && Core.PlayerState.Car == _marker.gameObject)) { JumpOff("the player took the car"); return; }
            if (_slot >= PatrolMarker.VehicleTurretSlots)
            {
                // Perf: the joint check at 4 Hz (was per frame); a lost marker still jumps off at once
                if (_marker == null) { JumpOff("the trailer detached"); return; }
                if (Time.time >= _nextTrailerCheck) { _nextTrailerCheck = Time.time + 0.25f; _trailerConnected = Core.ModApi.Get<Core.IVehicleAttachments>()?.Connected(_marker.gameObject, _car)==true; }
                if (!_trailerConnected) { JumpOff("the trailer detached"); return; }
            }
            if (_gunner||!_warboyPrefab) return;

            Vector3 tpos, tvel; GameObject tcar;
            bool has = TrafficFactions.DriveTarget(_car,_carMarker,out tpos,out tvel,out tcar) && !Core.PlayerState.GodMode && WorldTraffic.CombatAllowed(_marker != null ? _marker.gameObject : _car);
            var to = tpos - transform.position; to.y = 0f;
            float dist = has ? to.magnitude : 9999f;
            if (!has && _mode == Mode.Witness) { _mode = Mode.Riding; _standAt = -1f; }

            if (_mode == Mode.Witness)
            {
                Face(to.sqrMagnitude > 0.01f ? to : _car.transform.forward);
                // on a motorcycle he crouches on the seat: after the scream he stands up first (crouch -> stand blend), then leaps
                bool standing = _bike && _standAt >= 0f;
                Blend(standing ? 3 : 0, standing ? StandSeconds * 0.8f : 0.15f);
                Escort();
                if (Time.time - _modeStart >= _windUp)
                {
                    if (_bike && _standAt < 0f) { _standAt = Time.time; return; }
                    if (_bike && Time.time - _standAt < StandSeconds) return;
                    // aimed at where the player is NOW and will be; only a jump that lands goes - else he waits (the driver closes in)
                    Vector3 launch; float flight;
                    if (CanLeap(tvel, tcar, out launch, out flight)) Leap(launch, flight);
                    else if (Time.time - _modeStart > _windUp + 5f)
                    {
                        _mode = Mode.Riding; _nextWitness = Time.time + 3f; _standAt = -1f;
                        if (Core.Log.L1) PatrolsModule.Verbose("Rider: " + name + " - no jump that can hit, back to throwing");
                    }
                }
                return;
            }

            Face(has && dist < PatrolsModule.RiderRange.Value * 1.5f && to.sqrMagnitude > 0.01f ? to : _car.transform.forward);

            // on a motorcycle he can only turn BikeArc degrees from the bike's nose: the player close by and out of that arc for a second
            // (beside or behind him) -> he jumps off and fights on foot
            bool inArc = !_bike || !has || ArcOff(to) <= Rider.BikeArc;
            if (_bike && PatrolsModule.RiderLances.Value && Core.ModApi.Get<Core.IRiderWeapons>()!=null && has && !inArc && dist <= PatrolsModule.RiderBailRange.Value && _mode == Mode.Riding)
            {
                _outOfArc += Time.deltaTime;
                if (_outOfArc > 1f) { JumpOff("cannot turn to the player"); return; }
            }
            else _outOfArc = 0f;

            // "Witness me!": the player's car comes within throwing range - always when this rider is wounded, else WitnessMeChance % once per
            // approach. Armed, the driver pulls up alongside, matching the player's heading and speed (Pilot escort); the scream starts once the
            // player is within RiderJumpRange, both cars go the same way (not head-on / apart) and a flat jump lands on the player's car.
            if (PatrolsModule.RiderLances.Value && Core.ModApi.Get<Core.IRiderWeapons>()!=null && has && tcar != null && _mode == Mode.Riding)
            {
                float near = (LeapAim(tcar) - transform.position).magnitude;
                if (near <= PatrolsModule.RiderRange.Value)
                {
                    if (!_inClose) { _inClose = true; _kamikazeArmed = UnityEngine.Random.Range(0f, 100f) < PatrolsModule.WitnessMeChance.Value; }
                    if (_kamikazeArmed || Wounded())
                    {
                        Escort();
                        Vector3 launch; float flight;
                        // only a car well inside his reach: the jump (both cars' speeds counted) takes at most RiderLeapReachPercent of the longest
                        if (inArc && near <= PatrolsModule.RiderJumpRange.Value && Time.time >= _nextWitness && CanLeap(tvel, tcar, out launch, out flight)
                            && flight <= Core.LanceTrajectory.MaxLeapTime * PatrolsModule.RiderLeapReachPercent.Value / 100f) { StartWitness(); return; }
                    }
                }
                else if (near > PatrolsModule.RiderRange.Value + 8f) _inClose = false;
            }
            else _inClose = false;

            if (!PatrolsModule.RiderLances.Value) { Blend(0, 0.15f); return; }

            if (_mode == Mode.Riding && has && inArc && dist > 3f && dist <= PatrolsModule.RiderRange.Value && Time.time >= _nextThrow && (_marker == null || !_marker.Exploded))
            {
                // only within the lance's real reach: a flat throw (<= RiderMaxThrowAngle up) at the lance's speed must hit the predicted spot
                Vector3 launch; float flight;
                var from = HandPos();
                if (Core.LanceTrajectory.Solve(from, CarVel(), tpos + Vector3.up * 0.9f, tvel, PatrolsModule.RiderLanceSpeed.Value, out launch, out flight)
                    && Core.LanceTrajectory.Elevation(launch) <= PatrolsModule.RiderMaxThrowAngle.Value)
                {
                    _mode = Mode.Throwing; _released = false; _modeStart = Time.time;
                    Play(1);
                    if (Lance != null) Lance.gameObject.SetActive(true);
                }
                else _nextThrow = Time.time + 0.5f;   // out of reach: look again shortly
            }
            if (_mode == Mode.Throwing)
            {
                float t = Time.time - _modeStart;
                float len = _visual?.Animated==true ? _visual.ThrowSeconds : 0.9f;
                if (!_released && t >= len * ReleaseFraction) { _released = true; Release(tpos, tvel); }
                if (t >= len)
                {
                    _mode = Mode.Riding;
                    _visual?.PauseThrow();
                    _nextThrow = Time.time + UnityEngine.Random.Range(PatrolsModule.RiderIntervalMin.Value, Mathf.Max(PatrolsModule.RiderIntervalMin.Value, PatrolsModule.RiderIntervalMax.Value));
                    _lanceBackAt = Time.time + 1.2f;   // "picks the next lance up"
                }
            }
            else if (Lance != null && !Lance.gameObject.activeSelf && Time.time >= _lanceBackAt) Lance.gameObject.SetActive(true);
            Blend(_mode == Mode.Throwing ? 1 : 0, 0.15f);
        }

        /// <summary>degrees between the vehicle's nose and a direction, about the vehicle's up</summary>
        private float ArcOff(Vector3 dir)
        {
            var up = _car.transform.up;
            var f = Vector3.ProjectOnPlane(_car.transform.forward, up); dir = Vector3.ProjectOnPlane(dir, up);
            return f.sqrMagnitude < 1e-4f || dir.sqrMagnitude < 1e-4f ? 0f : Mathf.Abs(Vector3.SignedAngle(f, dir, up));
        }

        // ------------------------------------------------------------ "Witness me!"

        /// <summary>Starts the "Witness me!" (wounded or a chance roll): the leap follows.</summary>
        private void StartWitness()
        {
            _mode = Mode.Witness; _modeStart = Time.time;
            if (Lance != null) Lance.gameObject.SetActive(true);
            _windUp = (Core.ModApi.Get<Core.IRiderVisuals>()?.Scream(transform) ?? 1.5f) + AfterScream;   // the whole scream first, then the leap
            if (Core.Log.L1) PatrolsModule.Verbose("Rider: " + name + " - WITNESS ME! (" + (Wounded() ? "wounded" : "chance roll") + ")");
        }

        /// <summary>Leap start point (the rider's chest).</summary>
        private Vector3 LeapFrom() { return transform.position + Vector3.up * 0.9f; }
        /// <summary>Leap aim point on the player's car.</summary>
        private static Vector3 LeapAim(GameObject tcar)
        {
            var tc = tcar.GetComponent<Rigidbody>();
            return (tc != null ? tc.worldCenterOfMass : tcar.transform.position) + Vector3.up * 0.4f;
        }

        /// <summary>the driver helps: Pilot drives alongside the player while this is refreshed</summary>
        private void Escort()
        {
            if (_pilot == null && _car != null) _pilot = (_marker != null ? _marker.gameObject : _car).GetComponent<Pilot>();
            if (_pilot != null) _pilot.EscortUntil = Time.time + 0.6f;
        }

        /// <summary>a jump that lands: both cars not going opposite ways or apart (&gt; 60 degrees between their headings, both moving), and a flat jump at RiderLeapSpeed meets the player's car on its predicted course</summary>
        private bool CanLeap(Vector3 tvel, GameObject tcar, out Vector3 launch, out float flight)
        {
            launch = Vector3.zero; flight = 0f;
            if (tcar == null) return false;
            var cv = CarVel(); cv.y = 0f; var pv = tvel; pv.y = 0f;
            if (cv.magnitude > 3f && pv.magnitude > 3f && Vector3.Angle(cv, pv) > 60f) return false;
            return Core.LanceTrajectory.FlatLeap(LeapFrom(), CarVel(), LeapAim(tcar), tvel, PatrolsModule.RiderLeapSpeed.Value, out launch, out flight);
        }

        /// <summary>off the roof in a long flat jump on the intercept course to the player's car, as a live bomb. His FSMs are off for the flight: the game cannot kill and swap him for a corpse mid-air (2.1.3's "the boy died, nothing blew up")</summary>
        private void Leap(Vector3 launch, float flight)
        {
            _vel = launch + CarVel();
            Unseat();
            foreach (var f in GetComponentsInChildren<PlayMakerFSM>(true)) if (f != null) { f.Fsm.RestartOnEnable = false; f.enabled = false; }
            if (Lance != null) Lance.gameObject.SetActive(true);
            Play(2);
            _mode = Mode.Flying; _modeStart = Time.time;
            if (Core.Log.L1) PatrolsModule.Verbose("Rider: " + name + " leaps at the player's car, " + flight.ToString("0.00") + " s, " + Core.LanceTrajectory.Elevation(launch).ToString("0") + " deg up");
        }

        /// <summary>The leap flight: carried toward the car; a hit detonates, a miss lands and fights on foot.</summary>
        private void Fly()
        {
            float dt = Time.deltaTime;
            Blend(2, 0.1f);
            var p = transform.position + Vector3.up * 0.9f;
            var step = _vel * dt + 0.5f * Physics.gravity * dt * dt;
            _vel += Physics.gravity * dt;
            float len = step.magnitude;
            if (len > 1e-4f)
            {
                int n = Physics.SphereCastNonAlloc(p, 0.4f, step / len, _hits, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue; Vector3 at = Vector3.zero;
                for (int i = 0; i < n; i++)
                {
                    var c = _hits[i].collider;
                    if (c == null || Own(c)) continue;
                    if (_hits[i].distance < best) { best = _hits[i].distance; at = _hits[i].distance > 0f ? _hits[i].point : p; }
                }
                if (best < float.MaxValue) { Detonate(at); return; }
            }
            transform.position += step;
            var flat = new Vector3(_vel.x, 0f, _vel.z);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            // touching the player's car even if no collider was swept (fast, thin parts): blows up there
            var pc = Core.PlayerState.Car;
            if (pc != null && (LeapAim(pc) - (transform.position + Vector3.up * 0.9f)).sqrMagnitude < 2.2f * 2.2f) { Detonate(transform.position + Vector3.up * 0.9f); return; }
            if (Time.time - _modeStart > 4f) Detonate(transform.position + Vector3.up * 0.9f);
        }

        /// <summary>True when the collider belongs to the rider or his car.</summary>
        private bool Own(Collider c)
        {
            if (c.transform.IsChildOf(transform)) return true;
            if (_car != null && c.transform.IsChildOf(_car.transform)) return true;
            if (_marker != null && c.transform.IsChildOf(_marker.transform)) return true;
            return Lance != null && c.transform.IsChildOf(Lance);
        }

        /// <summary>the blast (Rider.Kaboom: no lance physics), the rider's carcass thrown</summary>
        private void Detonate(Vector3 at)
        {
            _mode = Mode.Done;
            Rider.Kaboom(at, name);
            try
            {
                var dead = Prefabs.FindAny(_prefab + "_Dead");
                if (dead != null)
                {
                    var body = UnityEngine.Object.Instantiate(dead, transform.position, transform.rotation);
                    CrewLooks.Transfer(gameObject, body);
                    body.SetActive(true);
                    foreach (var r in body.GetComponentsInChildren<Rigidbody>(true)) r.velocity = _vel * 0.3f + Vector3.up * 4f + UnityEngine.Random.insideUnitSphere * 3f;
                    UnityEngine.Object.Destroy(body, 120f);
                }
                if (Core.Log.L1) PatrolsModule.Verbose("Rider: " + name + " blew up at " + at);
            }
            catch (Exception e) { Core.Log.Warn(null,"Rider: detonate: " + e.Message); }
            if (_marker != null) _marker.DropRider(gameObject);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------ dismount (the crew bails, the car is vacated or taken)

        /// <summary>Jumps off the car (leap / bike bail) and fights on foot.</summary>
        internal void JumpOff(string why)
        {
            if (_mode == Mode.Flying || _mode == Mode.Dismount || _mode == Mode.Done || _car == null) return;
            if (!Alive()) { Die(); return; }
            if(CrewReturn.FrontDoor(_marker!=null?_marker.gameObject:_car)!=null){CombatDismount();return;}
            // 2.2.3: he becomes a normal NPC at once - a fresh mob of his kind (same health, AI on) takes his place and jumps off with
            // physics, playing Apocaplayer's Jump on its own animator while in the air (was: a kinematic hop, a stiff upright body)
            var vel = _car.transform.right * _side * 3.2f + Vector3.up * 4.2f + CarVel() * 0.7f;
            if (Core.Log.L1) PatrolsModule.Verbose("Rider: " + name + " jumps off " + _car.name + " (" + why + ")");
            SpawnMob(transform.position, vel);
        }

        /// <summary>Dismounts the rider into a foot fighter (crew return order handed over).</summary>
        internal void CombatDismount()
        {
            if(_mode==Mode.Flying||_mode==Mode.Dismount||_mode==Mode.Done||_car==null)return;
            if(!Alive()){Die();return;}
            var owner=_marker!=null?_marker.gameObject:_car;
            if(CrewReturn.FrontDoor(owner)==null){JumpOff("crew under attack");return;}
            _mode=Mode.Done;if(_marker!=null)_marker.DropRider(gameObject);
            var mob=Patrol.BailOut(owner,gameObject,CrewLooks.Identity(gameObject,_prefab),1f,false);
            if(mob!=null)Dismounted(mob);
        }

        /// <summary>the fresh mob that replaces this body (jump-off), with the given velocity; this body is removed</summary>
        private void SpawnMob(Vector3 at, Vector3 vel)
        {
            _mode = Mode.Done;
            float health = Health();
            var prefab = Prefabs.FindAny(_prefab);
            if (prefab != null)
            {
                var mob = UnityEngine.Object.Instantiate(prefab, at, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                SimpleBanditBridge.Fight(mob);
                TrafficFactions.Dismount(mob,_car);
                mob.SetActive(true);
                Core.ItemSpawner.Name(mob, prefab.name); CrewLooks.Transfer(gameObject, mob); Core.ItemSpawner.AddExisting(mob, false);
                if (health > 0f) { Patrol.SetHealth(mob, health); mob.AddComponent<Patrol.LateHealth>().Value = health; }
                if (_car != null)   // it leaves the vehicle: never caught on it
                {
                    var carCols = _car.GetComponentsInChildren<Collider>(true);
                    foreach (var a in mob.GetComponentsInChildren<Collider>(true)) foreach (var b in carCols) if (a != null && b != null) Physics.IgnoreCollision(a, b, true);
                }
                mob.AddComponent<RiderLanding>().Velocity = vel;
                if (Core.Log.L1) PatrolsModule.Verbose("Rider: " + mob.name + " jumped off and fights on foot");
                Dismounted(mob);
            }
            if (_marker != null) _marker.DropRider(gameObject);
            Destroy(gameObject);
        }

        /// <summary>awareness handover + return-to-car membership: the turret spot (anchor local position, standing pose) is kept for re-seating</summary>
        private void Dismounted(GameObject mob)
        {
            var owner = _marker != null ? _marker.gameObject : _car;
            string identity = CrewLooks.Identity(mob, CrewLooks.Identity(gameObject, _prefab));
            string who = _gunner || !_warboyPrefab ? identity : Rider.Warboy;
            float[] local = null;
            if (_anchor != null) { var lp = _anchor.localPosition; local = new[] { lp.x, lp.y, lp.z }; }
            CrewReturn.Dismounted(owner, mob, CrewReturn.RoleTurret, _slot, identity, who, local, _standing);
        }

        /// <summary>A short hop off the car.</summary>
        private void Hop()
        {
            float dt = Time.deltaTime;
            Blend(2, 0.1f);
            var step = _vel * dt + 0.5f * Physics.gravity * dt * dt;
            _vel += Physics.gravity * dt;
            var p = transform.position + Vector3.up * 0.5f;
            float len = step.magnitude;
            bool landed = false; Vector3 ground = transform.position + step;
            if (len > 1e-4f && _vel.y < 0f)
            {
                int n = Physics.RaycastNonAlloc(p, Vector3.down, _hits, 0.5f - step.y + 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++) { var c = _hits[i].collider; if (c == null || Own(c) || _hits[i].normal.y < 0.5f) continue; landed = true; ground = _hits[i].point; break; }
            }
            if (!landed) transform.position += step;
            if (landed || Time.time - _modeStart > 2.5f) Land(landed ? ground : transform.position);
        }

        /// <summary>on the ground: a free mob of the same kind with the same health takes over (AI on), like the game's bail-out</summary>
        private void Land(Vector3 at)
        {
            _mode = Mode.Done;
            float health = Health();
            var prefab = Prefabs.FindAny(_prefab);
            if (prefab != null)
            {
                var mob = UnityEngine.Object.Instantiate(prefab, at + Vector3.up * 0.5f, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                SimpleBanditBridge.Fight(mob);
                TrafficFactions.Dismount(mob,_car);
                mob.SetActive(true);
                var col = mob.GetComponent<Collider>();
                if (col != null) mob.transform.position += Vector3.up * (at.y + 0.15f - col.bounds.min.y);
                Core.ItemSpawner.Name(mob, prefab.name); CrewLooks.Transfer(gameObject, mob); Core.ItemSpawner.AddExisting(mob, false);
                if (health > 0f) { Patrol.SetHealth(mob, health); mob.AddComponent<Patrol.LateHealth>().Value = health; }
                if (Core.Log.L1) PatrolsModule.Verbose("Rider: " + mob.name + " landed and fights on foot");
                var departed=new Core.Events.CrewBailedOut { Vehicle=Core.Entities.Of(_car),Passenger=Core.Entities.Of(gameObject),Npc=Core.Entities.Of(mob),Reason=Core.Events.CrewLeaveReason.Jump,WorldPos=Core.Origin.World(mob.transform.position) };Core.ModEvents.Publish(ref departed);
                Dismounted(mob);
            }
            if (_marker != null) _marker.DropRider(gameObject);
            Destroy(gameObject);
        }
    }
}
