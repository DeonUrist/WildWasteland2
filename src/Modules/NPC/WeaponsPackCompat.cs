using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    // No pack assembly reference: observe its own attack and burning-status contracts.
    internal static class WeaponsPackCompat
    {
        private static bool installed, unavailable;
        private static FieldInfo actor;
        private static FieldInfo drawn;
        internal static bool ChainsawDrawn { get; private set; }
        private static readonly Dictionary<object, GameObject> burning = new Dictionary<object, GameObject>();
        private static readonly Dictionary<CreateObject, SawHit> cuts = new Dictionary<CreateObject, SawHit>();
        private static readonly List<CreateObject> expired = new List<CreateObject>();
        internal sealed class SawHit
        {
            internal GameObject Owner, Corpse; internal FsmFloat Health; internal float Before, Until;
            internal Vector3 Point; internal bool Lethal, Done;
            internal readonly List<CreateObject> Actions = new List<CreateObject>();
            internal readonly List<FloatCompare> Death = new List<FloatCompare>();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Tick()
        {
            if (!installed && !unavailable) Install();
            foreach (var pair in cuts)
                if (pair.Value.Done || Time.time > pair.Value.Until) expired.Add(pair.Key);
            foreach (var action in expired) cuts.Remove(action);
            expired.Clear();
            // ChainsawLimbs.Tick(); // Dismemberment disabled.
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Install()
        {
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("apocalypter.weaponspack", out info) || ReferenceEquals(info.Instance, null)) { unavailable = true; return; }
            try
            {
                var plugin = info.Instance.GetType();
                var status = plugin.Assembly.GetType("ChainsawPrototype.FlamethrowerIgniteStatus", true);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                actor = status.GetField("actor", flags);
                drawn = plugin.GetField("enabledPreview", flags);
                var equip = plugin.GetMethod("StartPreview", flags, null, Type.EmptyTypes, null);
                var holster = plugin.GetMethod("StopPreview", flags, null, Type.EmptyTypes, null);
                var touch = status.GetMethod("Touch", flags, null, new[] { typeof(float), typeof(float) }, null);
                var end = status.GetMethod("BeginEnding", flags, null, new[] { typeof(float) }, null);
                var destroy = status.GetMethod("OnDestroy", flags, null, Type.EmptyTypes, null);
                var hit = plugin.GetMethod("ProcessHit", flags, null, new[] { typeof(RaycastHit), typeof(float), typeof(bool), typeof(float), typeof(bool) }, null);
                if (actor == null || actor.FieldType != typeof(Transform) || drawn == null || drawn.FieldType != typeof(bool)
                    || equip == null || holster == null || touch == null || touch.ReturnType != typeof(bool)
                    || end == null || end.ReturnType != typeof(void) || destroy == null || destroy.ReturnType != typeof(void) || hit == null)
                    throw new MissingMemberException("unsupported burning/attack contract");
                bool ok = Core.Patches.Patch(NpcModule.Instance, touch, postfix: AccessTools.Method(typeof(WeaponsPackCompat), nameof(AfterTouch)));
                ok &= Core.Patches.Patch(NpcModule.Instance, end, postfix: AccessTools.Method(typeof(WeaponsPackCompat), nameof(AfterBurnEnd)));
                ok &= Core.Patches.Patch(NpcModule.Instance, destroy, postfix: AccessTools.Method(typeof(WeaponsPackCompat), nameof(AfterBurnEnd)));
                ok &= Core.Patches.Patch(NpcModule.Instance, hit, prefix: AccessTools.Method(typeof(WeaponsPackCompat), nameof(BeforeSaw)), postfix: AccessTools.Method(typeof(WeaponsPackCompat), nameof(AfterSaw)));
                ok &= Core.Patches.Patch(NpcModule.Instance, equip, postfix: AccessTools.Method(typeof(WeaponsPackCompat), nameof(AfterEquip)));
                ok &= Core.Patches.Patch(NpcModule.Instance, holster, postfix: AccessTools.Method(typeof(WeaponsPackCompat), nameof(AfterHolster)));
                installed = ok; unavailable = !ok;
                ChainsawDrawn = ok && (bool)drawn.GetValue(info.Instance);
                if (ok) Core.Log.Always("[NPC] Weapons Pack " + info.Metadata.Version + ": burning fear and chainsaw kills connected.");
            }
            catch (Exception e) { unavailable = true; Core.Log.Warn("[NPC] Weapons Pack integration unavailable: " + e.GetBaseException().Message); }
        }

        private static void AfterTouch(object __instance, bool __result)
        {
            if (!installed || !__result || !NpcModule.Running || !GameRefs.InGame || burning.ContainsKey(__instance)) return;
            try
            {
                var target = actor.GetValue(__instance) as Transform;
                if (target == null) return;
                burning[__instance] = target.gameObject;
                var player = GameRefs.Player;
                Brain.FireRetreat(target.gameObject, player != null ? player.transform.position : target.position, true);
            }
            catch (Exception e) { Core.Log.Warn("[NPC] Burning fear: " + e.GetBaseException().Message); }
        }

        private static void AfterEquip(object __instance) { ChainsawDrawn = installed && (bool)drawn.GetValue(__instance); }
        private static void AfterHolster() { ChainsawDrawn = false; }

        private static void AfterBurnEnd(object __instance)
        {
            GameObject target;
            if (!burning.TryGetValue(__instance, out target)) return;
            burning.Remove(__instance);
            if (NpcModule.Running && target != null) Brain.FireRetreat(target, target.transform.position, false);
        }

        private static void BeforeSaw(RaycastHit __0, float __1, out SawHit __state)
        {
            __state = null;
            if (!installed || !NpcModule.Running || !GameRefs.InGame || __0.collider == null || !(__1 > 0f)) return;
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
            catch (Exception e) { Core.Log.Warn("[NPC] Chainsaw death capture: " + e.GetBaseException().Message); }
        }

        private static bool Killed(SawHit hit)
        {
            if (!(hit.Health.Value < hit.Before)) return false;
            if (hit.Health.Value <= 0f) return true;
            foreach (var compare in hit.Death)
                if ((compare.lessThan != null && hit.Health.Value < compare.float2.Value) || (compare.equal != null && hit.Health.Value == compare.float2.Value)) return true;
            return false;
        }

        private static void AfterSaw(SawHit __state)
        {
            if (__state == null) return;
            try
            {
                __state.Lethal = Killed(__state);
                if (!__state.Lethal) { foreach (var action in __state.Actions) cuts.Remove(action); return; }
                if (__state.Corpse != null) FinishKill(__state);
            }
            catch (Exception e) { Core.Log.Warn("[NPC] Chainsaw kill: " + e.GetBaseException().Message); }
        }

        internal static void AfterCorpse(CreateObject action)
        {
            if (cuts.Count == 0 || !NpcModule.Running) return;
            SawHit hit;
            if (!cuts.TryGetValue(action, out hit) || hit.Done || Time.time > hit.Until || action.storeObject == null) return;
            var corpse = action.storeObject.Value;
            if (corpse == null) return;
            hit.Corpse = corpse;
            if (hit.Lethal || Killed(hit)) FinishKill(hit);
        }

        private static void FinishKill(SawHit hit)
        {
            if (hit.Done) return;
            hit.Done = true;
            Brain.ChainsawWitnesses(hit.Corpse);
            // ChainsawLimbs.Cut(hit.Corpse, hit.Point); // Dismemberment disabled.
        }

        internal static void Reset() { cuts.Clear(); expired.Clear(); burning.Clear(); /* ChainsawLimbs.Clear(); */ ChainsawDrawn = false; }
        internal static void Shutdown() { Reset(); installed = unavailable = false; actor = drawn = null; }
    }
}
