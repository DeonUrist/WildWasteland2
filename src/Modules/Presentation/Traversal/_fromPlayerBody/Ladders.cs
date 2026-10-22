using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    // (WW) Ladders: the game's Player/UseLadder FSM still detects the ladder (its trigger tag "Ladder", state "input"); while it holds the
    // player this controller takes over: weapon stowed, the player centred on the ladder facing it, moved up / down by the Vertical axis
    // with the Climbing Ladder animation (its cycle follows the climbed height, so going down plays it backwards). Jump lets go (weapon
    // back), the ground ends it at the bottom, the platform at the top (a waist mantle when climbing is on, else a step forward).
    // The same PoseClip is meant for NPC climbers later.
    internal sealed class LadderController
    {
        internal static LadderController Instance;
        internal const string Resource = "PlayerBody.Ladder.climbing-ladder.wwpose";
        static PoseClip clip;
        static bool clipTried;

        Rigidbody body;
        ClimbGeometry geometry;
        PlayMakerFSM useLadder;
        FsmGameObject ladderVar;
        GameObject fsmOwner;
        PlayerTraversal animation;
        readonly List<PlayMakerFSM> suspended = new List<PlayMakerFSM>();
        readonly List<RendererState> hidden = new List<RendererState>();
        readonly RaycastHit[] hits = new RaycastHit[16];
        struct RendererState { public Renderer Renderer; public bool Enabled; }

        bool active, oldKinematic, oldGravity, hasPlatform, mantleChecked;
        RigidbodyInterpolation oldInterpolation;
        Collider ladder;
        Vector3 axisPoint, axisUp, side;   // a point on the ladder's centre line, its long axis (leaning ladders), the horizontal direction to the climber
        float bottomY, topY, platformY, feetY, snap, cooldown;
        Vector3 snapFrom;
        Quaternion facing;
        internal static string LastResult = "";

        internal static void Init() { if (Instance == null) Instance = new LadderController(); }
        internal static void TickInstance() { if (Instance != null) Instance.Tick(); }
        internal static void FixedTickInstance() { if (Instance != null) Instance.FixedTick(); }
        internal static void StopInstance() { if (Instance != null) Instance.Finish(Exit.Cancel, "stopped"); }
        internal static void ResetInstance() { if (Instance != null) { Instance.Finish(Exit.Cancel, "reset"); Instance.body = null; Instance.useLadder = null; Instance.fsmOwner = null; } }
        internal static bool Active { get { return Instance != null && Instance.active; } }
        internal static void ShiftInstance(Vector3 delta)
        {
            var c = Instance;
            if (c == null || !c.active) return;
            if (c.rungs != null) for (int i = 0; i < c.rungs.Length; i++) c.rungs[i] += delta.y;
            c.rung0 += delta.y;
            c.axisPoint += delta; c.snapFrom += delta; c.bottomY += delta.y; c.topY += delta.y; c.platformY += delta.y; c.feetY += delta.y;
        }

        static float Speed { get { return PlayerBodyModule.LadderSpeed.Value; } }
        bool sliding; float slideV;
        static float Distance { get { return (clip != null ? clip.ToeDepth : .4f) + PlayerBodyModule.LadderOffset.Value; } }
        const float Snap = .2f, MantleBelow = .95f;

        enum Exit { Cancel, Bottom, Top, Mantle, Jump }

        void Tick()
        {
            try
            {
                if (!PlayerBodyModule.Running) return;
                if (active)
                {
                    if (!PlayerBodyModule.Ladders.Value || animation == null || !animation.Active || body == null || body.gameObject != ModAPI.PlayerRoot
                        || ladder == null || !ladder.enabled || !ladder.gameObject.activeInHierarchy)
                    { Finish(Exit.Cancel, "player or ladder unavailable"); return; }
                    if (Time.timeScale < .01f) return;
                    if (!ModAPI.PlayerCanTraverse) { Finish(Exit.Cancel, "player cannot continue"); return; }
                    if (!Cursor.visible && !Core.ApocasetterBridge.InputBlocked && GameBindings.Down("Jump", KeyCode.Space)) { Finish(Exit.Jump, "jumped off"); return; }
                    animation.Facing = facing;
                    // the cycle follows the rendered (interpolated) height every frame, not the 50 Hz physics steps
                    Services.LadderSlidePhase = PlayerBodyModule.LadderSlidePose.Value; Services.LadderSlideSpeed = PlayerBodyModule.LadderSlideSpeed.Value;
                    // sliding down: one held pose (hands and feet on the rails, no rung steps); climbing: the cycle on the rungs
                    animation.Rungs = sliding || rungSource == "animation spacing" ? null : rungs;
                    animation.Phase = sliding ? PlayerBodyModule.LadderSlidePose.Value : PhaseAt(body.transform.position.y - Game.StandingHeight * .5f);
                    return;
                }
                if (!PlayerBodyModule.Ladders.Value || Time.unscaledTime < cooldown) return;
                var player = ModAPI.PlayerRoot;
                if (player == null) return;
                if (fsmOwner != player)
                {
                    fsmOwner = player; useLadder = Core.FsmUtil.Find(player, "UseLadder");
                    ladderVar = useLadder != null ? useLadder.FsmVariables.GetFsmGameObject("ladder") : null;
                }
                if (useLadder == null || !useLadder.enabled || useLadder.ActiveStateName != "input") return;
                if (ClimbingController.Instance != null && ClimbingController.Instance.Climbing) return;
                if (ModAPI.PlayerTraversalActive || !ModAPI.PlayerCanTraverse) return;
                if (clip == null && !clipTried) { clipTried = true; clip = PoseClip.Shared("ClimbingLadder"); }
                if (clip == null) return;
                var go = ladderVar != null ? ladderVar.Value : null;
                if (go != null) Begin(player, go);
            }
            catch (Exception e) { Finish(Exit.Cancel, "exception"); PlayerBodyModule.Error(e); }
        }

        void Begin(GameObject player, GameObject ladderGo)
        {
            cooldown = Time.unscaledTime + 1f;   // a refused grab is retried after a second, not every frame
            var rb = player.GetComponent<Rigidbody>();
            if (rb == null) return;
            if (body != rb) { body = rb; geometry = new ClimbGeometry(rb); }
            var box = ladderGo.GetComponent<BoxCollider>();
            ladder = box != null ? (Collider)box : ladderGo.GetComponent<Collider>();
            if (ladder == null) return;

            // the ladder's frame: the trigger box's long horizontal axis runs along the rungs, the short one through the ladder
            // (native ladders: a trigger box, long local Y, often leaning ~10 degrees against their wall; ladder_M's solid slab is beside it)
            Vector3 center, through; float length;
            var t = ladder.transform;
            if (box != null && Mathf.Abs(Vector3.Dot(t.up, Vector3.up)) > .7f)
            {
                var s = t.lossyScale;
                float sx = Mathf.Abs(s.x) * box.size.x, sz = Mathf.Abs(s.z) * box.size.z;
                center = t.TransformPoint(box.center); length = Mathf.Abs(s.y) * box.size.y;
                axisUp = t.up.y >= 0f ? t.up : -t.up;
                through = sx >= sz ? t.forward : t.right;
            }
            else
            {
                var b = ladder.bounds; center = b.center; length = b.size.y; axisUp = Vector3.up;
                through = b.size.x >= b.size.z ? Vector3.forward : Vector3.right;
            }
            through.y = 0f;
            if (through.sqrMagnitude < 1e-4f) return;
            through.Normalize();
            bottomY = center.y - length * .5f * axisUp.y; topY = center.y + length * .5f * axisUp.y;
            axisPoint = center;
            float height = topY - bottomY;

            // climb on the player's side unless only the other side has room
            float halfH = Game.StandingHeight * .5f;
            Vector3 p = body.position;
            Vector3 mine = Vector3.Dot(p - center, through) >= 0f ? through : -through;
            feetY = Mathf.Clamp(p.y - halfH, bottomY - 1f, topY);
            float midFeet = Mathf.Clamp(feetY, bottomY, Mathf.Max(bottomY, topY - Game.StandingHeight));
            // a leaning ladder has one climbing side, the one its foot stands out to (the top rests against the wall): always that one
            Vector3 lean = axisUp; lean.y = 0f;
            if (lean.sqrMagnitude > .05f * .05f) mine = Vector3.Dot(-lean, through) >= 0f ? through : -through;
            else if (!geometry.Clear(OnLine(mine, midFeet), OnLine(mine, midFeet)) && geometry.Clear(OnLine(-mine, midFeet), OnLine(-mine, midFeet))) mine = -mine;
            side = mine;
            // face the ladder, pitched with a leaning one (the animation's ladder is vertical)
            Vector3 toLadder = -side - axisUp * Vector3.Dot(-side, axisUp);
            facing = Quaternion.LookRotation(toLadder.sqrMagnitude > 1e-4f ? toLadder.normalized : -side, axisUp);

            // the platform the ladder leads to: on the far side of the ladder, near its top
            hasPlatform = false;
            Vector3 probe = OnLine(side, topY - Game.StandingHeight * .5f + .5f) - side * (Distance + .6f);
            int n = Physics.RaycastNonAlloc(probe, Vector3.down, hits, height + 1.5f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.collider.attachedRigidbody == body || h.normal.y < .7f) continue;
                if (h.point.y > bottomY + 1f && h.point.y <= topY + .5f && h.point.y > best) { best = h.point.y; hasPlatform = true; }
            }
            platformY = hasPlatform ? best : topY - Game.StandingHeight * .5f;

            animation = ModAPI.BeginPlayerPoseTraversal(clip, facing);
            if (animation == null) { LastResult = "the body animation is busy"; return; }
            animation.StowWeapon = true;
            animation.FollowHeadInFirstPerson = true;
            animation.EyeForward = PlayerBodyModule.LadderEyeForward.Value;
            animation.NeckBack = PlayerBodyModule.LadderNeckBack.Value;
            FindRungs(t, center);
            animation.Phase = PhaseAt(feetY);
            active = true;   // from here every change is restored by Finish
            Services.PlayerLadderSide = side; Services.PlayerLadder = ladder;
            oldKinematic = body.isKinematic; oldGravity = body.useGravity; oldInterpolation = body.interpolation;
            // the game's own ladder FSM sets the velocity every frame: paused here, sent "back" when the player lets go
            Suspend(useLadder);
            foreach (var fsm in body.GetComponents<PlayMakerFSM>())
                if (fsm.enabled && (fsm.FsmName == "Jump" || fsm.FsmName == "InCar")) Suspend(fsm);
            var holder = animation.Camera;
            if (holder != null)
            {
                foreach (var fsm in holder.GetComponentsInChildren<PlayMakerFSM>(true))
                    if (fsm.enabled && (fsm.FsmName == "Attack" || fsm.FsmName == "Reload" || fsm.FsmName == "ReloadAnimation")) Suspend(fsm);
                foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled && (Under(renderer.transform, "WeaponsArm") || Under(renderer.transform, "QuickItems") || Under(renderer.transform, "ItemAnim")))
                    { hidden.Add(new RendererState { Renderer = renderer, Enabled = renderer.enabled }); renderer.enabled = false; }
            }
            body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            body.useGravity = false; body.isKinematic = true; body.interpolation = RigidbodyInterpolation.Interpolate;   // MovePosition: smooth between physics steps
            snapFrom = body.position; snap = 0f; mantleChecked = false;
            LastResult = "climbing " + ladder.name;
            if (PlayerBodyModule.VerboseOn) PlayerBodyModule.Info("Ladder " + ladder.transform.parent?.name + "/" + ladder.name + ": " + bottomY.ToString("0.00") + ".." + topY.ToString("0.00")
                + " m, platform " + (hasPlatform ? platformY.ToString("0.00") : "none") + ", feet " + feetY.ToString("0.00") + ", " + rungs.Length + " rungs (" + rungSource + ") every " + rungStep.ToString("0.000") + " m from " + rung0.ToString("0.00"));
        }

        // ---------- rungs: one animation cycle climbs two rungs, phase 0 = the left sole on a rung ----------
        // Native ladder meshes (ladder_M, ladder_S; Assets/raw/Mesh dump): first rung centre and spacing in mesh space. Other ladders (the
        // crane's, built into its mesh) use the animation's own spacing from the trigger's bottom.
        static readonly string[] RungMeshes = { "ladder_M", "ladder_S" };
        static readonly float[] RungFirst = { -1.5545f, -.6005f }, RungStep = { .6309f, .636f };
        static readonly int[] RungCount = { 6, 3 };
        float rung0, rungStep;
        float[] rungs;                                // world heights, ascending (shared with the animation for the foot IK)
        readonly List<float> found = new List<float>();
        string rungSource = "";
        void FindRungs(Transform trigger, Vector3 center)
        {
            found.Clear();
            var p = trigger.parent;
            if (p != null)
            {
                MeshRungs(p.GetComponent<MeshFilter>());
                for (int i = 0; i < p.childCount; i++) MeshRungs(p.GetChild(i).GetComponent<MeshFilter>());
            }
            rungSource = found.Count >= 2 ? "ladder mesh" : "";
            if (found.Count < 2) { found.Clear(); CastRungs(); rungSource = found.Count >= 3 ? "cast" : ""; }
            if (found.Count < 3 && rungSource != "ladder mesh")
            {
                // nothing measurable: the animation's own spacing from the ladder's bottom (no foot correction)
                found.Clear(); rungSource = "animation spacing";
                float step = clip.Rise * .5f;
                for (float y = bottomY; y <= topY + step; y += step) found.Add(y);
            }
            found.Sort();
            for (int i = found.Count - 1; i > 0; i--) if (found[i] - found[i - 1] < .1f) found.RemoveAt(i);   // stacked meshes overlap
            rungs = found.ToArray();
            rung0 = rungs[0]; rungStep = rungs.Length > 1 ? (rungs[rungs.Length - 1] - rungs[0]) / (rungs.Length - 1) : clip.Rise * .5f;
            animation.Rungs = rungSource == "animation spacing" ? null : rungs;
        }
        void MeshRungs(MeshFilter mf)
        {
            if (mf == null || mf.sharedMesh == null) return;
            string n = mf.sharedMesh.name;
            for (int k = 0; k < RungMeshes.Length; k++)
            {
                if (!n.StartsWith(RungMeshes[k], StringComparison.Ordinal)) continue;
                var t = mf.transform;
                for (int i = 0; i < RungCount[k]; i++) found.Add(t.TransformPoint(new Vector3(0f, RungFirst[k] + RungStep[k] * i, 0f)).y);
                return;
            }
        }
        // ladders built into another mesh (the crane's: bars every .75 m inside crane.001): a small sphere cast down the ladder's centre
        // line, restarted below each bar it lands on
        void CastRungs()
        {
            float y = topY + .3f;
            for (int guard = 0; guard < 64 && y > bottomY - .3f; guard++)
            {
                Vector3 o = axisPoint + axisUp * ((y - axisPoint.y) / Mathf.Max(.2f, axisUp.y));
                RaycastHit h;
                if (!Physics.SphereCast(o, .04f, Vector3.down, out h, y - bottomY + .3f, ~0, QueryTriggerInteraction.Ignore)) break;
                if (h.collider.attachedRigidbody == body) { y = h.point.y - .15f; continue; }
                if (h.normal.y > .5f && h.point.y > bottomY + .15f) found.Add(h.point.y - .025f);   // a bar's centre, not the ground
                y = h.point.y - .14f;
            }
            if (found.Count < 3) return;
            found.Sort();
            float step = (found[found.Count - 1] - found[0]) / (found.Count - 1);
            if (step < .2f || step > 1.2f) found.Clear();
        }
        // phase 0, 1, 2... = the left sole on every other rung (one animation cycle climbs two rungs, whatever their spacing)
        float PhaseAt(float feet)
        {
            var r = rungs;
            if (r == null || r.Length < 2) return (feet - rung0) / Mathf.Max(.2f, rungStep * 2f);
            if (feet <= r[0]) return (feet - r[0]) / Mathf.Max(.2f, (r[1] - r[0]) * 2f);
            int last = r.Length - 1;
            if (feet >= r[last]) return (last + (feet - r[last]) / Mathf.Max(.1f, r[last] - r[last - 1])) * .5f;
            int k = 0; while (k < last - 1 && feet >= r[k + 1]) k++;
            return (k + (feet - r[k]) / Mathf.Max(.05f, r[k + 1] - r[k])) * .5f;
        }

        // the body centre for feet at `feet`: on the ladder's (leaning) centre line at the hips' height, Distance out on the climber's side
        Vector3 OnLine(Vector3 dir, float feet)
        {
            float y = feet + Game.StandingHeight * .5f;
            Vector3 p = axisPoint + axisUp * ((y - axisPoint.y) / Mathf.Max(.2f, axisUp.y)) + dir * Distance;
            p.y = y;
            return p;
        }

        static bool Under(Transform t, string name) { for (; t != null; t = t.parent) if (t.name == name) return true; return false; }
        void Suspend(PlayMakerFSM fsm) { if (fsm == null) return; suspended.Add(fsm); fsm.enabled = false; }

        void FixedTick()
        {
            if (!active || body == null || Time.timeScale < .01f) return;
            try
            {
                float dt = Time.fixedDeltaTime;
                float v = Cursor.visible || Core.ApocasetterBridge.InputBlocked ? 0f : Mathf.Clamp(Input.GetAxisRaw("Vertical"), -1f, 1f);
                // up: climbs at LadderSpeed; down: a slide that speeds up to LadderSlideSpeed
                if (v < 0f) { slideV = Mathf.Min(slideV + 14f * dt, PlayerBodyModule.LadderSlideSpeed.Value); sliding = true; }
                else { slideV = 0f; sliding = false; }
                float dy = v < 0f ? -slideV * dt : v * Speed * dt;
                float maxFeet = hasPlatform ? platformY + .02f : topY - Game.StandingHeight * .6f;
                float next = Mathf.Min(feetY + dy, maxFeet);
                next = Blocked(feetY, next);
                // the ground below ends a descent
                float ground;
                if (dy < 0f && Ground(next, out ground) && next <= ground + .02f) { feetY = ground; Place(); Finish(Exit.Bottom, "reached the ground"); return; }
                feetY = next;
                Place();
                if (dy > 0f && hasPlatform)
                {
                    // with climbing on, a waist mantle onto the platform once it is in reach (checked once; refused = climb on and step off)
                    if (!mantleChecked && feetY >= platformY - MantleBelow && PlayerBodyModule.ClimbingOn.Value && ClimbingController.ClipsLoaded)
                    {
                        mantleChecked = true;
                        ClimbTarget probe;
                        body.isKinematic = false; body.useGravity = false;
                        bool ok = geometry.Find(-side, PlayerBodyModule.ClimbMinHeight.Value, Mathf.Max(PlayerBodyModule.ClimbMinHeight.Value, PlayerBodyModule.ClimbMaxHeight.Value),
                            PlayerBodyModule.ClimbReach.Value, true, out probe);
                        body.velocity = Vector3.zero; body.isKinematic = true;
                        if (ok) { Finish(Exit.Mantle, "mantle at the top"); return; }
                        if (PlayerBodyModule.VerboseOn) PlayerBodyModule.Info("Ladder: no mantle at the top (" + geometry.Rejection + "), climbing on to step off");
                    }
                    if (feetY >= platformY) { Finish(Exit.Top, "stepped onto the platform"); return; }
                }
            }
            catch (Exception e) { Finish(Exit.Cancel, "physics exception"); PlayerBodyModule.Error(e); }
        }

        // NPCs on this ladder (POI navigation) cannot be climbed through: stop a body length short of them
        float Blocked(float feet, float next)
        {
            var list = Services.LadderClimbers; if (list.Count == 0 || next == feet) return next;
            Vector3 me = OnLine(side, feet);
            for (int i = 0; i < list.Count; i++)
            {
                Vector3 c = list[i];
                if (new Vector2(c.x - me.x, c.z - me.z).sqrMagnitude > 1.2f * 1.2f) continue;
                if (next > feet && c.y > feet - .2f) next = Mathf.Min(next, Mathf.Max(feet, c.y - 1.85f));
                if (next < feet && c.y < feet + .2f) next = Mathf.Max(next, Mathf.Min(feet, c.y + 1.85f));
            }
            return next;
        }

        void Place()
        {
            var target = OnLine(side, feetY);
            if (snap < Snap) { snap += Time.fixedDeltaTime; target = Vector3.Lerp(snapFrom, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(snap / Snap))); }
            body.MovePosition(target);
        }

        bool Ground(float feet, out float y)
        {
            y = float.NegativeInfinity;
            Vector3 from = OnLine(side, feet) - Vector3.up * (Game.StandingHeight * .5f - .3f);
            int n = Physics.RaycastNonAlloc(from, Vector3.down, hits, .5f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.collider.attachedRigidbody == body || h.collider.transform.IsChildOf(body.transform) || h.normal.y < .6f) continue;
                if (h.point.y > y) y = h.point.y;
            }
            return y > float.NegativeInfinity;
        }

        void Finish(Exit how, string why)
        {
            if (!active) { if (animation != null) { animation.Dispose(); animation = null; } return; }
            active = false;
            Services.PlayerLadder = null;
            if (body != null)
            {
                if (how == Exit.Top)
                {
                    // no mantle: a short step from the ladder onto the platform, if the capsule fits there
                    Vector3 from = body.position, to = OnLine(side, platformY + .02f) - side * (Distance + .45f);
                    if (geometry != null && geometry.Clear(from, from + Vector3.up * (to.y - from.y)) && geometry.Clear(new Vector3(from.x, to.y, from.z), to)) body.position = to;
                }
                body.isKinematic = oldKinematic; body.useGravity = oldGravity; body.interpolation = oldInterpolation;
                if (!body.isKinematic)
                {
                    body.angularVelocity = Vector3.zero;
                    body.velocity = Vector3.zero;
                }
            }
            if (animation != null) { animation.Dispose(); animation = null; }
            foreach (var s in hidden) if (s.Renderer != null) s.Renderer.enabled = s.Enabled;
            hidden.Clear();
            foreach (var fsm in suspended) if (fsm != null) fsm.enabled = true;
            suspended.Clear();
            // the ladder FSM leaves "input" (it restores Movement / GroundTest); it grabs again only on a new trigger entry
            if (useLadder != null && useLadder.enabled && useLadder.ActiveStateName == "input") useLadder.SendEvent("back");
            if (how == Exit.Jump && body != null && !body.isKinematic) body.velocity = side * 2.2f + Vector3.up * 2.5f;   // push off backwards
            cooldown = Time.unscaledTime + .3f;
            LastResult = why;
            if (PlayerBodyModule.VerboseOn) PlayerBodyModule.Info("Ladder: " + why);
            if (how == Exit.Mantle && ClimbingController.Instance != null && !ClimbingController.Instance.TryMantle(-side) && PlayerBodyModule.VerboseOn)
                PlayerBodyModule.Info("Ladder: the mantle was refused (" + ClimbingController.LastReject + ")");
        }
    }
}
