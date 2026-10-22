using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// CT10 (plan §7, research B §1.3, §3.3): Player [Movement] (Sources fsm/scene1_game/Player.md:L497-L567) writes the Player
    /// CapsuleCollider.height twice per frame by reflection in Standing, Crouching and Crawling: #3 = {ZeroFricitonHeight}, then #5 =
    /// {standingHeight} (both everyFrame), so the collider is rebuilt every frame although the value only changes during the 0.2 s
    /// crouch/stand tweens. Stand-ins: #3 keeps its OnEnter write and skips its per-frame write (#5 overwrites it later in the same
    /// FsmState.OnUpdate pass; #4 between them is a TweenFloat on a variable, nothing reads the collider in between); #5 writes per
    /// frame only when the collider's height differs from {standingHeight} (also catching any other writer, as vanilla's overwrite
    /// does). Final height every frame = vanilla's. Verified per action: SetProperty, CapsuleCollider target, "height", variable name.
    /// </summary>
    public sealed class PlayerCapsuleFeature : Feature
    {
        public override string PlanId { get { return "CT10"; } }
        public override string Key { get { return "Player capsule"; } }
        public override string Title { get { return "Player collider height writes"; } }
        public override string Category { get { return Categories.Content; } }
        public override int Order { get { return 12; } }
        public override string Help { get { return "The player's collider height is written only when it changes (crouch/stand), not twice every frame (exact)."; } }

        internal static bool On;
        readonly UndoLog _undo = new UndoLog();
        internal static long Writes, Skips;

        protected override void OnEnable(FeatureMode mode) { On = true; FsmEdits.Watch(this, "Movement", "Player", Ready); }
        protected override void OnDisable() { On = false; FsmEdits.Unwatch(this); _undo.RevertAll(); }
        public override void OnSceneLoaded() { _undo.Prune(); }

        void Ready(PlayMakerFSM f)
        {
            if (f.gameObject.name != "Player") return;
            string[] states = { "Standing", "Crouching", "Crawling" };
            for (int i = 0; i < states.Length; i++)
            {
                var fsm = f.Fsm;
                var a = FsmEdits.Act<SetProperty>(this, fsm, states[i], 3, "Movement/" + states[i] + "#3");
                var b = FsmEdits.Act<SetProperty>(this, fsm, states[i], 5, "Movement/" + states[i] + "#5");
                if (a == null || b == null) continue;
                if (!Height(a, "ZeroFricitonHeight") || !Height(b, "standingHeight") || !a.everyFrame || !b.everyFrame)
                { FsmEdits.Warn(this, "Movement/" + states[i], "height writes changed"); continue; }
                var st = FsmEdits.State(fsm, states[i]);
                FsmEdits.Replace(_undo, f, st, 3, new OverwrittenWrite(a));
                FsmEdits.Replace(_undo, f, st, 5, new ChangedWrite(b));
            }
        }

        static bool Height(SetProperty s, string varName)
        {
            var p = s.targetProperty;
            if (p == null || p.PropertyName != "height" || !(p.TargetObject.Value is CapsuleCollider) || p.TargetObject.UseVariable) return false;
            return p.FloatParameter != null && p.FloatParameter.UseVariable && p.FloatParameter.Name == varName;
        }

        /// <summary>#3: vanilla on enter; its per-frame write is dead (overwritten by #5 in the same update pass).</summary>
        sealed class OverwrittenWrite : ProxyAction
        {
            public OverwrittenWrite(SetProperty s) : base(s) { }
            protected override void Tick() { if (On) { Skips++; return; } base.Tick(); }
        }

        /// <summary>#5: per-frame write only when the collider differs (same final value as vanilla's unconditional write).</summary>
        sealed class ChangedWrite : ProxyAction
        {
            readonly SetProperty _s;
            public ChangedWrite(SetProperty s) : base(s) { _s = s; }
            protected override void Tick()
            {
                if (!On) { base.Tick(); return; }
                var c = _s.targetProperty.TargetObject.Value as CapsuleCollider;
                var v = _s.targetProperty.FloatParameter;
                if (c != null && !v.IsNone && c.height == v.Value) { Skips++; return; }
                Writes++;
                base.Tick();
            }
        }

        public override void DrawStats(ToolLayout l) { l.Value("Height writes / skipped", Writes + " / " + Skips); }
    }
}
