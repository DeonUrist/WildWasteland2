using System;
using System.Collections.Generic;

namespace WildWasteland.Modules.PlayerBody
{
    // (2.2.0) ModAPI: the animation decisions for ANY humanoid that another mod animates with the player's clips (ModAPI.Character), frame by frame,
    // with no Unity types - tools/modapitest runs it for every weapon kind x every situation outside the game and checks / draws the result.
    // The player and NPCs supply inputs to this one decision model: locomotion, turns, jumps, reloads, pumps, throws and strikes. Every clip
    // time is set explicitly here (the playables run at speed 0), so the plan alone decides what is shown.
    internal sealed class CharPlan
    {
        // ---- the clips the player's body plays (Loco.cs / Body.cs) - the only ones ModAPI offers: the legs (LocoPlan's 42 slots with their
        // stand-ins), the hands, the whole-body actions, throws and strikes. The bundle's other clips (death poses ...) are left out on purpose.
        public static readonly string[] Uppers = { "RifleAim", "RifleFire", "RifleCrouchAim", "RifleCrouchFire", "RifleReload", "RifleIdleLow", "RifleIdle", "RifleRunLow", "RifleRun",
                                                   "RifleWalkLow", "RifleWalk", "RifleCrouchStrafeRight", "ShotgunPump", "PistolIdle", "PistolRun", "PistolFire", "PistolReload",
                                                   "Idle", "Walk", "WalkBack", "Run", "CrouchIdle" };
        public static readonly string[] Actions = { "Kick", "Jump", "PistolJump", "RifleJumpUp", "RifleJumpLoop", "RifleJumpDown", "LeftTurn", "RightTurn",
                                                    "RifleTurnLeft", "RifleTurnRight", "RifleCrouchTurnLeft", "RifleCrouchTurnRight", "RifleWalkToStop" };
        public static readonly string[] Hands = { "Throw", "ThrowRight", "Punch", "Punch1", "Punch2", "Melee", "MeleeCombo" };
        /// <summary>Every clip name the player set uses (locomotion slots, hands, actions, strikes) that the bundle has.</summary>
        public static List<string> PlayerClips(Func<string, bool> has)
        {
            var l = new List<string>();
            for (int i = 0; i < LocoPlan.BN; i++)
            {
                bool rev; string n = LocoPlan.Resolve(i, has, out rev);
                if (n != null && !l.Contains(n)) l.Add(n);
            }
            foreach (var group in new[] { Uppers, Actions, Hands })
                foreach (var n in group) if (has(n) && !l.Contains(n)) l.Add(n);
            l.Sort(StringComparer.Ordinal);
            return l;
        }

        // ---- what the caller (ModAPI.Character) gives every frame
        public float Lx, Lz;                 // planar velocity in the character's own frame, m/s (x = its right, z = its forward)
        public float Yaw;                    // its facing, degrees about up
        public int Kind;                     // LocoPlan.K_*
        public bool Crouch, Aim, Fire, Airborne;
        public bool TurnInPlace = true, WalkToStop = true;
        // Optional client inputs. NPC callers leave these unset and retain timed command behavior.
        public bool FollowNativeReload, NativeReload, Seated;
        public float NativeReloadFraction = -1f;
        public string SeatedHandsClip, Preview;
        private bool _nativeReloadWas;
        private string _meleeClip = "Melee";
        /// <summary>Current strike timing, shared by the graph and the player's hit-alignment adapter.</summary>
        internal string StrikeClip { get { return _striking ? _strikeClip : null; } }
        /// <summary>Native-clock start of the active shared strike.</summary>
        internal float StrikeAt { get { return _strikeAt; } }
        /// <summary>Duration of the active shared strike.</summary>
        internal float StrikeCycle { get { return _strikeCycle; } }
        /// <summary>Clip-space hit point used by player aim alignment.</summary>
        internal float StrikeHit { get { return _strikeHit; } }
        internal string StrikeHand { get; private set; }
        /// <summary>True while the first whole-clip melee swing is active.</summary>
        internal bool MeleePlaying { get { return _now < _meleeUntil; } }
        /// <summary>Start of the accepted throw on the shared clock.</summary>
        internal float ThrowAt { get { return _throwAt; } }
        /// <summary>End of the accepted throw, retained across weapon changes.</summary>
        internal float ThrowUntil { get { return _throwUntil; } }
        /// <summary>Native release lead in clip seconds.</summary>
        internal float ThrowFrom { get { return _throwFrom; } }
        /// <summary>Clip selected by the shared throw command.</summary>
        internal string ThrowClip { get { return _throwClip; } }

        // ---- the clips (from the bundle in the game, from the FBX files in the test)
        public Func<string, bool> Has;
        public Func<string, float> Length;           // seconds
        public Func<string, bool> Looping;
        public Func<string, float> PhaseOf;          // stride phase (left foot highest), -1 = none
        public Func<string, float, float> NativeOf;  // ground speed at which the clip plays at 1x (its root motion), else the fallback

        // ---- settings (the player's, read from Apocaplayer's config by the Character)
        public float RunFrom = 3.2f, SprintFrom = 4.3f, DirBlendSpeed = 360f, RunLegsTurn = 75f, UpperPhase = 0f, PumpSpeed = 2f, StrikeWindup = 0.08f;
        public float WalkNative = 1.4f, RunNative = 3.8f, SprintNative = 5.5f, CrouchNative = 1.0f, JumpClipStart = 0.2f, TurnStartRate = 8f;

