using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Modules.NPC;

namespace WildWasteland.Modules.Patrols
{
    // Ranged passengers retain vanilla detection and firing, but never receive movement or animation FSMs. Their
    // visible body always uses the exact same fixed Pose component as the driver. Sound2 preserves weapon audio.
    /// <summary>Seated ranged combat of vehicle occupants and quest followers (vanilla Attack FSM gating, target search, ranged-only, AI muting); NPCAI part of PassengerGuard (Wave A split of Patrols Crews/Passenger.cs).</summary>
    internal partial class PassengerGuard : MonoBehaviour
    {
        internal static readonly string[] CombatFsms = { "Detection", "Attack", "RangedAttackWait", "Damage Ranged", "WeaponType", "Sound2" };
        private static readonly string[] TargetFsms = { "Detection", "Attack", "RangedAttackWait", "Damage Ranged", "WeaponType" };
        internal static readonly string[] RangedHumans = { "Boltjaw", "Flexa", "Lugnut", "Scrud", "Sprokka", "Bolter", "Gunnar", "Lugger", "Pistoleer" };

        private GameObject _passenger, _car;
        private Transform _anchor;
        private PlayMakerFSM _attack;
        private PlayMakerFSM[] _combat, _fsms;
        private GameObject[] _meleeWeapons;
        private Renderer[] _meleeRenderers = new Renderer[0];
        private Collider[] _meleeColliders = new Collider[0];
        private Pose _pose;   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
        private bool _rangedPrefab, _ranged, _targetDiagnosticLogged, _targetAcquired;
        // target sources, resolved once by reflection (was: every frame): FsmGameObject variables and action fields whose
        // name suggests a target, with their score
        private FsmGameObject[] _targetVars; private int[] _targetVarScores;
        private FsmStateAction[] _targetActions; private FieldInfo[] _targetFields; private int[] _targetFieldScores;
        private GameObject _aimTargetGo; private Transform _aimHead;   // AimPoint cache
        private float _nextTargetSearch; private GameObject _lastTarget;
        // driver mode: the same combat, but only in random bursts; the driver pose in between
        private bool _driverMode, _shooting;
        private float _nextBurst, _burstUntil;
        // the fire decision (target in arc and in range) is taken every FireCheckSeconds, not per frame: switching the Attack FSM on restarts it,
        // and a target on the edge of the arc used to restart it every frame
        private const float FireCheckSeconds = 0.5f;
        private float _nextFireCheck; private bool _canFire;
        private GameObject _fireTarget;
        private bool _questFollower; private GameObject _questTarget; private float _questBlockedUntil;
        internal void QuestTarget(GameObject target, float blockedUntil)
        { _questFollower = true; _questTarget = target; _questBlockedUntil = blockedUntil; }
        internal void QuestRanged() { _rangedPrefab = _ranged = true; _questFollower = true; _nextFireCheck = 0; }
        internal bool QuestCanFire { get { return _canFire && Time.time >= _questBlockedUntil; } }
        private static readonly RaycastHit[] AnswerHits=new RaycastHit[16];
        internal bool CanAnswer(GameObject enemy)
        {
            if(!_ranged||!CombatAllowed||!_canFire||enemy==null||enemy!=_fireTarget)return false;
            var from=_passenger.transform.position+_car.transform.up*1.3f;var to=AimPoint(enemy)-from;
            int count=Physics.RaycastNonAlloc(from,to.normalized,AnswerHits,to.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==AnswerHits.Length)return false;
            for(int i=0;i<count;i++){var c=AnswerHits[i].collider;if(c!=null&&!c.transform.IsChildOf(_passenger.transform)&&!c.transform.IsChildOf(enemy.transform))return false;}
            return true;
        }

        internal static bool IsRangedHuman(GameObject who) { return IsRanged(who); }

        internal static void Prepare(GameObject passenger, bool combatAllowed)
        {
            bool ranged = IsRanged(passenger);
            if (ranged)
            {
                ForceRangedOnly(passenger);
                NeutralizeMeleeDamage(passenger);
            }
            Crew.MuteAi(passenger, ranged && combatAllowed ? CombatFsms : null);
        }

        private static void MuteNonCombatAi(GameObject passenger, bool combatAllowed)
        {
            MuteNonCombatAi(passenger.GetComponents<PlayMakerFSM>(), IsRanged(passenger), combatAllowed);
        }

