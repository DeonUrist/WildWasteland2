using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Core
{
    // Native local supply counts and save-registered partial drops.
    internal static class InventorySupplies
    {
        internal static readonly string[] Keys = { "arrow", "22caliber", "9mm", "20gauge", "12gauge", "556", "762", "3006", "grenade", "battery", "nuts", "bandage", "first_aid" };
        static readonly string[] Prefabs = { "ammo_arrow", "ammo_box_22", "ammo_box_9mm", "ammo_box_20gauge", "ammo_box_12gauge", "ammo_box_556mm", "ammo_box_762mm", "ammo_box_3006", "grenade", "flashlight_batteries", "tin_can_nuts", "bandage_1", "first_aid_1" };
        sealed class MedicalSupply { internal PlayMakerFSM Slot, QuantityFsm; internal FsmGameObject Item; internal GameObject Source; internal FsmInt Quantity; }
        static readonly MedicalSupply[] Medical = { new MedicalSupply(), new MedicalSupply() };
        static PlayMakerFSM _ammo;
        static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();
        static ES3Internal.ES3ReferenceMgrBase _manager;
        static int _scan;
        internal static bool Ready { get { return SaveSystem.LocalReady && !SinglePlayerSaving.Serializing && SaveRuntime.Health() >= 1; } }
        internal static void Install() { Scheduler.Sliced(null, "Inventory supply prefabs", 500, Discover); }
        static bool Discover()
        {
            var manager = ES3Internal.ES3ReferenceMgrBase.Current;
            if (manager == null) return false;
            if (_manager != manager) { _manager = manager; _scan = 0; PrefabCache.Clear(); }
            if (_scan >= manager.prefabs.Count) return false;
            var prefab = manager.prefabs[_scan++];
            if (prefab != null && Array.IndexOf(Prefabs, prefab.name) >= 0) PrefabCache[prefab.name] = prefab.gameObject;
            return true;
        }
        internal static FsmInt Count(int kind)
        {
            if (kind < 0 || kind >= Keys.Length) return null;
            if (kind >= 11) return ReadMedical(kind).Quantity;
            if (kind == 10) return FsmVariables.GlobalVariables.FindFsmInt("Money");
            if (_ammo == null && GameRefs.GameManager != null) _ammo = FsmUtil.Find(GameRefs.GameManager, "Ammo");
            return _ammo != null ? _ammo.FsmVariables.FindFsmInt("ammo_" + Keys[kind]) : null;
        }
        static MedicalSupply ReadMedical(int kind)
        {
            var supply = Medical[kind - 11];
            if (supply.Slot == null)
            {
                var quick = GameRefs.PlayerCameraHolder != null ? GameRefs.PlayerCameraHolder.transform.Find("PlayerCamera/QuickItems/" + (kind == 11 ? "Bandage_Item" : "FirstAid_Item")) : null;
                supply.Slot = quick != null ? FsmUtil.Find(quick.gameObject, "SlotEmptyFull") : null;
                supply.Item = supply.Slot != null ? supply.Slot.FsmVariables.FindFsmGameObject("item") : null;
                supply.Source = null; supply.Quantity = null; supply.QuantityFsm = null;
            }
            var source = supply.Item != null ? supply.Item.Value : null;
            if (source == null || supply.Slot == null || source.transform.parent != supply.Slot.transform) source = null;
            if (!ReferenceEquals(source, supply.Source))
            {
                supply.Source = source; supply.QuantityFsm = source != null ? FsmUtil.Find(source, "Quantity") : null;
                supply.Quantity = supply.QuantityFsm != null ? supply.QuantityFsm.FsmVariables.FindFsmInt("Quantity") : null;
            }
            return supply;
        }
        internal static bool Drop(int kind, int quantity)
        {
            var count = Count(kind);
            if (!Ready || !Application.isFocused || GameRefs.Player == null || count == null || quantity < 1 || quantity > count.Value) return false;
            var medical = kind >= 11 ? ReadMedical(kind) : null;
            if (medical != null && (medical.Source == null || medical.Slot.ActiveStateName != "full")) return false;
            GameObject prefab; PrefabCache.TryGetValue(Prefabs[kind], out prefab);
            var counterFsm = GameRefs.ItemNameId != null ? FsmUtil.Find(GameRefs.ItemNameId, "itemNameID") : null;
            var counter = counterFsm != null ? counterFsm.FsmVariables.FindFsmInt("intName") : null;
            PlayMakerArrayListProxy registry = null;
            if (GameRefs.NewGOArrayList != null) foreach (var list in GameRefs.NewGOArrayList.GetComponents<PlayMakerArrayListProxy>())
                if (list.referenceName == "ArrayList_Items") { registry = list; break; }
            if (prefab == null || counter == null || registry == null) return false;
            var player = GameRefs.Player.transform;
            var camera = GameRefs.MainCamera != null ? GameRefs.MainCamera.transform : player;
            var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = player.forward;
            // Keep creation at the player even when a chase camera is far behind the vehicle.
            var position = player.position + Vector3.up + forward * .8f;
            var staging = new GameObject("WildWasteland.InventoryDrop"); staging.SetActive(false);
            var items = new List<GameObject>();
            try
            {
                // Native grenades save no stack count. Each one must remain an individual grenade.
                int total = kind == 8 ? quantity : 1;
                for (int i = 0; i < total; i++)
                {
                    var item = UnityEngine.Object.Instantiate(prefab, position + forward * (.04f * i), Quaternion.identity, staging.transform);
                    items.Add(item); item.SetActive(false);
                    var randomize = FsmUtil.Find(item, "Randomize"); if (randomize != null) randomize.enabled = false;
                    var fsm = FsmUtil.Find(item, kind >= 11 ? "Quantity" : kind == 9 ? "Batteries" : kind == 10 ? "Nuts" : "Ammo");
                    var amount = fsm != null ? fsm.FsmVariables.FindFsmInt(kind >= 11 ? "Quantity" : kind == 9 ? "batteries" : kind == 10 ? "nuts" : "ammo") : null;
                    if (amount == null) throw new InvalidOperationException("Missing native supply count");
                    amount.Value = kind == 8 ? 1 : quantity;
                    counter.Value++;
                    item.name = Prefabs[kind] + "(Clone)" + counter.Value;
                    registry.arrayList.Add(item);
                }
            }
            catch (Exception e)
            {
                foreach (var item in items) { registry.arrayList.Remove(item); UnityEngine.Object.Destroy(item); }
                UnityEngine.Object.Destroy(staging); Log.Warn("[Inventory] Inventory drop retained supplies: " + e.Message); return false;
            }
            count.Value -= quantity;
            if (medical != null)
            {
                if (count.Value == 0) medical.QuantityFsm.Fsm.SetState("delete");
                // Refresh the native quick-slot quantity/UI without entering its healing states.
                medical.Slot.Fsm.SetState(count.Value > 0 ? "full" : "empty");
            }
            foreach (var item in items)
            {
                item.transform.SetParent(null, true); item.SetActive(true);
                var body = item.GetComponent<Rigidbody>(); if (body != null) { body.isKinematic = false; body.useGravity = true; body.velocity = forward; }
            }
            UnityEngine.Object.Destroy(staging);
            if (Log.Verbose) Log.Info("[Inventory] Inventory dropped " + quantity + " x " + Keys[kind] + " through native local creation");
            return true;
        }
    }
}
