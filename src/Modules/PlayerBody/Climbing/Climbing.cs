using System;
using System.Collections.Generic;
using System.IO;

using BepInEx;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    internal sealed class ClimbingController
    {
        internal static ClimbingController Instance;
        const float FinishFraction = .65f;
        int jumpAttemptFrame = -1;
        bool jumpConsumed;
        float jumpProbeUntil, nextJumpProbe;
        // (WW) the settings are PlayerBodyModule entries: [PlayerBody / Climbing] Climbing, JumpTriggersClimbing (F6), [Keys] Climb,
        // and the tuning in F7 (tools.cfg [PlayerBody / Climbing]); migrated from Apocaplayer [climbing]/[Debug] and Apocaclimber's cfg
        static ConfigEntry<bool> enabledMod { get { return PlayerBodyModule.ClimbingOn; } }
        static ConfigEntry<bool> jumpTriggers { get { return PlayerBodyModule.JumpTriggersClimbing; } }
        static ConfigEntry<float> minHeight { get { return PlayerBodyModule.ClimbMinHeight; } }
        static ConfigEntry<float> maxHeight { get { return PlayerBodyModule.ClimbMaxHeight; } }
        static ConfigEntry<float> reach { get { return PlayerBodyModule.ClimbReach; } }
        static ConfigEntry<float> waistLimit { get { return PlayerBodyModule.ClimbWaistLimit; } }
        static ConfigEntry<float> speed { get { return PlayerBodyModule.ClimbSpeed; } }
        // the climbing bundle stays loaded for the whole game session (loading the same AssetBundle twice fails after a module restart)
        static AssetBundle bundle;
        static AnimationClip waist, high;
        Rigidbody body;
        ClimbGeometry geometry;
        ClimbTarget target;
        PlayerTraversal animation;
        readonly List<PlayMakerFSM> suspended = new List<PlayMakerFSM>();
        readonly List<RendererState> hidden = new List<RendererState>();
        bool oldKinematic, oldGravity, climbing, highClimb;
        RigidbodyInterpolation oldInterpolation;
        float elapsed, duration, cooldown, playbackSpeed;
        Vector3 lastSafe, surfaceLocal;
        Quaternion surfaceRotation;

        struct RendererState { public Renderer Renderer; public bool Enabled; }

        // module Init (was the constructor at config time): one controller, the clips loaded once
        internal static void Init()
        {
            if (Instance == null) Instance = new ClimbingController();
            if (waist != null && high != null) return;
            try
            {
                if (bundle == null)
                {
                    string path = PlayerBodyModule.ModPath("Models/apocaplayer_climbing.bundle");
                    bundle = AssetBundle.LoadFromFile(path);
                }
                if (bundle != null) foreach (var clip in bundle.LoadAllAssets<AnimationClip>())
                {
                    if (clip.name == "ClimbWaist") waist = clip;
                    if (clip.name == "ClimbHigh") high = clip;
                }
                if (waist == null || high == null || !waist.humanMotion || !high.humanMotion)
                    throw new InvalidDataException("Missing Humanoid ClimbWaist/ClimbHigh clips");
                PlayerBodyModule.Info("Integrated climbing ready; waist " + waist.length.ToString("0.00") + "s, high " + high.length.ToString("0.00") + "s.");
            }
            catch (Exception e) { PlayerBodyModule.Error("Climbing animation bundle: " + e.Message); waist = high = null; }
        }
        internal static bool ClipsLoaded { get { return waist != null && high != null; } }
        internal static AnimationClip Clip(bool highClimb) { return highClimb ? high : waist; }
        internal bool Climbing { get { return climbing; } }

        // the scheduler jobs (was ClimbRunner, [DefaultExecutionOrder(-1000)]: Update -> Phase.EarlyUpdate, FixedUpdate -> EarlyFixedUpdate)
        internal static void TickInstance() { if (Instance != null) Instance.Tick(); }
        internal static void FixedTickInstance() { if (Instance != null) Instance.FixedTick(); }
        internal static void StopInstance() { if (Instance != null) Instance.Stop(); }
        internal static void ResetInstance() { if (Instance != null) Instance.Reset(); }
        // (WW) floating origin: the climb path and the last safe spot are world positions kept for the climb's duration
        internal static void ShiftInstance(Vector3 delta)
        {
            var c = Instance;
            if (c == null || !c.climbing) return;
            c.target.Start += delta; c.target.Lift += delta; c.target.End += delta; c.lastSafe += delta;
        }

        internal bool TryJumpClimb()
        {
            if (!jumpTriggers.Value || !GameBindings.Down("Jump", KeyCode.Space) || !enabledMod.Value
                || !ModAPI.PlayerCanTraverse || Cursor.visible) return false;
            if (climbing) return true;
            // A failed early Update probe must not block the Jump FSM's later
            // probe: movement/grounding may have updated in between those calls.
            if (jumpAttemptFrame != Time.frameCount)
            { jumpProbeUntil = Time.time + 2f; nextJumpProbe = Time.time + .05f; }
            jumpAttemptFrame = Time.frameCount;
            jumpConsumed = TryStart(true);
            return jumpConsumed;
        }

        // (WW ladders) the top of a ladder: mantle onto the platform the ladder faces
        Vector3 mantleForward;
        internal bool TryMantle(Vector3 forward)
        {
            mantleForward = forward;
            try { return TryStart(true); }
            finally { mantleForward = Vector3.zero; }
        }

        bool TryStart(bool allowAirborne)
        {
            if (climbing || !PlayerBodyModule.Running || !enabledMod.Value || waist == null || high == null
                || Time.unscaledTime < cooldown || !ModAPI.PlayerCanTraverse || Cursor.visible) return false;
            var player = ModAPI.PlayerRoot;
            var rb = player != null ? player.GetComponent<Rigidbody>() : null;
            if (rb == null) return false;
            if (body != rb) { body = rb; geometry = new ClimbGeometry(rb); }
            var camera = Game.PlayerCamera;
            var forward = mantleForward.sqrMagnitude > 0f ? mantleForward : camera != null ? camera.forward : player.transform.forward;
            if (!geometry.Find(forward, minHeight.Value, Mathf.Max(minHeight.Value, maxHeight.Value), reach.Value, allowAirborne, out target))
            { if (LastReject != geometry.Rejection) Reject(geometry.Rejection); return false; }
            Begin();
            return climbing;
        }

        internal void Tick()
        {
            try
            {
                if (climbing)
                {
                    if (!PlayerBodyModule.Running || !enabledMod.Value || animation == null || !animation.Active || !body || body.gameObject != ModAPI.PlayerRoot
                        || target.Surface == null || !target.Surface.enabled || !target.Surface.gameObject.activeInHierarchy)
                    { Finish(false, "Player or surface unavailable"); return; }
                    if (Time.timeScale < .01f) return;
                    if (!ModAPI.PlayerCanTraverse) { Finish(false, "Player cannot continue climbing"); return; }
                    // Parked cars work. Cancel safely if somebody moves or rotates the
                    // support instead of dragging the character through new geometry.
                    if (!ClimbGeometry.Slow(target.Surface)
                        || Vector3.Distance(target.Surface.transform.TransformPoint(surfaceLocal), target.End) > .08f
                        || Quaternion.Angle(target.Surface.transform.rotation, surfaceRotation) > 3f)
                    { Finish(false, "Surface moved"); return; }
                    animation.Sample(Mathf.Min(elapsed, duration) * playbackSpeed, target.Facing);
                    return;
                }
                if (jumpTriggers.Value && GameBindings.Down("Jump", KeyCode.Space)) TryJumpClimb();
                if (!climbing && jumpTriggers.Value && Time.time < jumpProbeUntil && Time.time >= nextJumpProbe && body != null
                    && (Game.JumpState == "Jump" || Game.JumpState == "Falling" || Mathf.Abs(body.velocity.y) > .6f))
                { nextJumpProbe = Time.time + .05f; TryStart(true); }
                if (!climbing && GameBindings.KeyDown(PlayerBodyModule.ClimbKeyCode)) TryStart(false);
            }
            catch (Exception e) { Finish(false, "Exception"); PlayerBodyModule.Error(e); }
        }

        void Begin()
        {
            highClimb = target.Rise > waistLimit.Value && !target.LowCatch;
            var clip = highClimb ? high : waist;
            animation = ModAPI.BeginPlayerTraversal(clip, target.Facing);
            if (animation == null) { Reject("Apocaplayer could not take control of the body animation"); return; }
            animation.StowWeapon = true;
            animation.FollowHeadInFirstPerson = true;
            oldKinematic = body.isKinematic; oldGravity = body.useGravity; oldInterpolation = body.interpolation;
            // Set climbing before any mutation so exceptions restore every saved state.
            climbing = true; jumpProbeUntil = 0; elapsed = 0; playbackSpeed = speed.Value;
            // Both source clips finish their ascent before the final standing tail.
            // Give movement back at 65%; the pose blends out without holding physics.
            duration = clip.length * FinishFraction / playbackSpeed; lastSafe = body.position;
            surfaceLocal = target.Surface.transform.InverseTransformPoint(target.End);
            surfaceRotation = target.Surface.transform.rotation;
            foreach (var fsm in body.GetComponents<PlayMakerFSM>())
                if (fsm.enabled && (fsm.FsmName == "Movement" || fsm.FsmName == "Jump" || fsm.FsmName == "InCar")) Suspend(fsm);
            // Keep the camera active; suppress attacks and the game's separate first-person
            // weapon meshes while both hands are needed for climbing.
            var holder = animation.Camera;
            if (holder != null)
            {
                foreach (var fsm in holder.GetComponentsInChildren<PlayMakerFSM>(true))
                    if (fsm.enabled && (fsm.FsmName == "Attack" || fsm.FsmName == "Reload" || fsm.FsmName == "ReloadAnimation")) Suspend(fsm);
                foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
                    if (renderer.transform.name != "" && (Under(renderer.transform, "WeaponsArm") || Under(renderer.transform, "QuickItems") || Under(renderer.transform, "ItemAnim")))
                    { hidden.Add(new RendererState { Renderer = renderer, Enabled = renderer.enabled }); renderer.enabled = false; }
            }
            body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            body.useGravity = false; body.isKinematic = true; body.interpolation = RigidbodyInterpolation.None;
            animation.Sample(0, target.Facing);
            if (PlayerBodyModule.VerboseOn) PlayerBodyModule.Info("Climb " + clip.name + " onto " + target.Surface.name + ", height " + target.Rise.ToString("0.00") + "m");
        }

        static bool Under(Transform t, string name)
        { for (; t != null; t = t.parent) if (t.name == name) return true; return false; }
        void Suspend(PlayMakerFSM fsm) { suspended.Add(fsm); fsm.enabled = false; }
        internal static string LastReject = "", LastResult = "";   // (WW) F7 "Climbing" page
        void Reject(string why) { LastReject = why; }

        internal void FixedTick()
        {
            if (!climbing || body == null || Time.timeScale < .01f) return;
            try
            {
                float nextTime = Mathf.Min(duration, elapsed + Time.fixedDeltaTime);
                var next = ClimbPath.At(target, nextTime / duration, highClimb);
                if (!geometry.Clear(body.position, next)) { Finish(false, "Path became blocked"); return; }
                body.position = next; lastSafe = next; elapsed = nextTime;
                animation.Sample(elapsed * playbackSpeed, target.Facing);
                if (elapsed >= duration) Finish(true, "Complete");
            }
            catch (Exception e) { Finish(false, "Physics exception"); PlayerBodyModule.Error(e); }
        }

        void Finish(bool completed, string why)
        {
            if (!climbing && animation == null) return;
            climbing = false;
            // Restore physics before restarting FSMs: their OnEnable can apply forces.
            if (body != null)
            {
                body.position = lastSafe;
                body.isKinematic = oldKinematic; body.useGravity = oldGravity; body.interpolation = oldInterpolation;
                if (!body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            }
            if (animation != null) { animation.Dispose(); animation = null; }
            foreach (var state in hidden) if (state.Renderer != null) state.Renderer.enabled = state.Enabled;
            hidden.Clear();
            foreach (var fsm in suspended) if (fsm != null) fsm.enabled = true;
            suspended.Clear();
            cooldown = Time.unscaledTime + .15f;
            LastResult = completed ? "finished" : why;
            if (PlayerBodyModule.VerboseOn) PlayerBodyModule.Info("Climb " + (completed ? "finished" : "cancelled") + ": " + why);
        }

        internal void Stop() { Finish(false, "Runner disabled"); }
        internal void Reset()
        { Stop(); body = null; geometry = null; cooldown = jumpProbeUntil = nextJumpProbe = 0; jumpAttemptFrame = -1; jumpConsumed = false; }
        
    }
}
