using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Presentation
{
    internal sealed class NpcRecovery : INpcRecovery
    {
        private sealed class Recovery { internal ZombieRig Rig; internal AnimationClip Clip; internal Animator Animator; internal string Name; internal float Since, Duration; }
        private readonly Dictionary<GameObject,Recovery> _live = new Dictionary<GameObject,Recovery>();
        private readonly List<GameObject> _dead = new List<GameObject>();
        internal void Init(IModule owner) { Scheduler.EveryFrame(owner,"Presentation.Perk getup",Tick,Phase.LateUpdateLast); }
        public bool Available(GameObject npc)
        {
            if (npc == null) return false;
            var animator = npc.GetComponentInChildren<Animator>(true);
            return animator != null && (animator.isHuman && ZombieRig.Ready && (ModApi.Get<ISkillReactions>()?.ClassOf(npc) ?? PerkEnemyClass.Other) == PerkEnemyClass.Humanoid || CanineVisuals.RecoveryClip(npc) != null);
        }
        public float Begin(GameObject npc,Vector3 front)
        {
            End(npc); if (!Available(npc)) return 0;
            var animator = npc.GetComponentInChildren<Animator>(true); var r = new Recovery { Animator = animator, Since = Time.time };
            if (animator.isHuman)
            {
                r.Rig = new ZombieRig(animator); var chest = animator.GetBoneTransform(HumanBodyBones.Chest); float best = -2;
                foreach (var clip in new[] { "GetUp1", "GetUp2", "GetUp3" })
                {
                    r.Rig.Play(clip,0,0); r.Rig.Sample(1);
                    float match = chest != null ? Vector3.Dot(chest.forward,front) : 0;
                    if (match > best) { best = match; r.Name = clip; }
                }
                r.Duration = Mathf.Min(3.2f,ZombieRig.Length(r.Name)); r.Rig.Play(r.Name,0,0); r.Rig.Sample(0);
            }
            else { r.Clip = CanineVisuals.RecoveryClip(npc); r.Duration = r.Clip.length; CanineVisuals.BeginRecovery(npc); }
            _live[npc] = r; return r.Duration;
        }
        public void End(GameObject npc)
        { if (ReferenceEquals(npc,null)) return; Recovery r; if (_live.TryGetValue(npc,out r)) { r.Rig?.Dispose(); if (r.Clip != null) CanineVisuals.EndRecovery(npc); _live.Remove(npc); } }
        private void Tick()
        {
            _dead.Clear(); foreach (var pair in _live)
            {
                var r = pair.Value;
                if (pair.Key == null || r.Animator == null) { _dead.Add(pair.Key); continue; }
                float time = Mathf.Min(r.Duration,Time.time - r.Since);
                if (r.Rig != null) { r.Rig.Play(r.Name,time,0); r.Rig.Sample(Time.deltaTime); }
                else CanineVisuals.SampleRecovery(pair.Key,r.Animator,r.Clip,time);
            }
            foreach (var npc in _dead) End(npc);
        }
        internal void Clear() { foreach (var r in _live.Values) r.Rig?.Dispose(); _live.Clear(); }
    }
}