        // ---- results (read by the Character after Step)
        public readonly string[] SlotClip = new string[LocoPlan.BN];
        public readonly bool[] SlotReverse = new bool[LocoPlan.BN];
        public readonly float[] W = new float[LocoPlan.BN];          // base weights
        public readonly float[] SlotTime = new float[LocoPlan.BN];   // seconds
        public float HipTurn;                                         // degrees, the hips about up (relaxed running)
        // whole-body action layer (kick, jumps, turns in place, walk-to-stop)
        public string ActionClip = ""; public float ActionTime, ActionW; public bool ActionIsTurn;
        // upper-body layer (strikes, throws): the current clip and the one fading out under it
        public string UpperClip = "", UpperOld; public float UpperTime, UpperOldTime, UpperX = 1f, UpperW;
        // the upper source (UpperRig: what the hands do) and the one fading out under it; RigEff = the weight applied this frame
        public string RigClip = "", RigOld; public float RigTime, RigOldTime, RigX = 1f, RigW, RigEff;
        public float ChestFollow;                                     // LocoPlan.Twist share (bare hands crouch-walking)
        public float RigWant { get { return _rigWant; } }               // 1 = a clip moves the hands (the hips' counter-turn is left to it)
        public float AimLiftW; public string AimLiftClip; public bool AimCrouched;
        public readonly Dictionary<string, float> PoseW = new Dictionary<string, float>();   // the clips moving the hands, by weight (weapon poses)
        public float CrouchW, RelaxW, M, RunW, SprintW, SpeedSmooth;
        /// <summary>A reload is running (at the last step's time).</summary>
        public bool Reloading { get { return _now < _reloadUntil; } }
        /// <summary>A reload runs at the given time.</summary>
        public bool ReloadingAt(float now) { return now < _reloadUntil; }
        /// <summary>A pump / bolt cocking is running.</summary>
        public bool Pumping { get { return _now < _pumpUntil; } }
        public string Log = "";                                       // what changed this frame (tests / verbose)
        /// <summary>(B05 perf) build <see cref="Log"/> only while true (set by the driver from Debug log level 2); no strings otherwise.</summary>
        public bool Logging;

        // ---- state
        private float _now, _stride, _moveAng, _rigSpeed = 1f;
        private bool _snap = true, _noCrouchClips, _hasSprint, _rigHold, _rigSync, _rigManualOn;
        private float _rigWant, _rigManual;
        private float _actionUntil, _actionFade = 6f, _actionSpeed = 1f;
        private float _upperWTarget;
        private float _fireUntil, _reloadStart, _reloadUntil, _reloadLen; private int _reloadRounds;
        private float _pumpAt = -10f, _pumpUntil; private bool _pumpStart;
        private bool _wantJump, _wantKick; private string _jumpPhase = ""; private float _jumpAt = -10f; private bool _wasAir;
        private bool _turnInit, _turnSettling; private float _turnLastYaw, _turnDeg, _turnTotal = 90f, _turnMovedAt; private float[] _turnCurve; private string _turnClip;
        private float _walkedAt = -10f, _stopAt = -10f;
        private string _throwClip = "Throw"; private float _throwAt = -10f, _throwFrom, _throwUntil;
        private bool _striking; private float _strikeAt, _strikeCycle = 0.4f, _strikeEnd = -10f, _strikeW, _strikeFrom, _strikeHit, _strikeTo; private string _strikeClip; private int _seqN, _punchN, _chainN;
        private float _meleeUntil, _meleeSpeed = 1f, _meleeAt;
        private int _lastKind = -1;
        private bool _built;

        public const float RigCross = 0.15f, UpperIn = 0.08f, UpperOut = 0.15f, UpperCross = 0.12f;
        public const float ThrowRelease = 0.85f;

