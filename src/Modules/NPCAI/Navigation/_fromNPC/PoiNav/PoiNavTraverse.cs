using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    // (WW) A human NPC taking a POI navigation link: a scripted, kinematic move (the brain is put on Travel so it neither steers nor
    // fights meanwhile) with the ladder / climb animation from PlayerBody. Ladder: line up at the foot, climb the rung line, step onto
    // the platform. Climb: rise in front of the face, then forward onto the top. Drop: a short jump down. One NPC per ladder at a time.
    internal static class PoiNavTraverse
    {
        sealed class Run
        {
            public GameObject Owner; public Rigidbody Rb; public PoiNav.Inst Inst; public int Link; public PoiNavLink L;
            public INpcTraversalAnimation Anim; public float Lift; public Vector3 Around; public bool HasAround;
            public bool Kinematic, Gravity; public RigidbodyInterpolation Interp; public float Snap;
            public float Gap; public bool Chasing, Reverse, Jumped;   // Gap: a hanging ladder's bottom above the floor (jump to grab)
            public Vector3 Start, From, To, Bottom, Top; public Quaternion Facing;
            public float T, Climb, Duration, H; public int Stage; public string Key; public bool Down;
            public float SlideV; public float OffSince = -1f; public List<PlayMakerFSM> Paused;
            public List<Collider> Ignored;   // the player's colliders this kinematic body must not shove meanwhile
            public List<Vector3> Glide; public float GlideLen;   // a stuck rescue: no link, slide along these route points
        }
        private static readonly Dictionary<int, Run> _runs = new Dictionary<int, Run>();
        private static readonly Dictionary<string, int> _ladders = new Dictionary<string, int>();
        private static readonly List<int> _done = new List<int>();

        internal static bool Busy(GameObject owner) { return owner != null && _runs.ContainsKey(owner.GetInstanceID()); }
        internal static int Count { get { return _runs.Count; } }

        // (WW) the stuck rescue: an NPC that has not moved on its POI route for a second slides (kinematic, through whatever holds its
        // body) along the next route points - the route is known to be walkable, so the body's width is never what stops it
        internal static bool Glide(GameObject owner, List<Vector3> feet)
        {
            if (owner == null || feet == null || feet.Count < 1 || Busy(owner)) return false;
            var rb = owner.GetComponent<Rigidbody>(); if (rb == null) return false;
            float lift = FeetOffset(owner, rb) + .04f;
            var pts = new List<Vector3> { owner.transform.position };
            var pl = GameRefs.Player;
            foreach (var f in feet)
            {
                Vector3 q = f + Vector3.up * lift;
                if (pl != null)
                {
                    Vector3 pp = pl.transform.position, last = pts[pts.Count - 1];
                    Vector3 fd = new Vector3(q.x - pp.x, 0f, q.z - pp.z);
                    if (fd.sqrMagnitude < 1.2f * 1.2f)      // never slide into the player: stop 1.2 m short of him
                    {
                        Vector3 seg = q - last; float sl = seg.magnitude;
                        for (float t = 0f; t <= sl; t += .1f) { Vector3 c = last + seg * (sl > 0f ? t / sl : 0f); if (new Vector2(c.x - pp.x, c.z - pp.z).sqrMagnitude < 1.2f * 1.2f) { if (t > .05f) pts.Add(last + seg * ((t - .1f) / sl)); break; } }
                        break;
                    }
                }
                pts.Add(q);
            }
            float len = 0f; for (int i = 1; i < pts.Count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
            if (len < .05f) return false;
            var run = new Run
            {
                Owner = owner, Rb = rb, Link = -1, Key = "", Glide = pts, GlideLen = len, Lift = lift,
                Kinematic = rb.isKinematic, Gravity = rb.useGravity, Interp = rb.interpolation, Start = pts[0], Facing = owner.transform.rotation
            };
            Brain.SetTravel(owner, true);
            rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            rb.useGravity = false; rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            NoShove(run, true);
            _runs[owner.GetInstanceID()] = run;
            if (NpcModule.SensesLog.Value) Log.Info("[NPC] " + owner.name + " was stuck on its POI route: slides " + len.ToString("0.0") + " m along it");
            return true;
        }
        static bool StepGlide(Run r)
        {
            float d = Mathf.Min(r.GlideLen, r.T * 2.4f);
            Vector3 p = r.Glide[r.Glide.Count - 1], dir = Vector3.zero;
            for (int i = 1; i < r.Glide.Count; i++)
            {
                float seg = Vector3.Distance(r.Glide[i - 1], r.Glide[i]);
                if (d <= seg || i == r.Glide.Count - 1) { p = Vector3.Lerp(r.Glide[i - 1], r.Glide[i], seg > 1e-4f ? Mathf.Clamp01(d / seg) : 1f); dir = r.Glide[i] - r.Glide[i - 1]; break; }
                d -= seg;
            }
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) r.Facing = Quaternion.Slerp(r.Facing, Quaternion.LookRotation(dir.normalized), .25f);
            Move(r, p);
            return r.T * 2.4f >= r.GlideLen;
        }

        internal static bool Begin(GameObject owner, PoiNav.Inst inst, int link, bool down)
        {
            var l = inst.Data.Links[link];
            string key = inst.Id + "/" + link;
            int holder;
            if (l.Kind == PoiLinkKind.Ladder && _ladders.TryGetValue(key, out holder) && holder != owner.GetInstanceID()) return false;
            var rb = owner.GetComponent<Rigidbody>(); if (rb == null) return false;
            var run = new Run
            {
                Owner = owner, Rb = rb, Inst = inst, Link = link, L = l, Key = key, Down = down,
                Kinematic = rb.isKinematic, Gravity = rb.useGravity, Interp = rb.interpolation,
                Start = owner.transform.position, From = inst.W(l.From), To = inst.W(l.To), Bottom = inst.W(l.LineBottom), Top = inst.W(l.LineTop),
                Facing = inst.Root.rotation * Quaternion.Euler(0f, l.Yaw, 0f)
            };
            Vector3 f = run.Facing * Vector3.forward; f.y = 0f; run.Facing = f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f.normalized) : Quaternion.identity;
            // the data holds where the FEET go; the body's pivot may sit above them (and a landing exactly on a thin roof can sink through it)
            Vector3 lift = Vector3.up * (FeetOffset(owner, rb) + .04f); run.Lift = lift.y;
            run.From += lift; run.To += lift; run.Bottom += lift; run.Top += lift;
            if (l.Kind == PoiLinkKind.Ladder)
            {
                // the body stands off the rung line by the animation's hips-to-rung distance, on the climber's side
                float depth = Services.Animation != null ? Services.Animation.LadderToeDepth : .44f;
                Vector3 back = -(run.Facing * Vector3.forward) * depth;
                // an NPC standing behind the ladder (or level with it) first walks round to the climbing side
                Vector3 line = run.Bottom; Vector3 rel = run.Start - line; rel.y = 0f;
                Vector3 sideDir = back.normalized;
                if (Vector3.Dot(rel, sideDir) < .25f)
                {
                    Vector3 across = Vector3.Cross(Vector3.up, sideDir); if (Vector3.Dot(rel, across) < 0f) across = -across;
                    run.Around = new Vector3(line.x, run.Start.y, line.z) + across * .7f + sideDir * (depth + .35f);
                    run.HasAround = true;
                }
                run.Gap = run.Bottom.y - run.From.y;
                run.Bottom += back; run.Top += back;
                run.Climb = Mathf.Max(.2f, run.Top.y - run.Bottom.y);
                _ladders[key] = owner.GetInstanceID();
            }
            if (l.Kind != PoiLinkKind.Ladder) run.Snap = Mathf.Clamp(Vector3.Distance(run.Start, run.From) / 6f, 0f, .25f);   // a short natural snap onto the start
            if (l.Kind == PoiLinkKind.Climb) run.Duration = (run.To.y - run.From.y) > 1.25f ? 1.9f : 1.3f;
            else run.Duration = Mathf.Clamp(Mathf.Sqrt(2f * Mathf.Max(.2f, run.From.y - run.To.y) / 9.81f) + .25f, .35f, 1f);
            Brain.SetTravel(owner, true);
            rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            rb.useGravity = false; rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            owner.transform.rotation = run.Facing;
            var anim = owner.GetComponentInChildren<Animator>();
            if (Services.Animation != null && anim != null && l.Kind != PoiLinkKind.Drop)
                run.Anim = Services.Animation.BeginNpcTraversal(anim, l.Kind == PoiLinkKind.Ladder ? "Ladder" : (run.To.y - run.From.y) > 1.25f ? "ClimbHigh" : "ClimbWaist");
            NoShove(run, true); NoAttack(run, true);
            _runs[owner.GetInstanceID()] = run;
            if (NpcModule.SensesLog.Value) Log.Info("[NPC] " + owner.name + " takes the " + l.Kind + " of " + inst.Poi + " (" + (run.To.y - run.From.y).ToString("0.0") + " m)");
            return true;
        }

        // on a ladder / mid-climb an NPC neither swings nor shoots: its attack FSMs are paused for the move (and resumed after)
        static void NoAttack(Run r, bool on)
        {
            try
            {
                if (on)
                {
                    r.Paused = new List<PlayMakerFSM>();
                    foreach (var f in r.Owner.GetComponentsInChildren<PlayMakerFSM>())
                        if (f != null && f.enabled && (f.FsmName == "Attack" || f.FsmName == "RangedAttackWait" || f.FsmName == "RangedAttack" || f.FsmName == "Shoot")) { f.enabled = false; r.Paused.Add(f); }
                }
                else if (r.Paused != null) { foreach (var f in r.Paused) if (f != null) f.enabled = true; r.Paused = null; }
            }
            catch (Exception) { }
        }

        // a kinematic body moved by MovePosition shoves anything dynamic out of its way with unlimited force: the player is left alone
        static void NoShove(Run r, bool on)
        {
            try
            {
                if (on)
                {
                    var player = GameRefs.Player; if (player == null || r.Owner == null) return;
                    r.Ignored = new List<Collider>();
                    var mine = r.Owner.GetComponentsInChildren<Collider>();
                    foreach (var pc in player.GetComponentsInChildren<Collider>())
                    {
                        if (pc == null || pc.isTrigger) continue;
                        foreach (var c in mine) if (c != null && !c.isTrigger) Physics.IgnoreCollision(c, pc, true);
                        r.Ignored.Add(pc);
                    }
                }
                else if (r.Ignored != null && r.Owner != null)
                {
                    var mine = r.Owner.GetComponentsInChildren<Collider>();
                    foreach (var pc in r.Ignored) if (pc != null) foreach (var c in mine) if (c != null && !c.isTrigger) Physics.IgnoreCollision(c, pc, false);
                    r.Ignored = null;
                }
            }
            catch (Exception) { }
        }

        internal static float FeetOffset(GameObject owner, Rigidbody rb)
        {
            float lowest = float.MaxValue;
            foreach (var c in owner.GetComponentsInChildren<Collider>())
                if (c != null && c.enabled && !c.isTrigger && c.attachedRigidbody == rb) lowest = Mathf.Min(lowest, c.bounds.min.y);
            return lowest == float.MaxValue ? 0f : Mathf.Clamp(owner.transform.position.y - lowest, -.2f, 1.5f);
        }

        // EarlyFixedUpdate: every traversal moves its body
        internal static void Tick()
        {
            Services.LadderClimbers.Clear();
            if (_runs.Count == 0 || Time.timeScale < .01f) return;
            float dt = Time.fixedDeltaTime;
            _done.Clear();
            foreach (var kv in _runs)
            {
                var r = kv.Value;
                try
                {
                    if (r.Owner == null || !r.Owner.activeInHierarchy || r.Rb == null || (r.L != null && r.Inst.Root == null)) { _done.Add(kv.Key); continue; }
                    if (Step(r, dt)) _done.Add(kv.Key);
                    else if (r.L != null && r.L.Kind == PoiLinkKind.Ladder) Services.LadderClimbers.Add(r.Owner.transform.position - Vector3.up * r.Lift);
                }
                catch (Exception e) { Log.Error("[NPC] POI traversal", e); _done.Add(kv.Key); }
            }
            foreach (int k in _done) { Run r; if (_runs.TryGetValue(k, out r)) End(r, k); }
        }

        // true when finished
        static bool Step(Run r, float dt)
        {
            r.T += dt;
            if (r.Glide != null) return StepGlide(r);
            switch (r.L.Kind)
            {
                case PoiLinkKind.Ladder:
                {
                    if (r.Down) return StepDown(r);
                    if (r.Stage == 0 && r.Gap > .7f)   // a hanging ladder: under it, then a jump up to the bottom rung
                    {
                        Vector3 under = new Vector3(r.Bottom.x, r.Start.y, r.Bottom.z);
                        float u = Mathf.Clamp01(r.T / .9f);
                        if (u < .45f) Move(r, Vector3.Lerp(r.Start, under, Mathf.SmoothStep(0f, 1f, u / .45f)));
                        else
                        {
                            if (!r.Jumped) { r.Jumped = true; Brain.HopAnim(r.Owner); }
                            float k = (u - .45f) / .55f;
                            Move(r, Vector3.Lerp(under, r.Bottom, 1f - (1f - k) * (1f - k)) + Vector3.up * Mathf.Sin(k * Mathf.PI) * .15f);
                        }
                        Phase(r, 0f);
                        if (u >= 1f) { r.Stage = 1; r.T = 0f; }
                        return false;
                    }
                    if (r.Stage == 0)       // line up at the foot of the ladder
                    {
                        float u = Mathf.Clamp01(r.T / (r.HasAround ? .5f : Mathf.Clamp(Vector3.Distance(r.Start, r.Bottom) / 6f, .08f, .25f)));
                        if (r.HasAround) Move(r, u < .5f ? Vector3.Lerp(r.Start, r.Around, Mathf.SmoothStep(0f, 1f, u * 2f)) : Vector3.Lerp(r.Around, r.Bottom, Mathf.SmoothStep(0f, 1f, u * 2f - 1f)));
                        else Move(r, Vector3.Lerp(r.Start, r.Bottom, Mathf.SmoothStep(0f, 1f, u)));
                        Phase(r, 0f);
                        if (u >= 1f) { r.Stage = 1; r.T = 0f; }
                        return false;
                    }
                    if (r.Stage == 1)       // up the rungs
                    {
                        float cap = PlayerCap(r, true); if (cap < float.MaxValue) r.Chasing = true;
                        // the player it climbed after jumped off below it: back down the way it came
                        if (r.Chasing && !r.Reverse && PlayerOff(r) && PlayerFeet(r) < NpcFeet(r) - 1.5f) r.Reverse = true;   // he fell / jumped down well below it
                        if (r.Reverse)
                        {
                            r.H -= Slide(r, dt); float hb = Mathf.Clamp(r.H, 0f, r.Climb);
                            Move(r, Vector3.Lerp(r.Bottom, r.Top, hb / r.Climb));
                            Phase(r, Services.LadderSlidePhase);
                            if (r.H <= 0f) { r.Stage = 3; r.T = 0f; if (r.Anim != null) { r.Anim.Dispose(); r.Anim = null; } }
                            return false;
                        }
                        r.H = Mathf.Min(cap, r.H + dt * PoiNav.LadderSpeed.Value); float h = Mathf.Clamp(r.H, 0f, r.Climb);
                        Move(r, Vector3.Lerp(r.Bottom, r.Top, h / r.Climb));
                        float rise = Services.Animation != null ? Services.Animation.LadderCycleRise : 1.24f;
                        Phase(r, h / Mathf.Max(.2f, rise));
                        if (h >= r.Climb) { r.Stage = 2; r.T = 0f; if (r.Anim != null) { r.Anim.Dispose(); r.Anim = null; } }
                        return false;
                    }
                    if (r.Stage == 3)       // turned back: off the bottom where it started (dropping from a hanging ladder)
                    {
                        float w = Mathf.Clamp01(r.T / .45f);
                        Vector3 p = Vector3.Lerp(r.Bottom, r.Start, w); p.y = Mathf.Lerp(r.Bottom.y, r.Start.y, r.Gap > .7f ? w * w : w);
                        Move(r, p);
                        return w >= 1f;
                    }
                    float v = Mathf.Clamp01(r.T / .5f);   // off onto the platform
                    Vector3 lift = new Vector3(r.Top.x, Mathf.Max(r.Top.y, r.To.y) + .05f, r.Top.z);
                    Move(r, v < .4f ? Vector3.Lerp(r.Top, lift, v / .4f) : Vector3.Lerp(lift, r.To, (v - .4f) / .6f));
                    return v >= 1f;
                }
                case PoiLinkKind.Climb:
                {
                    if (r.T < r.Snap) { Move(r, Vector3.Lerp(r.Start, r.From, Mathf.SmoothStep(0f, 1f, r.T / r.Snap))); return false; }
                    float u = Mathf.Clamp01((r.T - r.Snap) / r.Duration);
                    Vector3 lift = new Vector3(r.From.x, r.To.y + .05f, r.From.z);
                    const float split = .62f;
                    Move(r, u < split ? Vector3.Lerp(r.From, lift, Mathf.SmoothStep(0f, 1f, u / split)) : Vector3.Lerp(lift, r.To, Mathf.SmoothStep(0f, 1f, (u - split) / (1f - split))));
                    Phase(r, u * .65f);   // like the player: the clip's standing tail is skipped
                    return u >= 1f;
                }
                default:
                {
                    if (r.T < r.Snap) { Move(r, Vector3.Lerp(r.Start, r.From, Mathf.SmoothStep(0f, 1f, r.T / r.Snap))); return false; }
                    float u = Mathf.Clamp01((r.T - r.Snap) / r.Duration);
                    Vector3 p = Vector3.Lerp(r.From, r.To, u);
                    p.y = Mathf.Lerp(r.From.y, r.To.y, u * u) + Mathf.Sin(u * Mathf.PI) * .25f;
                    Move(r, p);
                    return u >= 1f;
                }
            }
        }
        // down a ladder: from the platform onto the top of the rung line, down the rungs (the animation plays backwards), off at the foot
        static bool StepDown(Run r)
        {
            float rise = Services.Animation != null ? Services.Animation.LadderCycleRise : 1.24f;
            if (r.Stage == 0)
            {
                float u = Mathf.Clamp01(r.T / .5f);
                Vector3 lift = new Vector3(r.Start.x, Mathf.Max(r.Start.y, r.Top.y) + .05f, r.Start.z);
                Move(r, u < .5f ? Vector3.Lerp(r.Start, lift, u / .5f) : Vector3.Lerp(lift, r.Top, (u - .5f) / .5f));
                Phase(r, r.Climb / Mathf.Max(.2f, rise));
                if (u >= 1f) { r.Stage = 1; r.T = 0f; }
                return false;
            }
            if (r.Stage == 1)
            {
                if (r.Stage == 1 && r.T <= Time.fixedDeltaTime * 1.5f) r.H = r.Climb;
                float cap = PlayerCap(r, false); if (cap > float.MinValue) r.Chasing = true;
                // the player it climbed down after jumped off above it (or climbed out at the top): back up the way it came
                if (r.Chasing && !r.Reverse && PlayerOff(r) && PlayerFeet(r) > NpcFeet(r) + 1.5f) r.Reverse = true;
                if (r.Reverse)
                {
                    r.H += Time.fixedDeltaTime * PoiNav.LadderSpeed.Value * 1.3f; float hb = Mathf.Clamp(r.H, 0f, r.Climb);
                    Move(r, Vector3.Lerp(r.Bottom, r.Top, hb / r.Climb));
                    Phase(r, hb / Mathf.Max(.2f, rise));
                    if (r.H >= r.Climb) { r.Stage = 3; r.T = 0f; if (r.Anim != null) { r.Anim.Dispose(); r.Anim = null; } }
                    return false;
                }
                float down = r.H - Slide(r, Time.fixedDeltaTime);
                if (down < cap) { down = cap; r.SlideV = 0f; }        // held up by the player below: waits there
                r.H = down; float h = Mathf.Clamp(r.H, 0f, r.Climb);
                Move(r, Vector3.Lerp(r.Bottom, r.Top, h / r.Climb));
                Phase(r, Services.LadderSlidePhase);
                if (h <= 0f) { r.Stage = 2; r.T = 0f; if (r.Anim != null) { r.Anim.Dispose(); r.Anim = null; } }
                return false;
            }
            if (r.Stage == 3)       // turned back: up and off onto the platform it came from
            {
                float w = Mathf.Clamp01(r.T / .5f);
                Vector3 lift = new Vector3(r.Top.x, Mathf.Max(r.Top.y, r.Start.y) + .05f, r.Top.z);
                Move(r, w < .4f ? Vector3.Lerp(r.Top, lift, w / .4f) : Vector3.Lerp(lift, r.Start, (w - .4f) / .6f));
                return w >= 1f;
            }
            float v = Mathf.Clamp01(r.T / .4f);
            Vector3 off = Vector3.Lerp(r.Bottom, r.From, v);
            if (r.Gap > .7f) off.y = Mathf.Lerp(r.Bottom.y, r.From.y, v * v);   // a hanging ladder: let go and drop to the floor
            Move(r, off);
            return v >= 1f;
        }

        // the player on this ladder: an NPC climbing towards him stops a body length short (and waits there) instead of passing him
        static float PlayerCap(Run r, bool up)
        {
            var ladder = Services.PlayerLadder; var player = GameRefs.Player;
            if (ladder == null || player == null) return up ? float.MaxValue : float.MinValue;
            Vector3 p = player.transform.position;
            Vector3 onLine = Vector3.Lerp(r.Bottom, r.Top, Mathf.InverseLerp(r.Bottom.y, r.Top.y, p.y));
            if (new Vector2(p.x - onLine.x, p.z - onLine.z).sqrMagnitude > 1.5f * 1.5f) return up ? float.MaxValue : float.MinValue;
            float feet = PlayerFeet(r) - (r.Bottom.y - r.Lift);         // the player's feet, as a height on this ladder (from the NPC's feet at the foot)
            return up ? (feet > r.H ? feet - 1.85f : float.MaxValue) : (feet < r.H ? feet + 1.85f : float.MinValue);
        }

        // feet against feet (Bottom / Top are the body's pivot, Lift above its feet)
        static float NpcFeet(Run r) { return Mathf.Lerp(r.Bottom.y, r.Top.y, Mathf.Clamp01(r.H / r.Climb)) - r.Lift; }
        static float PlayerFeet(Run r)
        {
            var p = GameRefs.Player; if (p == null) return r.Bottom.y - r.Lift;
            var c = p.GetComponent<Collider>(); return c != null ? c.bounds.min.y : p.transform.position.y - .85f;
        }
        // the player has been off every ladder for half a second (not the moment of a jump onto a platform)
        static bool PlayerOff(Run r)
        {
            if (Services.PlayerLadder != null) { r.OffSince = -1f; return false; }
            if (r.OffSince < 0f) r.OffSince = Time.time;
            return Time.time - r.OffSince > .5f;
        }

        // down a ladder: a slide that speeds up to the player's slide speed (metres this step)
        static float Slide(Run r, float dt) { r.SlideV = Mathf.Min(r.SlideV + 14f * dt, Services.LadderSlideSpeed); return r.SlideV * dt; }
        static void Move(Run r, Vector3 p) { r.Rb.MovePosition(p); r.Owner.transform.rotation = r.Facing; }
        static void Phase(Run r, float p) { if (r.Anim != null && r.Anim.Valid) r.Anim.Phase = p; }

        static void End(Run r, int key)
        {
            _runs.Remove(key);
            if (r.L != null && r.L.Kind == PoiLinkKind.Ladder) _ladders.Remove(r.Key);
            if (r.Anim != null) { r.Anim.Dispose(); r.Anim = null; }
            if (r.Rb != null)
            {
                r.Rb.isKinematic = r.Kinematic; r.Rb.useGravity = r.Gravity; r.Rb.interpolation = r.Interp;
                if (!r.Rb.isKinematic) { r.Rb.velocity = Vector3.zero; }
            }
            NoShove(r, false); NoAttack(r, false);
            if (r.Owner != null) Brain.SetTravel(r.Owner, false);
            if (r.Reverse && r.Owner != null) PoiNav.DropRoute(r.Owner);   // turned back: plan again from here
        }

        internal static void Shift(Vector3 d)
        {
            foreach (var r in _runs.Values) { if (r.Glide != null) for (int i = 0; i < r.Glide.Count; i++) r.Glide[i] += d; r.Start += d; r.Around += d; r.From += d; r.To += d; r.Bottom += d; r.Top += d; }
        }
        internal static void Clear()
        {
            var all = new List<KeyValuePair<int, Run>>(_runs);
            foreach (var kv in all) { try { End(kv.Value, kv.Key); } catch (Exception) { } }
            _runs.Clear(); _ladders.Clear();
        }
    }
}
