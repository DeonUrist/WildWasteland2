using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // Runs after animation/pose work, including frames where the distant pose is throttled.
    [DefaultExecutionOrder(20000)]
    [ES3NonSerializable]
    internal sealed class RiderAnchorGuard : MonoBehaviour
    {
        private System.Func<bool> _mounted;
        private Transform _anchor;
        private Rigidbody _body;
        private bool _gunner;

        /// <summary>Adds the guard to a mounted rider (Animator set to always animate so procedural poses do not reuse a culled pose).</summary>
        internal static void Attach(GameObject who, System.Func<bool> mounted, Transform anchor, bool gunner)
        {
            var guard = who.AddComponent<RiderAnchorGuard>();
            guard._mounted = mounted; guard._anchor = anchor; guard._gunner = gunner;
            guard._body = who.GetComponent<Rigidbody>();
            // A culled Animator retains the last bone pose; procedural adjustments would then reuse it.
            var animator = who.GetComponentInChildren<Animator>();
            if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        /// <summary>After animation and pose work: keeps the rider parented and on its anchor with a kinematic body that follows the transform.</summary>
        private void LateUpdate()
        { if (!PatrolsModule.Running) return;   // refs-ok: REFACTOR-REQUEST B06-4 (Patrols running state; no IPatrols query yet)
            if (_mounted == null || !_mounted() || _anchor == null) return;
            if (transform.parent != _anchor) transform.SetParent(_anchor, true);
            // Every turret's sitting/standing height is owned by Pose, including lance throwers.
            if (!_gunner) transform.position = _anchor.position;
            if (_body == null) return;
            if (!_body.isKinematic) _body.isKinematic = true;
            // A parented kinematic body must follow the current transform when its platform sleeps/wakes.
            if (_body.position != transform.position) _body.position = transform.position;
            if (_body.rotation != transform.rotation) _body.rotation = transform.rotation;
        }
    }
}
