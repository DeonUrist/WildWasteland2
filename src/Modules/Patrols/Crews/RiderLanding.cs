using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

using WildWasteland.Core;


namespace WildWasteland.Modules.Patrols
{
    /// <summary>Traffic-owned dismount velocity, landing detection and awareness handoff; Presentation supplies a disposable jump visual.</summary>
    internal sealed class RiderLanding : MonoBehaviour
    {
        internal Vector3 Velocity;
        private int _frames; private bool _pushed, _landed; private float _start, _landedAt;
        private IDisposable _visual;
        private Rigidbody _rb; private Collider _col;
        private static readonly RaycastHit[] _hits = new RaycastHit[8];

        /// <summary>Starts the Presentation-owned jump visual while retaining the actual native body for landing control.</summary>
        private void Start()
        {
            _start = Time.time;
            _rb = GetComponent<Rigidbody>(); _col = GetComponent<Collider>();
            _visual=ModApi.Get<IRiderVisuals>()?.Jump(gameObject);
        }

        /// <summary>Applies the jump velocity once the mob FSMs have started (second physics step).</summary>
        private void FixedUpdate()
        { if (!PatrolsModule.Running) { _visual?.Dispose();Destroy(this);return; }
            if (_pushed || ++_frames < 2) return;
            _pushed = true;
            if (_rb != null && !_rb.isKinematic) _rb.velocity = Velocity;
        }

        /// <summary>Ends the clip on landing (or after 1.5 s), turns the mob to the player and alerts it, then removes the component.</summary>
        private void Update()
        { if (!PatrolsModule.Running) { _visual?.Dispose();Destroy(this);return; }
            float t = Time.time - _start;
            bool grounded = false;
            if (t > 0.25f && _col != null && (_rb == null || _rb.velocity.y <= 0.5f))
            {
                var b = _col.bounds;
                int n = Physics.RaycastNonAlloc(new Vector3(b.center.x, b.min.y + 0.2f, b.center.z), Vector3.down, _hits, 0.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++) if (_hits[i].collider != null && !_hits[i].collider.transform.IsChildOf(transform)) { grounded = true; break; }
            }
            if (!_landed && (grounded || t > 1.5f))
            {
                _landed = true; _landedAt = Time.time;
                _visual?.Dispose();_visual=null;   // its own animator again
            }
            if (!_landed) return;
            // 2.2.4: on the ground it looks around quickly - a fast turn to the player - and is alerted: the Detection FSM leaves
            // notDetected for inLOS (Alert, Attack on; the range sensor keeps the player as its target), as if it had been shot.
            // Vanilla only notices a player inside its view cone, and it lands facing the way the car went.
            var p = Core.PlayerState.Transform;
            if (p != null)
            {
                var d = p.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 1e-3f)
                {
                    var want = Quaternion.LookRotation(d.normalized, Vector3.up);
                    var rot = Quaternion.RotateTowards(transform.rotation, want, 900f * Time.deltaTime);
                    if (_rb != null) _rb.MoveRotation(rot); else transform.rotation = rot;
                    if (Quaternion.Angle(rot, want) < 5f || Time.time - _landedAt > 0.6f) { Alert(gameObject); Destroy(this); }
                    return;
                }
            }
            Alert(gameObject); Destroy(this);
        }

        /// <summary>Sends the mob Detection FSM from notDetected to inLOS (it noticed the player).</summary>
        internal static void Alert(GameObject mob)
        {
            if (mob == null) return;
            foreach (var f in mob.GetComponents<PlayMakerFSM>())
                if (f != null && f.FsmName == "Detection" && f.enabled && f.Fsm.Initialized && f.ActiveStateName == "notDetected")
                {
                    f.Fsm.SetState("inLOS");
                    if (Log.L2) Log.Deep(PatrolsModule.Instance, "Rider: " + mob.name + " looks around and spots you");
                }
        }

        /// <summary>Destroys the jump graph if it still runs.</summary>
        private void OnDestroy() { _visual?.Dispose();_visual=null; }
    }
}
