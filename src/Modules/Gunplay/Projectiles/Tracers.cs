using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    // Visible bullets for every gun-wielding human (raiders, Coyotes, traders' guards ...).
    //
    // Vanilla (asset dump 2026-10-01, FSM "Damage Ranged", same on every shooter): state trigger = sound + muzzle flash, aim the
    // child AttackRaycast_Ranged (RaySensor, Length 80, DetectsOn layers 6+10, ObstructedBy 0/8/9/11/14) at detectedObj + a random
    // {offset}, then SensorGetDetectionRayHit: target reached -> state attack (CreateObject RangedHit_Effect at {hitPoint},
    // SetFsmFloat <target>/Bodypart.Damage = damage, SendEvent Damage to the target, Wait rpm) else -> shoot (Wait rpm, nothing).
    //
    // Here: a Harmony prefix on SensorGetDetectionRayHit.OnEnter, only inside "Damage Ranged" of an owner holding a gun or a crossbow,
    // fires a projectile from the gun's fire_effect point toward the same aimed point and sends the FSM down the miss branch, so the
    // burst rhythm stays vanilla. The projectile reproduces the attack state when it reaches the target. Monsters with a
    // "Damage Ranged" FSM (no gun model) keep the vanilla path.
    //
    // Cost: projectiles are structs in one list; each one costs one short RaycastNonAlloc per frame (the distance it moved); all lines
    // are one camera-facing mesh = one draw call with an unlit material (Sprites/Default: no lighting, same look day and night).
    // Above [Tracers] MaxTracers live projectiles a shot is resolved at once (hitscan) instead, so no damage is ever lost.
    internal static partial class Tracers
    {
        internal enum Kind { Pistol, Smg, Rifle, Sniper, Shotgun, Crossbow }
        /// <summary>Crossbow damage multiplier (read by the Apocaspawner tooltip bridge); the value lives in the weapon catalog now.</summary>
        [Obsolete("refactor: use ModApi IWeaponStats.Get(handle, WeaponStat.DamageMultiplier)")]
        internal const float CrossbowDamageMultiplier = 2.5f;

        private struct Shot
        {
            public Vector3 Pos, Dir, Start;
            public float Speed, Range, Damage, Travelled;
            public GameObject Target, ShooterRoot;
            public GameObject Impact;
            public Transform Visual;
            public string EventName, EventFsm;
            public bool Bolt, Shotgun, Alive;
            public int Layers, DetectLayers;
            public PlayerGun Gun;            // non-null: the player's shot (first collider it meets is hit, like the vanilla camera ray)
            public GameObject Player;
            public GameObject Head;          // the target's head object (own Bodypart FSM), for head hits
            public CapsuleCollider Cap;      // the player's body capsule (virtual hitbox) - only for shots at the Player
            public Kind Kind;                // (1.5.0) the gun type: how well it penetrates car parts
            public float DmgMult;            // damage left after the car parts it went through (1 = none)
            public int Passed, Passed0, Passed1;   // car parts gone through (at most 2) and their instance ids
            public bool Orb;                 // (1.1.0) a monster's projectile (Orbs.cs): penetrates like a pistol bullet
            // (WW perf, D2) fixed-rate flight: Pos is simulated up to Ahead seconds past the frame time, Prev = Pos before the last step of
            // StepLen seconds; the drawn bullet is between them (RenderOf)
            public float Ahead, StepLen;
            public Vector3 Prev;
            public long Id;                  // session-unique projectile id (events)
            public bool Hit;                 // ended by a hit (ProjectileEnded)
        }

        // A first-person gun under PlayerCamera/WeaponsArm/Parent/<gun>: its Attack FSM, the Raycast it fires and the states that
        // handle a hit (getLayer / actorHitSound / hit), replayed when the bullet lands.
        private sealed class PlayerGun
        {
            public Fsm Fsm;
            public Kind Kind;
            public Transform Muzzle;
            public int Pellets = 1;
            public int Handle = -1;          // weapon catalog handle
            public float PelletSpread;
            public float RangeMultiplier = 1f, DamageMultiplier = 1f;
            public FsmFloat Damage;          // the hit state's Bodypart.Damage value (per bullet / pellet)
            public int Layers;
            public FsmStateAction[] GetLayer, ActorHit, Hit;
        }

        private sealed class ShooterInfo
        {
            public Transform Muzzle, Weapon;
            public Vector3 MuzzleLocal;
            public GameObject Owner;
            public Kind Kind;
            public bool IsGun;
            public int ObstructLayers = 19201, DetectLayers = 1088;
            public GameObject Impact;
            public FsmFloat DamageVar;      // the attack state's SetFsmFloat.setValue (usually {Damage Value})
            public float DamageLiteral;
            public float LeadSkill = 1f;    // this NPC's share of [NpcAim] LeadAccuracy (0.5..1, rolled once)
            public string EventName = "Damage";
            public string EventFsm;         // the vanilla SendEvent's named FSM (null = every FSM on the object, as PlayMaker broadcasts)
            public string BodypartFsm = "Bodypart", BodypartVar = "Damage";
            public int Handle = -1;         // weapon catalog handle of the held gun
            public float Retry; public int Tries;   // no weapon found: look again after Retry (the gun model may be inactive for a frame), 10 tries at most
        }

        private static readonly List<Shot> _shots = new List<Shot>(256);
        private static readonly Dictionary<int, ShooterInfo> _shooters = new Dictionary<int, ShooterInfo>();
        private static readonly RaycastHit[] _hits = new RaycastHit[64];
        private static readonly List<KeyValuePair<Rigidbody, Vector3>> _pushes = new List<KeyValuePair<Rigidbody, Vector3>>();
        private static readonly List<KeyValuePair<GameObject, Vector3>> _popped = new List<KeyValuePair<GameObject, Vector3>>();
        private static int _poppedFrame;
        private static readonly HashSet<GameObject> _forceFree = new HashSet<GameObject>();   // popped wheels: freed by hand if CheckTag doesn't
        private static int _playerMuzzleLogs;

        public static void OnSceneLoaded()
        {
            ClearBolts();
            _shots.Clear();
            _shooters.Clear();
            _counts.Clear();
            _rangedHit = null; _metalImpact = null; _effectsLooked = false;
            _carRoots.Clear();
            _headMul.Clear(); 
            _reported.Clear();
            _guns.Clear();
            _notGun.Clear();
            _heads.Clear();
            _player = null;
            _playerMuzzleLogs = 0;
            _playerTracked = false;
            _orbs.Clear();
            _pushes.Clear();
            _popped.Clear(); _forceFree.Clear(); _meleeWheels.Clear(); _isMelee.Clear();
            // (WW) also the fitted-wheel swing state (was kept across scenes; keyed by destroyed objects' ids)
            _wSwing = null; _wDone = false; _wPrev.Clear(); _wDamage.Clear(); _wAttack.Clear();
        }

        // (WW) Init: every static back to its initial value, so the module starts again cleanly after Shutdown
        internal static void ResetAll()
        {
            OnSceneLoaded();
            _nextFind = 0f; _nextSweep = 0f; _poppedFrame = 0;
            _playerVel = Vector3.zero; _playerLast = Vector3.zero;
            _drawnEmpty = false; _drawnPaused = false;
            Snapshot();
        }

        // (WW) module off: bullets in flight vanish, created objects are destroyed (the vanilla TranslatePosition moves orbs again)
        internal static void Shutdown()
        {
            OnSceneLoaded();
            if (_drawGo != null) UnityEngine.Object.Destroy(_drawGo);
            if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
            if (_mat != null) UnityEngine.Object.Destroy(_mat);
            _drawGo = null; _mesh = null; _mat = null; _drawnEmpty = false; _drawnPaused = false;
            if (_stab != null) UnityEngine.Object.Destroy(_stab);
            _stab = null; _stabTried = false;
        }

        // (WW) FloatingOrigin jumped: bullets in flight move with the world (Gunplay did not handle this; they jumped 1000 m)
        internal static void OriginShift(Vector3 delta)
        {
            for (int i = 0; i < _shots.Count; i++) { var s = _shots[i]; s.Pos += delta; s.Start += delta; s.Prev += delta; _shots[i] = s; }
            foreach (var b in _landedBolts) if (b.Visual != null && b.Visual.parent == null) b.Visual.position += delta;
            _playerLast += delta;
        }

        // F7 live info
        internal static int InFlight { get { return _shots.Count; } }
        internal static int OrbCount { get { return _orbs.Count; } }
        internal static int ShooterCount { get { return _shooters.Count; } }
        internal static int PlayerGunCount { get { return _guns.Count; } }

        // ---------- simulation ----------
        // settings read once per frame (not per bullet)
        private static float _npcRadius, _fullUntil, _headMult, _bodyR, _headR;
        private static float _nextSweep;

        private static void Snapshot()
        {
            _npcRadius = Mathf.Max(0f, GunplaySettings.NpcHitRadius.Value);
            _fullUntil = Mathf.Clamp(GunplaySettings.FullDamageUntil.Value, 0f, 99f) / 100f;
            _headMult = Mathf.Max(0f, GunplaySettings.HeadshotMultiplier.Value);
            _bodyR = Mathf.Max(0.03f, GunplaySettings.PlayerBodyRadius.Value);
            _headR = Mathf.Max(0.03f, GunplaySettings.PlayerHeadRadius.Value);
        }

        public static void Tick()
        {
            float dt = Time.deltaTime;
            TickBolts();
            Snapshot();
            if (GunplaySettings.MeleeOnWheels.Value)                          // (WW) [Guns] MeleeOnWheels
                try { SwingAtFittedWheels(); } catch (Exception e) { Core.Log.Error("[Guns] Tracers melee (fitted wheels)", e); }
            if (_meleeWheels.Count > 0) { try { CheckMeleeWheels(); } catch (Exception e) { Core.Log.Error("[Guns] Tracers melee", e); _meleeWheels.Clear(); } }
            if (dt <= 0f) { if (!_drawnPaused) { Draw(); _drawnPaused = true; } return; }   // paused: the mesh is drawn once and left as it is
            _drawnPaused = false;
            if (GunplaySettings.Lead) TrackPlayer(dt);
            // (WW perf, D2) bullets fly in fixed 1/60 s steps instead of one cast per bullet per frame: a bullet is stepped only once its
            // simulated time is used up, then by whole ticks covering this frame in ONE swept cast (below 60 fps that is one step per frame of
            // the same length as before; above it, ~60 casts a second whatever the fps). The path swept is the same; a hit can land up to
            // 1/60 s before the frame that would have reached it. Drawn per frame between the last two simulated points.
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var s = _shots[i];
                s.Ahead -= dt;
                if (s.Ahead <= 0f)
                {
                    int ticks = Mathf.Max(1, Mathf.CeilToInt(-s.Ahead * SimHz - 1e-3f));
                    float st = ticks / SimHz;
                    s.Prev = s.Pos; s.StepLen = st; s.Ahead += st;
                    try { Step(ref s, st); }
                    catch (Exception e) { s.Alive = false; Core.Log.Error("[Guns] Tracers: bullet dropped", e); }
                }
                if (s.Alive) { Vector3 rp; float rt; RenderOf(ref s, out rp, out rt); MoveBoltAt(ref s, rp); _shots[i] = s; }
                else { if (s.Visual != null) UnityEngine.Object.Destroy(s.Visual.gameObject); CombatEvents.Ended(s.Id, s.Pos, s.Hit); _shots.RemoveAt(i); }
            }
            if (Time.unscaledTime >= _nextSweep) { _nextSweep = Time.unscaledTime + 30f; Sweep(); }
            if (_pushes.Count > 0)
            {
                foreach (var p in _pushes) if (p.Key != null) p.Key.AddForce(p.Value, ForceMode.VelocityChange);
                _pushes.Clear();
            }
            if (_popped.Count > 0 && Time.frameCount >= _poppedFrame)
            {
                // CheckTag adds the Rigidbody a frame or two after de_Attach
                for (int i = _popped.Count - 1; i >= 0; i--)
                {
                    var go = _popped[i].Key;
                    var rb = go != null ? go.GetComponent<Rigidbody>() : null;
                    if (rb != null && go.transform.parent == null) { rb.AddForce(_popped[i].Value, ForceMode.VelocityChange); _popped.RemoveAt(i); _forceFree.Remove(go); }
                    else if (go == null) { _popped.RemoveAt(i); _forceFree.RemoveWhere(IsNull); }
                    else if (Time.frameCount > _poppedFrame + 30 && !_forceFree.Contains(go)) _popped.RemoveAt(i);
                    else if (Time.frameCount > _poppedFrame + 30)
                    {
                        _forceFree.Remove(go);
                        // the part's CheckTag didn't free it: do it the way it would
                        go.transform.SetParent(null, true);
                        if (rb == null) rb = go.AddComponent<Rigidbody>();
                        rb.isKinematic = false;
                        rb.AddForce(_popped[i].Value, ForceMode.VelocityChange);
                        _popped.RemoveAt(i);
                    }
                }
            }
            Draw();
        }

        // caches keyed by dead NPCs would otherwise hold their objects until the next scene load
        private static readonly List<int> _dead = new List<int>();
        private static void Sweep()
        {
            _dead.Clear();
            foreach (var kv in _shooters) if (kv.Value.Owner == null) _dead.Add(kv.Key);
            foreach (var k in _dead) _shooters.Remove(k);
            _dead.Clear();
            foreach (var kv in _heads) if (kv.Value == null) _dead.Add(kv.Key);
            foreach (var k in _dead) _heads.Remove(k);
            _dead.Clear();
            ActorQuery.Sweep();
            // instance-id keyed yes/no caches: cheap to rebuild, so they are simply emptied every sweep instead of growing all session
            _counts.Clear(); _headMul.Clear(); _carRoots.Clear(); _isMelee.Clear(); 
            _forceFree.RemoveWhere(IsNull);
            SweepOrbs();
            // (WW) swing caches keyed by weapon / FSM instance ids
            if (_wAttack.Count > 64) _wAttack.Clear();
            if (_wPrev.Count > 64) _wPrev.Clear();
            if (_wDamage.Count > 64) _wDamage.Clear();
        }

        private static readonly Predicate<GameObject> IsNull = g => g == null;

        // ---------- drawing: one mesh, camera-facing quads ----------
        private static GameObject _drawGo;
        private static Mesh _mesh;
        private static Material _mat;
        private static readonly List<Vector3> _v = new List<Vector3>();
        private static readonly List<Color> _c = new List<Color>();
        private static readonly List<int> _t = new List<int>();

        private static bool _drawnEmpty, _drawnPaused;
        private static void Draw()
        {
            if (_shots.Count == 0 && _drawnEmpty) return;      // nothing to draw and the mesh is already empty
            _drawnEmpty = _shots.Count == 0;
            if (_drawGo == null)
            {
                _drawGo = new GameObject("WildWasteland.Guns.Tracers") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(_drawGo);
                _mesh = new Mesh { name = "Tracers" };
                _mesh.MarkDynamic();
                _drawGo.AddComponent<MeshFilter>().sharedMesh = _mesh;
                var mr = _drawGo.AddComponent<MeshRenderer>();
                var sh = Shader.Find("Sprites/Default");
                _mat = new Material(sh) { name = "TracerLine", renderQueue = 3100 };
                mr.sharedMaterial = _mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            float glow = Mathf.Max(0f, GunplaySettings.TracerGlow.Value);
            _mat.color = new Color(glow, glow, glow, 1f);

            _v.Clear(); _c.Clear(); _t.Clear();
            var cam = GameRefs.MainCamera;
            if (cam != null && _shots.Count > 0)
            {
                Vector3 eye = cam.transform.position;
                Color tc = GunplaySettings.TracerColor.Value, bc = GunplaySettings.BoltColor.Value;
                float tw = GunplaySettings.TracerWidth.Value, tl = GunplaySettings.TracerLength.Value;
                float bw = GunplaySettings.BoltWidth.Value, bl = GunplaySettings.BoltLength.Value;
                bool tracers = GunplaySettings.TracersDraw.Value, mine = GunplaySettings.PlayerGunTracers.Value;
                foreach (var s in _shots)
                {
                    if (s.Bolt) continue;
                    if (!tracers && !mine && !s.Bolt) continue;
                    if (!s.Bolt && (!tracers || (s.Gun != null && !mine))) continue;      // [Gunplay] Tracers / PlayerGunTracers (bolts always show)
                    var sr = s; Vector3 rp; float rt; RenderOf(ref sr, out rp, out rt);   // (WW perf, D2) the drawn point of this frame
                    float len = Mathf.Min(s.Bolt ? bl : tl, rt);
                    if (len <= 0.01f) continue;
                    Vector3 head = rp, tail = rp - s.Dir * len;
                    Vector3 side = Vector3.Cross(s.Dir, (head - eye).normalized);
                    if (side.sqrMagnitude < 1e-6f) continue;
                    side = side.normalized * ((s.Bolt ? bw : tw) * 0.5f);
                    Color head_c = s.Bolt ? bc : tc, tail_c = head_c;
                    tail_c.a = s.Bolt ? head_c.a : 0f;           // tracers fade toward the tail, bolts are solid
                    int b = _v.Count;
                    _v.Add(tail - side); _v.Add(tail + side); _v.Add(head + side); _v.Add(head - side);
                    _c.Add(tail_c); _c.Add(tail_c); _c.Add(head_c); _c.Add(head_c);
                    _t.Add(b); _t.Add(b + 1); _t.Add(b + 2); _t.Add(b); _t.Add(b + 2); _t.Add(b + 3);
                }
            }
            _mesh.Clear();
            if (_v.Count > 0)
            {
                _mesh.SetVertices(_v);
                _mesh.SetColors(_c);
                _mesh.SetTriangles(_t, 0);
                _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            }
        }
    }
}
