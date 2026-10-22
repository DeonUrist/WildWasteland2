using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Weapons Pack part serving NPC AI (no pack assembly reference; observes the pack's own attack and burning-status contracts):
    /// a burning NPC retreats while it burns, hostile humans that see a chainsaw kill flee, and a drawn chainsaw triples the player's
    /// footstep radius (read by NPC Senses through <see cref="ChainsawDrawn"/>). Acts only while the NPC module runs.
    /// </summary>
    internal static class WeaponsPackFear
    {
        private static IModule _owner;
        private static bool installed;
        private static Func<object, Transform> actor;
        private static Func<object, bool> drawn;
        /// <summary>True while the pack's chainsaw is drawn (NPC Senses footstep radius x3).</summary>
        internal static bool ChainsawDrawn { get; private set; }
        private static readonly Dictionary<object, GameObject> burning = new Dictionary<object, GameObject>();
        private static readonly Dictionary<CreateObject, SawHit> cuts = new Dictionary<CreateObject, SawHit>();
        private static readonly List<CreateObject> expired = new List<CreateObject>();
        /// <summary>One chainsaw contact on an FSM-health target: the corpse-spawning actions and death compares captured before the hit.</summary>
        internal sealed class SawHit
        {
            internal GameObject Owner, Corpse; internal FsmFloat Health; internal float Before, Until;
            internal Vector3 Point; internal bool Lethal, Done;
            internal readonly List<CreateObject> Actions = new List<CreateObject>();
            internal readonly List<FloatCompare> Death = new List<FloatCompare>();
        }

        /// <summary>Job "Compatibility.WeaponsPack.Cuts" (2 s, scaled time): forgets finished or stale chainsaw contacts.</summary>
        internal static void Tick()
        {
            if (cuts.Count == 0) return;
            foreach (var pair in cuts)
                if (pair.Value.Done || Time.time > pair.Value.Until) expired.Add(pair.Key);
            foreach (var action in expired) cuts.Remove(action);
            expired.Clear();
        }

        /// <summary>Resolves the burning status and attack members, then patches them under the bridge scope (independent of the other parts).</summary>
        internal static void Install(IModule owner, BepInEx.PluginInfo info)
        {
            _owner = owner;
            try
            {
                var plugin = info.Instance.GetType();
                var status = plugin.Assembly.GetType("ChainsawPrototype.FlamethrowerIgniteStatus", true);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var actorField = status.GetField("actor", flags);
                var drawnField = plugin.GetField("enabledPreview", flags);
                var equip = plugin.GetMethod("StartPreview", flags, null, Type.EmptyTypes, null);
                var holster = plugin.GetMethod("StopPreview", flags, null, Type.EmptyTypes, null);
                var touch = status.GetMethod("Touch", flags, null, new[] { typeof(float), typeof(float) }, null);
                var end = status.GetMethod("BeginEnding", flags, null, new[] { typeof(float) }, null);
                var destroy = status.GetMethod("OnDestroy", flags, null, Type.EmptyTypes, null);
                var hit = plugin.GetMethod("ProcessHit", flags, null, new[] { typeof(RaycastHit), typeof(float), typeof(bool), typeof(float), typeof(bool) }, null);
                if (actorField == null || actorField.FieldType != typeof(Transform) || drawnField == null || drawnField.FieldType != typeof(bool)
                    || equip == null || holster == null || touch == null || touch.ReturnType != typeof(bool)
                    || end == null || end.ReturnType != typeof(void) || destroy == null || destroy.ReturnType != typeof(void) || hit == null)
                    throw new MissingMemberException("unsupported burning/attack contract");
                actor = Access.Read<Transform>(actorField); drawn = Access.Read<bool>(drawnField);
                bool ok = Core.Patches.Patch(owner, touch, postfix: AccessTools.Method(typeof(WeaponsPackFear), nameof(AfterTouch)));
                ok &= Core.Patches.Patch(owner, end, postfix: AccessTools.Method(typeof(WeaponsPackFear), nameof(AfterBurnEnd)));
                ok &= Core.Patches.Patch(owner, destroy, postfix: AccessTools.Method(typeof(WeaponsPackFear), nameof(AfterBurnEnd)));
                ok &= Core.Patches.Patch(owner, hit, prefix: AccessTools.Method(typeof(WeaponsPackFear), nameof(BeforeSaw)), postfix: AccessTools.Method(typeof(WeaponsPackFear), nameof(AfterSaw)));
                ok &= Core.Patches.Patch(owner, equip, postfix: AccessTools.Method(typeof(WeaponsPackFear), nameof(AfterEquip)));
                ok &= Core.Patches.Patch(owner, holster, postfix: AccessTools.Method(typeof(WeaponsPackFear), nameof(AfterHolster)));
                installed = ok;
                ChainsawDrawn = ok && drawn(info.Instance);
                if (ok) Log.Main(owner, "Weapons Pack " + info.Metadata.Version + ": burning fear and chainsaw kills connected.");
                else Log.Warn(owner, "Weapons Pack fear/chainsaw hooks failed; NPCs ignore burning and chainsaw kills");
            }
            catch (Exception e) { installed = false; Log.Warn(owner, "Weapons Pack integration unavailable: " + e.GetBaseException().Message); }
        }

        /// <summary>Postfix of FlamethrowerIgniteStatus.Touch (per flame contact): a newly burning NPC retreats from the player while it burns.</summary>
        private static void AfterTouch(object __instance, bool __result)
        {
            if (!installed || !__result || !CompatibilityHost.Npc.Running || !GameRefs.InGame || burning.ContainsKey(__instance)) return;
            try
            {
                var target = actor(__instance);
                if (target == null) return;
                burning[__instance] = target.gameObject;
                var player = GameRefs.Player;
                ModApi.Get<INpcFear>()?.FireRetreat(target.gameObject, player != null ? player.transform.position : target.position, true); // refs-ok: REFACTOR-REQUEST B17-2
            }
            catch (Exception e) { Log.Warn(_owner, "Burning fear: " + e.GetBaseException().Message); }
        }

        /// <summary>Postfix of the pack's StartPreview (chainsaw drawn).</summary>
        private static void AfterEquip(object __instance) { ChainsawDrawn = installed && drawn(__instance); }
        /// <summary>Postfix of the pack's StopPreview (chainsaw holstered).</summary>
        private static void AfterHolster() { ChainsawDrawn = false; }

        /// <summary>Postfix of BeginEnding/OnDestroy of the burning status: the NPC stops its burning retreat.</summary>
        private static void AfterBurnEnd(object __instance)
        {
            GameObject target;
            if (!burning.TryGetValue(__instance, out target)) return;
            burning.Remove(__instance);
            if (CompatibilityHost.Npc.Running && target != null) ModApi.Get<INpcFear>()?.FireRetreat(target, target.transform.position, false); // refs-ok: REFACTOR-REQUEST B17-2
        }

        /// <summary>Prefix of the pack's ProcessHit (per chainsaw contact): captures the target's corpse actions and death compares.</summary>
        private static void BeforeSaw(RaycastHit __0, float __1, out SawHit __state)
        {
            __state = null;
            if (!installed || !CompatibilityHost.Npc.Running || !GameRefs.InGame || __0.collider == null || !(__1 > 0f)) return;
            try
            {
                PlayMakerFSM fsm = null;
                for (var node = __0.collider.transform; node != null && fsm == null; node = node.parent) fsm = FsmUtil.Find(node.gameObject, "Health");
                if (fsm == null || fsm.Fsm == null || !fsm.Fsm.Initialized) return;
                var hp = fsm.FsmVariables.FindFsmFloat("Health");
                if (hp == null || hp.Value < 1f) return;
                var hit = new SawHit { Owner = fsm.gameObject, Health = hp, Before = hp.Value, Point = __0.point, Until = Time.time + 2f };
                foreach (var state in fsm.Fsm.States)
                    foreach (var action in state.Actions)
                    {
                        var create = action as CreateObject;
                        var prefab = create == null || create.gameObject == null ? null : create.gameObject.Value;
                        if (create != null && create.Enabled && prefab != null && prefab.name.EndsWith("_Dead", StringComparison.OrdinalIgnoreCase)
                            && prefab.GetComponentInChildren<Rigidbody>(true) != null) hit.Actions.Add(create);
                    }
                var current = fsm.Fsm.ActiveState;
                if (current != null)
                    foreach (var action in current.Actions)
                    {
                        var compare = action as FloatCompare;
                        if (compare == null || !compare.Enabled || compare.float1 == null || !compare.float1.UseVariable
                            || !ReferenceEquals(fsm.FsmVariables.FindFsmFloat(compare.float1.Name), hp) || compare.float2 == null || compare.float2.UseVariable
                            || compare.tolerance == null || compare.tolerance.Value != 0f) continue;
                        foreach (var transition in current.Transitions)
                            if (transition != null && transition.FsmEvent != null
                                && ((compare.lessThan != null && transition.FsmEvent.Name == compare.lessThan.Name) || (compare.equal != null && transition.FsmEvent.Name == compare.equal.Name)))
                                foreach (var target in fsm.Fsm.States)
                                    if (target.Name == transition.ToState)
                                        foreach (var candidate in target.Actions) if (hit.Actions.Contains(candidate as CreateObject)) hit.Death.Add(compare);
                    }
                if (hit.Actions.Count == 0) return;
                foreach (var action in hit.Actions) cuts[action] = hit;
                __state = hit;
            }
            catch (Exception e) { Log.Warn(_owner, "Chainsaw death capture: " + e.GetBaseException().Message); }
        }

        /// <summary>True when the hit lowered the health to 0 or past one of the captured death compares.</summary>
        private static bool Killed(SawHit hit)
        {
            if (!(hit.Health.Value < hit.Before)) return false;
            if (hit.Health.Value <= 0f) return true;
            foreach (var compare in hit.Death)
                if ((compare.lessThan != null && hit.Health.Value < compare.float2.Value) || (compare.equal != null && hit.Health.Value == compare.float2.Value)) return true;
            return false;
        }

        /// <summary>Postfix of ProcessHit: decides lethality; a corpse already created finishes the kill.</summary>
        private static void AfterSaw(SawHit __state)
        {
            if (__state == null) return;
            try
            {
                __state.Lethal = Killed(__state);
                if (!__state.Lethal) { foreach (var action in __state.Actions) cuts.Remove(action); return; }
                if (__state.Corpse != null) FinishKill(__state);
            }
            catch (Exception e) { Log.Warn(_owner, "Chainsaw kill: " + e.GetBaseException().Message); }
        }

        /// <summary>Core CreateObject.OnEnter postfix (every CreateObject action; early-out while no chainsaw contact is pending): links the corpse of a cut target.</summary>
        internal static void AfterCorpse(CreateObject action)
        {
            if (cuts.Count == 0 || !CompatibilityHost.Npc.Running) return;
            SawHit hit;
            if (!cuts.TryGetValue(action, out hit) || hit.Done || Time.time > hit.Until || action.storeObject == null) return;
            var corpse = action.storeObject.Value;
            if (corpse == null) return;
            hit.Corpse = corpse;
            if (hit.Lethal || Killed(hit)) FinishKill(hit);
        }

        /// <summary>Lets hostile humans that see the corpse flee (once per kill).</summary>
        private static void FinishKill(SawHit hit)
        {
            if (hit.Done) return;
            hit.Done = true;
            ModApi.Get<INpcFear>()?.ChainsawWitnesses(hit.Corpse); // refs-ok: REFACTOR-REQUEST B17-2
        }

        /// <summary>Forgets cuts, burning NPCs and the drawn flag (load begin, scene change).</summary>
        internal static void Reset() { cuts.Clear(); expired.Clear(); burning.Clear(); ChainsawDrawn = false; }
        /// <summary>Clears all state and delegates; the scope removes the patches.</summary>
        internal static void Shutdown() { Reset(); installed = false; actor = null; drawn = null; _owner = null; }
    }
}
