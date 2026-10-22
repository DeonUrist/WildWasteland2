using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Brain part: cover (low health, the game's hide, Dog Companion threat, fright retreat spots) and the cover stay.</summary>
    internal static partial class Brain
    {
        // ---------- cover (1.1.0) ----------
        // (perf, README 8 #5) the "Hide" cover objects come from a Core WorldIndex watch (sliced world walk, tiles and scene loads) instead
        // of a FindGameObjectsWithTag scene scan every 10 s from the think path; the active ones are copied into _hides every 10 s as before
        private static readonly List<GameObject> _hides = new List<GameObject>(); private static float _nextHides;
        private static WildWasteland.Core.WorldWatch _hideWatch;

        /// <summary>Registers the WorldIndex watch for objects tagged "Hide" (module start).</summary>
        internal static void InstallHides(WildWasteland.Core.IModule owner) { _hideWatch = WildWasteland.Core.WorldIndex.Watch(owner, IsHideObject, null, null, false); _nextHides = 0f; }

        /// <summary>WorldIndex filter: the node is tagged "Hide" (a cover object of a camp).</summary>
        private static bool IsHideObject(Transform t, string name) { return t.CompareTag("Hide"); }

        /// <summary>Refreshes the active cover objects from the watch (every 10 s).</summary>
        private static void RefreshHides()
        {
            _hides.Clear();
            var found = _hideWatch != null ? _hideWatch.Found : null;
            if (found == null) return;
            for (int i = 0; i < found.Count; i++) { var t = found[i]; if (t != null && t.gameObject.activeInHierarchy) _hides.Add(t.gameObject); }
        }

        /// <summary>Nearest active cover object within range of pos (null when none); refreshes the shared list every 10 s.</summary>
        private static GameObject NearestHide(Vector3 pos, float range)
        {
            if (Time.time >= _nextHides) { _nextHides = Time.time + 10f; RefreshHides(); }
            GameObject best = null; float bd = range * range;
            if (_hides != null) foreach (var h in _hides) { if (h == null) continue; float d2 = (h.transform.position - pos).sqrMagnitude; if (d2 < bd) { bd = d2; best = h; } }
            return best;
        }

        /// <summary>Low-health cover decision (and the game's own hide state): a camp cover object or a map spot out of the target's sight within CoverRange.</summary>
        private static void CoverCheck(Npc n, float now, string ast, int kind, Vector3 tp)
        {
            // the game's hide: its own FindClosest result (the Attack FSM's Hide variable), while it is in that state
            if (ast == "hide")
            {
                if (now < n.CoverCooldownUntil) return;
                var h = DrivingHide(n, now);
                if (h == null) { n.Attack.SendEvent("back"); return; }
                if (h != null && (h.transform.position - n.T.position).sqrMagnitude <= 60f * 60f) { StartCover(n, h.transform.position, now, 60f, true, "the game's hide: " + h.name); return; }
                return;
            }
            float below = NpcaiSettings.CoverBelow.Value;
            if (below <= 0f || kind != 1 || now < n.NextCoverCheck || now < n.CoverCooldownUntil || n.HpVar == null) return;
            n.NextCoverCheck = now + 3f;
            float hp = n.HpVar.Value;
            if (hp > n.HpMax) n.HpMax = hp;
            if (n.HpMax <= 0f || hp >= n.HpMax * below / 100f) return;
            float range = NpcaiSettings.CoverRange.Value;
            Vector3 spot; string what;
            var hide = NearestHide(n.T.position, range);
            if (hide != null && (hide.transform.position - tp).sqrMagnitude > 16f) { spot = hide.transform.position; what = "cover object " + hide.name; }
            else if (Nav.CoverSpot(n.T.position, tp, range, out spot)) what = "a spot out of sight on the map";
            else { n.NextCoverCheck = now + 10f; return; }
            StartCover(n, spot, now, UnityEngine.Random.Range(NpcaiSettings.CoverMin.Value, Mathf.Max(NpcaiSettings.CoverMin.Value, NpcaiSettings.CoverMax.Value)), false, "health " + hp.ToString("0") + "/" + n.HpMax.ToString("0") + ", " + what);
        }

        /// <summary>The game's hide against a player in a car: the nearest camp cover object within 60 m (humans with melee only), re-picked once a second.</summary>
        private static GameObject DrivingHide(Npc n, float now)
        {
            var agent = Senses.AgentFor(n.Owner);
            if (n.Ranged || agent == null || !agent.Human || !Senses.PlayerInCar) return null;
            if (now >= n.NextVehicleHideCheck)
            {
                n.NextVehicleHideCheck = now + 1f; n.VehicleHide = null;
                NearestHide(n.T.position, 60f); float best = 60f * 60f;
                if (_hides != null) foreach (var h in _hides)
                {
                    if (h == null) continue; float d = (h.transform.position - n.T.position).sqrMagnitude; if (d >= best) continue;
                    var root = h.transform; while (root != null && !root.name.StartsWith("Camp_", StringComparison.Ordinal)) root = root.parent;
                    if (root != null) { n.VehicleHide = h; best = d; }
                }
            }
            return n.VehicleHide != null && (n.VehicleHide.transform.position - n.T.position).sqrMagnitude <= 60f * 60f ? n.VehicleHide : null;
        }

        /// <summary>A human fighting the player with a Dog Companion close by runs to cover away from the dog.</summary>
        private static void CompanionCoverCheck(Npc n, float now)
        {
            if (now < n.NextDogCoverCheck || now < n.DogCoverRetry || n.T.parent != null) return;
            n.NextDogCoverCheck = now + .5f;
            var agent = Senses.AgentFor(n.Owner);
            if (agent == null || !agent.Human || !agent.Hostile.Contains("Player") || (agent.State != Senses.State.Combat && agent.State != Senses.State.Hide)) return;
            var threat = Core.ModApi.Get<Core.ICompanionThreat>()?.CloseThreat(n.Owner, 3f);   // refs-ok: REFACTOR-REQUEST B07-11 (Dog Companion threat query)
            if (threat == null) return;
            foreach (var fsm in n.Owner.GetComponents<PlayMakerFSM>()) if (fsm.FsmName == "BossUI") return;
            Vector3 dogPos = threat.transform.position, spot = Vector3.zero;
            float range = NpcaiSettings.CoverRange.Value, best = range * range; bool found = false;
            NearestHide(n.T.position, range); // refresh the existing shared Hide cache
            foreach (var hide in _hides)
            {
                if (hide == null) continue;
                Vector3 p = hide.transform.position;
                float distance = (p - n.T.position).sqrMagnitude;
                if (distance < 2.25f || distance >= best || (p - dogPos).sqrMagnitude < 25f) continue;
                best = distance; spot = p; found = true;
            }
            if (!found) found = Nav.CoverSpot(n.T.position, dogPos, range, out spot) && (spot - dogPos).sqrMagnitude >= 25f && (spot - n.T.position).sqrMagnitude > 2.25f;
            if (!found) return; // no cover: keep fighting, rather than inventing a flee state
            StartCover(n, spot, now, 4f, false, "nearby companion dog: brief retreat to cover");
            n.DogCoverThreat = threat; n.DogCoverRetry = now + 12f;
        }

        /// <summary>Starts a cover stay: spot, duration, cooldown; the NPC runs there.</summary>
        private static void StartCover(Npc n, Vector3 spot, float now, float seconds, bool fromHide, string why)
        {
            n.CoverPos = spot; n.CoverArrived = false; n.CoverFromHide = fromHide; n.CoverSince = now; n.CoverUntil = now + seconds;
            n.CoverCooldownUntil = n.CoverUntil + 20f;
            SetMode(n, Mode.Cover, WildWasteland.Core.Log.L2 ? why + ", " + Vector3.Distance(spot, n.T.position).ToString("0") + " m away" : "");
        }

        /// <summary>Cover stay: run to the spot, fight from it, leave early when the target comes close or the stay is over.</summary>
        private static void CoverThink(Npc n, float now, string ast, int kind, Vector3 tp)
        {
            Vector3 c3 = n.CoverPos - n.T.position; c3.y = 0f;
            float cd = c3.magnitude, td = n.Dist;
            // leaves: the stay is over, the target is on top of it, the game's hide ended (a slow target again), or it is searching /
            // vanilla (no target at all). A ghost (lost sight) does not pull a wounded raider out of cover.
            bool dogCover = n.DogCoverThreat != null;
            bool leave = now >= n.CoverUntil || (!dogCover && kind == 1 && td < 8f)
                || (n.CoverFromHide && DrivingHide(n, now) == null)
                || (dogCover && (!n.DogCoverThreat.activeInHierarchy || n.DogCoverThreat.transform.parent != null || (n.DogCoverThreat.transform.position - n.T.position).sqrMagnitude > 36f))
                || (n.CoverFromHide && ast != "hide" && now - n.CoverSince > 1f) || kind == 3 || (kind == 0 && Senses.On);
            if (leave)
            {
                if (n.CoverFromHide && ast == "hide") n.Attack.SendEvent("back");
                if (n.CoverArrived) Move(n, true);
                SetMode(n, Mode.Chase, WildWasteland.Core.Log.L2 ? (kind == 1 && td < 8f ? "target too close for cover" : now >= n.CoverUntil ? "cover time over" : kind != 1 ? "nothing to hide from" : "the game's hide ended") : "");
                return;
            }
            if (n.CoverArrived) return;              // holds there: faces the target (Tick), the game's burst logic + Aim shoot from cover
            if (cd <= 1.5f)
            {
                n.CoverArrived = true; n.HasHeading = false;
                Move(n, false);
                if (n.Ranged) { Aim(n); if (NpcaiSettings.CrouchChance.Value > 0f && UnityEngine.Random.Range(0f, 100f) < NpcaiSettings.CrouchChance.Value * 1.5f) Crouch(n, true); }
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " in cover, " + td.ToString("0") + " m from its target");
                return;
            }
            MoveToward(n, n.CoverPos, c3, cd, now);
        }

        /// <summary>Senses: hit by something - a holding gunman steps aside and (maybe) kneels</summary>
        internal static void Hit(GameObject owner)
        {
            var n = Get(owner);
            if (n == null || n.Mode == Mode.Off || n.Rb == null) return;
            if (n.Mode == Mode.Hold || (n.Mode == Mode.Cover && n.CoverArrived))
            {
                // a shooter that is hit sidesteps 2-3 m (SideStepSpeed m/s for SideStepSeconds), at most every 1.5 s, and kneels more often
                float now = Time.time;
                if (now < n.MakeWayUntil + 1.5f) return;
                if (n.Crouched) Crouch(n, false);
                else if (n.Ranged && NpcaiSettings.CrouchChance.Value > 0f && UnityEngine.Random.Range(0f, 100f) < NpcaiSettings.CrouchChance.Value * 2f) Crouch(n, true);
                if (n.Body == null) return;       // no strafe clips (Apocaplayer not installed): no sidestep - sliding without an animation looks wrong
                bool left = UnityEngine.Random.value < 0.5f;
                Vector3 dir = n.T.right * (left ? -1f : 1f);
                Vector3 origin = n.Col != null ? n.Col.bounds.center : n.T.position + Vector3.up;
                if (Physics.Raycast(origin, dir, 2.5f, PathMask, QueryTriggerInteraction.Ignore)) { dir = -dir; left = !left; }      // a wall that way: the other way
                n.MakeWayDir = dir; n.MakeWaySpeed = 2.2f; n.MakeWayUntil = now + 0.9f;
                StrafeClip(n, left);
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " is hit, sidesteps " + (left ? "left" : "right"));
            }
        }

        /// <summary>speed while turning: 1 within 30 deg of the wanted heading, down to 0.1 at 120 deg and beyond (also used by Idle)</summary>
        internal static float TurnSpeedFactor(float offDeg) { return Mover.SpeedFactor(offDeg); }

        // (1.4.7) Blocked: over BlockedSeconds the body covered less than BlockedRatio of the distance it was driven (running in place against
        // something, grinding along a wall) -> the same answer as a stuck: hop, or a step back and another way (on a map route the blocked
        // heading is skipped for BlockedMemory s; 3 times in 8 s -> full feelers for 3 s). Never keeps pushing.
        // (1.2.1) how close to a remembered spot counts as being there: [Senses] ArriveDistance for people, but a creature (no humanoid body)
        // that bites with a short AttackRaycast (a small scorpion: 0.3 m) must come to half its reach, else it stops 1.5 m short of the
        // player's exact position, "searches" there and never bites
        private static readonly Dictionary<int, float> _arrive = new Dictionary<int, float>();
        /// <summary>How close to a remembered spot counts as arrived: [Senses] ArriveDistance for people, half the bite reach for creatures with a short attack ray.</summary>
        internal static float ArriveFor(GameObject owner)
        {
            float cfg = Mathf.Max(0.5f, NpcaiSettings.ArriveDistance.Value);
            if (owner == null) return cfg;
            int id = owner.GetInstanceID(); float v;
            if (_arrive.TryGetValue(id, out v)) return v;
            v = cfg;
            try
            {
                var an = owner.GetComponentInChildren<Animator>(true);
                bool human = an != null && an.avatar != null && an.avatar.isHuman;
                if (!human)
                {
                    var r = owner.transform.Find("AttackRaycast");
                    var s = r != null ? r.GetComponent<Micosmo.SensorToolkit.RaySensor>() : null;
                    if (s != null && s.Length > 0.01f) v = Mathf.Clamp(s.Length * 0.5f, 0.1f, cfg);
                }
            }
            catch (Exception) { }
            if (_arrive.Count > 400) _arrive.Clear();
            _arrive[id] = v;
            return v;
        }

        /// <summary>Blocked check: over BlockedSeconds the body covered less than BlockedRatio of the distance it was driven (running in place, grinding along a wall).</summary>
        private static bool Blocked(Npc n, float now)
        {
            float dt = now - n.BlkLastThink; n.BlkLastThink = now;
            bool moving = n.Mode == Mode.Chase || n.Mode == Mode.Advance || (n.Mode == Mode.Cover && !n.CoverArrived);
            // (1.2.1) a swing (the Attack FSM's attack state) stops the body by itself: standing there is not being blocked, no step back
            if (moving) { string ast = AttackState(n); if (ast != null && !ChaseState(ast)) moving = false; }
            if (!moving || dt > 1f || Senses.Blown(n.Owner) || n.Rb == null) { BlkReset(n, now); return false; }
            n.BlkDriven += n.CmdSpeed * dt;
            if (now - n.BlkSince < Mathf.Max(0.2f, NpcaiSettings.BlockedSeconds.Value)) return false;
            Vector3 mv = n.T.position - n.BlkPos; mv.y = 0f;
            float driven = n.BlkDriven, covered = mv.magnitude;
            BlkReset(n, now);
            if (driven < 0.6f || covered >= driven * NpcaiSettings.BlockedRatio.Value) return false;
            if (PoiNav.Guiding(n.Owner)) return false;     // (WW) on a POI route its own watchdog slides the body on (no step back, no feelers)
            if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " blocked (covered " + covered.ToString("0.0") + " of " + driven.ToString("0.0") + " m)" + (n.OnNav ? " on its map route" : ""));
            OnStuck(n);
            return true;
        }
        /// <summary>Starts a new blocked-check window at the current position.</summary>
        private static void BlkReset(Npc n, float now) { n.BlkSince = now; n.BlkDriven = 0f; n.BlkPos = n.T.position; }

    }
}
