using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Gunplay
{
    internal sealed partial class PerkCombat
    {
        internal static PerkCombat Instance;
        internal sealed class Blast
        {
            internal int Pending;
            internal readonly Dictionary<GameObject,BlastHit> Hits = new Dictionary<GameObject,BlastHit>();
            internal bool Done;
        }
        internal sealed class BlastHit { internal float Damage, Health; internal Context Context; }
        internal sealed class Context
        {
            internal GameObject Source, Target;
            internal WeaponKind Kind;
            internal bool Unarmed, Kick, Unaware, Player, Head, Cutting, IncomingHandled;
            internal Blast Blast;
        }
        private sealed class DamageWrite { internal FsmFloat Original; internal Context Context; internal GameObject Victim; internal float Before, Damage; }
        private sealed class Bleed { internal float Tick, Next; internal int Left; internal Context Context; }
        private readonly Dictionary<GameObject,Context> _contexts = new Dictionary<GameObject,Context>();
        private readonly Dictionary<FsmFloatAdd,DamageWrite> _damageWrites = new Dictionary<FsmFloatAdd,DamageWrite>();
        private readonly Dictionary<GameObject,Bleed> _bleeds = new Dictionary<GameObject,Bleed>();
        private readonly Dictionary<GameObject,Blast> _nativeBlasts = new Dictionary<GameObject,Blast>();
        private readonly List<GameObject> _bleedDone = new List<GameObject>();
        private readonly HashSet<GameObject> _awardedKills = new HashSet<GameObject>();
        private readonly Dictionary<GameObject,Context> _externalMelee = new Dictionary<GameObject,Context>();
        private readonly Dictionary<PlayMakerFSM,int> _capacities = new Dictionary<PlayMakerFSM,int>();
        private bool _secondary;
        internal static Blast CurrentBlast;
        internal static float AdrenalineUntil;
        private void InitSkillCombat(IModule owner)
        {
            Hooks.FsmFloatAddEnter.Prefix(owner,Damage); Hooks.FsmFloatAddEnter.Postfix(owner,Damaged);
            Hooks.EnterState.Prefix(owner,NativeBlastState);
            Hooks.EnterState.Prefix(owner,IncomingState);
            ModEvents.Subscribe<PlayerWeaponKill>(owner,SkillKill);
            Scheduler.Interval(owner,"Gunplay.Bleeding",.1f,Bleeding);
            InitWeaponSpeeds(owner);
        }
        private bool IncomingState(Fsm fsm,FsmState state)
        {
            if (fsm.Name != "Bodypart" || state.Name != "damage" || !Player(fsm.GameObject)) return true;
            var value = fsm.Variables.FindFsmFloat("Damage"); if (value == null || value.Value >= 0) return true;
            Context context;
            if (!_contexts.TryGetValue(fsm.GameObject,out context)) { context = new Context { Kind = WeaponKind.Other, Target = fsm.GameObject }; _contexts[fsm.GameObject] = context; }
            if (context.IncomingHandled) return true; context.IncomingHandled = true;
            if (SkillPerks.Moving && SkillPerks.Has("Dodge")) value.Value *= .75f;
            if (!context.Player && context.Kind == WeaponKind.Melee && SkillPerks.Has("Parry") && HoldingMelee() && Random.value < (ModApi.Get<IPlayerSkills>()?.Bonus(PlayerSkillId.Melee) ?? 0)) value.Value = 0;
            return true;
        }
        internal void Stage(GameObject target, GameObject source, WeaponKind kind, bool unarmed)
        {
            if (target == null || _secondary) return;
            var root = ActorQuery.Creature(target.transform); if (root == null && !ActorQuery.IsPlayerObj(target)) return;
            var victim = root != null ? root.gameObject : GameRefs.Player;
            Blast blast = CurrentBlast;
            if (blast == null && source != null) _nativeBlasts.TryGetValue(source,out blast);
            _contexts[target] = new Context { Source = source, Target = target, Kind = kind, Unarmed = unarmed, Kick = source != null && source.name == "kick", Player = OwnerOf(source) == Entities.Player,
                Head = ActorQuery.IsHead(target), Unaware = ModApi.Get<ISkillReactions>()?.Unaware(victim) ?? false, Blast = blast };
        }
        private bool Damage(FsmFloatAdd action)
        {
            if (_secondary || action.fsmName.Value != "Health" || action.variableName.Value != "Health" || action.add.Value >= 0) return true;
            var victim = action.Fsm.GetOwnerDefaultTarget(action.gameObject); if (victim == null) return true;
            var hp = ActorQuery.HealthOf(victim); if (float.IsNaN(hp) || hp < 1) return true;
            Context context; _contexts.TryGetValue(action.Owner,out context); _contexts.Remove(action.Owner);
            float damage = -action.add.Value;
            if (victim == GameRefs.Player)
            {
                if ((context == null || !context.IncomingHandled) && SkillPerks.Moving && SkillPerks.Has("Dodge")) damage *= .75f;
                if (context != null && !context.IncomingHandled && !context.Player && context.Kind == WeaponKind.Melee && SkillPerks.Has("Parry") && HoldingMelee() && Random.value < (ModApi.Get<IPlayerSkills>()?.Bonus(PlayerSkillId.Melee) ?? 0)) damage = 0;
            }
            else if (context != null && context.Player)
            {
                if (context.Kind == WeaponKind.Shotgun && context.Blast != null)
                {
                    BlastHit hit; if (!context.Blast.Hits.TryGetValue(victim,out hit)) { hit = new BlastHit { Health = hp, Context = context }; context.Blast.Hits.Add(victim,hit); }
                    hit.Damage += damage; action.Finish(); return false;
                }
                var reactions = ModApi.Get<ISkillReactions>();
                float max = reactions?.MaximumHealth(victim) ?? hp;
                if (context.Unarmed && SkillPerks.Has("OnePunch") && hp >= max - .001f) damage *= 2;
                if (context.Kind == WeaponKind.Melee && !context.Unarmed && SkillPerks.Has("Executioner") && hp <= max * .25f) damage = Mathf.Max(damage,hp + 1);
                if (context.Kind == WeaponKind.Pistol && SkillPerks.Has("Deadeye") && Random.value < (context.Head ? .5f : .25f)) damage *= 2;
                if (SkillPerks.Rifle(context.Kind) && context.Unaware && SkillPerks.Has("Sniper")) damage *= 3;
                if (context.Kind == WeaponKind.Smg && SkillPerks.Has("CloseQuarters"))
                { float range = WeaponStats.KindRange(WeaponKind.Smg); damage *= 1 + .5f * Mathf.Clamp01(1 - Vector3.Distance(victim.transform.position,GameRefs.Player.transform.position) / range); }
            }
            _damageWrites[action] = new DamageWrite { Original = action.add, Context = context, Victim = victim, Before = hp, Damage = damage };
            action.add = new FsmFloat(-damage); return true;
        }
        private static bool HoldingMelee()
        {
            var state = ModApi.Get<IWeaponState>();
            return state != null && state.HasWeapon && (ModApi.Get<IWeaponCatalog>()?.KindOf(state.WeaponKey) ?? WeaponKind.Other) == WeaponKind.Melee;
        }
        private void Damaged(FsmFloatAdd action)
        {
            DamageWrite write; if (!_damageWrites.TryGetValue(action,out write)) return;
            _damageWrites.Remove(action); action.add = write.Original;
            if (write.Context != null && write.Context.Player && write.Damage > 0 && ActorQuery.HealthOf(write.Victim) < write.Before) Effects(write.Victim,write.Context,write.Damage,write.Before);
        }
        private void Effects(GameObject victim,Context context,float damage,float before)
        {
            RecordHit(victim,Entities.Player,context.Kind,context.Unarmed);
            if (ActorQuery.HealthOf(victim) < 1) AwardKill(victim,context.Kind,context.Unarmed);
            var reactions = ModApi.Get<ISkillReactions>(); if (reactions == null) return;
            var cls = reactions.ClassOf(victim); Vector3 push = victim.transform.position - GameRefs.Player.transform.position; push.y = 0; push = push.normalized * 4;
            if (context.Kind == WeaponKind.Smg && SkillPerks.Has("BulletHose") && Random.value < .15f) Refund(context.Source);
            if (context.Unarmed)
            {
                bool boss = (ModApi.Get<INpcControl>()?.IsBoss(victim) ?? false) || FsmUtil.Find(victim,"BossUI") != null;
                bool eligible = !boss && (cls == PerkEnemyClass.Humanoid || cls == PerkEnemyClass.Small || cls == PerkEnemyClass.Quadruped) || SkillPerks.Has("OnePunch") && cls != PerkEnemyClass.Other;
                bool ragdoll = SkillPerks.Has("ParalysisStrike"); float chance = ragdoll ? .25f : SkillPerks.Has("OnePunch") ? .2f : SkillPerks.Has("Stagger") ? .1f : 0;
                bool trigger = eligible && Random.value < chance;
                reactions.React(victim,context.Kick && SkillPerks.Has("Pushback") ? push : Vector3.zero,trigger ? (ragdoll ? Random.Range(2f,3f) : 1f) : 0,ragdoll);
            }
            else if (context.Kind == WeaponKind.Melee && SkillPerks.Has("MeleeProficiency"))
            {
                if (context.Cutting || context.Source != null && Tracers.IsBlade(context.Source.name)) _bleeds[victim] = new Bleed { Tick = damage / 3, Next = Time.time + 1, Left = 3, Context = context };
                else if ((cls == PerkEnemyClass.Humanoid || cls == PerkEnemyClass.LargeMutant || cls == PerkEnemyClass.Juggernaut) && Random.value < .2f) reactions.React(victim,Vector3.zero,1,false);
            }
            else if (context.Kind == WeaponKind.Pistol && context.Head && SkillPerks.Has("HighNoon"))
            { float chance = cls == PerkEnemyClass.Juggernaut ? .1f : cls == PerkEnemyClass.LargeMutant ? .5f : cls == PerkEnemyClass.Other ? 0 : 1; if (Random.value < chance) reactions.React(victim,push,2.5f,true); }
            else if (context.Kind == WeaponKind.Shotgun && SkillPerks.Has("BigBlast"))
            { bool ragdoll = SkillPerks.Has("Boomstick"); float chance = Mathf.Clamp01(damage * (ragdoll ? 2 : 1) / before); reactions.React(victim,push,Random.value < chance ? (ragdoll ? 2.5f : 1) : 0,ragdoll); }
        }
        private void NativeDamage(GameObject victim,float amount,Context context)
        {
            var part = FsmUtil.Find(victim,"Bodypart"); if (part == null) return;
            var value = part.FsmVariables.FindFsmFloat("Damage"); if (value == null) return;
            _secondary = true;
            try { RecordHit(victim,Entities.Player,context.Kind,context.Unarmed); value.Value = -amount; part.SendEvent("Damage"); }
            finally { _secondary = false; }
            if (ActorQuery.HealthOf(victim) < 1) AwardKill(victim,context.Kind,context.Unarmed);
        }
        internal void Complete(Blast blast)
        {
            if (blast == null || blast.Done || --blast.Pending > 0) return; blast.Done = true;
            float total = 0; foreach (var hit in blast.Hits.Values) total += hit.Damage;
            bool crowd = blast.Hits.Count > 1 && SkillPerks.Has("CrowdControl");
            foreach (var pair in blast.Hits)
            {
                if (pair.Key == null || ActorQuery.HealthOf(pair.Key) < 1) continue;
                float damage = crowd ? total : pair.Value.Damage;
                NativeDamage(pair.Key,damage,pair.Value.Context); Effects(pair.Key,pair.Value.Context,damage,pair.Value.Health);
                Bridge.PlayerHit(pair.Key,pair.Key.transform.position,-damage,false);
                CombatEvents.Hurt(pair.Key,GameRefs.Player,damage,HurtKind.Projectile,pair.Key.name,pair.Key.transform.position,-1,false,ActorQuery.HealthOf(pair.Key) < 1);
            }
            blast.Hits.Clear();
        }
        private bool NativeBlastState(Fsm fsm,FsmState state)
        {
            if (fsm.Name != "Attack" || OwnerOf(fsm.GameObject) != Entities.Player || Kind(fsm.GameObject) != WeaponKind.Shotgun) return true;
            Blast blast;
            if (state.Name == "sound" && SkillPerks.Has("BigBlast")) { _nativeBlasts[fsm.GameObject] = new Blast { Pending = 1 }; }
            else if ((state.Name == "wait" || state.Name == "on") && _nativeBlasts.TryGetValue(fsm.GameObject,out blast)) { _nativeBlasts.Remove(fsm.GameObject); Complete(blast); }
            return true;
        }
        private void Bleeding()
        {
            _bleedDone.Clear(); foreach (var pair in _bleeds)
            {
                if (pair.Key == null || ActorQuery.HealthOf(pair.Key) < 1) { _bleedDone.Add(pair.Key); continue; }
                var bleed = pair.Value; if (Time.time < bleed.Next) continue;
                NativeDamage(pair.Key,bleed.Tick,bleed.Context); bleed.Next += 1; if (--bleed.Left == 0) _bleedDone.Add(pair.Key);
            }
            foreach (var npc in _bleedDone) _bleeds.Remove(npc);
        }
        private void Refund(GameObject source)
        {
            var reload = PerGunAmmo.CurrentReload; if (reload == null || source != reload.gameObject) return;
            var ammo = reload.FsmVariables.FindFsmInt("ammo_in_gun"); if (ammo == null) return;
            var capacity = reload.FsmVariables.FindFsmInt("ammo_max");
            int handle; var catalog = ModApi.Get<IWeaponCatalog>(); int max = capacity != null ? capacity.Value : catalog != null && catalog.TryResolve(source,out handle) ? Mathf.RoundToInt(WeaponStats.Instance.Get(handle,WeaponStat.MagazineCapacity)) : 0;
            if (max <= 0 && !_capacities.TryGetValue(reload,out max))
            {
                foreach (var state in reload.FsmStates) foreach (var action in state.Actions)
                {
                    var compare = action as IntCompare;
                    if (compare != null && compare.integer1.Name == "ammo_in_gun" && !compare.integer2.UseVariable && compare.integer2.Value > 0 && compare.greaterThan != null)
                        max = Mathf.Max(max,compare.integer2.Value + (compare.equal == null ? 1 : 0));
                }
                _capacities[reload] = max;
            }
            if (max > 0 && ammo.Value < max) ammo.Value++;
        }
        private void SkillKill(ref PlayerWeaponKill kill)
        {
            if (kill.Kind == WeaponKind.Smg && SkillPerks.Has("AdrenalineRush")) AdrenalineUntil = Time.time + 5;
            if (kill.Kind == WeaponKind.Melee && !kill.Unarmed && SkillPerks.Has("Undertaker")) ModApi.Get<INpcFear>()?.MeleeWitnesses(Entities.Find(kill.Victim));
        }
        private void AwardKill(GameObject victim,WeaponKind kind,bool unarmed)
        {
            if (victim == null || !_awardedKills.Add(victim)) return;
            _hits.Remove(victim);
            var killed = new PlayerWeaponKill { Kind = kind, Unarmed = unarmed, Victim = Entities.Of(victim), WorldPos = Origin.World(victim.transform.position) }; ModEvents.Publish(ref killed);
        }
        public float PrepareMelee(GameObject target,float damage,bool cutting)
        {
            var root = target != null ? ActorQuery.Creature(target.transform) : null; if (root == null) return damage;
            var victim = root.gameObject; float hp = ActorQuery.HealthOf(victim);
            if (float.IsNaN(hp) || hp < 1) return damage;
            _externalMelee[victim] = new Context { Source = GameRefs.Player, Target = victim, Kind = WeaponKind.Melee, Player = true, Cutting = cutting };
            RecordHit(victim,Entities.Player,WeaponKind.Melee,false);
            damage *= (ModApi.Get<IPlayerSkills>()?.DamageMultiplier(WeaponKind.Melee,false) ?? 1) * (ModApi.Get<IPlayerPerkEffects>()?.DamageMultiplier(WeaponKind.Melee,PlayerState.Health * .01f) ?? 1);
            if (SkillPerks.Has("Executioner") && hp <= (ModApi.Get<ISkillReactions>()?.MaximumHealth(victim) ?? hp) * .25f) damage = Mathf.Max(damage,hp + 1);
            return damage;
        }
        public void FinishMelee(GameObject target,float damage)
        { Context context; if (target != null && _externalMelee.TryGetValue(target,out context)) { _externalMelee.Remove(target); Effects(target,context,damage,ActorQuery.HealthOf(target) + damage); } }
        private void ClearSkillCombat()
        {
            foreach (var pair in _damageWrites) pair.Key.add = pair.Value.Original;
            _damageWrites.Clear(); _contexts.Clear(); _bleeds.Clear(); _nativeBlasts.Clear(); _awardedKills.Clear(); _externalMelee.Clear(); _capacities.Clear(); CurrentBlast = null; AdrenalineUntil = 0; ClearWeaponSpeeds();
        }
    }
}
