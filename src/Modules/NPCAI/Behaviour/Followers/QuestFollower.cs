using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.NPC;

namespace WildWasteland.Modules.Quests
{
    internal static class QuestFollower
    {
        /// <summary>Current read-only contract; absent while Quests is disabled.</summary>
        private static IQuestHireState State { get { return ModApi.Get<IQuestHireState>(); } }
        /// <summary>Real NPCAI body provider over the existing companion implementation.</summary>
        internal sealed class BodyApi : IQuestFollowerBody
        {
            /// <summary>Advances the current companion body.</summary>
            public void Tick() { QuestFollower.Tick(); }
            /// <summary>Applies final mounted pose constraints.</summary>
            public void Late() { QuestFollower.Late(); }
            /// <summary>Restores native body ownership.</summary>
            public void Release() { QuestFollower.Release(); }
            /// <summary>Shows the current shot and reload.</summary>
            public void Shot() { QuestFollower.Shot(); }
            /// <summary>Plays a native state when permitted by the shared body.</summary>
            public void Play(Animator animator, string state, int layer, float time) { QuestFollower.Play(animator, state, layer, time); }
        }
        private static GameObject _current;
        private static Animator _animator;
        private static PlayMakerFSM _attack;
        private static Transform _weapon;
        private static Transform[] _models;
        private static IAnimatedCharacter _body;
        private static IAnimationApi _animation;
        private static bool _ownsBody;
        private static float _reloadShown, _retry;
        private static float _weaponAt;
        private static string _weaponKey;
        private static string[] _modelKeys;
        /// <summary>Plays an animator state unless the follower's animation body drives that animator.</summary>
        internal static void Play(Animator animator, string name, int layer, float time)
        { if (_body == null || !_body.Valid || animator != _animator) animator.Play(name, layer, time); }

