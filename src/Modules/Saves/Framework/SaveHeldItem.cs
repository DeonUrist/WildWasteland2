using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using Patches = WildWasteland.Core.Patches;

namespace WildWasteland.Modules.Saves
{
    /// <summary>Releases native hand custody before saving and repairs that custody in existing snapshots.</summary>
    internal static class SaveHeldItem
    {
        static readonly HashSet<GameObject> Loaded = new HashSet<GameObject>();
        /// <summary>Observes deserialized parents before native load FSMs can detach the item.</summary>
        internal static void Install(IModule owner)
        {
            if (!Patches.Patch(owner, AccessTools.Method(typeof(ES3Types.ES3Type_GameObject), "ReadComponents"), postfix: AccessTools.Method(typeof(SaveHeldItem), "ReadComponents")))
                throw new InvalidOperationException("Held-item load observation failed");
            ModEvents.Subscribe<Core.Events.WorldReady>(owner, WorldReady);
        }
        /// <summary>Forgets references on scene/load reset and module shutdown.</summary>
        internal static void Clear() { Loaded.Clear(); }
        /// <summary>Returns only the native physical grab holder, excluding equipped weapons.</summary>
        static Transform Hand()
        { var camera = PlayerState.PlayerCamera; return camera != null ? camera.Find("Hand") : null; }
        /// <summary>Retains logical pocket custody even when its object is temporarily in the hand.</summary>
        static bool PocketOwned(GameObject item)
        {
            var slots = ModApi.Get<IInventorySlots>();
            if (slots != null) for (int i = 0; i < slots.Count; i++) if (slots.ItemIn(i) == item.name) return true;
            return false;
        }
        /// <summary>Captures the exact saved hand parent after its Transform component was read.</summary>
        static void ReadComponents(GameObject go)
        {
            if (!SaveBus.Loading || go == null || go.GetComponent<ES3Internal.ES3Prefab>() == null) return;
            var hand = Hand();
            if (hand != null && go.transform.parent == hand) Loaded.Add(go);
        }
        /// <summary>Drops through vanilla before freezing, with bounded admission and live settling.</summary>
        internal static IEnumerator Prepare(int generation, Action releasing)
        {
            float deadline = Time.unscaledTime + 8f;
            while (generation == SaveBus.Generation && SaveSystem.LocalReady)
            {
                var hand = Hand(); var item = PlayerInventory.FirstItem(hand);
                if (item == null || PocketOwned(item)) yield break;
                var grab = FsmUtil.Find(hand.parent.gameObject, "GrabItem");
                var reference = grab != null ? grab.FsmVariables.FindFsmGameObject("Item") : null;
                if (grab != null && reference != null && reference.Value == item && grab.ActiveStateName == "ItemInHand")
                {
                    releasing();
                    grab.SendEvent("drop");
                    Log.Main(SavesModule.Instance, "Save: dropping held " + item.name + " through vanilla GrabItem");
                    do { yield return null; } while (grab != null && grab.ActiveStateName == "Drop" && Time.unscaledTime < deadline && generation == SaveBus.Generation && SaveSystem.LocalReady);
                    if (generation != SaveBus.Generation || !SaveSystem.LocalReady) yield break;
                    if (item != null && (item.transform.IsChildOf(hand) || grab == null || grab.ActiveStateName != "idle")) throw new IOException("Held item did not finish vanilla Drop; save cancelled");
                    float until = Time.time + .75f;
                    deadline = Time.unscaledTime + 8f;
                    while (Time.time < until && generation == SaveBus.Generation && SaveSystem.LocalReady)
                    {
                        if (Time.unscaledTime >= deadline) throw new IOException("Held item physics stayed paused; save cancelled before serialization");
                        yield return null;
                    }
                    yield break;
                }
                if (Time.unscaledTime >= deadline) throw new IOException("Held item is busy; save cancelled before serialization");
                yield return null;
            }
        }
        /// <summary>Repairs only saved native hand roots after every native item deserialization pass.</summary>
        static void WorldReady(ref Core.Events.WorldReady ready)
        {
            try
            {
                if (!ready.FromLoad) return;
                foreach (var item in Loaded)
                {
                    if (item == null || PocketOwned(item)) continue;
                    var hand = Hand(); var parent = item.transform.parent;
                    if (parent != null && parent != hand) continue;
                    // Match GrabItem/Drop's item-owned events; never run them on nested cargo colliders.
                    foreach (var fsm in item.GetComponents<PlayMakerFSM>())
                    {
                        if (fsm.FsmName == "Colliders") fsm.SendEvent("CollidersActivate");
                        else if (fsm.FsmName == "UseCan" || fsm.FsmName == "UseCan_2") fsm.SendEvent("OutofHand");
                        else if (fsm.FsmName == "LockPhysics") fsm.SendEvent("LockPhysics_OFF");
                    }
                    var collider = item.GetComponent<Collider>(); if (collider != null) { collider.enabled = true; collider.isTrigger = false; }
                    item.transform.SetParent(null, true);
                    var player = GameRefs.Player;
                    if (player != null && item.transform.position.y < player.transform.position.y - 5f)
                        item.transform.position = player.transform.position + Vector3.up * 2f;
                    var body = item.GetComponent<Rigidbody>();
                    if (body != null) { body.isKinematic = false; body.useGravity = true; body.detectCollisions = true; body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.WakeUp(); }
                    Log.Main(SavesModule.Instance, "Load: repaired saved held " + item.name + " through native drop physics");
                }
            }
            finally { Loaded.Clear(); }
        }
    }
}