        // the periodic guard: the FSM array and the ranged verdict are resolved once at Attach, not every half second
        private static void MuteNonCombatAi(PlayMakerFSM[] fsms, bool ranged, bool combatAllowed)
        {
            if (ranged)
                foreach (var fsm in fsms)
                    if (fsm != null && fsm.FsmName == "Damage" && fsm.enabled) fsm.enabled = false;
            Crew.MuteAi(fsms, ranged && combatAllowed ? CombatFsms : null);
        }

        // a human turret gunner (2.2.0) also needs [Combat] HumanTurrets
        private bool _turret; private float _turretArc;   // arc 0 = all around (roof); a motorcycle seat: BikeArc
        // Perf: verdict + car marker cached for 0.25 s (random first phase per guard), was ~8 GetComponent + journey scan per frame
        private float _nextCombatCheck; private bool _combatAllowed, _combatPhased; private PatrolMarker _marker;   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
        private bool CombatAllowed
        {
            get
            {
                if (Time.time < _nextCombatCheck) return _combatAllowed;
                _nextCombatCheck = Time.time + (_combatPhased ? 0.25f : 0.25f * UnityEngine.Random.value); _combatPhased = true;
                _marker = _car != null ? _car.GetComponent<PatrolMarker>() : null;   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
                _combatAllowed = CombatAllowedNow; return _combatAllowed;
            }
        }
        private bool CombatAllowedNow
        {
            get
            {
                var traffic = WorldTraffic.AgentOf(_car);
                if (traffic != null && _driverMode && traffic.Truck && WorldTraffic.Find(traffic.Journey)?.convoy == true) return false;
                return PatrolsModule.RangedCombat.Value && (!_turret || PatrolsModule.RiderLances.Value) && WorldTraffic.CombatAllowed(_car);
            }
        }

        // Ranged enemies normally switch to their separate melee Damage FSM when Attack's distance FloatCompare emits
        // d_melee. Redirect every melee event output to the same ranged event already used by that FSM, then keep the
        // melee damage FSM disabled. This preserves native targeting, cadence, projectile damage and Sound2 at all ranges.
        private static void ForceRangedOnly(GameObject passenger)
        {
            var attack = Array.Find(passenger.GetComponents<PlayMakerFSM>(), f => f.FsmName == "Attack");
            if (attack == null) return;
            FsmEvent rangedEvent = null;
            foreach (var state in attack.FsmStates)
                foreach (var action in state.Actions)
                    if (action != null)
                        foreach (var field in action.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            if (field.FieldType != typeof(FsmEvent)) continue;
                            var ev = field.GetValue(action) as FsmEvent;
                            if (ev != null && ev.Name.IndexOf("ranged", StringComparison.OrdinalIgnoreCase) >= 0)
                                rangedEvent = ev;
                        }
            int redirected = 0;
            if (rangedEvent != null)
                foreach (var state in attack.FsmStates)
                    foreach (var action in state.Actions)
                        if (action != null)
                            foreach (var field in action.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                            {
                                if (field.FieldType != typeof(FsmEvent)) continue;
                                var ev = field.GetValue(action) as FsmEvent;
                                if (ev == null || ev.Name.IndexOf("melee", StringComparison.OrdinalIgnoreCase) < 0) continue;
                                field.SetValue(action, rangedEvent);
                                redirected++;
                            }
            foreach (var fsm in passenger.GetComponents<PlayMakerFSM>())
                if (fsm.FsmName == "Damage") fsm.enabled = false;
            if (Core.Log.L1) NpcaiRuntime.Main("Passenger: forced ranged-only Attack on " + passenger.name + "; redirected "
                + redirected + " melee event output(s)" + (rangedEvent == null ? " (ranged event not found)" : " to " + rangedEvent.Name));
        }

        // any occupant: melee / fire-contact damage FSMs and the FireDamage hitbox off (a body that can be shot but hurts nobody by touch)
        internal static void NeutralizeContact(GameObject who) { NeutralizeMeleeDamage(who); }

        private static void NeutralizeMeleeDamage(GameObject passenger)
        {
            int disabledActions = 0;
            foreach (var fsm in passenger.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "Damage" && fsm.FsmName != "FireDamage") continue;
                foreach (var state in fsm.FsmStates)
                    foreach (var action in state.Actions)
                        if (action != null && action.Enabled)
                        {
                            action.Enabled = false;
                            disabledActions++;
                        }
                fsm.enabled = false;
            }

            var hitbox = Patrol.FindChild(passenger.transform, "FireDamageCollider");
            if (hitbox != null)
                foreach (var collider in hitbox.GetComponents<Collider>()) collider.enabled = false;

            if (Core.Log.L1) NpcaiRuntime.Main("Passenger: neutralized melee/contact damage on " + passenger.name
                + "; disabled " + disabledActions + " FSM action(s)" + (hitbox == null ? "; hitbox not found" : "; hitbox disabled"));
        }

