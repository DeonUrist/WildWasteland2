using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Senses part: hearing: gunshots, explosions, the player's engine and horn, thrown items, shared by Noise() with the responder limit and the storm shrink.</summary>
    internal static partial class Senses
    {
        // ---------- sounds ----------
        // one ghost per event, offered to every NPC within the radius
        private static readonly List<Agent> _responders = new List<Agent>();
        private static readonly HashSet<Agent> _allowed = new HashSet<Agent>();
        private sealed class ByDistance : IComparer<Agent>
        {
            internal static readonly ByDistance Instance = new ByDistance(); internal static Vector3 Pos;
            /// <summary>Compares two agents by distance to the noise position.</summary>
            public int Compare(Agent x, Agent y) { return (x.T.position - Pos).sqrMagnitude.CompareTo((y.T.position - Pos).sqrMagnitude); }
        }
        /// <summary>One noise event: the nearest GunshotResponders (plus everyone close) of the NPCs within the radius (storm-shrunk, peaceful factions and the tornado-deafened left out) get the shared ghost or a ghost of their own.</summary>
        private static void Noise(GameObject source, Vector3 pos, float radius, Src src, string about, Func<Agent, bool> filter, Func<Agent, Vector3> at, GameObject subject = null, Action<Agent> told1 = null)
        {
            if (radius <= 0f || !On) return;
            PublishNoise(source, pos, radius, src);
            float now = Time.time;
            // a gunshot / blast does not pull a whole camp: the nearest GunshotResponders go (and everyone within GunshotNearRange); the rest
            // only hear it (nothing for them yet)
            HashSet<Agent> allowed = null;
            if (src == Src.Gunshot && NpcaiSettings.GunshotResponders.Value > 0)
            {
                _responders.Clear();
                float near2 = NpcaiSettings.GunshotNearRange.Value * NpcaiSettings.GunshotNearRange.Value, r2a = radius * radius;
                foreach (var kv in _agents) { var a = kv.Value; if (a.Owner != null && !a.Spider && (!SimpleBandits.Has(a.Owner)||SimpleBandits.Fighting(a.Owner)) && a.T.parent == null && (a.T.position - pos).sqrMagnitude <= r2a && (filter == null || filter(a))) _responders.Add(a); }
                if (_responders.Count > NpcaiSettings.GunshotResponders.Value)
                {
                    ByDistance.Pos = pos;           // (perf) a static comparer and a reused set: no closure / HashSet per shot
                    _responders.Sort(ByDistance.Instance);
                    allowed = _allowed; allowed.Clear();
                    for (int i = 0; i < _responders.Count; i++) if (i < NpcaiSettings.GunshotResponders.Value || (_responders[i].T.position - pos).sqrMagnitude <= near2) allowed.Add(_responders[i]);
                }
            }
            Transform sroot = source != null ? source.transform.root : null;
            // An event about the player (the player's shots, shouts, engine, thrown items; an NPC shooting or shouting at the player) means
            // nothing to an NPC whose faction is at peace with the player (Coyotes towns): it only alerts NPCs hostile to the player. Hits
            // still count (being shot makes the game turn the faction hostile anyway) and so do fights between NPCs.
            bool aboutPlayer = src != Src.Hit && PlayerRelated(source, subject);
            float perkHearing = aboutPlayer ? (Core.ModApi.Get<Core.IPlayerPerkEffects>()?.MutantDetectionMultiplier ?? 1f) : 1f;
            Ghost shared = at == null ? GetOrMake(src, pos, source, about, 0f, 1f, now) : null;
            if (shared != null) shared.Subject = subject;
            int told = 0;
            float r2 = radius * radius;
            // a sandstorm swallows gunfire, explosions, shouts and engines: the range shrinks once (never twice) when the sound starts in a
            // storm or the listener stands in one. No storm loaded: nothing is tested.
            bool stormy = (src == Src.Gunshot || src == Src.Shout || src == Src.Engine) && Storm.AnyForSenses;
            bool srcStorm = stormy && Storm.InForSenses(pos);
            float hf = Storm.HearingFactor, rs2 = r2 * hf * hf;
            foreach (var kv in _agents)
            {
                var a = kv.Value;
                if (a.Owner == null || (SimpleBandits.Has(a.Owner)&&!SimpleBandits.Fighting(a.Owner)) || a.T.parent != null || (sroot != null && a.T == sroot)) continue;
                float d2 = (a.T.position - pos).sqrMagnitude;
                float hearing = a.Tag == "Mutant" || a.Tag == "MutantSanity" ? perkHearing : 1f;
                if (d2 > r2 * hearing * hearing) continue;
                if (stormy && (srcStorm || a.InStorm) && d2 > rs2 * hearing * hearing) continue;
                if (filter != null && !filter(a)) continue;
                if (allowed != null && !a.Spider && !allowed.Contains(a)) continue;
                if (aboutPlayer && !a.Hostile.Contains("Player")) continue;
                if (a.Blown) continue;                                      // the tornado's roar: hears nothing
                if (aboutPlayer && OutsideCaveWeb(a)) { Log(a, "ignores noise: player outside cave and beyond 3 m"); continue; }
                if (a.Spider && (src == Src.Gunshot || src == Src.Shout)) a.AmbushDist = 0f;
                Ghost g = shared;
                if (g == null) { g = GetOrMake(src, at(a), source, about, 0f, 1f, now); g.Subject = subject; }
                if (Assign(a, g, src, now)) { told++; if (told1 != null) told1(a); }
            }
            if (shared != null && shared.Holders.Count == 0) { int i = _ghosts.IndexOf(shared); if (i >= 0) KillGhost(i); }
            if (WildWasteland.Core.Log.L2 && told > 0) NpcaiRuntime.Deep("Senses: " + src + " (" + about + ") within " + radius.ToString("0") + " m alerts " + told + (shared != null ? " -> ghost #" + shared.Id : ""));
        }

        /// <summary>True for the player object or any of its children.</summary>
        private static bool IsPlayer(GameObject go)
        {
            if (go == null || _player == null) return false;
            return go == _player || go.transform.IsChildOf(_player.transform);
        }

        /// <summary>the event is about the player: the player made it, it is about the player, or an NPC made it while fighting / hunting the player</summary>
        private static bool PlayerRelated(GameObject source, GameObject subject)
        {
            FindPlayer();
            if (_player == null) return false;
            if (IsPlayer(subject) || IsPlayer(source)) return true;
            if (_playerCar != null && source != null && source.transform.root == _playerCar.transform.root) return true;
            if (source == null) return false;
            var sa = Get(source.transform.root.gameObject);
            if (sa != null)
            {
                if ((sa.State == State.Combat || sa.State == State.Hide) && IsPlayer(sa.Target)) return true;
                if (sa.Ghost != null && IsPlayer(sa.Ghost.Subject)) return true;
                return false;
            }
            // not one of ours (a raider seated in an Apocapatrol car): its own Detection target
            var t = Aim.TargetOf(source.transform.root.gameObject);
            return IsPlayer(t);
        }

        // Tracers: a gun fired (the player's or an NPC's), once per shot (pellets of one blast merge through the 1 s source window)
        internal static float LastPlayerShotAt = -100f;     // for the brain: a quiet player is reloading or hiding
        /// <summary>A gun was fired (INpcApi.Shot): heard within the gun kind's (player) or NPC type's range as a ghost 2-4 m off the shooter.</summary>
        internal static void Shot(GameObject shooter, Vector3 pos, WeaponRanges.Kind kind, bool player)
        {
            if (player) LastPlayerShotAt = Time.time;
            if (!On) return;
            float radius = player ? PlayerShotRange(kind) : NpcShotRange(shooter, kind);
            if (player) HeardPlayerShot(pos, radius, Time.time);
            string about = WildWasteland.Core.Log.L2 || WildWasteland.Core.NavigationDiagnosticState.Overlay ? (player ? "player" : Name(shooter)) + " " + kind.ToString().ToLowerInvariant() : "gunshot";
            // a shot is heard, not seen: the spot is 2-4 m off (merged per shooter within 1 s, so a burst makes one ghost)
            Vector2 off = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2f, 4f);
            Noise(shooter, OnGround(pos + new Vector3(off.x, 0f, off.y), pos.y), radius, Src.Gunshot, about, null, null, shooter);
        }

        /// <summary>Hearing range of a player shot by weapon kind ([Senses] ShotRange*).</summary>
        private static float PlayerShotRange(WeaponRanges.Kind k)
        {
            switch (k)
            {
                case WeaponRanges.Kind.Pistol: return NpcaiSettings.ShotRangePistol.Value;
                case WeaponRanges.Kind.Smg: return NpcaiSettings.ShotRangeSmg.Value;
                case WeaponRanges.Kind.Sniper: return NpcaiSettings.ShotRangeSniper.Value;
                case WeaponRanges.Kind.Shotgun: return NpcaiSettings.ShotRangeShotgun.Value;
                case WeaponRanges.Kind.Crossbow: return NpcaiSettings.ShotRangeCrossbow.Value;
                default: return NpcaiSettings.ShotRangeRifle.Value;
            }
        }

        private static Dictionary<string, float> _npcShot; private static string _npcShotSrc;
        /// <summary>Hearing range of an NPC's shot: its type in [Senses] NpcShotRanges, else by weapon kind.</summary>
        private static float NpcShotRange(GameObject shooter, WeaponRanges.Kind k)
        {
            string cfg = NpcaiSettings.NpcShotRanges.Value ?? "";
            if (_npcShot == null || _npcShotSrc != cfg)
            {
                _npcShotSrc = cfg; _npcShot = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in cfg.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = part.IndexOfAny(new[] { '=', ':' });
                    float v;
                    if (eq > 0 && float.TryParse(part.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) _npcShot[part.Substring(0, eq).Trim()] = v;
                }
            }
            float r;
            if (shooter != null && _npcShot.TryGetValue(PrefabOf(shooter), out r)) return r;
            return PlayerShotRange(k);
        }

        // ---------- explosions ----------
        // Grenades and blast lance 1 (Explosion_Grenade), blast lance 2 (Explosion_Can), Blast Rat and Blast Zombie deaths (Explosion_BlastRat /
        // _BlastZombie): each item/creature FSM spawns the blast prefab with a CreateObject action (explode state, Health FSM). Harmony postfix on
        // CreateObject.OnEnter: a spawned prefab named in [Senses] BlastPrefabs is a GUNSHOT-ranked noise within BlastRange at the blast.
        // Car explosions are not CreateObject spawns (Apocapatrol instantiates its blast itself) - they come only through AfterBlast.
        private static HashSet<string> _blastNames; private static string _blastSrc;
        /// <summary>CreateObject.OnEnter postfix: structures for the nav and the cover watch, brains for spawned gunmen, and the [Senses] BlastPrefabs explosions as gunshot-ranked noise.</summary>
        public static void AfterCreateObject(CreateObject __instance)
        {
            if (!NpcaiRuntime.Running) return;
            try
            {
                if (__instance.gameObject == null) return;
                var prefab = __instance.gameObject.Value;
                if (prefab == null) return;
                string pname = PrefabOf(prefab);        // prefab assets are few: cached, so this hook costs no string per spawn
                var made = __instance.storeObject != null ? __instance.storeObject.Value : null;
                if (made != null && Nav.On && pname.Length > 5 && (pname[0] == 'C' || pname[0] == 'B')) Nav.Spawned(made);   // Camp_N / Cave_N / Building_N
                if (made != null && pname.Length > 5 && (pname[0] == 'C' || pname[0] == 'B') && (pname.StartsWith("Camp_", StringComparison.Ordinal) || pname.StartsWith("Cave_", StringComparison.Ordinal) || pname.StartsWith("Building_", StringComparison.Ordinal))) Core.WorldIndex.QueueRoot(made.transform);   // (WW) its "Hide" cover objects join the brain's WorldIndex watch
                if (made != null && Brain.On) Brain.Spawned(made);      // (1.1.1) gunmen get Apocaplayer's clips from their first frames
                if (!On || NpcaiSettings.BlastRange.Value <= 0f) return;
                string cfg = NpcaiSettings.BlastPrefabs.Value ?? "";
                if (_blastNames == null || _blastSrc != cfg)
                {
                    _blastSrc = cfg; _blastNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var part in cfg.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)) _blastNames.Add(part.Trim());
                }
                if (!_blastNames.Contains(pname)) return;
                Vector3 pos;
                if (made != null && Prefab(made.name) == pname) pos = made.transform.position;
                else
                {
                    var sp = __instance.spawnPoint != null ? __instance.spawnPoint.Value : null;
                    pos = sp != null ? sp.transform.position : (__instance.Fsm != null && __instance.Fsm.GameObject != null ? __instance.Fsm.GameObject.transform.position : Vector3.zero);
                    if (__instance.position != null && !__instance.position.IsNone) pos += __instance.position.Value;
                }
                var owner = __instance.Fsm != null ? __instance.Fsm.GameObject : null;
                Noise(owner, pos, NpcaiSettings.BlastRange.Value, Src.Gunshot, (owner != null ? Prefab(owner.name) : "something") + " exploding", null, null);
            }
            catch (Exception e) { NpcaiRuntime.Error("Senses: " + e); }
        }

        /// <summary>Tracers: a bullet hit a creature - it knows where that came from</summary>
        internal static void Hurt(GameObject victim, GameObject attacker)
        {
            if (!On || victim == null || attacker == null) return;
            var hit=victim.transform;while(hit!=null&&!hit.CompareTag("Coyotes"))hit=hit.parent;
            if(hit!=null&&IsPlayer(attacker)){var relation=CoyoteRelation();if(relation!=null&&relation.Fsm.Initialized)relation.SendEvent("FactionCoyotesEnemy");}
            { var observed=new Core.Events.AttackObserved { Victim=Core.Entities.Of(victim),Attacker=Core.Entities.Of(attacker) };Core.ModEvents.Publish(ref observed); }
            var a = Get(victim.transform.root.gameObject);
            if (a == null) return;
            if(a.CrewBoarding)return;
            if (IsPlayer(attacker)) { FoughtFor(a, _player, Time.time, "hit by him"); if (SimpleBandits.Has(a.Owner)) a.NextLook = Time.time; }   // (crew AI)
            if(SimpleBandits.Has(a.Owner))return; // Combat dismounts take the player through the memory (SimpleLook); seated crew runs no senses.
            a.AmbushDist = 0f;      // (1.3.2) a hit ends the ambush: it comes up and fights
            Brain.Hit(a.Owner);
            if (a.State == State.Combat || a.State == State.Hide || a.Blown) return;
            float now = Time.time;
            // it knows the direction the hit came from; the distance only roughly (trackers: exactly)
            Vector3 from = attacker.transform.root.position;
            if (!a.Tracker) from = Fuzz(from, a.T.position, 0.15f);
            var g = GetOrMake(Src.Hit, from, attacker.transform.root.gameObject, "hit by " + Name(attacker), 0f, 1f, now);
            g.Subject = attacker.transform.root.gameObject;
            bool had = a.Ghost == g;
            if (Assign(a, g, Src.Hit, now)) { if (!had) if (WildWasteland.Core.Log.L2) Log(a, "is hit by " + Name(attacker) + ", goes for ghost #" + g.Id); }   // once per shot, not per pellet
            else if (g.Holders.Count == 0) { int i = _ghosts.IndexOf(g); if (i >= 0) KillGhost(i); }
        }

        // the player's car: a running engine is heard EngineMinRange..EngineMaxRange m by horsepower, half of it idling, nothing switched off
        private static Dictionary<int, float> _hpOf = new Dictionary<int, float>();
        // The car horn (the car's DriveTrigger/INPUT [INPUT_Horn] FSM: Horn button down -> loop the horn clip, up -> stop): heard like the
        // engine (same rank, peaceful factions, sandstorm) by everyone within the engine's longest range + 50 m (every horn the same), every
        // engine tick while it sounds (a short tap counts too: the AudioPlay hook stamps the start).
        private static Fsm _hornFsm; private static float _hornAt = -10f;

        private static Vector3 _hornPos; private static float _hornEr2;
        private static readonly Func<Agent, bool> _beyondEngine = BeyondEngine;
        /// <summary>Noise filter of the horn: only NPCs beyond the engine's range.</summary>
        private static bool BeyondEngine(Agent a) { return (a.T.position - _hornPos).sqrMagnitude > _hornEr2; }
        private static readonly Dictionary<int, string> _engineAbout = new Dictionary<int, string>();
        private static GameObject _engineRbCar; private static Rigidbody _engineRb;
        /// <summary>Engine tick (every 0.5 s): the player's running engine and his horn are heard as ENGINE noise around the car.</summary>
        private static void Engine(float now)
        {
            var car = PlayerCar();
            if (car == null) return;
            bool engineOn = Apocapatrol_EngineRunning(car);
            bool hornOn = _hornFsm != null && _hornFsm.GameObject != null && _hornFsm.GameObject.transform.root == car.transform.root
                          && (now - _hornAt < 0.6f || _hornFsm.ActiveStateName == "on");
            if (!engineOn && !hornOn) return;
            Vector3 cpos = car.transform.position;
            float er = 0f;
            if (engineOn)
            {
                string about;
                er = EngineRadius(car, out about);
                Noise(car, cpos, er, Src.Engine, about, null, null, _player);
                ReleaseEngineHolders(car, cpos, er, now);
            }
            if (hornOn)
            {
                // its own ghost (source = the horn object): it follows the car while the horn sounds and then stays where it last sounded,
                // so NPCs it reached beyond the engine's range walk there. NPCs within the engine's range keep the engine ghost.
                _hornPos = cpos; _hornEr2 = er * er;        // (perf) a static filter, no closure every 0.5 s
                Noise(_hornFsm.GameObject, cpos, NpcaiSettings.EngineMaxRange.Value + 50f, Src.Engine, "horn", engineOn ? _beyondEngine : null, null, _player);
            }
        }

        /// <summary>Engine hearing range by horsepower (read once per car from its child names), shrunk while idling.</summary>
        private static float EngineRadius(GameObject car, out string about)
        {
            float hp;
            if (!_hpOf.TryGetValue(car.GetInstanceID(), out hp))
            {
                hp = 0f;
                foreach (var t in car.GetComponentsInChildren<Transform>(true))
                {
                    int i = t.name.IndexOf("HP", StringComparison.OrdinalIgnoreCase);
                    if (i <= 0) continue;
                    int j = i - 1; while (j >= 0 && (char.IsDigit(t.name[j]) || t.name[j] == '.')) j--;
                    float v;
                    if (float.TryParse(t.name.Substring(j + 1, i - j - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0f) { hp = v; break; }
                }
                _hpOf[car.GetInstanceID()] = hp;
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: " + car.name + " engine " + (hp > 0f ? hp + " HP" : "unknown"));
            }
            float lo = NpcaiSettings.EngineMinHp.Value, hi = Mathf.Max(lo + 1f, NpcaiSettings.EngineMaxHp.Value);
            float k = hp > 0f ? Mathf.Clamp01((hp - lo) / (hi - lo)) : 0.5f;
            float radius = Mathf.Lerp(NpcaiSettings.EngineMinRange.Value, NpcaiSettings.EngineMaxRange.Value, k);
            if (!ReferenceEquals(_engineRbCar, car)) { _engineRbCar = car; _engineRb = car.GetComponent<Rigidbody>(); }     // (perf) per car, not per tick
            var rb = _engineRb;
            float speed = rb != null ? rb.velocity.magnitude : 0f;
            float thr = Core.VehicleEngine.Throttle(car);
            if (speed < 1.5f && thr < 0.05f) radius *= Mathf.Clamp01(NpcaiSettings.EngineIdleFactor.Value / 100f);
            if (!_engineAbout.TryGetValue(car.GetInstanceID(), out about)) { about = "engine " + (hp > 0f ? hp + " HP" : ""); _engineAbout[car.GetInstanceID()] = about; }
            return radius;
        }

        /// <summary>The shared engine ghost moved with the car: holders now out of earshot stop following it and search where they stand.</summary>
        private static void ReleaseEngineHolders(GameObject car, Vector3 cpos, float radius, float now)
        {
            // the engine ghost is one shared ghost that moves with the car and is refreshed every tick: an NPC that drove out of earshot
            // would follow it for ever - let go of it where the NPC stands
            foreach (var g in _ghosts)
            {
                if (g.Src != Src.Engine || g.Source != car || g.Retired) continue;
                _scratch.Clear();
                foreach (var h in g.Holders) if (h.Owner == null || (h.T.position - cpos).sqrMagnitude > radius * radius * 1.3f) _scratch.Add(h);
                foreach (var h in _scratch)
                {
                    if (h.Owner == null) { g.Holders.Remove(h); continue; }
                    Log(h, "lost the engine sound, searches here");
                    h.Ghost = null; h.GhostPrio = Src.None; g.Holders.Remove(h);
                    h.State = State.Search; h.SearchUntil = now + Mathf.Max(0f, NpcaiSettings.SearchSeconds.Value);
                }
                if (g.Holders.Count == 0) { int i = _ghosts.IndexOf(g); if (i >= 0) KillGhost(i); }
                break;
            }
        }

        /// <summary>True while the car's engine runs (Core vehicle engine state).</summary>
        private static bool Apocapatrol_EngineRunning(GameObject car) { return Core.VehicleEngine.Running(car); }

        /// <summary>the player throws a held item (GrabItem FSM state Throw): the landing spot attracts NPCs within ThrowRange</summary>
        private static void Thrown(float now, float dt)
        {
            if (_grabFsm == null)
            {
                var cam = Core.GameRefs.MainCamera;
                if (cam != null) foreach (var f in cam.GetComponents<PlayMakerFSM>()) if (f.FsmName == "GrabItem") { _grabFsm = f; _grabItemVar = f.FsmVariables.GetFsmGameObject("Item"); break; }
                if (_grabFsm == null) return;
            }
            if (_grabFsm.Fsm == null || !_grabFsm.Fsm.Initialized) return;
            string st = _grabFsm.ActiveStateName;
            if (st != _grabState)
            {
                _grabState = st;
                if (st == "Throw" && _grabItemVar != null && _grabItemVar.Value != null)
                {
                    _thrown = _grabItemVar.Value.GetComponent<Rigidbody>();
                    _thrownSince = now; _thrownRest = 0f;
                    if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: " + Name(_grabItemVar.Value) + " thrown");
                }
            }
            if (_thrown == null) return;
            if (_thrown.velocity.magnitude < 0.5f) _thrownRest += dt; else _thrownRest = 0f;
            if (_thrownRest > 0.3f || now - _thrownSince > 6f)
            {
                Noise(_thrown.gameObject, _thrown.position, NpcaiSettings.ThrowRange.Value, Src.Thrown, "thrown " + Prefab(_thrown.name), null, null, _player);
                _thrown = null;
            }
        }

    }
}
