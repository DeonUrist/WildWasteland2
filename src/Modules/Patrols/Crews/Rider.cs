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
    // A blast-lance rider sits or stands at its template's turret anchor,
    // who throws the game's blast lance (the blastlance_1_explode projectile the player's lance creates) at the player every few seconds.
    // Animation: Apocaplayer's humanoid clips when that mod is loaded (CrouchIdle looping, Throw as a one-shot) through a PlayableGraph on the
    // mob's own Animator; without Apocaplayer the mob keeps its idle and the lance is thrown without a swing.
    internal static partial class Rider
    {
        internal const string Projectile = "blastlance_1_explode";   // what the player's blast lance [Attack] FSM creates and pushes at 30 m/s
        internal const string LanceItem = "blastlance_1";             // the item, used as the visual in the rider's hand

        // ------------------------------------------------------------ human turrets (2.2.0)
        internal const string Warboy = "Warboy", WarboyPrefab = "Scraffa";
        internal const float BikeArc = 55f;   // 2.2.1: on a motorcycle a turret sits straight and turns only its upper body this far left / right
        // what the editors offer: a Warboy (blast lances, "Witness me!") or a ranged crew human (a gunner); "None" first, the rest A-Z
        internal static readonly string[] TurretChoices = { "None", "Boltjaw", "Flexa", "Gungirl", "Lugnut", "Scrud", "Shoota", "Sprokka", Warboy, "Bodyguard", "Bolter", "Gunnar", "Lugger", "Mechanic", "Merchant", "Organic_Mechanic", "Pistoleer", "Pup" };
        internal static bool IsGunner(string who) { return !string.IsNullOrEmpty(who) && Array.Exists(PassengerGuard.RangedHumans, n => string.Equals(n, CrewLooks.BasePrefab(who.Trim()), StringComparison.OrdinalIgnoreCase)); }
        // a stored value as the editors show it: "" = None, a ranged human as is, anything else (Warboy, the 2.1.x "Scraffa") = Warboy
        internal static bool IsWarboy(string who)=>string.Equals(who,Warboy,StringComparison.OrdinalIgnoreCase)||string.Equals(who,"Scraffa",StringComparison.OrdinalIgnoreCase);
        internal static string Display(string stored) { return string.IsNullOrEmpty(stored) ? "None" : IsWarboy(stored)?Warboy:stored.Trim(); }
        internal static string Stored(string display) { return string.IsNullOrEmpty(display) || display == "None" ? "" : display; }
        internal static bool IsBikeBody(string body) { return MotorcycleIntegration.IsBody(body) || string.Equals(body, "Halfbreed", StringComparison.OrdinalIgnoreCase); }
        // turret spots a vehicle has: 3 on a car (roof), 1 on a motorcycle, none on a truck
        internal static int Slots(string body, bool truck, bool motorcycleKind)
        {
            if (truck || string.Equals(body, "Rustcargo", StringComparison.OrdinalIgnoreCase)) return 0;
            return motorcycleKind || IsBikeBody(body) ? 1 : 3;
        }

        internal static GameObject Seat(GameObject car, PatrolMarker marker, int slot, string who, float[] localPos, GameObject platform = null,bool standing=false,bool randomize=true)
        {
            if (marker == null || slot < 0 || slot >= marker.Riders.Length) return null;
            if (platform != null) car = platform;
            bool trailer = slot >= PatrolMarker.VehicleTurretSlots;
            bool gunner = IsGunner(who);
            string prefabName = gunner ? who.Trim() : string.Equals(who, Warboy, StringComparison.OrdinalIgnoreCase) ? WarboyPrefab : who.Trim();
            var prefab = Prefabs.FindAny(prefabName);
            if (prefab == null) { Core.Log.Warn("[Patrols] " + "Human turret prefab not found: " + prefabName); return null; }
            bool bike = !trailer && IsBikeBody(TemplateExporter.PrefabName(car.name));
            var anchor = new GameObject("Apocapatrol.RiderPos" + (slot > 0 ? (slot + 1).ToString() : "")).transform;
            anchor.SetParent(car.transform, false);
            anchor.localPosition = localPos != null && localPos.Length == 3 ? new Vector3(localPos[0], localPos[1], localPos[2]) : AutoSpot(car, trailer ? slot - PatrolMarker.VehicleTurretSlots : slot, bike);
            anchor.localRotation = Quaternion.identity;

            var go = UnityEngine.Object.Instantiate(prefab, anchor.position, anchor.rotation);
            SimpleBanditBridge.Mark(go);
            TrafficFactions.Actor(go,marker!=null?marker.Faction:"Scrapyard");
            go.SetActive(true);
            if (gunner) PassengerGuard.Prepare(go, !marker.SetupOnly&&PatrolsModule.RangedCombat.Value);   // setup actors never run combat
            else Crew.MuteAi(go);                  // no mover / detection / attack FSMs: the guard does the throwing
            PassengerGuard.NeutralizeContact(go);  // melee / fire-contact damage FSMs and hitbox off (a passive body that can be shot)
            go.name = prefab.name + "(Rider)";
            CrewLooks.Dress(go, prefabName, randomize);
            // no per-pair IgnoreCollision with the car (thousands of PhysX pair rebuilds per seat): the body is all triggers (NoContacts)
            var root = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
            root.isKinematic = true;
            root.interpolation = RigidbodyInterpolation.None;
            NoContacts(go);
            go.transform.SetParent(anchor, true);
            marker.Riders[slot] = go; marker.RiderAnchors[slot] = anchor;
            var riderGuard = go.AddComponent<RiderGuard>();
            riderGuard.Init(car, marker, anchor, prefab.name, slot, gunner, bike,standing);
            var pose = Pose.Apply(go, anchor, prefab.name);
            if(gunner)PassengerGuard.AttachTurret(go, car, anchor, bike ? BikeArc : 0f);
            pose.ConfigureTurret(standing,bike,IsWarboy(who));
            RiderAnchorGuard.Attach(go, riderGuard, anchor, true);
            MerchantGuards.Attach(car,go,slot);
            PatrolsModule.Verbose("Human turret " + (slot + 1) + ": " + go.name + (gunner ? " (gunner)" : " (Warboy)") + " on " + car.name + " at local " + anchor.localPosition.ToString("F2"));
            return go;
        }

        // A turret body never touches anything physically while it rides - no contact can shove the vehicle (a bike was pushed backwards
        // by its sitter: weapon props the game toggles on drop their per-pair collision ignores). 2.2.2 switched detectCollisions off, which
        // also hid the body from raycasts: shots went through it into the driver. 2.2.3: every collider of the body (inactive props included)
        // is a trigger instead - no contacts, but the game's raycasts hit triggers (QueriesHitTriggers), so it is shot as before. Every
        // Rigidbody kinematic. The body is only ever replaced (a fresh mob jumps off, the game's carcass on death), never made solid again.
        // Seated drivers, passengers and turrets (#133 follow-up): NO physical contact with anything while seated - only bullet
        // hits (vanilla raycasts hit triggers; WW Guns counts triggers on objects with a Bodypart FSM, which every NPC root and head
        // has). Replaces the old per-pair Physics.IgnoreCollision of every occupant collider vs every car collider. Bail-out and
        // death never reuse the seated body (a fresh prefab instance / the game's _Dead ragdoll), so it is never made solid again.
        internal static void NoContacts(GameObject go)
        {
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) rb.isKinematic = true;
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                if (c == null || c is TerrainCollider) continue;
                var mc = c as MeshCollider; if (mc != null && !mc.convex) mc.convex = true;   // a trigger mesh collider must be convex
                c.isTrigger = true;
            }
        }

        // Cheap periodic re-check: only the occupant's own colliders (no car colliders, no pair rebuilds). Reused buffers.
        private static readonly List<Collider> _ncCols = new List<Collider>();
        private static readonly List<Rigidbody> _ncBodies = new List<Rigidbody>();
        internal static void KeepNoContacts(GameObject go)
        {
            if (go == null) return;
            _ncCols.Clear(); go.GetComponentsInChildren(true, _ncCols);
            for (int i = 0; i < _ncCols.Count; i++)
            {
                var c = _ncCols[i];
                if (c == null || c.isTrigger || c is TerrainCollider) continue;
                var mc = c as MeshCollider; if (mc != null && !mc.convex) mc.convex = true;
                c.isTrigger = true;
            }
            _ncCols.Clear();
            _ncBodies.Clear(); go.GetComponentsInChildren(true, _ncBodies);
            for (int i = 0; i < _ncBodies.Count; i++) { var rb = _ncBodies[i]; if (rb != null && !rb.isKinematic) rb.isKinematic = true; }
            _ncBodies.Clear();
        }

        // the automatic spot (feet). Car: turret 1 on the top of the car (the highest point of its renderers, centred), turrets 2 and 3 behind
        // it to the left and right, on whatever roof is under them. Motorcycle: behind the driver on the seat.
        internal static Vector3 AutoSpot(GameObject car, int slot, bool bike)
        {
            var ct = car.transform;
            if (bike)
            {
                var sit = Patrol.FindChild(ct, "sitPos");
                if (sit != null) return ct.InverseTransformPoint(sit.position) + new Vector3(0f, -0.25f, -0.55f);   // the seat behind the driver (feet / seat surface)
            }
            bool any = false; var b = new Bounds();   // frame-local bounds of the renderers
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled || r.GetType().Name == "ParticleSystemRenderer") continue;
                bool occupant = false;
                for (var a = r.transform; a != null && a != ct; a = a.parent)
                    if (a.name.IndexOf("(Driver)", StringComparison.Ordinal) >= 0 || a.name.IndexOf("(Passenger)", StringComparison.Ordinal) >= 0 || a.name.IndexOf("(Rider)", StringComparison.Ordinal) >= 0 || a.name == "PhysicsLock" || a.name=="Apocapatrol.Merchant") { occupant = true; break; }
                if (occupant) continue;
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = ct.InverseTransformPoint(new Vector3((i & 1) == 0 ? wb.min.x : wb.max.x, (i & 2) == 0 ? wb.min.y : wb.max.y, (i & 4) == 0 ? wb.min.z : wb.max.z));
                    if (!any) { b = new Bounds(c, Vector3.zero); any = true; } else b.Encapsulate(c);
                }
            }
            if (!any) return new Vector3(slot == 1 ? -0.4f : slot == 2 ? 0.4f : 0f, 1.2f, slot > 0 ? -0.6f : 0f);
            if (Core.ModApi.Get<Core.IVehicleAttachments>().IsTrailer(car))
            {
                // Use the bed's footprint, excluding the long tow tongue from the four positions.
                var bed = car.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("PhysicsLock", StringComparison.Ordinal));
                var box = bed != null ? bed.GetComponent<BoxCollider>() : null;
                if (box != null)
                {
                    var center = ct.InverseTransformPoint(box.transform.TransformPoint(box.center));
                    var size = box.size;
                    b = new Bounds(new Vector3(center.x, b.center.y, center.z), new Vector3(size.x, b.size.y, size.z));
                }
                float tx = b.center.x + Mathf.Min(0.65f, b.extents.x * 0.5f) * ((slot & 1) == 0 ? -1f : 1f);
                float tz = b.center.z + Mathf.Min(1.2f, b.extents.z * 0.45f) * (slot < 2 ? 1f : -1f);
                return new Vector3(tx, TopAt(car, new Vector3(tx, b.max.y, tz), b.max.y) + 0.02f, tz);
            }
            // the world-AABB corners overestimate a turned car; the top over the middle is still right
            var top = TopAt(car, new Vector3(b.center.x, b.max.y, b.center.z), b.max.y);
            if (slot == 0) return new Vector3(0f, top + 0.02f, b.center.z);
            float x = Mathf.Min(0.45f, b.extents.x * 0.4f) * (slot == 1 ? -1f : 1f), z = b.center.z - Mathf.Min(0.7f, b.extents.z * 0.3f);
            return new Vector3(x, TopAt(car, new Vector3(x, b.max.y, z), top) + 0.02f, z);
        }

        // the highest car surface under a frame-local point (a ray down onto the car's own colliders); fallback when none
        private static readonly RaycastHit[] _downHits = new RaycastHit[32];
        private static float TopAt(GameObject car, Vector3 local, float fallback)
        {
            var ct = car.transform;
            var from = ct.TransformPoint(local) + ct.up * 1.5f;
            int n = Physics.RaycastNonAlloc(from, -ct.up, _downHits, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                var c = _downHits[i].collider;
                if (c == null || !c.transform.IsChildOf(ct)) continue;
                bool occupant = false;
                for (var a = c.transform; a != null && a != ct; a = a.parent) if (a.name=="Apocapatrol.Merchant" || (a.name.IndexOf("(", StringComparison.Ordinal) >= 0 && (a.name.EndsWith("(Driver)", StringComparison.Ordinal) || a.name.EndsWith("(Passenger)", StringComparison.Ordinal) || a.name.EndsWith("(Rider)", StringComparison.Ordinal)))) { occupant = true; break; }
                if (occupant) continue;
                float y = ct.InverseTransformPoint(_downHits[i].point).y;
                if (y > best) best = y;
            }
            return best > float.MinValue ? best : fallback;
        }

        // "Witness me!" - Sounds/witness-me.wav (16-bit PCM, normalised to the game's human_hurt voice: -23.8 LUFS, peak -5.6 dBTP) as a
        // 3D voice on the rider
        private static readonly List<AudioClip> _screams = new List<AudioClip>(4); private static bool _screamLooked;
        // plays the scream; returns its length (1.5 s when the wav is missing: the rider still pauses before leaping)
        internal static float Scream(Transform at)
        {
            try
            {
                if (!_screamLooked)
                {
                    _screamLooked = true;
                    for (int i = 1; i <= 4; i++)
                    {
                        string clipName = i == 1 ? "witness-me" : "witness-me" + i;
                        var path = System.IO.Path.Combine(PatrolsModule.DataDir, "Sounds", clipName + ".wav");
                        var clip = WildWasteland.Core.Audio.TryLoadWav(path, clipName, 1.8f);   // +5 dB over the normalised file (peak about -0.5 dBFS)
                        if (clip != null) _screams.Add(clip);
                        else Core.Log.Warn("[Patrols] " + "Rider: " + path + " not found or not a PCM wav - skipped");
                    }
                }
                if (_screams.Count == 0 || at == null) return 1.5f;
                var scream = _screams[UnityEngine.Random.Range(0, _screams.Count)];
                var go = new GameObject("Apocapatrol.WitnessMe");
                go.transform.SetParent(at, false); go.transform.localPosition = Vector3.up * 1.6f;
                var src = go.AddComponent<AudioSource>();
                // loud and far: full volume within 35 m, still clearly heard at 200+ m (mostly 3D, a little 2D so distance doesn't swallow it)
                src.clip = scream; src.spatialBlend = 0.8f; src.rolloffMode = AudioRolloffMode.Logarithmic; src.minDistance = 35f; src.maxDistance = 400f;
                src.volume = 1f; src.priority = 64; src.dopplerLevel = 0f;
                src.Play();
                UnityEngine.Object.Destroy(go, scream.length + 0.2f);
                if (Core.Log.Verbose) PatrolsModule.Verbose("Rider: scream " + scream.name);
                return scream.length;
            }
            catch (Exception e) { PatrolsModule.Verbose("Rider: scream: " + e.Message); return 1.5f; }
        }

        internal static Vector3 ParseV3(string s, Vector3 def)
        {
            try
            {
                var p = (s ?? "").Split(',');
                if (p.Length != 3) return def;
                return new Vector3(float.Parse(p[0].Trim(), System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[1].Trim(), System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[2].Trim(), System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception) { return def; }
        }
        internal static string FormatV3(Vector3 v) { return v.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", " + v.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", " + v.z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
    }

    internal sealed partial class RiderGuard : MonoBehaviour
    {
        private enum Mode { Riding, Throwing, Witness, Flying, Dismount, Done }
        private GameObject _car; private PatrolMarker _marker; private Transform _anchor; private string _prefab;
        private PlayMakerFSM _health; private FsmFloat _healthVar; private float _maxHealth = -1f;
        private Rigidbody _carRb;
        private Animator _anim;
        private PlayableGraph _graph; private bool _graphOk;
        private AnimationMixerPlayable _mixer; private AnimationClipPlayable _idle, _throw, _jump;
        private float _throwLen = 1.2f, _jumpLen = 1f;
        private object _body;   // 2.6.1: Apocaplayer's ModAPI character (PlayerAnims) - the player's crouch / throw / jump; null = the clips graph below
        private readonly float[] _w = new float[4];
        private Mode _mode = Mode.Riding;
        private int _slot; private bool _gunner, _bike;   // 2.2.0: turret spot 0..2, a gunner (PassengerGuard shoots, Pose crouches / seats), on a motorcycle
        private bool _standing;
        internal bool StandingPose=>_standing||(_bike&&_standAt>=0f);
        internal bool AnimatedPose=>(_body!=null&&PlayerAnims.Alive(_body))||_graphOk;
        internal void MaxHealth(float health){_maxHealth=health;}
        private float _standAt = -1f;                      // a Warboy on a bike stands up (crouch -> stand blend) before the leap
        private float _nextIgnore, _outOfArc;
        private bool _warboyPrefab;                        // Perf: the prefab compare once at Init (was per frame)
        private PatrolMarker _carMarker; private float _nextTrailerCheck; private bool _trailerConnected;
        internal bool IsWarboy { get { return !_gunner && _warboyPrefab && (_mode == Mode.Riding || _mode == Mode.Throwing || _mode == Mode.Witness); } }
        internal bool Mounted { get { return (_mode == Mode.Riding || _mode == Mode.Throwing || _mode == Mode.Witness) && Alive(); } }
        private AnimationClipPlayable _stand;
        private bool _released, _inClose, _kamikazeArmed;
        private float _nextWitness;
        private Pilot _pilot;
        private float _modeStart, _nextThrow, _nextHide, _lanceBackAt;
        private Vector3 _vel;                       // flight velocity (witness jump / dismount hop)
        private float _side = 1f;
        private Transform _hand; internal Transform Lance;   // the visual lance in the right hand (RiderApi moves it)
        private readonly List<Renderer> _ownWeapons = new List<Renderer>();
        private Collider[] _carCols = new Collider[0], _myCols = new Collider[0];
        private static readonly RaycastHit[] _hits = new RaycastHit[16];
        private const float ReleaseFraction = 0.42f;   // where in the Throw clip the lance leaves the hand
        private const float StandSeconds = 0.5f;       // a Warboy on a motorcycle: crouch -> stand before the leap
        private const float AfterScream = 0.35f;       // pause between the end of "Witness me!" and the leap, s - the player's chance to get away
        private float _windUp = 1.85f;

        internal void Init(GameObject car, PatrolMarker marker, Transform anchor, string prefab, int slot, bool gunner, bool bike,bool standing)
        {
            _car = car; _marker = marker; _anchor = anchor; _prefab = prefab; _slot = slot; _gunner = gunner; _bike = bike;
            _warboyPrefab = string.Equals(prefab,Rider.WarboyPrefab,StringComparison.OrdinalIgnoreCase); _carMarker = car.GetComponent<PatrolMarker>(); _nextTrailerCheck = 0f;
            _standing=standing;
            _carRb = car.GetComponent<Rigidbody>();
            _health = GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Health");
            _nextThrow = Time.time + UnityEngine.Random.Range(2f, 4f);
            _anim = GetComponentInChildren<Animator>();
            var lHand = Rider.Bone(transform, "LeftHand"); _hand = Rider.Bone(transform, "RightHand") ?? lHand;
            foreach (var h in new[] { lHand, _hand }) if (h != null) foreach (var r in h.GetComponentsInChildren<Renderer>(true)) _ownWeapons.Add(r);
            _carCols = car.GetComponentsInChildren<Collider>(true);
            _myCols = GetComponentsInChildren<Collider>(true);
            _side = Vector3.Dot(anchor.position - car.transform.position, car.transform.right) >= 0f ? 1f : (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            if (_gunner||!_warboyPrefab) return;
            HideOwnWeapons();
            BuildGraph();
            BuildLance();
        }

        private float Health()
        {
            if (_health == null || !_health.Fsm.Initialized) return -1f;
            if (_healthVar == null) _healthVar = _health.FsmVariables.GetFsmFloat("Health");
            if (_healthVar == null) return -1f;
            if (_maxHealth < 0f || _healthVar.Value > _maxHealth) _maxHealth = _healthVar.Value;
            return _healthVar.Value;
        }
        private bool Alive() { float h = Health(); return h < 0f || h > 0f; }
        private bool Wounded() { float h = Health(); return h > 0f && _maxHealth > 0f && h < _maxHealth - 0.5f; }

        private Vector3 HandPos() { return _hand != null ? _hand.position : transform.position + Vector3.up * 1.2f; }
        private Vector3 CarVel() { return _carRb != null ? _carRb.velocity : Vector3.zero; }

        private void IgnoreOwn(GameObject go)
        {
            var mine = go.GetComponentsInChildren<Collider>(true);
            foreach (var c in _carCols) foreach (var p in mine) if (c != null && p != null) Physics.IgnoreCollision(p, c, true);
        }

        // leaves the roof anchor: free kinematic body, no more collisions to ignore, the lance prop stays in the hand
        private void Unseat()
        {
            if (_marker != null) _marker.DropRider(gameObject);   // the car no longer carries it (Explode won't bail it again)
            transform.SetParent(null, true);
            var up = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).sqrMagnitude > 1e-4f ? Vector3.ProjectOnPlane(transform.forward, Vector3.up) : Vector3.forward, Vector3.up);
            transform.rotation = up;
        }

        private void Die()
        {
            _mode = Mode.Done;
            if (_graphOk) { _graph.Destroy(); _graphOk = false; }   // the ragdoll / death takes over
            ReleaseBody();
            var pose = GetComponent<Pose>(); if (pose != null) pose.ReleaseBody();   // a gunner: its gun back in its own hand
            if (Lance != null) Destroy(Lance.gameObject);
            if (_marker != null) _marker.DropRider(gameObject);
        }

        private void OnDestroy()
        {
            if (_mode == Mode.Flying && gameObject.scene.isLoaded && Time.timeScale > 0f) { _mode = Mode.Done; Rider.Kaboom(transform.position + Vector3.up * 0.9f, name); }   // removed mid-air by something else: still goes off
            if (_graphOk) { try { _graph.Destroy(); } catch (Exception) { } _graphOk = false; }
            ReleaseBody();
            if (Lance != null) Destroy(Lance.gameObject);
        }

        private void ReleaseBody()
        {
            if (_body == null) return;
            try { PlayerAnims.Dispose(_body); } catch (Exception) { }
            _body = null;
        }
    }

}