        /// <summary>Clamps to 0..1 (Unity-free).</summary>
        private static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }
        /// <summary>Clamps to a..b (Unity-free).</summary>
        private static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }
        /// <summary>Moves c toward t by at most d (Unity-free).</summary>
        private static float MoveTowards(float c, float t, float d) { return Math.Abs(t - c) <= d ? t : c + Math.Sign(t - c) * d; }
        /// <summary>Clamped linear interpolation (Unity-free).</summary>
        private static float Lerp(float a, float b, float t) { t = Clamp01(t); return a + (b - a) * t; }
        /// <summary>Shortest signed angle from a to b, degrees (Unity-free).</summary>
        private static float DeltaAngle(float a, float b) { float d = (b - a) % 360f; if (d > 180f) d -= 360f; if (d < -180f) d += 360f; return d; }
        /// <summary>Length of a clip, 1 s when unknown.</summary>
        private float Len(string c) { float l = c != null && Has(c) ? Length(c) : 0f; return l > 0.01f ? l : 1f; }
        /// <summary>True for a looping clip.</summary>
        private bool Loops(string c) { return c != null && Looping != null && Looping(c); }

        /// <summary>the base slots' clips (with the bundle's stand-ins) - once</summary>
        public void Build()
        {
            if (_built) return;
            _built = true;
            for (int i = 0; i < LocoPlan.BN; i++)
            {
                bool rev;
                SlotClip[i] = LocoPlan.Resolve(i, Has, out rev) ?? "RifleIdle";
                SlotReverse[i] = rev;
            }
            _noCrouchClips = !Has("RifleCrouchIdle") || !Has("RifleCrouchWalk");
            _hasSprint = Has("RifleSprint");
        }

        // ---------------------------------------------------------------- commands
        /// <summary>The next step jumps to its targets without blending (spawn, view switch, resume).</summary>
        public void Snap() { _snap = true; }
        /// <summary>Advances the plan's clock for a command given between frames.</summary>
        public void SetNow(float now) { if (now > _now) _now = now; }      // a command's time (between frames: the caller's clock)
        /// <summary>Keeps the firing pose for at least the given seconds.</summary>
        public void Shoot(float seconds) { _fireUntil = Math.Max(_fireUntil, _now + Math.Max(0.05f, seconds)); }
        /// <summary>reload over 'seconds'; rounds &gt; 0 = one round at a time (the reload clip's round part, rounds times; RifleReload / PistolReload only)</summary>
        public void Reload(float seconds, int rounds)
        {
            _reloadStart = _now; _reloadLen = Math.Max(0.2f, seconds); _reloadUntil = _now + _reloadLen; _reloadRounds = Math.Max(0, rounds);
            _pumpUntil = 0f;
            if (Logging) Log += "reload " + _reloadLen.ToString("0.00") + "s/" + _reloadRounds + "; ";
        }
        /// <summary>Cancels the running reload.</summary>
        public void CancelReload() { _reloadUntil = 0f; }
        /// <summary>Requests the pump / bolt cocking at the next step.</summary>
        public void Pump() { _pumpStart = true; }
        /// <summary>Requests the jump take-off at the next step.</summary>
        public void Jump() { _wantJump = true; }
        /// <summary>Requests a kick at the next step.</summary>
        public void Kick() { _wantKick = true; }
        /// <summary>Starts a throw (left hand quick grenade or right hand), at most one per 1.2 s.</summary>
        public void Throw(bool rightHand) { Throw(rightHand, false); }
        /// <summary>Starts a native-timed throw at its release lead without restarting on a weapon change.</summary>
        internal void Throw(bool rightHand, bool timed)
        {
            if (_now - _throwAt < 1.2f) return;
            string n = rightHand && Has("ThrowRight") ? "ThrowRight" : "Throw";
            if (!Has(n)) return;
            _throwAt = _now; _throwClip = n; _throwFrom = timed ? Clamp(ThrowRelease - 0.4f, 0f, Len(n) * 0.5f) : 0f;
            _throwUntil = _now + Clamp(Len(n) - _throwFrom, 0.5f, 2f);
            if (Logging) Log += "throw " + n + "; ";
        }
        /// <summary>one blow: bare hands = the Punch clip's blows in order (Punch1 / Punch2 by turns without it); melee = the Melee clip over the swing, chained swings MeleeCombo's two blows by turns. cycle = how long the swing takes (s)</summary>
        public void Strike(float cycle)
        {
            cycle = Clamp(cycle, 0.2f, 1.5f);
            bool chained = _striking || _now < _meleeUntil || _now - _strikeEnd < 0.25f;
            if (Kind == LocoPlan.K_NONE)
            {
                if (Has("Punch")) { _seqN = chained ? (_seqN + 1) % PunchSeq.Length : 0; SetStrike(PunchSeq[_seqN], cycle); return; }
                for (int k = 0; k < 2; k++)
                {
                    int i = (_punchN + k) % 2;
                    string primary = i == 0 ? "Punch1" : "Punch2", alias = i == 0 ? "Melee1" : "Melee2";
                    string c = Has(primary) ? primary : Has(alias) ? alias : null;
                    if (c == null) continue;
                    _punchN = i + 1;
                    SetStrike(new[] { c, i == 0 ? "0.75" : "0.3", i == 0 ? "1.0" : "0.5", i == 0 ? "1.5" : "0.95" }, cycle);
                    return;
                }
                return;
            }
            _chainN = chained ? _chainN + 1 : 0;
            if (_chainN > 0 && Has("MeleeCombo")) { SetStrike(Combo[(_chainN - 1) % 2], cycle); _meleeUntil = 0f; return; }
            _meleeClip = Has("Melee") ? "Melee" : "enemy_1_attack";
            if (!Has(_meleeClip)) return;
            // the first swing: the whole Melee clip fitted to the swing (as the player's first swing)
            _striking = false; _strikeW = 0f; _strikeClip = null;
            _meleeAt = _now; _meleeUntil = _now + cycle; _meleeSpeed = Clamp(Len(_meleeClip) / cycle, 0.5f, 5f);
            if (Logging) Log += "melee " + cycle.ToString("0.00") + "s; ";
        }
        // Shared strikes: clip, from (wind-up), hit (the blow lands), to (back on guard), clip seconds
        private static readonly string[][] PunchSeq = {
            new[] { "Punch", "0.30", "0.47", "0.65" }, new[] { "Punch", "0.65", "0.80", "1.00" }, new[] { "Punch", "0.97", "1.17", "1.32" }, new[] { "Punch", "1.30", "1.47", "1.65" },
            new[] { "Punch", "1.70", "1.87", "2.05" }, new[] { "Punch", "1.93", "2.07", "2.22" }, new[] { "Punch", "2.17", "2.33", "2.47" }, new[] { "Punch", "2.40", "2.53", "2.85" } };
        private static readonly string[][] Combo = { new[] { "MeleeCombo", "1.5", "1.73", "2.3" }, new[] { "MeleeCombo", "0.8", "1.07", "1.5" } };
        /// <summary>Parses an invariant float.</summary>
        private static float F(string s) { return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture); }
        /// <summary>Starts one strike of a punch/melee sequence (clip, from, hit, to times) over the given cycle.</summary>
        private void SetStrike(string[] s, float cycle)
        {
            _strikeClip = s[0]; _strikeFrom = F(s[1]); _strikeHit = F(s[2]); _strikeTo = F(s[3]);
            StrikeHand = Kind != LocoPlan.K_NONE ? null : _strikeClip == "Punch" ? ((_seqN == 2 || _seqN == 3 || _seqN == 5 || _seqN == 7) ? "mixamorig:RightHand" : "mixamorig:LeftHand")
                : (_strikeClip == "Punch1" || _strikeClip == "Melee1" ? "mixamorig:RightHand" : "mixamorig:LeftHand");
            _striking = true; _strikeAt = _now; _strikeCycle = cycle; _strikeW = 1f; _meleeUntil = 0f;
            if (Logging) Log += "strike " + _strikeClip + "@" + _strikeHit + "; ";
        }

        /// <summary>Fits the active first melee swing to the observed native arms duration without starting a new combo.</summary>
        internal void RetimeMelee(float seconds)
        {
            if (_meleeUntil <= 0f) return;
            seconds = Clamp(seconds, 0.2f, 2f);
            _meleeUntil = _meleeAt + seconds; _meleeSpeed = Clamp(Len(_meleeClip) / seconds, 0.5f, 5f);
        }
        /// <summary>Stops a third-person kick immediately when the player returns to first person.</summary>
        internal void SuppressKick() { if(ActionClip=="Kick") { _actionUntil=0f;ActionW=0f; } _wantKick=false; }
        /// <summary>Ends weapon-specific actions while preserving a grenade/lance already in flight.</summary>
        internal void CancelWeaponActions()
        {
            _reloadUntil=_pumpUntil=_meleeUntil=0f;_nativeReloadWas=false;_striking=false;_strikeClip=null;_strikeW=0f;
            _strikeEnd=-10f;_chainN=_seqN=0;
        }
        /// <summary>Changes seat ownership without restarting a native throw, melee swing or reload already in progress.</summary>
        internal void SeatChanged()
        {
            _actionUntil = 0f; ActionW = 0f; ActionClip = "";
            _wantJump = _wantKick = _wasAir = false; _jumpPhase = "";
            _turnClip = null; _turnInit = false; _snap = true;
        }
        /// <summary>Ends transient actions when traversal or death takes over the character.</summary>
        internal void ResetActions()
        {
            _reloadUntil = _pumpUntil = _throwUntil = _meleeUntil = _fireUntil = _actionUntil = 0f;
            _striking = _wantJump = _wantKick = _pumpStart = _wasAir = _nativeReloadWas = false;
            _strikeClip = null; _strikeW = UpperW = RigW = RigEff = ActionW = AimLiftW = 0f;
            UpperClip = RigClip = ActionClip = ""; UpperOld = RigOld = null;
            _jumpPhase = ""; _turnClip = null; _turnInit = false; _snap = true;
            _strikeEnd = -10f; _chainN = _seqN = 0;
        }

        // ---------------------------------------------------------------- one frame
        /// <summary>One frame of the decision model: stance and velocity in, 42 locomotion weights/times, hand rig, action and upper-body clips, poses and turns out.</summary>
        public void Step(float now, float dt)
        {
            Build();
            Log = "";
            _now = now;
            if (dt < 0f) dt = 0f;
            bool snap = _snap;
            if (Kind != _lastKind) { if (_lastKind >= 0) { _reloadUntil = 0f; _pumpUntil = 0f; } _lastKind = Kind; }

            // ---- speed, run, sprint, crouch (shared locomotion)
            float speed = (float)Math.Sqrt(Lx * Lx + Lz * Lz);
            SpeedSmooth = snap ? speed : Lerp(SpeedSmooth, speed, 1f - (float)Math.Exp(-dt * 10f));
            // (ModAPI) an NPC's velocity can jump from standing to running in one frame (the player's never does): the legs need at least 1/8 s
            float m = Clamp01(SpeedSmooth / 0.4f);
            m = snap ? m : MoveTowards(M, m, dt * 8f);
            CrouchW = snap ? (Crouch ? 1f : 0f) : MoveTowards(CrouchW, Crouch ? 1f : 0f, dt * 4f);
            float crouch = _noCrouchClips ? 0f : CrouchW;
            float r = Clamp01((SpeedSmooth - RunFrom) / 0.8f);
            if (CrouchW > 0.5f) r = 0f;
            RunW = snap ? r : MoveTowards(RunW, r, dt * 4f);
            float sprint = _hasSprint ? Clamp01((SpeedSmooth - SprintFrom) / 0.6f) : 0f;
            SprintW = snap ? sprint : MoveTowards(SprintW, sprint, dt * 4f);
            M = m;

            bool gun = Kind == LocoPlan.K_RIFLE || Kind == LocoPlan.K_PISTOL;
            bool aiming = gun && Aim;
            bool shooting = gun && (Fire || now < _fireUntil);
            bool relaxed = LocoPlan.Relaxed(Kind, aiming, shooting);
            RelaxW = snap ? (relaxed ? 1f : 0f) : MoveTowards(RelaxW, relaxed ? 1f : 0f, dt * 5f);

            // ---- legs: the direction turns toward the way it moves (DirBlendSpeed), then the base weights and the shared stride
            float wantAng = (Lx * Lx + Lz * Lz) > 1e-4f ? (float)(Math.Atan2(Lx, Lz) * 180.0 / Math.PI) : _moveAng;
            _moveAng = (snap || SpeedSmooth < 0.15f) ? wantAng : LocoPlan.SlewAngle(_moveAng, wantAng, DirBlendSpeed * dt);
            float lm = (float)Math.Sqrt(Lx * Lx + Lz * Lz);
            float dx = (float)Math.Sin(_moveAng * Math.PI / 180.0) * lm, dz = (float)Math.Cos(_moveAng * Math.PI / 180.0) * lm;
            float hipWant;
            LocoPlan.Weights(m, RunW, SprintW, dx, dz, crouch, RelaxW, LocoPlan.CrouchMirror(Kind), RunLegsTurn, W, out hipWant);
            float rate = 0f, rateW = 0f;
            for (int i = 0; i < LocoPlan.BN; i++)
            {
                if (!LocoPlan.Moving(i) || W[i] <= 0.001f) continue;
                float native = i == LocoPlan.B_RSPRINT || (i >= LocoPlan.B_SPRINT && i < LocoPlan.B_CIDLE) ? SprintNative
                             : i == LocoPlan.B_RRUN || (i >= LocoPlan.B_RUN && i < LocoPlan.B_SPRINT) ? RunNative
                             : (i >= LocoPlan.B_CWALK && i < LocoPlan.B_RIDLE) || i == LocoPlan.B_RCLEFT ? CrouchNative : WalkNative;
                if (NativeOf != null) native = NativeOf(SlotClip[i], native);
                float sp = Clamp(SpeedSmooth / Math.Max(0.2f, native), 0.5f, 2f);
                rate += W[i] * sp / Len(SlotClip[i]); rateW += W[i];
            }
            if (rateW > 0.001f) _stride += dt * rate / rateW;
            _stride -= (float)Math.Floor(_stride);
            for (int i = 0; i < LocoPlan.BN; i++)
            {
                float len = Len(SlotClip[i]);
                if (!LocoPlan.Moving(i)) { SlotTime[i] = (float)((now % len + len) % len); continue; }   // idles loop on the clock
                float off = PhaseOf != null ? PhaseOf(SlotClip[i]) : -1f;
                SlotTime[i] = (float)(LocoPlan.Time01(_stride, off, SlotReverse[i]) * len);
            }
            HipTurn = snap ? hipWant : Lerp(HipTurn, hipWant, 1f - (float)Math.Exp(-dt * 8f));

            // ---- reload, cocking
            if (FollowNativeReload)
            {
                if (NativeReload && !_nativeReloadWas) { _reloadStart = now; _reloadLen = Len(Kind == LocoPlan.K_PISTOL ? "PistolReload" : "RifleReload"); _reloadRounds = 0; }
                _reloadUntil = NativeReload ? now + 0.05f : 0f; _nativeReloadWas = NativeReload;
            }
            bool reloading = gun && now < _reloadUntil;
            if (_pumpStart)
            {
                _pumpStart = false;
                if (Kind == LocoPlan.K_RIFLE && !reloading && Has("ShotgunPump"))
                {
                    _pumpAt = now; _pumpUntil = now + (LocoPlan.PumpTo - LocoPlan.PumpFrom) * Len("ShotgunPump") / Math.Max(0.1f, PumpSpeed);
                    if (Logging) Log += "pump; ";
                    if (RigClip == "ShotgunPump") RigClip = "";             // each cock starts the rack again
                }
            }
            if (reloading || Kind != LocoPlan.K_RIFLE) _pumpUntil = 0f;
            bool pumping = now < _pumpUntil;

            // ---- whole-body actions: kick, jump, turn in place, walk-to-stop
            if (_actionSpeed != 0f) ActionTime += dt * _actionSpeed;
            if (_wantKick) { _wantKick = false; if (Has("Kick")) { StartAction("Kick", 0f); _actionSpeed = Len("Kick") / 0.55f; _actionUntil = now + 0.55f; _actionFade = 1f / 0.15f; ActionIsTurn = false; } }
            bool air = Airborne;
            if (_wantJump)
            {
                _wantJump = false;
                if (_jumpPhase == "" || now - _jumpAt > 0.6f)
                {
                    _jumpAt = now;
                    string jc = Kind == LocoPlan.K_RIFLE && Has("RifleJumpUp") ? "RifleJumpUp" : Kind == LocoPlan.K_PISTOL && Has("PistolJump") ? "PistolJump" : "Jump";
                    if (Has(jc))
                    {
                        float l, ap, to; bool marks = LocoPlan.JumpMarks(jc, out l, out ap, out to);
                        float from = marks && l > 0f ? Math.Max(0f, l - 0.1f / Len(jc)) : jc == "RifleJumpUp" ? 0f : JumpClipStart;
                        StartAction(jc, from);
                        if (jc == "RifleJumpUp") _actionUntil = now + 10f;
                        ActionIsTurn = false; _actionFade = 10f;
                        _jumpPhase = jc == "RifleJumpUp" ? "up" : "single";
                        if (Logging) Log += "jump " + jc + "; ";
                    }
                }
            }
            if (_jumpPhase != "")
            {
                float l, ap, to;
                if (_jumpPhase == "up" && ActionTime >= Len(ActionClip) - 0.05f && air && Has("RifleJumpLoop")) { StartAction("RifleJumpLoop", 0f); _actionUntil = now + 10f; _jumpPhase = "loop"; }
                else if (_jumpPhase == "up" && ActionTime >= Len(ActionClip) - 0.05f && !air) { _actionSpeed = 0f; ActionTime = Len(ActionClip) - 0.001f; }
                else if (_jumpPhase == "single" && air && LocoPlan.JumpMarks(ActionClip, out l, out ap, out to) && ap > 0f && ActionTime >= ap * Len(ActionClip))
                { _actionSpeed = 0f; _actionUntil = Math.Max(_actionUntil, now + 0.5f); }
                bool landed = (_wasAir && !air && now - _jumpAt > 0.2f) || (!air && !_wasAir && now - _jumpAt > 0.5f);
                if (landed)
                {
                    bool still = m < 0.5f;
                    string land = _jumpPhase == "single" ? ActionClip : Has("RifleJumpDown") ? "RifleJumpDown" : null;
                    if (land != null && still)
                    {
                        float t = LocoPlan.JumpMarks(land, out l, out ap, out to) && to > 0f ? to : 0f;
                        if (_jumpPhase == "single" && ActionTime > t * Len(land)) t = ActionTime / Len(land);    // never back in time
                        StartAction(land, t);
                        _actionUntil = now + Math.Min(0.25f, (1f - t) * Len(land)) + 0.15f;
                    }
                    else _actionUntil = Math.Min(_actionUntil, now + 0.15f);
                    _actionFade = 10f; _jumpPhase = "";
                    if (Logging) Log += "land; ";
                }
            }
            _wasAir = air;
            if (TurnInPlace) DoTurn(Yaw, m, crouch, relaxed, dt); else EndTurn(false);
            bool walkingNow = relaxed && crouch < 0.5f && m > 0.6f && RunW < 0.5f && Lz > 0.9f * lm;
            if (walkingNow) _walkedAt = now;
            if (WalkToStop && relaxed && crouch < 0.5f && speed < 0.25f && m > 0.3f && now - _walkedAt < 0.3f && now > _actionUntil
                && Has("RifleWalkToStop") && _stopAt < _walkedAt && W[LocoPlan.B_RWALK] > 0.3f)
            {
                _stopAt = now;
                float from = LocoPlan.StopStart(_stride);
                if (from >= 0f)
                {
                    StartAction("RifleWalkToStop", from);
                    _actionUntil = now + (LocoPlan.StopTo - from) * Len("RifleWalkToStop") + 0.15f;
                    ActionIsTurn = true; _actionFade = 6f;
                    if (Logging) Log += "walk-to-stop; ";
                }
            }
            if (ActionClip == "RifleWalkToStop" && now < _actionUntil && (speed > 0.5f || crouch > 0.5f || !relaxed)) _actionUntil = Math.Min(_actionUntil, now + 0.15f);
            {
                bool on = now < _actionUntil - 0.15f;
                ActionW = snap && !on ? 0f : MoveTowards(ActionW, on ? 1f : 0f, dt * (on ? 10f : _actionFade));
                if (!on && ActionW <= 0f) _actionFade = 6f;
                if (ActionClip != "" && !Loops(ActionClip) && ActionTime > Len(ActionClip)) ActionTime = Len(ActionClip) - 0.001f;
                if (ActionClip != "" && Loops(ActionClip)) ActionTime = ActionTime % Len(ActionClip);
            }

            // ---- upper-body layer: throws, strikes
            if (now < _throwUntil && Has(_throwClip)) { SetUpper(_throwClip, 1f, 0f, snap); UpperTime = _throwFrom + (now - _throwAt); }
            else if (_meleeUntil > 0f && now < _meleeUntil && Has(_meleeClip)) { SetUpper(_meleeClip, 1f, 0f, snap); UpperTime = Math.Min(Len(_meleeClip) - 0.001f, (now - _meleeAt) * _meleeSpeed); }
            else if (_strikeClip != null)
            {
                if (_meleeUntil > 0f) { _meleeUntil = 0f; _strikeEnd = now; }
                float el = now - _strikeAt, t;
                if (_striking && el >= _strikeCycle) { _striking = false; _strikeEnd = now; }
                if (_striking)
                {
                    float wu = Math.Min(StrikeWindup, _strikeCycle * 0.4f);
                    t = el < wu ? Lerp(_strikeFrom, _strikeHit, el / Math.Max(0.001f, wu)) : Lerp(_strikeHit, _strikeTo, (el - wu) / Math.Max(0.01f, _strikeCycle - wu));
                }
                else { t = _strikeTo; _strikeW = MoveTowards(_strikeW, 0f, dt * 8f); }
                if (_strikeW <= 0f) { _strikeClip = null; SetUpper("", 0f, 0f, snap); }
                else { SetUpper(_strikeClip, _strikeW, 0f, snap); UpperTime = t; }
            }
            else if (Seated && !string.IsNullOrEmpty(SeatedHandsClip) && Has(SeatedHandsClip))
            {
                if (reloading && !_seatedReloadWas) UpperClip = "";
                SetUpper(SeatedHandsClip, 1f, 0f, snap);
                UpperTime = reloading ? (NativeReloadFraction >= 0f ? NativeReloadFraction * Len(SeatedHandsClip) : Math.Min(Len(SeatedHandsClip) - .001f, now - _reloadStart))
                    : (UpperTime + dt) % Len(SeatedHandsClip);
            }
            else
            {
                if (_meleeUntil > 0f && now >= _meleeUntil) { _meleeUntil = 0f; _strikeEnd = now; }
                SetUpper("", 0f, 0f, snap);
            }
            _seatedReloadWas = reloading;
            UpperW = snap ? _upperWTarget : MoveTowards(UpperW, _upperWTarget, dt / (_upperWTarget > UpperW ? UpperIn : UpperOut));
            if (UpperOld != null) { UpperX = snap ? 1f : MoveTowards(UpperX, 1f, dt / UpperCross); if (UpperX >= 1f) UpperOld = null; }
            else UpperX = 1f;

            // ---- the upper source: what the hands do (LocoPlan.Upper), reloads one round at a time, the pump
            bool hold, sync;
            string up = LocoPlan.Upper(Kind, relaxed, aiming, shooting, reloading, pumping, crouch, m, RunW, dz, W[LocoPlan.B_RCLEFT], Has, out hold, out sync);
            _rigManualOn = false;
            if (up == "ShotgunPump") RigSet(up, false, false, snap, PumpSpeed);
            else if (up != null) RigSet(up, hold, sync, snap, 1f);
            if (reloading && (up == "RifleReload" || up == "PistolReload"))
            {
                float el = now - _reloadStart, a, b;
                if (_reloadRounds > 0 && LocoPlan.RoundSegment(up, out a, out b))
                {   // rounds evenly over the reload time: the round part of the clip once per round
                    float per = _reloadLen / _reloadRounds;
                    int idx = Math.Min(_reloadRounds - 1, (int)Math.Floor(el / per));
                    float k = Clamp01((el - idx * per) / per);
                    _rigManual = a + k * (b - a);
                }
                else _rigManual = FollowNativeReload ? (NativeReloadFraction >= 0f ? Clamp01(NativeReloadFraction) : Clamp01(el / _reloadLen)) : Clamp01(el / _reloadLen) * 0.999f;     // the whole clip fitted to the reload time
                _rigManualOn = true;
            }
            _rigWant = up != null ? 1f : 0f;
            // the rig's clip time
            if (RigClip != "" && Has(RigClip))
            {
                float len = Len(RigClip);
                if (_rigManualOn) RigTime = _rigManual * len;
                else if (RigClip == "ShotgunPump") RigTime = Math.Min(len - 0.001f, LocoPlan.PumpFrom * len + (now - _pumpAt) * _rigSpeed);
                else if (_rigHold) RigTime = 0f;
                else if (_rigSync && _bdom() && len > 0.01f) RigTime = (float)(LocoPlan.Time01(_stride + UpperPhase, PhaseOf != null ? PhaseOf(RigClip) : -1f, false) * len);
                else
                {
                    RigTime += dt * _rigSpeed;
                    if (Loops(RigClip)) RigTime %= len; else if (RigTime > len) RigTime = len - 0.001f;
                }
            }
            RigW = snap ? _rigWant : MoveTowards(RigW, _rigWant, dt / RigCross);
            if (RigOld != null) { RigX = snap ? 1f : MoveTowards(RigX, 1f, dt / RigCross); if (RigX >= 1f) RigOld = null; }
            else RigX = 1f;
            float rigScale = (1f - UpperW) * (ActionIsTurn ? 1f : 1f - ActionW);
            RigEff = RigClip != "" ? RigW * rigScale : 0f;
            if (RigEff < 0.001f) RigEff = 0f;
            ChestFollow = LocoPlan.ChestFollowOf(Kind, crouch, m) * RigEff * (1f - ActionW);
            // aim lift: while an aim / fire clip moves the hands of a gun (not reloading)
            bool liftOn = gun && AimLift.IsAimClip(RigClip) && RigEff > 0.01f && !reloading;
            AimLiftW = snap ? (liftOn ? 1f : 0f) : MoveTowards(AimLiftW, liftOn ? 1f : 0f, dt * 6f);
            if (liftOn) { AimLiftClip = RigClip; AimCrouched = RigClip.Contains("Crouch") || CrouchW > 0.5f; }

            // ---- the weapon pose: which clips move the hands, by weight (Loco.PoseWeights)
            PoseW.Clear();
            float wAct = ActionW, wUp = UpperW, wRig = RigEff;
            float wBase = (1f - wAct) * (1f - wUp) * (1f - wRig);
            if (wBase > 0.001f) for (int i = 0; i < LocoPlan.BN; i++) if (W[i] > 0.001f) AddPose(SlotClip[i], W[i] * wBase);
            if (wRig > 0.001f && RigClip != "") AddPose(RigClip, wRig * (1f - wAct) * (1f - wUp));
            if (wUp > 0.001f && UpperClip != "") AddPose(UpperClip, wUp);
            if (wAct > 0.001f && ActionClip != "") AddPose(ActionClip, wAct * (1f - wUp));
            if (Seated)
            {
                Array.Clear(W, 0, W.Length); W[Crouch && !_noCrouchClips ? LocoPlan.B_CIDLE : LocoPlan.B_IDLE] = 1f;
                HipTurn = ActionW = RigW = RigEff = ChestFollow = AimLiftW = 0f;
                PoseW.Clear(); if (UpperW > .001f && UpperClip != "") AddPose(UpperClip, UpperW);
            }
            ApplyPreview(now);
            _snap = false;
        }
        private bool _seatedReloadWas;
        /// <summary>Uses the same graph outputs for authoring previews without a second animation driver.</summary>
        private void ApplyPreview(float now)
        {
            if (string.IsNullOrEmpty(Preview) || !Has(Preview) || Seated) return;
            Array.Clear(W, 0, W.Length); int slot = -1;
            for (int i = 0; i < SlotClip.Length; i++) if (SlotClip[i] == Preview) { slot = i; break; }
            W[slot >= 0 ? slot : Preview.Contains("Crouch") && !_noCrouchClips ? LocoPlan.B_CIDLE : LocoPlan.B_IDLE] = 1f;
            CrouchW = Preview.Contains("Crouch") ? 1f : 0f;
            HipTurn = ChestFollow = UpperW = ActionW = RigEff = 0f; UpperOld = RigOld = null;
            float t = now % Len(Preview);
            if (slot >= 0) SlotTime[slot] = t;
            else if (Preview.Contains("Jump") || Preview.Contains("Turn") || Preview == "Kick") { ActionClip = Preview; ActionTime = t; ActionW = 1f; }
            else { RigClip = Preview; RigTime = t; RigX = RigW = RigEff = 1f; }
            bool lift = AimLift.IsAimClip(Preview);
            AimLiftW = lift ? 1f : 0f; AimLiftClip = lift ? Preview : null; AimCrouched = Preview.Contains("Crouch");
            PoseW.Clear(); PoseW[Preview] = 1f;
        }
        /// <summary>True when a moving locomotion slot carries weight.</summary>
        private bool _bdom() { for (int i = 0; i < LocoPlan.BN; i++) if (LocoPlan.Moving(i) && W[i] > 0.05f) return true; return false; }
        /// <summary>Adds weight to a pose clip for the weapon-pose blend.</summary>
        private void AddPose(string n, float w) { float v; PoseW.TryGetValue(n, out v); PoseW[n] = v + w; }

        /// <summary>Starts a whole-body action clip (kick, jump, turn) from a share of its length.</summary>
        private void StartAction(string name, float startFraction)
        {
            if (!Has(name)) return;
            float len = Len(name);
            ActionClip = name;
            float t0 = Clamp01(startFraction) * len;
            ActionTime = t0; _actionSpeed = 1f;
            _actionUntil = _now + len - t0;
        }

        /// <summary>Sets the upper-body clip and its target weight (the previous clip fades out under it unless snapped).</summary>
        private void SetUpper(string clip, float weight, float speed, bool snap)
        {
            if (string.IsNullOrEmpty(clip)) { _upperWTarget = 0f; return; }
            if (clip != UpperClip)
            {
                if (UpperClip != "" && UpperW > 0.01f && !snap) { UpperOld = UpperClip; UpperOldTime = UpperTime; UpperX = 0f; }
                else { UpperOld = null; UpperX = 1f; }
                UpperClip = clip; UpperTime = 0f;
            }
            _upperWTarget = weight;
        }

        /// <summary>Sets the hand-rig clip (held still or synced to the legs) and its speed.</summary>
        private void RigSet(string clip, bool hold, bool sync, bool snap, float speed)
        {
            if (clip != RigClip)
            {
                if (RigClip != "" && RigW > 0.01f && !snap) { RigOld = RigClip; RigOldTime = RigTime; RigX = 0f; }
                else { RigOld = null; RigX = 1f; }
                RigClip = clip; RigTime = 0f;
                if (Logging) Log += "hands " + clip + "; ";
            }
            _rigHold = hold; _rigSync = sync; _rigSpeed = speed;
        }

        /// <summary>standing still and turning: the turn clip's feet are driven by the turn itself (shared character turn model)</summary>
        private void DoTurn(float yawDeg, float m, float crouch, bool relaxed, float dt)
        {
            if (!_turnInit) { _turnInit = true; _turnLastYaw = yawDeg; }
            float d = DeltaAngle(_turnLastYaw, yawDeg);
            _turnLastYaw = yawDeg;
            bool mine = _turnClip != null && ActionClip == _turnClip && _now < _actionUntil;
            if (m > 0.15f || (_now < _actionUntil && !mine)) { EndTurn(mine && m <= 0.15f); return; }
            if (Math.Abs(d) > TurnStartRate * Math.Max(dt, 1e-3f))
            {
                bool left = d < 0f;
                string clip = crouch > 0.5f ? (left ? "RifleCrouchTurnLeft" : "RifleCrouchTurnRight") : relaxed && Has(left ? "LeftTurn" : "RightTurn") ? (left ? "LeftTurn" : "RightTurn") : (left ? "RifleTurnLeft" : "RifleTurnRight");
                if (!Has(clip)) clip = left ? "RifleTurnLeft" : "RifleTurnRight";
                if (!Has(clip)) return;
                if (mine && _turnClip == clip && _turnSettling) _turnDeg = LocoPlan.TurnFrac(_turnCurve, Clamp01(ActionTime / Len(clip))) * _turnTotal;
                else if (!mine || _turnClip != clip)
                {
                    StartAction(clip, 0f);
                    _turnClip = clip; _turnDeg = 0f;
                    LocoPlan.TurnCurve(clip, out _turnTotal, out _turnCurve);
                }
                _turnDeg += Math.Abs(d);
                while (_turnDeg >= _turnTotal) _turnDeg -= _turnTotal;
                _actionSpeed = 0f;
                ActionTime = LocoPlan.TurnTime(_turnCurve, _turnDeg / _turnTotal) * Len(clip);
                _actionUntil = _now + 0.4f;
                ActionIsTurn = true; _actionFade = 10f;
                _turnMovedAt = _now; _turnSettling = false;
            }
            else if (mine && !_turnSettling && _now - _turnMovedAt > 0.12f) EndTurn(true);
        }
        /// <summary>Ends a turn-in-place clip, settling the feet when asked.</summary>
        private void EndTurn(bool settle)
        {
            if (_turnClip == null) return;
            if (ActionClip == _turnClip && _now < _actionUntil)
            {
                float frac = _turnTotal > 0f ? _turnDeg / _turnTotal : 0f;
                if (settle && frac > 0.5f) { _actionSpeed = 1f; _actionUntil = _now + Math.Max(0.05f, Len(_turnClip) - ActionTime) + 0.1f; }
                else _actionUntil = Math.Min(_actionUntil, _now + 0.15f);
                _actionFade = 10f;
            }
            _turnSettling = true;
            if (!settle) _turnClip = null;
        }
    }
}
