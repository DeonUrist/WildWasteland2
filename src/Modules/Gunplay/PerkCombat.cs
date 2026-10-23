using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>Damage queries at the hit boundary; explicit ownership follows native explosive creation.</summary>
    internal sealed partial class PerkCombat : ICombatSources
    {
        private struct Source { internal EntityId Owner; internal WeaponKind Kind; }
        private struct FinalHit { internal WeaponKind Kind; internal bool Unarmed; }
        private readonly Dictionary<GameObject, FinalHit> _hits = new Dictionary<GameObject, FinalHit>();
        private sealed class Write { internal FsmFloat Original; internal readonly FsmFloat Modified = new FsmFloat(); }
        private readonly Dictionary<GameObject, Source> _sources = new Dictionary<GameObject, Source>();
        private readonly Dictionary<GameObject, WeaponKind> _kinds = new Dictionary<GameObject, WeaponKind>();
        private readonly Dictionary<SetFsmFloat, Write> _writes = new Dictionary<SetFsmFloat, Write>();
        private readonly List<GameObject> _sweep = new List<GameObject>();
        private readonly List<SetFsmFloat> _actionSweep = new List<SetFsmFloat>();
        internal void Init(IModule owner)
        {
            Instance = this; InitSkillCombat(owner);
            Hooks.CreateObjectEnter.Postfix(owner, Created);
            Hooks.SetFsmFloatEnter.Prefix(owner, BeforeDamage);
            Hooks.SetFsmFloatEnter.Postfix(owner, AfterDamage);
            Hooks.SendEventEnter.Prefix(owner, BeforeEvent);
            Hooks.SendEventUpdate.Prefix(owner, BeforeEvent);
            Hooks.ExplosionShouldApplyForce.Prefix(owner, BeforeForce);
            Scheduler.Interval(owner, "Gunplay.Perk source cleanup", 1f, Sweep);
        }
        private static bool Player(GameObject go)
        {
            if (go == null) return false;
            var player = GameRefs.Player; if (go == player || player != null && go.transform.IsChildOf(player.transform)) return true;
            var holder = GameRefs.PlayerCameraHolder; return holder != null && go.transform.IsChildOf(holder.transform);
        }
        public EntityId OwnerOf(GameObject source)
        {
            if (Player(source)) return Entities.Player;
            var car = PlayerState.CarRoot;
            if (source != null && car != null && (source == car.gameObject || source.transform.IsChildOf(car) && FsmUtil.Find(source, "CarAttack") != null)) return Entities.Player;
            Source found;
            for (var node = source != null ? source.transform : null; node != null; node = node.parent)
            {
                if (_sources.TryGetValue(node.gameObject, out found)) return found.Owner;
                var health = FsmUtil.Find(node.gameObject, "Health"); if (health != null) return Entities.Of(node.gameObject);
            }
            return EntityId.None;
        }
        public void Assign(GameObject source, EntityId owner)
        {
            if (!owner.IsValid) return;
            for (var node = source != null ? source.transform : null; node != null; node = node.parent)
            {
                if (FsmUtil.Find(node.gameObject, "Explosion") == null && FsmUtil.Find(node.gameObject, "Explode") == null) continue;
                _sources[node.gameObject] = new Source { Owner = owner, Kind = WeaponKind.Thrown }; return;
            }
        }
        public void RecordHit(GameObject target, EntityId attacker, WeaponKind kind, bool unarmed)
        {
            for (var node = target != null ? target.transform : null; node != null; node = node.parent)
            {
                if (node.gameObject == GameRefs.Player || node.CompareTag("vehPart") || node.CompareTag("vehPartRemoved")) return;
                if (FsmUtil.Find(node.gameObject, "Health") == null) continue;
                if (attacker == Entities.Player) _hits[node.gameObject] = new FinalHit { Kind = kind, Unarmed = unarmed };
                else _hits.Remove(node.gameObject);
                return;
            }
        }
        private static bool Unarmed(GameObject source)
        { return source != null && (source == GameRefs.Player || source.name == "kick" || source.name == "hands" || source.name == "hands_model" || source.name.IndexOf("fist", StringComparison.OrdinalIgnoreCase) >= 0); }
        private WeaponKind Kind(GameObject source)
        {
            if (source == null) return WeaponKind.Other;
            Source owned; if (_sources.TryGetValue(source, out owned)) return owned.Kind;
            WeaponKind kind; if (_kinds.TryGetValue(source, out kind)) return kind;
            int handle; var catalog = ModApi.Get<IWeaponCatalog>();
            if (catalog != null && catalog.TryResolve(source, out handle)) kind = catalog.KindOf(handle);
            else kind = Unarmed(source) ? WeaponKind.Melee : WeaponKind.Other;
            if (source.name.IndexOf("grenade", StringComparison.OrdinalIgnoreCase) >= 0) kind = WeaponKind.Thrown;
            _kinds[source] = kind; return kind;
        }
        private void Created(CreateObject action)
        {
            var fsm = action.Fsm;
            if (fsm != null && fsm.Name == "Health" && fsm.ActiveStateName == "spawn")
            {
                var corpse = action.storeObject != null ? action.storeObject.Value : null; FinalHit hit;
                if (corpse != null && corpse.name.IndexOf("_Dead", StringComparison.OrdinalIgnoreCase) >= 0 && _hits.TryGetValue(action.Owner, out hit))
                { AwardKill(action.Owner,hit.Kind,hit.Unarmed); }
            }
            if (fsm == null || fsm.Name != "Attack" && fsm.Name != "Explode" && fsm.Name != "Explosion" && fsm.Name != "Health" && !_sources.ContainsKey(action.Owner)) return;
            var prefab = action.gameObject != null ? action.gameObject.Value : null;
            if (prefab == null || (prefab.name.IndexOf("expl", StringComparison.OrdinalIgnoreCase) < 0 && prefab.name.IndexOf("projectile", StringComparison.OrdinalIgnoreCase) < 0)) return;
            var made = action.storeObject != null ? action.storeObject.Value : null; if (made == null) return;
            var owner = OwnerOf(action.Owner); if (!owner.IsValid) return;
            _sources[made] = new Source { Owner = owner, Kind = Kind(action.Owner) };
        }
        private bool BeforeDamage(SetFsmFloat action)
        {
            if (action.variableName.Value != "Damage" || action.fsmName.Value != "Bodypart" || action.setValue.Value >= 0) return true;
            var owner = OwnerOf(action.Owner);
            var target = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
            var kind = Kind(action.Owner); bool unarmed = Unarmed(action.Owner);
            if (owner != Entities.Player && (action.Fsm.Name == "Damage" || action.Fsm.ActiveStateName == "attack_melee")) kind = WeaponKind.Melee;
            Stage(target, action.Owner, kind, unarmed);
            RecordHit(target, owner, kind, unarmed);
            if (owner != Entities.Player) return true;
            var effects = ModApi.Get<IPlayerPerkEffects>();
            bool ownBlast = action.Fsm.Name == "ExplosionDamage";
            float factor = ownBlast && Player(target) && (effects?.IgnoreOwnExplosions ?? false) ? 0 : (effects?.DamageMultiplier(kind, PlayerState.Health * .01f) ?? 1f) * (ModApi.Get<IPlayerSkills>()?.DamageMultiplier(kind, unarmed) ?? 1f);
            Assign(target, owner);
            if (factor == 1f) return true;
            Write write;
            if (!_writes.TryGetValue(action, out write)) { write = new Write(); _writes.Add(action, write); }
            write.Original = action.setValue; write.Modified.Value = action.setValue.Value * factor; action.setValue = write.Modified;
            return true;
        }
        private void AfterDamage(SetFsmFloat action)
        {
            Write write;
            if (_writes.TryGetValue(action, out write) && ReferenceEquals(action.setValue, write.Modified)) action.setValue = write.Original;
        }
        private bool BeforeEvent(SendEvent action)
        {
            if (action.Fsm.Name != "ExplosionDamage" || action.sendEvent == null || (action.sendEvent.Name != "Damage" && action.sendEvent.Name != "DamageFlammable")
                || !(ModApi.Get<IPlayerPerkEffects>()?.IgnoreOwnExplosions ?? false) || OwnerOf(action.Owner) != Entities.Player) return true;
            var target = action.eventTarget != null ? action.Fsm.GetOwnerDefaultTarget(action.eventTarget.gameObject) : null;
            if (!Player(target)) return true;
            action.Finish(); return false;
        }
        private bool BeforeForce(ref Hooks.ExplosionForceArgs args)
        {
            if (!(ModApi.Get<IPlayerPerkEffects>()?.IgnoreOwnExplosions ?? false) || !Player(args.Target) || OwnerOf(args.Explosion.Owner) != Entities.Player) return true;
            args.Result = false; return false;
        }
        private void Sweep()
        {
            _awardedKills.RemoveWhere(go => go == null);
            _sweep.Clear(); foreach (var pair in _hits) if (pair.Key == null) _sweep.Add(pair.Key);
            foreach (var key in _sweep) _hits.Remove(key);
            _sweep.Clear(); foreach (var pair in _sources) if (pair.Key == null) _sweep.Add(pair.Key);
            foreach (var key in _sweep) _sources.Remove(key);
            _sweep.Clear(); foreach (var pair in _kinds) if (pair.Key == null) _sweep.Add(pair.Key);
            foreach (var key in _sweep) _kinds.Remove(key);
            _actionSweep.Clear(); foreach (var pair in _writes) if (pair.Key.Owner == null) _actionSweep.Add(pair.Key);
            foreach (var key in _actionSweep) _writes.Remove(key);
        }
        internal void Clear()
        {
            ClearSkillCombat();
            foreach (var pair in _writes) if (ReferenceEquals(pair.Key.setValue, pair.Value.Modified)) pair.Key.setValue = pair.Value.Original;
            _sources.Clear(); _kinds.Clear(); _writes.Clear(); _hits.Clear();
        }
    }
}