        private static bool IsRanged(GameObject passenger)
        {
            if (passenger == null) return false;
            string name = passenger.name;
            int suffix = name.IndexOf('('); if (suffix >= 0) name = name.Substring(0, suffix);
            name = CrewAppearance.BasePrefab(name);   // refs-ok: REFACTOR-REQUEST B07-12 (part of a Patrols crew/traffic type; B11 moves callers to INpcControl/ISeatedCombat)
            if (Array.FindIndex(RangedHumans, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) < 0) return false;
            bool wait = Array.Exists(passenger.GetComponents<PlayMakerFSM>(), f => f.FsmName == "RangedAttackWait");
            return wait && Patrol.FindChild(passenger.transform, "AttackRaycast_Ranged") != null;
        }

        private void Update()
        { if (!PatrolsModule.Running) return;
            if (_passenger == null || _car == null || _anchor == null) { Destroy(this); return; }
            if (Time.timeScale <= 0f) return;

            bool enabled = _rangedPrefab && CombatAllowed;
            if (enabled != _ranged)
            {
                _ranged = enabled;
                _targetAcquired = false;
                if (_ranged) foreach (var fsm in _combat) if (fsm != null) fsm.enabled = true;
                else if (_attack != null) _attack.enabled = false;
            }
            if (Time.time >= _nextGuard) { _nextGuard = Time.time + 0.5f; MuteNonCombatAi(_fsms, _rangedPrefab, CombatAllowed); }
            if (!_ranged)
            {
                if (_driverMode && _shooting) { _shooting = false; if (_pose != null) _pose.SetShooting(false); ScheduleBurst(); }
                if (_pose != null) _pose.SetAim(Vector3.zero, false);
                return;
            }

            GameObject target = _questFollower ? _questTarget : _marker!=null?TrafficFactions.ForCar(_car,_marker):FindVanillaTarget();
            if(_targetVars!=null)for(int i=0;i<_targetVars.Length;i++)_targetVars[i].Value=target;
            if (_turret && target != null && Core.PlayerState.Transform != null && target == Core.PlayerState.Transform.gameObject && Core.PlayerState.GodMode) target = null;   // a human turret leaves a god-mode player alone
            Vector3 aimPoint = Vector3.zero;
            if (target != null) { _targetAcquired = true; aimPoint = AimPoint(target); }
            if (Time.time >= _nextFireCheck || target == null || target != _fireTarget)
            {
                _fireTarget = target;
                _nextFireCheck = Time.time + FireCheckSeconds;
                bool ok = false;
                if (target != null)
                {
                    var flat = Vector3.ProjectOnPlane(aimPoint - _passenger.transform.position, _car.transform.up);
                    var facing = _anchor != null ? _anchor.forward : _car.transform.forward;   // the seat's facing (the bus chair looks sideways)
                    if (flat.sqrMagnitude > 0.001f)
                        ok = ((_turret && _turretArc <= 0f) || Mathf.Abs(Vector3.SignedAngle(facing, flat, _car.transform.up)) <= (_turret ? _turretArc : PatrolsModule.FireArc.Value))
                          && (aimPoint - _passenger.transform.position).magnitude <= PatrolsModule.ShootDistance.Value;
                }
                _canFire = ok;
            }
            if (_questFollower && Time.time < _questBlockedUntil) _canFire = false;
            bool inArc = _canFire;   // held for the interval (the aim itself still follows the target every frame)
            // awareness handover at dismount (CrewReturn): this crew is engaging the player
            if (inArc && _marker != null && !_questFollower && target != null) { var pl = Core.PlayerState.Transform; if (pl != null && ReferenceEquals(target, pl.gameObject)) _marker.PlayerContactAt = Time.time; }

            if (_driverMode)
            {
                // bursts: open one when due and a target is in the arc, close it after FireBurstSeconds
                if (!_shooting && Time.time >= _nextBurst && target != null && inArc)
                {
                    _shooting = true;
                    _burstUntil = Time.time + PatrolsModule.FireBurstSeconds.Value;
                    if (_pose != null) _pose.SetShooting(true);
                    if (Core.Log.L1) NpcaiRuntime.Main("Driver: " + _passenger.name + " opens fire for " + PatrolsModule.FireBurstSeconds.Value + " s");
                }
                else if (_shooting && (Time.time >= _burstUntil || target == null))
                {
                    _shooting = false;
                    if (_pose != null) _pose.SetShooting(false);
                    ScheduleBurst();
                    if (Core.Log.L1) NpcaiRuntime.Main("Driver: " + _passenger.name + " back on the wheel, next burst in " + (_nextBurst - Time.time).ToString("0") + " s");
                }
                if (!_shooting)
                {
                    if (_attack != null) _attack.enabled = false;
                    if (_pose != null) _pose.SetAim(Vector3.zero, false);
                    return;
                }
                // within the burst the gun pose is held only while it really fires (target in the arc); otherwise hands back on the wheel
                if (_pose != null) _pose.SetShooting(target != null && inArc);
            }

            if (_attack != null && (_targetAcquired || target != null)) _attack.enabled = target != null && inArc;
            if (_pose != null) _pose.SetAim(aimPoint, target != null && inArc);
        }

