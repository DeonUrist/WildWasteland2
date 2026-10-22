using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    // Hired help. Talk to a Bodyguard, Pup, Bolter, Pistoleer, Gunnar or Lugger and spend reputation with their faction to have them come with you
    // for a day: they walk after you, ride in the passenger seat of your car when there is one, and go after whatever hurts you.
    // One at a time. Fighting is NPCAI's job (its public Api.Inform sends an NPC after a target) and the mod needs NPCAI and
    // Apocapatrol to be installed, so hiring is refused without them.
    /// <summary>
    /// Hired follower, Quests part: the hire contract (price, start/attach, dismiss, expiry, death/loss outcome, reputation) and the
    /// per-frame driver. Wave A split: follower AI -> NPCAI (Behaviour/_fromQuests/Hire.Follower.cs, Hire.QueryCache.cs), crew seat ->
    /// Patrols (_fromQuests/Hire.Seat.cs), poses/recoil -> Presentation (Character/_fromQuests/Hire.Poses.cs), gun stats/sounds/shots ->
    /// Gunplay (_fromQuests/Hire.Guns.cs). <see cref="Update"/> stays whole here (its expiry part is Quests, the rest follower AI: B07/B18).
    /// </summary>
    internal static partial class Hire
    {
        static ConfigEntry<bool> enabled, rideCars;
        static ConfigEntry<string> prices;
        static ConfigEntry<float> durationHours, followDist, teleportDist, rideRange;
        static ConfigEntry<int> deathPenalty;
        static ConfigEntry<UnityEngine.InputSystem.Key> dismissKey;

        static QuestBook book;
        static Action<string> toast;
        static Action<string, int, string> changeRep;

        // ---- the follower -----------------------------------------------------------------------------
        internal static GameObject npc;
        static bool wasMountedAtLoss;
        static readonly List<Collider> lateOwn = new List<Collider>(), lateCar = new List<Collider>();
        static float restoreAt;
        static ConfigEntry<string> ignoreNames; static ConfigEntry<float> leash;
        static RigidbodyInterpolation wasInterp; static CollisionDetectionMode wasDetect; static bool wasGravity = true;
        static float outSince = -1f; const float ExitDelay = 1.5f;
        static bool endPending;
        static int rescues; static float firstRescue;
        static bool attached;                   // npc was set (it may have been destroyed since)
        static string displayName = "";
        static float searchUntil;               // real time: how long to look for the saved follower after a load
        static bool searching;
        static float lastDistance;              // last known distance to the player, to tell a death from an unload
        static PlayMakerFSM moveFsm, healthFsm;
        static float nextState, nextSteer, nextThreat, nextExclude;
        static float tooFarSince = -1f;
        static Vector3 heading;

        // fighting
        internal static GameObject target;
        static float engagedUntil;
        static float lastInform;

        // the player's health, to see when something hurts them
        static PlayMakerFSM playerHealth;
        static FsmFloat playerHealthVar;
        static float lastPlayerHealth = -1f;
        static float hurtUntil;
        static float nextHealthFind;

        // riding
        internal static bool mounted;
        static GameObject mountObj;
        static Transform origParent;
        static GameObject carObj;
        static float inCarSince = -1f;
        static readonly List<Behaviour> muted = new List<Behaviour>();
        static readonly List<Collider> ignoredCars = new List<Collider>();
        static readonly List<Collider> ownCols = new List<Collider>();
        static bool wasKinematic;
        static Component poseComp;
        static Component guardComp;                       // Apocapatrol's PassengerGuard on a seated gunner
        static readonly List<KeyValuePair<Renderer, bool>> rendSnap = new List<KeyValuePair<Renderer, bool>>();
        static readonly List<KeyValuePair<Collider, bool>> colSnap = new List<KeyValuePair<Collider, bool>>();
        static ConfigEntry<bool> shootFromCar;
        static ConfigEntry<float> shotDamage, shotInterval;
        static ConfigEntry<bool> realFire, shotAnim;
        static ConfigEntry<float> recoilDeg;
        static ConfigEntry<string> magSizes, reloadTimes, reloadSoundFrom;
        static bool recoilHooked;
        static ConfigEntry<string> rangedNames;
        static ConfigEntry<string> armPoseLike;
        static PlayMakerFSM inCarFsm;
        static float nextCarFind;

        static readonly string[] MuteNames = { "Movement", "Unstuck", "Rotate", "Detection", "Attack", "RangedAttackWait", "Damage Ranged" };

        public static bool Enabled { get { return enabled != null && enabled.Value; } }
        public static bool Active { get { return book != null && !string.IsNullOrEmpty(book.HireName); } }
        public static string Name { get { return displayName; } }

        public static void Init(HireSettings cfg, QuestBook b, Action<string> toastFn, Action<string, int, string> repFn)
        {
            book = b; toast = toastFn; changeRep = repFn;
            enabled = cfg.enabled;
            prices = cfg.prices;
            durationHours = cfg.durationHours;
            deathPenalty = cfg.deathPenalty;
            followDist = cfg.followDist;
            teleportDist = cfg.teleportDist;
            ignoreNames = cfg.ignoreNames;
            leash = cfg.leash;
            rideCars = cfg.rideCars;
            shootFromCar = cfg.shootFromCar;
            realFire = cfg.realFire;
            shotAnim = cfg.shotAnim;
            recoilDeg = cfg.recoilDeg;
            if (!recoilHooked) { recoilHooked = true; Application.onBeforeRender += Recoil; }
            magSizes = cfg.magSizes;
            reloadTimes = cfg.reloadTimes;
            reloadSoundFrom = cfg.reloadSoundFrom;
            shotDamage = cfg.shotDamage;
            shotInterval = cfg.shotInterval;
            rangedNames = cfg.rangedNames;
            armPoseLike = cfg.armPoseLike;
            rideRange = cfg.rideRange;
            dismissKey = cfg.dismissKey;
        }

        // ---- prices ---------------------------------------------------------------------------------------
        // the cost in reputation of hiring an NPC with this name, or -1 when it is not for hire
        public static int CostFor(string npcName)
        {
            if (!Enabled || string.IsNullOrEmpty(npcName)) return -1;
            string n = Util.Norm(npcName);
            foreach (string part in (prices.Value ?? "").Split(','))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string key = Util.Norm(part.Substring(0, eq));
                int cost;
                if (key.Length == 0 || !int.TryParse(part.Substring(eq + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out cost)) continue;
                if (cost > 0 && Util.Norm(Game.BaseName(npcName)).StartsWith(key, StringComparison.Ordinal)) return cost;
            }
            return -1;
        }

        // "NPCAI and Apocapatrol" etc.; empty when both are there
        public static string MissingMods() { return Services.Npc == null ? "NPC AI" : Services.FollowerVehicles == null ? "Patrols" : ""; }

        public static bool IsFollower(Transform t)
        {
            return Active && t != null && npc != null && (t.gameObject == npc || t.IsChildOf(npc.transform));
        }

        // ---- start / stop ---------------------------------------------------------------------------------------
        public static void Start(GameObject go, string factionKey, string shownName)
        {
            if (go == null) return;
            double gh = Game.GameHours();
            book.HireName = go.name;
            book.HireUntil = gh >= 0.0 ? gh + Math.Max(0.1f, durationHours.Value) : -1.0;
            book.HireFaction = factionKey ?? "";
            book.HireKind = shownName ?? "";
            Attach(go);
            toast("Hired " + displayName + " for " + (durationHours.Value >= 24f ? Math.Round(durationHours.Value / 24f, 1).ToString("0.#", CultureInfo.InvariantCulture) + " day(s)" : durationHours.Value.ToString("0.#", CultureInfo.InvariantCulture) + " hours") + ". " + dismissHint());
            Plugin.Info("Hired " + go.name + " (" + book.HireKind + ") until game hour " + book.HireUntil.ToString("0.0", CultureInfo.InvariantCulture));
        }

        static string dismissHint()
        {
            return dismissKey.Value != UnityEngine.InputSystem.Key.None ? "Press " + dismissKey.Value + " to send them away." : "Talk to them to send them away.";
        }

        static void Attach(GameObject go)
        {
            npc = go;
            attached = true;
            magLeft = -1; footSetup = false; footShooting = false;
            fsmSnap.Clear();
            foreach (PlayMakerFSM f in QuestWorld.Fsms(go)) if (f != null) fsmSnap.Add(new KeyValuePair<PlayMakerFSM, bool>(f, f.enabled));
            displayName = string.IsNullOrEmpty(book.HireKind) ? Game.BaseName(go.name) : book.HireKind;
            moveFsm = FindFsm(go, "Movement");
            healthFsm = FindFsm(go, "Health");
            target = null;
            engagedUntil = 0f;
            mounted = false;
            searching = false;
            tooFarSince = -1f;
            heading = Vector3.zero;
            lastPlayerHealth = -1f;
            lastDistance = 0f;
            wasMountedAtLoss = false;
            Game.InvalidateLiveNames();
        }

        // Ends the hire. The NPC goes back to being a normal NPC where it stands.
        public static void Dismiss(string message)
        {
            if (!Active) return;
            if (mounted) Dismount(true);
            ReleaseNpc();
            string n = displayName.Length > 0 ? displayName : (book.HireKind ?? "your help");
            Clear();
            if (!string.IsNullOrEmpty(message)) toast(message.Replace("{name}", n));
        }

        static void Clear()
        {
            book.HireName = ""; book.HireUntil = 0.0; book.HireFaction = ""; book.HireKind = "";
            npc = null; attached = false; target = null; moveFsm = null; healthFsm = null; searching = false; mounted = false; displayName = "";
            carObj = null; inCarSince = -1f; endPending = false; guardComp = null;
        }

        // new world / slot being loaded: forget the objects (the book itself is reloaded by the caller)
        public static void Reset()
        {
            if (mounted) { try { Dismount(false); } catch (Exception) { } }
            ReleaseNpc(); QuestFollower.Release(); RestoreCollisions();
            npc = null; attached = false; target = null; moveFsm = null; healthFsm = null; searching = false; mounted = false; displayName = "";
            carObj = null; inCarSince = -1f; endPending = false; playerHealth = null; playerHealthVar = null; lastPlayerHealth = -1f; inCarFsm = null;
            Ranged.Clear(); _nearestName = null;
            muted.Clear(); ignoredCars.Clear(); ownCols.Clear(); lateOwn.Clear(); lateCar.Clear();
        }

        static PlayMakerFSM FindFsm(GameObject go, string name)
        {
            if (go == null) return null;
            PlayMakerFSM[] fsms = QuestWorld.Fsms(go);
            for (int i = 0; i < fsms.Length; i++) if (fsms[i] != null && fsms[i].FsmName == name) return fsms[i];
            return null;
        }

        // ---- every frame ----------------------------------------------------------------------------------------
        public static void Update()
        {
            
            if (!Enabled || book == null) return;
            RestoreCollisions();                // also after the hire has ended, so a dropped-off follower stops ignoring the car
            if (!Active) return;

            Transform pl = Game.Player();
            if (pl == null) return;

            // after a load the saved follower has to be found again by name
            if (!attached)
            {
                if (!searching) { searching = true; searchUntil = Time.unscaledTime + 60f; }
                Transform t = NearestByName(book.HireName, pl.position);
                if (t != null) { Attach(t.gameObject); Plugin.Info("Hired help " + book.HireName + " found again."); Calm(pl, true); nextCalm = Time.time + 0.5f; }
                else if (Time.unscaledTime > searchUntil)
                {
                    string n = string.IsNullOrEmpty(book.HireKind) ? "Your hired help" : book.HireKind;
                    Clear();
                    toast(n + " is gone. The hire is over.");
                }
                return;
            }

            // dead, or gone
            if (npc == null) { Gone(); return; }
            wasMountedAtLoss = mounted;
            if (healthFsm != null && healthFsm.ActiveStateName != "health" && healthFsm.Fsm != null && healthFsm.Fsm.Initialized) { Died(); return; }

            double gh = Game.GameHours();
            if (book.HireUntil > 0.0 && gh >= 0.0 && gh >= book.HireUntil)
            {
                // riding: they stay in the car until you get out
                if (mounted) { if (!endPending) { endPending = true; toast(displayName + "'s day is up. They'll get out when you do."); } }
                else { Dismiss("{name}'s day is up. They head back."); return; }
            }

            float dist = Game.HDist(npc.transform.position, pl.position);
            lastDistance = dist;
            float now = Time.time;

            WatchPlayerHealth(pl);
            bool inCar;
            GameObject car = PlayerCar(pl, out inCar);

            // riding
            if (!rideCars.Value && mounted) Dismount(true);
            if (rideCars.Value)
            {
                if (inCar && car != null)
                {
                    if (!mounted)
                    {
                        if (inCarSince < 0f) inCarSince = now;
                        if (now - inCarSince > 1.2f && dist <= rideRange.Value) { if (!TryMount(car)) inCarSince = now + 30f; }     // no seat: do not try again for a while
                    }
                    else if (carObj != car) Dismount(true);
                    else outSince = -1f;
                }
                else
                {
                    inCarSince = -1f;
                    if (mounted)
                    {
                        // wait a moment after you step out, so the ground and the car settle before the follower gets out
                        if (outSince < 0f) outSince = now;
                        if (now - outSince >= ExitDelay) Dismount(true);
                    }
                }
            }
            if (mounted)
            {
                if (now >= nextColCheck) { nextColCheck = now + 0.2f; IgnoreNewColliders(); Rigidbody mrb = npc.GetComponent<Rigidbody>(); if (mrb != null && !mrb.isKinematic) { mrb.velocity = Vector3.zero; mrb.isKinematic = true; } }
                if (guardComp != null) { Threats(pl, dist); ExcludeFromIdle(); FeedTarget(); TurnToTarget(); HoldFire(); PlayDueSounds(); WatchShots(); FireShots(); }       // the gunner needs targets from NPCAI
                return;                         // the seat is kept in LateUpdate
            }
            if (endPending) { Dismiss("{name} has gone their own way."); return; }
            if (now < settleUntil) WakeBrain(false);

            if (now >= nextCalm) { nextCalm = now + (now < calmFast ? 0.5f : 2f); Calm(pl, false); }
            RescueIfLost(pl);
            Threats(pl, dist);
            FootWatch(now);
            ExcludeFromIdle();
            Follow(pl, dist, now);
            CatchUp(pl, dist, now);
        }

        // one line about the body, for the log when something goes wrong
        static void LogBody(string what)
        {
            try
            {
                Rigidbody rb = npc.GetComponent<Rigidbody>();
                int on = 0, off = 0, trig = 0;
                foreach (Collider c in npc.GetComponentsInChildren<Collider>(true)) { if (c.enabled) on++; else off++; if (c.isTrigger) trig++; }
                Rigidbody[] rbs = npc.GetComponentsInChildren<Rigidbody>(true);
                RaycastHit h; bool g = Physics.Raycast(npc.transform.position + Vector3.up * 2f, Vector3.down, out h, 200f, ~0, QueryTriggerInteraction.Ignore);
                Plugin.Info("Hire: body (" + what + "): pos " + npc.transform.position.ToString("0.0") + ", layer " + npc.layer + ", parent " + (npc.transform.parent != null ? npc.transform.parent.name : "none")
                    + ", rb " + (rb == null ? "none" : "kin=" + rb.isKinematic + " grav=" + rb.useGravity + " detect=" + rb.detectCollisions + " mode=" + rb.collisionDetectionMode + " vel=" + rb.velocity.ToString("0.0"))
                    + ", rigidbodies " + rbs.Length + ", colliders on/off/trigger " + on + "/" + off + "/" + trig
                    + ", ground below " + (g ? h.collider.name + " at " + h.point.y.ToString("0.0") : "none"));
            }
            catch (Exception e) { Plugin.Info("Hire: LogBody failed: " + e.Message); }
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            int guard = 0;
            while (queue.Count > 0 && guard++ < 4000)
            {
                Transform t = queue.Dequeue();
                if (t.name == name) return t;
                for (int i = 0; i < t.childCount; i++) queue.Enqueue(t.GetChild(i));
            }
            return null;
        }

        // ---- the end -------------------------------------------------------------------------------------------------
        static void Died()
        {
            string n = displayName;
            string fac = book.HireFaction;
            if (mounted) Dismount(false);
            Clear();
            toast("Your hired help " + n + " was killed.");
            if (!string.IsNullOrEmpty(fac) && deathPenalty.Value > 0) changeRep(fac, -deathPenalty.Value, null);
        }

        // the object is gone: killed if it was close to you, otherwise it was just unloaded (the hire simply ends)
        static void Gone()
        {
            if (lastDistance < 80f && !wasMountedAtLoss) { Died(); return; }
            string n = displayName;
            if (mounted) Dismount(false);
            Clear();
            toast(n + " got lost. The hire is over.");
        }
        internal static void Shutdown()
        {
            QuestFollower.Release();
            if (mounted) Dismount(true);
            ReleaseNpc(); RestoreCollisions(); Reset();
            if (recoilHooked) Application.onBeforeRender -= Recoil;
            recoilHooked = false; book = null; toast = null; changeRep = null;
        }
    }
}
