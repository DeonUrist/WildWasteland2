using System;
using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Idle part owned by Presentation: the run-in-place animation fix (RunInPlace). The run-clip restart inside Drive stays in NPCAI until Wave B (B05/B07 split Drive).</summary>
    internal static partial class Idle
    {
        // (1.4.9) An idle NPC that nobody moves (waiting to go back, standing at home) must not play the run: the game's Movement FSM left in
        // its Run state (the run animation, a forward push every frame) or our own run animation left on -> back to the game's Idle.
        private static void RunInPlace(Ctl c)
        {
            try
            {
                if (!c.MovementLooked)
                {
                    c.MovementLooked = true;
                    foreach (var f in c.A.Owner.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Movement") { c.Movement = f; break; }
                }
                bool fsmRun = c.Movement != null && c.Movement.Fsm != null && c.Movement.Fsm.ActiveStateName == "Run";
                bool animRun = c.Anim != null && c.Anim.isActiveAndEnabled && c.Anim.GetCurrentAnimatorStateInfo(0).shortNameHash == RunHash && !c.Anim.IsInTransition(0);
                if (!fsmRun && !animRun) return;
                if (fsmRun) c.Movement.SendEvent("Animal_Idle");
                if (c.Anim != null) { c.Anim.speed = 1f; c.Anim.Play("idle", 0, 0f); }
                if (c.Rb != null) { var v = c.Rb.velocity; c.Rb.velocity = new Vector3(0f, v.y, 0f); }
                Log(c, "was running in place (" + (fsmRun ? "game's Movement in Run" : "run animation left on") + "), now stands");
            }
            catch (Exception e) { NpcModule.Log.LogError("Idle anim: " + e.Message); c.NextAnimCheck = Time.time + 30f; }
        }
    }
}
