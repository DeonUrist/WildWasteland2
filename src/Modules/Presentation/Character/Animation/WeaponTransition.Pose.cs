using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    /// <summary>First-person weapon lowering pose of the smooth weapon switch (Wave A split; B05 turns it into IWeaponPose.SetLowerWeight). Pose runs as job Guns.WeaponTransitionPose (LateUpdateLast); Restore undoes it at the next Tick.</summary>
    internal static partial class WeaponTransition
    {
        private static void Restore()
        {
            if (_posed != null) { _posed.localPosition = _position; _posed.localRotation = _rotation; }
            _posed = null;
        }

        private static void Pose()
        {
            if (!GunsModule.SmoothWeaponSwitch.Value || _handler == null || _weight <= .001f || !GameRefs.InPlay) return;
            var parent = PlayerRig.Parent;
            if (parent == null) return;
            _posed = parent; _position = parent.localPosition; _rotation = parent.localRotation;
            parent.localPosition += new Vector3(0f, -.45f, -.12f) * _weight;
            parent.localRotation *= Quaternion.Euler(25f * _weight, 0f, 0f);
        }
    }
}
