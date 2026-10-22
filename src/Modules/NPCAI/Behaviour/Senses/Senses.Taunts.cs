using System;
using UnityEngine;
using HutongGames.PlayMaker.Actions;
using WildWasteland.Core;
namespace WildWasteland.Modules.NPCAI
{
    /// <summary>NPC-owned taunt timing, knowledge sharing and horn observation.</summary>
    internal static partial class Senses
    {
        // the human Sound FSM's combat shout (Harmony prefix on AudioPlay.OnEnter, FSM "Sound", state "attack")
        /// <summary>AudioPlay.OnEnter prefix: the human Sound FSM's combat shout plays only as a taunt (Taunt decides).</summary>
        public static bool BeforeAudioPlay(AudioPlay __instance)
        {
            try
            {
                if (!On) return true;
                var fsm = __instance.Fsm;
                if (fsm == null) return true;
                if (fsm.Name == "INPUT_Horn") { _hornFsm = fsm; _hornAt = Time.time; return true; }    // the player's car horn starts (Engine() picks it up)
                if (fsm.Name != "Sound" || fsm.ActiveStateName != "attack") return true;
                var a = Get(fsm.GameObject);
                if (a == null) return true;
                // The game's Sound FSM shouts every 0.1-4 s for as long as the NPC has any target, a ghost included. Here a shout only
                // when it sees its target right now, the first time at once, then every TauntMin..TauntMax s; a suppressed shout
                // ends the state at once (the FSM keeps cycling, silent).
                float now = Time.time;
                if (!SeesNow(a) || now < a.NextShout)
                {
                    if (__instance.finishedEvent != null) fsm.Event(__instance.finishedEvent);
                    __instance.Finish();
                    return false;
                }
                a.NextShout = now + UnityEngine.Random.Range(Mathf.Max(1f, NpcaiSettings.TauntMin.Value), Mathf.Max(NpcaiSettings.TauntMin.Value, NpcaiSettings.TauntMax.Value));
                if (a.Human && NpcaiSettings.TauntRange.Value > 0f) Taunt(a);
            }
            catch (Exception e) { Core.Log.Error(NpcaiRuntime.Module, "Senses: " + e); }
            return true;
        }

        /// <summary>A human's taunt: passes its knowledge to friends in TauntRange and tells enemies where it stands.</summary>
        private static void Taunt(Agent t)
        {
            float now = Time.time, range = NpcaiSettings.TauntRange.Value;
            // friends: where the shouter's target is (or the ghost the shouter is going to)
            Vector3 where; bool known = false; Ghost tg = null;
            if ((t.State == State.Combat || t.State == State.Hide) && t.Target != null) { where = Fuzz(t.Target.transform.position, t.T.position, 0.2f); known = true; }   // "over there!": roughly
            else if (t.Ghost != null && t.State == State.Investigate && !t.Ghost.Retired) { where = t.Ghost.Pos; tg = t.Ghost; known = true; }   // a searcher found nothing: nothing to pass on
            else where = t.T.position;
            if (known)
            {
                string tag = t.Tag;
                Ghost shared = tg;
                // a shout passes on roughly what the shouter sees (a SHOUT-rank ghost near the target: friends go and look, they do not get a
                // live position) or the very ghost it is going to, at that ghost's rank
                if (shared == null) { shared = GetOrMake(Src.Shout, where, t.Target, Name(t.Owner) + " shouted about " + Name(t.Target), 6f, 15f, now); shared.Subject = t.Target; }   // one ghost per sighting: shouts refresh it instead of minting new ones
                Src relay = tg == null ? Src.Shout : t.GhostPrio;
                int told = 0;
                foreach (var kv in _agents)
                {
                    var f = kv.Value;
                    if (f == t || f.Owner == null || !f.Human || f.Tag != tag || f.T.parent != null) continue;
                    if ((f.T.position - t.T.position).sqrMagnitude > range * range) continue;
                    // a ghost acquired once (from a shout or any other way) is never re-sent by a shout: it would wake a searching NPC
                    // back to investigating the same spot again and again while its friends keep shouting
                    if (f.Ghost == shared || f.Heard.Contains(shared.Id)) continue;
                    if (f.Ghost != null && f.Ghost.Subject != null && f.Ghost.Subject == shared.Subject && (f.Ghost.Pos - shared.Pos).sqrMagnitude < 16f) continue;   // already knows the same thing
                    if (Assign(f, shared, relay, now)) { told++; f.Heard.Add(shared.Id); }
                }
                if (shared != tg && shared.Holders.Count == 0) { int i = _ghosts.IndexOf(shared); if (i >= 0) KillGhost(i); }
                if (WildWasteland.Core.Log.L2 && told > 0) Core.Log.Deep(NpcaiRuntime.Module, "Senses: " + Name(t.Owner) + "'s shout sends " + told + " " + tag + " to " + (tg != null ? "ghost #" + tg.Id : "its target"));
            }
            // enemies: the shouter gave itself away
            string ttag = t.Tag;
            int key = -t.Owner.GetInstanceID();
            Noise(t.Owner, t.T.position, range, Src.Shout, Name(t.Owner) + " shouting", a => a.Hostile.Contains(ttag) && !a.Heard.Contains(key), null, t.Owner,
                a => a.Heard.Add(key));
        }

    }
}
