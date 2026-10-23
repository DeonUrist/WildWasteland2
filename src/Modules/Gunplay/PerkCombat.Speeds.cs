using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Gunplay
{
    internal sealed partial class PerkCombat
    {
        private readonly Dictionary<Wait,FsmFloat> _waits = new Dictionary<Wait,FsmFloat>();
        private readonly Dictionary<Animator,float> _animators = new Dictionary<Animator,float>();
        private readonly Dictionary<Raycast,FsmFloat> _ranges = new Dictionary<Raycast,FsmFloat>();
        private readonly Dictionary<RandomFloat,Vector2> _spreads = new Dictionary<RandomFloat,Vector2>();
        private readonly Dictionary<Animator,int> _animationRoles = new Dictionary<Animator,int>();
        private void InitWeaponSpeeds(IModule owner) { Hooks.EnterState.Prefix(owner,WeaponSpeedState); Scheduler.Interval(owner,"Gunplay.Perk animation speeds",.05f,RefreshAnimationSpeeds); }
        private void RefreshAnimationSpeeds()
        { foreach (var pair in _animators) if (pair.Key != null) { int role; if (_animationRoles.TryGetValue(pair.Key,out role)) pair.Key.speed = pair.Value * (role == 0 ? 1 : Speed(Kind(pair.Key.gameObject),role == 2)); } }
        private static float Speed(WeaponKind kind,bool reload)
        {
            if (kind == WeaponKind.Pistol && SkillPerks.Has(reload ? "Gunslinger" : "QuickReflexes")) return 1.5f;
            if (kind == WeaponKind.Shotgun && SkillPerks.Has("FastPump")) return 1.5f;
            if (kind == WeaponKind.Smg && SkillPerks.Has("AdrenalineRush") && Time.time < AdrenalineUntil) return 1.5f;
            return 1;
        }
        private bool WeaponSpeedState(Fsm fsm,FsmState state)
        {
            if (!Player(fsm.GameObject)) return true;
            var kind = Kind(fsm.GameObject); if (kind == WeaponKind.Other || kind == WeaponKind.Melee) return true;
            bool reload = fsm.Name.Contains("Reload"); bool attack = fsm.Name == "Attack" || fsm.Name == "Animation" || fsm.Name.Contains("AttackAnimation") || kind == WeaponKind.Shotgun && fsm.Name == "EffectPlay";
            if (!reload && !attack) return true;
            float speed = Speed(kind,reload);
            foreach (var action in state.Actions)
            {
                var wait = action as Wait;
                if (wait != null) { FsmFloat original; if (!_waits.TryGetValue(wait,out original)) { original = wait.time; _waits.Add(wait,original); } wait.time = new FsmFloat(original.Value / speed); }
                var ray = action as Raycast;
                if (ray != null && SkillPerks.Rifle(kind)) { FsmFloat original; if (!_ranges.TryGetValue(ray,out original)) { original = ray.distance; _ranges.Add(ray,original); } ray.distance = new FsmFloat(original.Value * (SkillPerks.Has("LongShot") ? 1.25f : 1)); }
                var random = action as RandomFloat;
                if (random != null && (state.Name == "fire" || state.Name == "setDirection"))
                {
                    Vector2 original; if (!_spreads.TryGetValue(random,out original)) { original = new Vector2(random.min.Value,random.max.Value); _spreads.Add(random,original); }
                    float factor = (GunplaySettings.TracersEnabled.Value && GunplaySettings.PlayerTracers.Value) ? 1 : ModApi.Get<IPlayerSkills>()?.SpreadMultiplier(kind) ?? 1;
                    random.min = new FsmFloat(original.x * factor); random.max = new FsmFloat(original.y * factor);
                }
            }
            if (fsm.Name.Contains("Animation") || fsm.Name == "Animation")
            {
                var animator = fsm.GameObject.GetComponent<Animator>();
                if (animator != null)
                { if (!_animators.ContainsKey(animator)) _animators[animator] = animator.speed; _animationRoles[animator] = state.Name == "off" || state.Name == "idle" ? 0 : reload ? 2 : 1; animator.speed = _animators[animator] * (_animationRoles[animator] == 0 ? 1 : speed); }
            }
            return true;
        }
        private void ClearWeaponSpeeds()
        {
            foreach (var pair in _waits) pair.Key.time = pair.Value;
            foreach (var pair in _ranges) pair.Key.distance = pair.Value;
            foreach (var pair in _animators) if (pair.Key != null) pair.Key.speed = pair.Value;
            foreach (var pair in _spreads) { pair.Key.min = new FsmFloat(pair.Value.x); pair.Key.max = new FsmFloat(pair.Value.y); }
            _waits.Clear(); _ranges.Clear(); _animators.Clear(); _spreads.Clear(); _animationRoles.Clear();
        }
    }
}
