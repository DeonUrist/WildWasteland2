using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // A rider's thrown blast lance: the game's lance only goes off when its head's trigger touches something, so one that lands flat or at an
    // angle used to lie there (and blow up later). Any solid hit - or 8 s - now sets it off, unless its own FSM is already exploding.
    internal sealed class LanceFuse : MonoBehaviour
    {
        private float _born; private bool _done;
        private PlayMakerFSM _fsm;
        private void Start() { _born = Time.time; _fsm = GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Explosion"); }
        private bool Exploding() { return _fsm != null && _fsm.Fsm != null && _fsm.Fsm.Initialized && (_fsm.ActiveStateName == "explode" || _fsm.ActiveStateName == "delete"); }
        private void OnCollisionEnter(Collision c) { if (!PatrolsModule.Running) return; if (!_done && Time.time - _born > 0.05f) Fire(c.contactCount > 0 ? c.GetContact(0).point : transform.position); }
        private void Update() { if (!PatrolsModule.Running) return; if (!_done && Time.time - _born > 8f) Fire(transform.position); }
        private void Fire(Vector3 at)
        {
            if (Exploding()) { _done = true; return; }
            _done = true;
            Rider.Boom(at);
            Destroy(gameObject);
        }
    }
}
