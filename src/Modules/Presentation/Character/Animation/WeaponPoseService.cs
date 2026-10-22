using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// IWeaponPose provider and the first-person weapon lowering pose (README 6.1 / 6.3 split, B09-5). Gunplay times the weapon switch and
    /// publishes <see cref="WeaponSwitchPhase"/>; this class keeps the player's lowering weight (the switch weight and every module's
    /// <see cref="RequestLower"/>, highest wins) and lowers the native first-person weapons parent with it in LateUpdateLast, undoing it in
    /// the next EarlyUpdate (same offsets and timing as the old Guns WeaponTransition.Pose/Restore pair). ADS weight follows
    /// <see cref="AdsChanged"/>; aim lift comes from the AimLift table.
    /// </summary>
    internal sealed class WeaponPoseService : IWeaponPose
    {
        static readonly Dictionary<IModule, float> _requests = new Dictionary<IModule, float>();
        static float _switchWeight, _ads;
        static Transform _posed;
        static Vector3 _position;
        static Quaternion _rotation;

        /// <summary>Lowering weight 0..1 of the character's weapon (only the player has one).</summary>
        public float LowerWeight(EntityId character) { return character == Entities.Player ? PlayerLower : 0f; }

        /// <summary>Aim-down-sights weight 0..1 (player: 1 while Gunplay reports aiming down sights).</summary>
        public float AdsWeight(EntityId character) { return character == Entities.Player ? _ads : 0f; }

        /// <summary>Requests a lowering weight from <paramref name="owner"/> for the player (the highest request wins; 0 clears it).</summary>
        public void RequestLower(IModule owner, EntityId character, float weight)
        {
            if (owner == null || character != Entities.Player) return;
            if (weight <= 0f) _requests.Remove(owner); else _requests[owner] = Mathf.Clamp01(weight);
        }

        /// <summary>Aim lift (degrees of hand lift) of a weapon key in its kind's standing aim clip.</summary>
        float IWeaponPose.AimLift(string weaponKey)
        {
            if (string.IsNullOrEmpty(weaponKey)) return 0f;
            var k = Props.KindOf(weaponKey);
            string clip = k == Props.Kind.Rifle ? "RifleAim" : k == Props.Kind.Pistol ? "PistolFire" : null;
            if (clip == null) return 0f;
            var v = AimLift.Get(weaponKey, clip, false);
            return v != null && v.Length > 0 ? v[0] : 0f;
        }

        /// <summary>The player's current lowering weight: the switch weight (while Gunplay runs) or the highest module request.</summary>
        static float PlayerLower
        {
            get
            {
                float w = ModApi.Get<IWeaponState>() != null ? _switchWeight : 0f;   // Gunplay off mid-switch: no stale lowering
                foreach (var kv in _requests) if (kv.Value > w) w = kv.Value;
                return w;
            }
        }

        /// <summary>WeaponSwitchPhase handler (Gunplay, EarlyUpdate): the switch's lowering weight (0 once done).</summary>
        internal static void OnSwitchPhase(ref WeaponSwitchPhase evt) { _switchWeight = evt.Stage == WeaponSwitchStage.Done ? 0f : evt.Weight; }

        /// <summary>AdsChanged handler (Gunplay): 1 while aiming down sights.</summary>
        internal static void OnAdsChanged(ref AdsChanged evt) { _ads = evt.Aiming ? 1f : 0f; }

        /// <summary>Job "Presentation.WeaponLowering" (LateUpdateLast): lowers and tilts the first-person weapons parent by the weight.</summary>
        internal static void Pose()
        {
            float w = PlayerLower;
            if (w <= .001f || !GameRefs.InPlay) return;
            var parent = PlayerState.WeaponsParent;
            if (parent == null) return;
            _posed = parent; _position = parent.localPosition; _rotation = parent.localRotation;
            parent.localPosition += new Vector3(0f, -.45f, -.12f) * w;
            parent.localRotation *= Quaternion.Euler(25f * w, 0f, 0f);
        }

        /// <summary>Job "Presentation.WeaponLoweringRestore" (EarlyUpdate, before Gunplay's switch tick) and shutdown: undoes last frame's pose.</summary>
        internal static void Restore()
        {
            if (_posed != null) { _posed.localPosition = _position; _posed.localRotation = _rotation; }
            _posed = null;
        }

        /// <summary>Clears every weight (module start/stop, scene load) after restoring the pose.</summary>
        internal static void Reset()
        {
            Restore();
            _requests.Clear(); _switchWeight = 0f; _ads = 0f;
        }
    }
}
