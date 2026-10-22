using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// The player's first-person rig (PlayerCameraHolder/PlayerCamera/WeaponsArm/Parent), shared by the fitted-wheel swing scan and
    /// the magazine/unload lookups. Found through Core.GameRefs (no GameObject.Find), retried every 2 s while missing, reset on scene load.
    /// </summary>
    internal static class PlayerRig
    {
        static Transform _cam, _arm, _parent;
        static float _next;
        internal static void Reset() { _cam = _arm = _parent = null; _next = 0f; }

        static void Find()
        {
            if (_parent != null && _cam != null && _arm != null) return;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            var holder = GameRefs.PlayerCameraHolder;
            if (holder == null) return;
            _cam = holder.transform.Find("PlayerCamera");
            _arm = _cam != null ? _cam.Find("WeaponsArm") : null;
            _parent = _arm != null ? _arm.Find("Parent") : null;
        }
        internal static Transform Camera { get { Find(); return _cam; } }
        internal static Transform WeaponsArm { get { Find(); return _arm; } }
        internal static Transform Parent { get { Find(); return _parent; } }
    }
}