        private static int ScoreOf(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            return n.Contains("target") ? 5 : n.Contains("player") ? 4 : n.Contains("enemy") ? 3 : n.Contains("closest") ? 2 : 0;
        }

        // One reflection pass over the combat FSMs: remember every FsmGameObject variable / action field that can hold a target.
        private void IndexTargetSources()
        {
            var vars = new List<FsmGameObject>(); var varScores = new List<int>();
            var actions = new List<FsmStateAction>(); var fields = new List<FieldInfo>(); var fieldScores = new List<int>();
            foreach (var fsm in _combat)
            {
                if (fsm == null || Array.IndexOf(TargetFsms, fsm.FsmName) < 0) continue;
                foreach (var variable in fsm.FsmVariables.GameObjectVariables)
                {
                    int s = ScoreOf(variable.Name);
                    if (s > 0) { vars.Add(variable); varScores.Add(s); }
                }
                foreach (var state in fsm.FsmStates)
                    foreach (var action in state.Actions)
                    {
                        if (action == null) continue;
                        foreach (var field in action.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            if (field.FieldType != typeof(FsmGameObject)) continue;
                            var value = field.GetValue(action) as FsmGameObject;
                            if (value == null) continue;
                            int s = Mathf.Max(ScoreOf(field.Name), ScoreOf(value.Name));
                            if (s > 0) { actions.Add(action); fields.Add(field); fieldScores.Add(s); }
                        }
                    }
            }
            _targetVars = vars.ToArray(); _targetVarScores = varScores.ToArray();
            _targetActions = actions.ToArray(); _targetFields = fields.ToArray(); _targetFieldScores = fieldScores.ToArray();
            if (Core.Log.L1) NpcaiRuntime.Main("Passenger: " + _passenger.name + " target sources: " + _targetVars.Length + " variables, " + _targetFields.Length + " action fields");
        }

        private GameObject FindVanillaTarget()
        {
            if (Time.time < _nextTargetSearch) return _lastTarget;     // 10× a second is plenty
            _nextTargetSearch = Time.time + 0.1f;
            GameObject best = null;
            int bestScore = 0;
            if (_targetVars != null)
                for (int i = 0; i < _targetVars.Length; i++)
                    if (_targetVarScores[i] > bestScore) Consider(_targetVars[i].Value, _targetVarScores[i], ref best, ref bestScore);
            if (_targetFields != null)
                for (int i = 0; i < _targetFields.Length; i++)
                {
                    if (_targetFieldScores[i] <= bestScore) continue;
                    var value = _targetFields[i].GetValue(_targetActions[i]) as FsmGameObject;
                    if (value != null) Consider(value.Value, _targetFieldScores[i], ref best, ref bestScore);
                }
            if (best == null && !_targetDiagnosticLogged)
            {
                _targetDiagnosticLogged = true;
                if (Core.Log.L1) NpcaiRuntime.Main("Passenger ranged combat: waiting for vanilla FSM target");
            }
            _lastTarget = best;
            return best;
        }

        private void Consider(GameObject candidate, int score, ref GameObject best, ref int bestScore)
        {
            if (candidate == null || candidate == _passenger || candidate.transform.IsChildOf(_passenger.transform)) return;
            best = candidate;
            bestScore = score;
        }
    }
}
