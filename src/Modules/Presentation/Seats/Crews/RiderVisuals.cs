using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Real rider visual leases tracked and released with Presentation.</summary>
    internal sealed class RiderVisuals : IRiderVisuals
    {
        private static readonly System.Collections.Generic.List<System.IDisposable> _leases=new System.Collections.Generic.List<System.IDisposable>();
        /// <summary>Creates a presentation-owned animation and held visual.</summary>
        /// <summary>Creates the temporary jump animation lease while traffic owns the landing.</summary>
        public System.IDisposable Jump(GameObject actor) { var lease=new RiderJumpVisual(actor);_leases.Add(lease);return lease; }
        /// <summary>Plays the actual rider voice and returns its wind-up duration.</summary>
        public float Scream(Transform at)=>RiderVoice.Scream(at);
        /// <summary>Creates and tracks the actual per-rider visual lease.</summary>
        public IRiderVisual Attach(GameObject actor,GameObject vehicle,IRiderPresentationState state,bool bike) { var lease=new RiderVisual(actor,state);_leases.Add(lease);return lease; }
        /// <summary>Turns a seated actor in the platform plane with the existing bicycle arc clamp.</summary>
        public void Face(GameObject actor,GameObject vehicle,bool bike,Vector3 dir)
        {
            var up=vehicle!=null?vehicle.transform.up:Vector3.up;dir=Vector3.ProjectOnPlane(dir,up);if(dir.sqrMagnitude<1e-4f)return;
            if(bike && vehicle!=null){var forward=Vector3.ProjectOnPlane(vehicle.transform.forward,up);if(forward.sqrMagnitude>1e-4f)dir=Quaternion.AngleAxis(Mathf.Clamp(Vector3.SignedAngle(forward,dir,up),-55f,55f),up)*forward;}
            actor.transform.rotation=Quaternion.RotateTowards(actor.transform.rotation,Quaternion.LookRotation(dir.normalized,up),240f*Time.deltaTime);
        }
        /// <summary>Releases surviving per-actor leases when the module stops or a scene changes.</summary>
        internal static void Clear() { foreach(var lease in _leases.ToArray())lease.Dispose();_leases.Clear();RiderVoice.Clear(); }
        /// <summary>Removes a released actor lease immediately.</summary>
        internal static void Remove(System.IDisposable lease) { _leases.Remove(lease); }
    }
}