        /// <summary>The follower fired: the body shoots and shows the reload (one round at a time when the weapon does).</summary>
        internal static void Shot()
        {
            if (_body == null || !_body.Valid) return;
            _body.Shoot();
            float until = (State != null ? State.ReloadUntil : 0f);
            if (ModApi.Get<INpcApi>() != null) ModApi.Get<INpcApi>().SetFollowerDriven((State != null ? State.Actor : null), true, until);
            if (until > Time.time && until != _reloadShown)
            { _reloadShown = until; _body.Reload(until - Time.time, ModApi.Get<IAnimationApi>() != null && ModApi.Get<IAnimationApi>().ReloadsOneRoundAtATime(_weaponKey) ? Math.Max(1, (State != null ? State.MagazineSize : 0)) : 0); }
        }
        /// <summary>Follower body tick (inside the Quests frame job): attaches the animation body, keeps the weapon, aims at the target, suspends the body for melee.</summary>
        internal static void Tick()
        {
            var npc = (State != null && State.Active) ? (State != null ? State.Actor : null) : null;
            if (_current != npc || _animation != ModApi.Get<IAnimationApi>() || (_body != null && !_body.Valid))
            {
                Release(); _current = npc; _animation = ModApi.Get<IAnimationApi>();
                if (npc != null)
                {
                    _animator = npc.GetComponent<Animator>() ?? npc.GetComponentInChildren<Animator>(true);
                    _attack = FsmUtil.Find(npc, "Attack");
                    _models = npc.GetComponentsInChildren<Transform>(true); _modelKeys = null; _weaponAt = 0;
                    _retry = 0;
                }
            }
            if (_current == null) return;
            if (ModApi.Get<INpcApi>() != null) ModApi.Get<INpcApi>().SetFollowerDriven(_current, true, (State != null ? State.ReloadUntil : 0f));
            if (_body == null && _animator != null && _animator.isHuman && _animation != null && _animation.Ready && Time.unscaledTime >= _retry)
            {
                _retry = Time.unscaledTime + 1f;
                _body = _animation.Find(_animator); _ownsBody = _body == null;
                if (_body == null) _body = _animation.Attach(_animator);
                if (_body != null)
                {
                    UpdateWeapon();
                    if (Core.Log.L1) NpcaiRuntime.Main("Follower animation attached: " + npc.name + " / " + (_weaponKey ?? "unarmed"));
                }
            }
            if (_body == null || !_body.Valid) return;
            UpdateWeapon();
            bool mounted = (State != null && State.Mounted); var target = (State != null ? State.Target : null);
            bool animated = _weapon != null && _animation.WeaponKind(_weaponKey) != "Melee" && (mounted || _attack == null || _attack.ActiveStateName != "attack_melee");
            if (!animated)
            {
                _body.Suspended = true;
                if (ModApi.Get<IFollowerVehicles>() != null) ModApi.Get<IFollowerVehicles>().Animated(_current, false);
                return;
            }
            if (_animator.speed != 1f) _animator.speed = 1f;
            _body.Suspended = false; _body.ManualVelocity = mounted; _body.Velocity = Vector3.zero;
            _body.Crouched = false; _body.Airborne = false; _body.Aiming = target != null; _body.Firing = false;
            if (target != null)
            {
                var v = target.transform.position + Vector3.up - (_current.transform.position + Vector3.up * 1.4f);
                _body.AimPitch = -Mathf.Atan2(v.y, Mathf.Sqrt(v.x * v.x + v.z * v.z)) * Mathf.Rad2Deg;
            }
            if (_reloadShown > 0 && (State != null ? State.ReloadUntil : 0f) <= Time.time) { _body.CancelReload(); _reloadShown = 0; }
            if (ModApi.Get<IFollowerVehicles>() != null) ModApi.Get<IFollowerVehicles>().Animated(_current, mounted);
        }
        /// <summary>Finds the follower's held gun among its models (twice a second at most) and gives it to the body.</summary>
        private static void UpdateWeapon()
        {
            if (_body == null || _animation == null || _models == null || Time.unscaledTime < _weaponAt) return;
            _weaponAt = Time.unscaledTime + .5f;
            if (_weapon != null && _weapon.gameObject.activeInHierarchy) return;
            if (_modelKeys == null || _modelKeys.Length != _models.Length)
            {
                // Child names do not change; look each one up once instead of every half second while unarmed.
                _modelKeys = new string[_models.Length];
                for (int i = 0; i < _models.Length; i++) _modelKeys[i] = _models[i] != null ? _animation.WeaponKey(_models[i].name) : null;
            }
            for (int i = 0; i < _models.Length; i++)
            {
                var child = _models[i];
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                var key = _modelKeys[i];
                if (!string.IsNullOrEmpty(key) && _animation.HasWeaponPoses(key) && child.GetComponentInChildren<Renderer>(true) != null)
                { _weapon = child; _weaponKey = key; _body.SetWeapon(child, key); return; }
            }
            if (_weapon != null) _body.SetWeapon(null);
            _weapon = null; _weaponKey = null;
        }
        /// <summary>Late tick: a seated follower's body gets no locomotion.</summary>
        internal static void Late()
        {
            // Quests' LateUpdate keeps the passenger on its seat; the body receives locomotion from its rigidbody on foot.
            if (_current != null && _body != null && _body.Valid && (State != null && State.Mounted)) _body.Velocity = Vector3.zero;
        }
        /// <summary>Releases the follower body (brain back, animation body disposed or suspended).</summary>
        internal static void Release()
        {
            if (_current != null)
            {
                if (ModApi.Get<INpcApi>() != null) ModApi.Get<INpcApi>().SetFollowerDriven(_current, false);
                if (ModApi.Get<IFollowerVehicles>() != null) ModApi.Get<IFollowerVehicles>().Animated(_current, false);
            }
            if (_body != null && _body.Valid) { _body.CancelReload(); _body.ManualVelocity = false; if (_ownsBody) _body.Dispose(); else _body.Suspended = true; }
            _body = null; _current = null; _animator = null; _weapon = null; _weaponKey = null; _animation = null; _reloadShown = 0;
            _models = null; _modelKeys = null;
            _attack = null;
        }
    }
}
