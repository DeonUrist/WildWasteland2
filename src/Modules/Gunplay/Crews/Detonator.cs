using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // sets a freshly spawned blast-lance projectile off once its FSMs have started (an event sent before Start is lost)
    /// <summary>Sets a freshly spawned blast-lance projectile off once its FSMs have started (an event sent before Start is lost).</summary>
    internal sealed class Detonator : MonoBehaviour
    {
        private int _frames;
        /// <summary>Two frames after spawn, sets the lance off through its Explosion FSM; removes itself after 10 frames.</summary>
        private void Update()
        { if (!PatrolsModule.Running) return;
            if (++_frames < 2) return;
            foreach (var f in GetComponents<PlayMakerFSM>()) if (f.FsmName == "Explosion" && f.Fsm.Initialized && f.ActiveStateName != "explode" && f.ActiveStateName != "delete") f.SendEvent("DamageFlammable");
            if (_frames > 10) Destroy(this);
        }
    }
}
