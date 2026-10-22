using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using WildWasteland.Core;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// Dog Companion (apocalypter.dogcompanion) bridge serving NPC AI and canine animation: Dog Companion strips the hound's Rigidbody
    /// and FSMs; only this adapter knows its private API. The companion is driven like a native hound (shared DogAnimations locomotion,
    /// combat holds and dashes, sitting in vehicles, sneaking with the player). Owned by the Compatibility DogCompanion bridge; acts only
    /// while the NPC module runs and its [NPC] DogCompanionCompat switch is on. Uses NPC/animation internals (REFACTOR-REQUEST B17-13).
    /// </summary>
    internal static class DogCompanionCompat
    {
        private static readonly Dictionary<int, Driver> Drivers = new Dictionary<int, Driver>();
        private static FieldInfo target, mode, lunge, lastFight, nextBite, targetGo, pawDepth;
        private static PropertyInfo feet, alive;
        private static MethodInfo step, setMoving, unstick, stuck, giveUp, hurt, snarl;
        private static ConfigEntry<float> runSpeed, biteDamage, leash;
        private static bool warnedBundle;
        private static PropertyInfo knownPlayer;
        // (WW perf, N12) the private API as compiled delegates (built once in Initialize; a member that cannot be compiled falls back to its
        // reflection call - the same result): Riding / SyncTarget / Feet ... run several times per frame per companion dog
        private static Func<object, object> _target, _targetGo, _knownPlayer;
        private static Func<object, int> _mode;
        private static Func<object, float> _nextBite, _pawDepth;
        private static Action<object, float> _setNextBite, _setLunge, _setLastFight, _giveUp, _hurt;
        private static Func<object, Vector3> _feet;
        private static Func<object, bool> _alive;
        private static Action<object, Vector3, float, float> _step;
        private static Action<object, bool, float> _setMoving;
        private static Action<object> _unstick, _snarl;
        private static Func<object, float, bool> _stuck;

        // (WW) patched through Core (module patches, removed by Core on shutdown). The whole contract is resolved before any patch; a failed
        // patch turns the bridge off (handlers return at once) instead of UnpatchSelf. [NPC] DogCompanionCompat switches it live.
        private static bool _ok;
        private static IModule _owner;
        /// <summary>True when the bridge resolved Dog Companion's contract and installed its patches.</summary>
        internal static bool Present { get { return _ok; } }
        /// <summary>Bridge active: resolved, NPC module running and its [NPC] DogCompanionCompat switch on.</summary>
        private static bool On { get { return _ok && CompatibilityHost.Npc.Running && NpcaiSettings.DogCompanionCompatEnabled.Value; } } // refs-ok: REFACTOR-REQUEST B17-13

        /// <summary>Resolves Dog Companion's complete private contract, then patches Dog/Npc/Distraction under <paramref name="module"/>.</summary>
        internal static void Initialize(IModule module)
        {
            _ok = false; _owner = module;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("apocalypter.dogcompanion", out info) || ReferenceEquals(info.Instance, null)) return;
            try
            {
                var assembly = info.Instance.GetType().Assembly;
                var dog = assembly.GetType("DogCompanion.Dog", true);
                var npc = assembly.GetType("DogCompanion.Npc", true);
                var config = assembly.GetType("DogCompanion.Plugin", true);
                knownPlayer = Property(assembly.GetType("DogCompanion.Game", true), "KnownPlayer");
                target = Field(dog, "_target"); mode = Field(dog, "_mode"); lunge = Field(dog, "_lunge");
                lastFight = Field(dog, "_lastFight"); nextBite = Field(dog, "_nextBite"); targetGo = Field(npc, "Go");
                pawDepth = Field(dog, "_pawDepth");
                feet = Property(dog, "Feet"); alive = Property(npc, "Alive");
                step = Method(dog, "Step"); setMoving = Method(dog, "SetMoving"); unstick = Method(dog, "Unstick");
                stuck = Method(dog, "Stuck"); giveUp = Method(dog, "GiveUp"); hurt = Method(npc, "Hurt"); snarl = Method(dog, "Snarl");
                runSpeed = (ConfigEntry<float>)Field(config, "RunSpeed").GetValue(null);
                biteDamage = (ConfigEntry<float>)Field(config, "BiteDamage").GetValue(null);
                leash = (ConfigEntry<float>)Field(config, "LeashDistance").GetValue(null);
                // Resolve the complete contract before installing any hooks.
                var update = Method(dog, "Update"); var chase = Method(dog, "ChaseTick");
                var late = Method(dog, "LateUpdate"); var disable = Method(dog, "OnDisable"); var steer = Method(dog, "Steer");
                var ride = Method(dog, "RideTick");
                var distraction = Method(assembly.GetType("DogCompanion.Distraction", true), "Nearest");
                bool all = true;
                all &= Core.Patches.Patch(module, update, prefix: Hook(nameof(BeforeUpdate)), postfix: Hook(nameof(AfterUpdate)));
                all &= Core.Patches.Patch(module, chase, prefix: Hook(nameof(BeforeChase)));
                all &= Core.Patches.Patch(module, late, postfix: Hook(nameof(AfterLateUpdate)));
                all &= Core.Patches.Patch(module, disable, prefix: Hook(nameof(BeforeDisable)));
                all &= Core.Patches.Patch(module, steer, prefix: Hook(nameof(BeforeSteer)));
                all &= Core.Patches.Patch(module, ride, postfix: Hook(nameof(AfterRideTick)));
                all &= Core.Patches.Patch(module, step, prefix: Hook(nameof(BeforeStep)));
                all &= Core.Patches.Patch(module, distraction, prefix: Hook(nameof(BeforeDistraction)));
                if (!all) throw new InvalidOperationException("a Harmony patch was refused");
                Compile();
                _ok = true;
                NpcaiSettings.DogCompanionCompatEnabled.SettingChanged += OnToggle; // refs-ok: REFACTOR-REQUEST B17-13
                Log.Main(module, "Dog Companion compatibility enabled (" + info.Metadata.Version + "): shared canine locomotion/combat and sitting in vehicles.");
            }
            catch (Exception e)
            {
                _ok = false;
                Log.Warn(module, "Dog Companion compatibility unavailable; keeping its original behavior: " + e.Message);
            }
        }

        /// <summary>[NPC] DogCompanionCompat switched off live: every companion dog is released (Dog Companion's own behaviour); switched on:
        /// they are taken again on their next Update.</summary>
        private static void OnToggle(object s, EventArgs a) { if (!NpcaiSettings.DogCompanionCompatEnabled.Value) ReleaseAll(); } // refs-ok: REFACTOR-REQUEST B17-13
        /// <summary>Hands every driven companion back to Dog Companion.</summary>
        private static void ReleaseAll()
        {
            foreach (var d in Drivers.Values) { try { if (d.Dog != null) DogAnimations.RemoveCompanion(d.Dog.gameObject); } catch (Exception) { } }
            Drivers.Clear();
        }
        /// <summary>Canine animation shutdown (DogAnimations): hands every companion back to Dog Companion; the bridge stays installed
        /// and takes them again once the animation runs (the bridge itself stops with <see cref="Stop"/>).</summary>
        internal static void Shutdown() { ReleaseAll(); }

        /// <summary>Bridge stop: releases every companion and unsubscribes the NPC switch (the scope removes the patches).</summary>
        internal static void Stop()
        {
            ReleaseAll();
            if (_ok) { try { NpcaiSettings.DogCompanionCompatEnabled.SettingChanged -= OnToggle; } catch (Exception) { } } // refs-ok: REFACTOR-REQUEST B17-13
            _ok = false; _owner = null;
        }
        /// <summary>Number of companion dogs currently driven.</summary>
        internal static int Count { get { return Drivers.Count; } }

        /// <summary>Required field (throws when missing).</summary>
        private static FieldInfo Field(Type type, string name)
        { return AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name); }
        /// <summary>Required property (throws when missing).</summary>
        private static PropertyInfo Property(Type type, string name)
        { return AccessTools.Property(type, name) ?? throw new MissingMemberException(type.FullName, name); }
        /// <summary>Required method (throws when missing).</summary>
        private static MethodInfo Method(Type type, string name)
        { return AccessTools.Method(type, name) ?? throw new MissingMethodException(type.FullName, name); }
        /// <summary>One of this adapter's patch methods.</summary>
        private static MethodInfo Hook(string name) { return AccessTools.Method(typeof(DogCompanionCompat), name); }

        // ---------- (WW perf) generated accessors (Core Access; replaces interpreted Expression.Compile, README 8 #7) ----------
        /// <summary>Generated getter of a field converted to <typeparamref name="T"/> (enum to int, boxing to object), or null.</summary>
        private static Func<object, T> FieldGet<T>(FieldInfo f)
        {
            try { return Access.Get<T>(f.DeclaringType, f.Name); }
            catch (Exception) { return null; }
        }
        /// <summary>Generated float field setter, or null.</summary>
        private static Action<object, float> FieldSetFloat(FieldInfo f)
        {
            if (f.FieldType != typeof(float) || f.IsInitOnly) return null;
            return Access.TypedSetter<float>(f);
        }
        /// <summary>Generated property getter converted to <typeparamref name="T"/>, or null.</summary>
        private static Func<object, T> PropGet<T>(PropertyInfo pi)
        {
            try { return Access.Get<T>(pi.DeclaringType, pi.Name); }
            catch (Exception) { return null; }
        }
        /// <summary>Generated call (first delegate parameter = target) when the parameter types match exactly, or null.</summary>
        private static TDelegate Call<TDelegate>(MethodInfo m, params Type[] args) where TDelegate : class
        {
            try
            {
                var mp = m.GetParameters();
                if (mp.Length != args.Length) return null;
                for (int i = 0; i < args.Length; i++) if (mp[i].ParameterType != args[i]) return null;
                var types = new Type[args.Length + 1]; types[0] = typeof(object); Array.Copy(args, 0, types, 1, args.Length);
                return Access.Call<TDelegate>(m, typeof(TDelegate).GetMethod("Invoke").ReturnType, types);
            }
            catch (Exception) { return null; }
        }
        /// <summary>Builds every accessor once (Initialize); a member that cannot be generated falls back to its reflection call.</summary>
        private static void Compile()
        {
            var ft = target; var fm = mode; var fl = lunge; var flf = lastFight; var fnb = nextBite; var fg = targetGo; var fpd = pawDepth;
            var pf = feet; var pa = alive; var pk = knownPlayer;
            var ms = step; var msm = setMoving; var mu = unstick; var mst = stuck; var mg = giveUp; var mh = hurt; var msn = snarl;
            _target = FieldGet<object>(ft) ?? (o => ft.GetValue(o));
            _targetGo = FieldGet<object>(fg) ?? (o => fg.GetValue(o));
            _mode = FieldGet<int>(fm) ?? (o => Convert.ToInt32(fm.GetValue(o)));
            _nextBite = FieldGet<float>(fnb) ?? (o => (float)fnb.GetValue(o));
            _pawDepth = FieldGet<float>(fpd) ?? (o => (float)fpd.GetValue(o));
            _setNextBite = FieldSetFloat(fnb) ?? ((o, v) => fnb.SetValue(o, v));
            _setLunge = FieldSetFloat(fl) ?? ((o, v) => fl.SetValue(o, v));
            _setLastFight = FieldSetFloat(flf) ?? ((o, v) => flf.SetValue(o, v));
            _feet = PropGet<Vector3>(pf) ?? (o => (Vector3)pf.GetValue(o, null));
            _alive = PropGet<bool>(pa) ?? (o => (bool)pa.GetValue(o, null));
            _knownPlayer = PropGet<object>(pk) ?? (o => pk.GetValue(null, null));
            _step = Call<Action<object, Vector3, float, float>>(ms, typeof(Vector3), typeof(float), typeof(float)) ?? ((o, a, b, c) => ms.Invoke(o, new object[] { a, b, c }));
            _setMoving = Call<Action<object, bool, float>>(msm, typeof(bool), typeof(float)) ?? ((o, a, b) => msm.Invoke(o, new object[] { a, b }));
            _unstick = Call<Action<object>>(mu) ?? (o => mu.Invoke(o, null));
            _snarl = Call<Action<object>>(msn) ?? (o => msn.Invoke(o, null));
            _stuck = Call<Func<object, float, bool>>(mst, typeof(float)) ?? ((o, a) => (bool)mst.Invoke(o, new object[] { a }));
            _giveUp = Call<Action<object, float>>(mg, typeof(float)) ?? ((o, a) => mg.Invoke(o, new object[] { a }));
            _hurt = Call<Action<object, float>>(mh, typeof(float)) ?? ((o, a) => mh.Invoke(o, new object[] { a }));
        }

        /// <summary>WW-side state of one companion dog: its NPC agent, hold/dash flags, speed and sneaking.</summary>
        internal sealed class Driver
        {
            internal readonly MonoBehaviour Dog;
            internal readonly Senses.Agent Agent;
            internal bool Held, Failed, FixedDash;
            internal float Speed;
            internal Vector3 Before;
            internal GameObject DashTarget;
            internal bool WantsSneak;
            private GameObject movementPlayer;
            private PlayMakerFSM playerMovement;
            private FsmFloat standingHeight;
            /// <summary>Creates the agent for a companion dog.</summary>
            internal Driver(MonoBehaviour dog)
            { Dog = dog; Agent = new Senses.Agent { Owner = dog.gameObject, T = dog.transform, Col = dog.GetComponent<Collider>() }; }
            /// <summary>Usable: not failed, dog alive and enabled.</summary>
            internal bool Active { get { return !Failed && Dog != null && Dog.isActiveAndEnabled; } }
            /// <summary>Dog Companion mode 1 = riding in the player's vehicle.</summary>
            internal bool Riding { get { return _mode(Dog) == 1; } }
            /// <summary>The player sneaks and the dog has no live target.</summary>
            internal bool Sneaking
            {
                get
                {
                    object npc = _target(Dog);
                    return WantsSneak && !Riding && Agent.Target == null && (npc == null || !_alive(npc));
                }
            }
            /// <summary>Reads the player's Movement FSM (crouch/crawl state or low standing height); FSM lookup cached per player.</summary>
            internal void RefreshSneaking()
            {
                var player = Senses.PlayerObj ?? (GameObject)_knownPlayer(null);
                if (player != movementPlayer || (player != null && playerMovement == null))
                {
                    movementPlayer = player; playerMovement = null; standingHeight = null;
                    if (player != null) foreach (var fsm in player.GetComponents<PlayMakerFSM>()) if (fsm.FsmName == "Movement") { playerMovement = fsm; standingHeight = fsm.FsmVariables.FindFsmFloat("standingHeight"); break; }
                }
                string state = playerMovement != null && playerMovement.enabled ? playerMovement.ActiveStateName ?? "" : "";
                WantsSneak = state.StartsWith("Crouch", StringComparison.OrdinalIgnoreCase) || state.StartsWith("Crawl", StringComparison.OrdinalIgnoreCase)
                    || (playerMovement != null && playerMovement.enabled && standingHeight != null && standingHeight.Value > .1f && standingHeight.Value < 1.4f);
            }
            /// <summary>Dog Companion's ground point of the dog.</summary>
            internal Vector3 Feet { get { return _feet(Dog); } }
            /// <summary>Paw height offset.</summary>
            internal float PawHeight { get { return -_pawDepth(Dog); } }
            /// <summary>Dog Companion's run speed x1.1.</summary>
            internal float RunSpeed { get { return runSpeed.Value * 1.1f; } }
            /// <summary>Dog Companion's next-bite time.</summary>
            internal float NativeNextBite { get { return _nextBite(Dog); } }
            /// <summary>Sets Dog Companion's next-bite time.</summary>
            internal void SetCooldown(float until) { _setNextBite(Dog, until); }
            /// <summary>Copies Dog Companion's live target into the NPC agent (combat/idle).</summary>
            internal void SyncTarget()
            {
                object npc = Riding ? null : _target(Dog);
                Agent.Target = npc != null && _alive(npc) ? (GameObject)_targetGo(npc) : null;
                Agent.State = Agent.Target != null ? Senses.State.Combat : Senses.State.Idle;
            }
            /// <summary>True when <paramref name="victim"/> is the dog's live target.</summary>
            internal bool TargetAlive(GameObject victim)
            {
                object npc = _target(Dog);
                return victim != null && npc != null && (GameObject)_targetGo(npc) == victim && _alive(npc);
            }
            /// <summary>Holds (stops) or releases the dog's own movement.</summary>
            internal void Halt(bool hold)
            { Held = hold; if (hold && Active) _setMoving(Dog, false, 0f); }
            /// <summary>A bite landed: Dog Companion damage, NPC hurt by the player, snarl.</summary>
            internal void Contact(GameObject victim)
            {
                if (!Active || Riding || !TargetAlive(victim)) return;
                _hurt(_target(Dog), biteDamage.Value);
                if (Senses.PlayerObj != null) Senses.Hurt(victim, Senses.PlayerObj);
                _snarl(Dog);
            }
            /// <summary>One committed dash step through Dog Companion's Step (steering bypass active).</summary>
            internal void Dash(Vector3 direction, float speed, float dt, GameObject victim)
            {
                if (!Active || Riding) return;
                FixedDash = true; DashTarget = victim;
                try { _step(Dog, Feet + direction * 5f, speed, dt); }
                finally { FixedDash = false; DashTarget = null; }
                Dog.transform.rotation = Quaternion.LookRotation(direction);
            }
            /// <summary>Replacement of Dog Companion's ChaseTick: leash, give-up and stuck handling, own navigation unless held/dashing.</summary>
            internal bool Chase(Transform player, float dt)
            {
                SyncTarget(); if (Agent.Target == null) return false;
                _setLastFight(Dog, Time.time);
                Vector3 distance = Agent.Target.transform.position - player.position; distance.y = 0f;
                if (distance.magnitude > leash.Value) { _giveUp(Dog, 5f); SyncTarget(); return false; }
                // The shared animation state owns holding and committed dashes. All
                // other chasing keeps Dog Companion's obstacle/ground navigation.
                if (!Held && !DogAnimations.CompanionDashing(Dog.gameObject))
                {
                    _step(Dog, Agent.Target.transform.position, RunSpeed, dt);
                    if (_stuck(Dog, 3f)) { _giveUp(Dog, 6f); SyncTarget(); }
                }
                else _unstick(Dog);
                return false;
            }
        }
        /// <summary>The driver of a dog (created and registered with the canine animation on first sight).</summary>
        private static Driver Get(MonoBehaviour dog)
        {
            if (dog == null) return null;
            Driver driver; int id = dog.GetInstanceID();
            if (!Drivers.TryGetValue(id, out driver))
            {
                driver = new Driver(dog);
                if (!DogAnimations.RegisterCompanion(driver.Agent, driver))
                {
                    if (!warnedBundle) { warnedBundle = true; Log.Warn(_owner, "Dog Companion needs the updated npcai_dogs.bundle (including sit clips); keeping its original behavior."); }
                    driver.Failed = true;
                }
                Drivers[id] = driver;
            }
            return driver;
        }
        /// <summary>The canine animation drives this dog.</summary>
        private static bool Owns(Driver d) { return d != null && !d.Failed && DogAnimations.Owns(d.Dog.gameObject); }
        /// <summary>Releases one dog after an error (logged).</summary>
        private static void Fail(Driver d, Exception e)
        {
            if (d == null) return;
            d.Failed = true; DogAnimations.RemoveCompanion(d.Dog.gameObject);
            Log.Warn(_owner, "Dog Companion compatibility released this dog after an error: " + e.GetBaseException().Message);
        }
        /// <summary>Prefix of Dog.Update (per companion per frame): target/sneak sync and animation restore.</summary>
        public static void BeforeUpdate(MonoBehaviour __instance)
        {
            if (!On) return;
            Driver d = null;
            try { d = Get(__instance); if (d == null) return; d.SyncTarget(); d.RefreshSneaking(); DogAnimations.RestoreCompanion(__instance.gameObject); d.Before = __instance.transform.position; }
            catch (Exception e) { Fail(d, e); }
        }
        /// <summary>Postfix of Dog.Update: measured speed, procedural jolt suppressed, animation tick.</summary>
        public static void AfterUpdate(MonoBehaviour __instance)
        {
            if (!On) return;
            Driver d;
            if (!Drivers.TryGetValue(__instance.GetInstanceID(), out d) || d.Failed) return;
            try
            {
                d.SyncTarget(); Vector3 moved = __instance.transform.position - d.Before; moved.y = 0f;
                d.Speed = d.Riding || moved.magnitude > 2f ? 0f : moved.magnitude / Mathf.Max(.001f, Time.deltaTime);
                if (Owns(d)) _setLunge(__instance, 1f); // suppress the old procedural jolt
                DogAnimations.TickCompanion(__instance.gameObject);
            }
            catch (Exception e) { Fail(d, e); }
        }
        /// <summary>Prefix of Dog.ChaseTick: the WW chase replaces it while the animation owns the dog.</summary>
        public static bool BeforeChase(MonoBehaviour __instance, Transform __0, float __1)
        {
            if (!On) return true;
            Driver d = null;
            try { d = Get(__instance); return !Owns(d) || d.Chase(__0, __1); }
            catch (Exception e) { Fail(d, e); return true; }
        }
        /// <summary>Prefix of Dog.Steer during a committed dash: a short sphere cast replaces its obstacle steering.</summary>
        public static bool BeforeSteer(MonoBehaviour __instance, Vector3 __0, Vector3 __1, ref Vector3 __result)
        {
            if (!On) return true;
            Driver d;
            if (!Drivers.TryGetValue(__instance.GetInstanceID(), out d) || !d.FixedDash) return true;
            RaycastHit hit;
            bool blocked = Physics.SphereCast(__0 + Vector3.up * .5f, .22f, __1, out hit, .4f,
                (1 << 0) | (1 << 8) | (1 << 9) | (1 << 11) | (1 << 14) | (1 << 16), QueryTriggerInteraction.Ignore);
            if (blocked && (hit.normal.y > .6f || (hit.collider != null && d.DashTarget != null && (hit.collider.gameObject == d.DashTarget || hit.collider.transform.IsChildOf(d.DashTarget.transform))))) blocked = false;
            __result = blocked ? Vector3.zero : __1;
            return false;
        }
        /// <summary>Prefix of Dog.Step: a sneaking companion walks at most 1.6 m/s.</summary>
        public static void BeforeStep(MonoBehaviour __instance, ref float __1)
        {
            if (!On) return;
            Driver d;
            if (Drivers.TryGetValue(__instance.GetInstanceID(), out d) && Owns(d) && d.Sneaking) __1 = Mathf.Min(__1, 1.6f);
        }
        /// <summary>Prefix of Distraction.Nearest: NPCs driven by the WW brain ignore Dog Companion's distractions.</summary>
        public static bool BeforeDistraction(FsmStateAction __0)
        {
            if (!On) return true;
            var fsm = __0 != null ? __0.Fsm : null;
            return fsm == null || !Senses.On || !NpcaiSettings.BrainEnabled.Value || Senses.AgentFor(fsm.GameObject) == null; // refs-ok: REFACTOR-REQUEST B17-13
        }
        /// <summary>A driven companion within <paramref name="range"/> of an enemy (NPC Brain: enemies react to the dog), or null.</summary>
        internal static GameObject CloseThreat(GameObject enemy, float range)
        {
            if (enemy == null) return null;
            foreach (var d in Drivers.Values)
                if (Owns(d) && !d.Riding && (d.Dog.transform.position - enemy.transform.position).sqrMagnitude <= range * range) return d.Dog.gameObject;
            return null;
        }
        /// <summary>Postfix of Dog.LateUpdate: late animation pass.</summary>
        public static void AfterLateUpdate(MonoBehaviour __instance)
        {
            if (!On) return;
            Driver d;
            if (!Drivers.TryGetValue(__instance.GetInstanceID(), out d) || d.Failed) return;
            try { DogAnimations.LateCompanion(__instance.gameObject); }
            catch (Exception e) { Fail(d, e); }
        }
        /// <summary>Postfix of Dog.RideTick: lowers a riding dog onto its seat.</summary>
        public static void AfterRideTick(MonoBehaviour __instance)
        {
            if (!On) return;
            Driver d = null;
            try
            {
                d = Get(__instance);
                if (Owns(d) && d.Riding) __instance.transform.localPosition += new Vector3(0f, DogCompanionSeating.UpOffset, DogCompanionSeating.ForwardOffset);
            }
            catch (Exception e) { Fail(d, e); }
        }
        /// <summary>Prefix of Dog.OnDisable: releases the dog.</summary>
        public static void BeforeDisable(MonoBehaviour __instance)
        {
            if (!_ok) return;
            DogAnimations.RemoveCompanion(__instance.gameObject);
            Drivers.Remove(__instance.GetInstanceID());
        }
        /// <summary>Scene change: forgets every driver (called by the canine animation).</summary>
        internal static void OnSceneLoaded() { Drivers.Clear(); }
    }

    /// <summary>Seat offsets of a riding companion dog (merged from DogCompanionSeating.cs; only used by DogCompanionCompat.AfterRideTick).</summary>
    internal static class DogCompanionSeating
    {
        // Lower the whole seated dog instead of translating the chest bones or
        // bending the neck. Preserve its original sitting proportions.
        internal const float ForwardOffset = .45f, UpOffset = -.125f;
    }
}
