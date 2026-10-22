using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Patrols
{
    // A jumped-off turret's fresh mob: its velocity is applied once its FSMs have started (Movement's Idle sets the velocity to 0 on entry),
    // and Apocaplayer's Jump clip plays on its own Animator through a PlayableGraph until it is back on the ground (0.25..1.5 s); then the
    // graph is destroyed and the mob's own animator controller is back in charge. Without Apocaplayer / a humanoid: just the jump.
    internal sealed class JumpClip : MonoBehaviour
    {
        internal Vector3 Velocity;
        private int _frames; private bool _pushed, _landed; private float _start, _landedAt;
        private PlayableGraph _graph; private bool _graphOk;
        private Rigidbody _rb; private Collider _col;
        private static readonly RaycastHit[] _hits = new RaycastHit[8];

        /// <summary>Starts the Jump clip on the mob Animator through a PlayableGraph (when the animation service has it).</summary>
        private void Start()
        {
            _start = Time.time;
            _rb = GetComponent<Rigidbody>(); _col = GetComponent<Collider>();
            try
            {
                var clip = Rider.Clip("Jump"); var anim = GetComponentInChildren<Animator>();
                if (clip != null && anim != null && anim.isHuman)
                {
                    _graph = PlayableGraph.Create("Apocapatrol.JumpOff");
                    _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                    var output = AnimationPlayableOutput.Create(_graph, "jump", anim);
                    output.SetSourcePlayable(AnimationClipPlayable.Create(_graph, clip));
                    _graph.Play(); _graphOk = true;
                }
            }
            catch (Exception e) { if (Log.L2) Log.Deep(PresentationModule.WorldOwner, "JumpClip: " + e.Message); }
        }

        /// <summary>Applies the jump velocity once the mob FSMs have started (second physics step).</summary>
        private void FixedUpdate()
        { if (!PatrolsModule.Running) return;   // refs-ok: REFACTOR-REQUEST B06-4 (Patrols running state; no IPatrols query yet)
            if (_pushed || ++_frames < 2) return;
            _pushed = true;
            if (_rb != null && !_rb.isKinematic) _rb.velocity = Velocity;
        }

        /// <summary>Ends the clip on landing (or after 1.5 s), turns the mob to the player and alerts it, then removes the component.</summary>
        private void Update()
        { if (!PatrolsModule.Running) return;   // refs-ok: REFACTOR-REQUEST B06-4 (Patrols running state; no IPatrols query yet)
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
                if (_graphOk) { try { _graph.Destroy(); } catch (Exception) { } _graphOk = false; }   // its own animator again
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
                    if (Log.L2) Log.Deep(PresentationModule.WorldOwner, "Rider: " + mob.name + " looks around and spots you");
                }
        }

        /// <summary>Destroys the jump graph if it still runs.</summary>
        private void OnDestroy() { if (_graphOk) { try { _graph.Destroy(); } catch (Exception) { } _graphOk = false; } }
    }
}
