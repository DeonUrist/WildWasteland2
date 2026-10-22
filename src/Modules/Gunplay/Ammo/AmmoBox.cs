using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Gunplay
{
    /// Spawns an ammo box item (same save-compatible recipe as Apocaspawner) holding the rounds that
    /// did not fit in the backpack.
    internal static class AmmoBox
    {
        private static readonly Dictionary<string, string> DefaultBoxes = new Dictionary<string, string>
        {
            { "ammo_762", "ammo_box_762mm" }, { "ammo_556", "ammo_box_556mm" }, { "ammo_9mm", "ammo_box_9mm" },
            { "ammo_12gauge", "ammo_box_12gauge" }, { "ammo_20gauge", "ammo_box_20gauge" }, { "ammo_22caliber", "ammo_box_22" },
            { "ammo_3006", "ammo_box_3006" }, { "ammo_arrow", "ammo_arrow" },
            { "ammo_battery", "flashlight_batteries" }, { "ammo_grenade", "grenade" },
            { "armorplate", "armorplate" },
        };
        private static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

        /// <summary>Item name of the box/item that carries a caliber (ammo_762 -> ammo_box_762mm, armorplate -> armorplate); null when unknown.</summary>
        private static string BoxName(string caliber)
        {
            string d;
            return DefaultBoxes.TryGetValue(caliber, out d) ? d : null;
        }

        /// <summary>Template object for a caliber's box: the asset prefab, else a scene copy "(Clone)" with the right FSM; cached per caliber (one asset scan at the first drop).</summary>
        private static GameObject FindPrefab(string caliber)
        {
            GameObject cached;
            if (_prefabs.TryGetValue(caliber, out cached) && cached != null) return cached;
            string name = BoxName(caliber);
            if (name == null) return null;
            GameObject asset = null, instance = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.transform.parent != null) continue;
                if (go.name != name && !go.name.StartsWith(name + "(Clone)", StringComparison.Ordinal)) continue;
                string fsmName = caliber == "armorplate" ? "ID" : caliber == "ammo_battery" ? "Batteries" : "Ammo";
                if (PerGunAmmo.Find(go, fsmName) == null) continue;
                if (!go.scene.IsValid()) { if (go.name == name && asset == null) asset = go; }
                else if (instance == null && go.name.StartsWith(name + "(Clone)", StringComparison.Ordinal)) instance = go;
            }
            var found = asset ?? instance;
            if (found != null) { _prefabs[caliber] = found; if (Log.L2) AmmoSettings.V("Ammo box template for " + caliber + ": " + found.name + (asset != null ? " (prefab)" : " (scene copy)")); }
            else GunplayLog.Warn("No ammo box prefab named '" + name + "' is loaded");
            return found;
        }

        /// <summary>Spawns save-registered box items holding <paramref name="rounds"/> of a caliber (grenades/plates one item each) in front of the camera; false keeps the ammo (no registry, no prefab, spawn failure).</summary>
        internal static bool Drop(string caliber, int rounds, Transform at)
        {
            if (rounds <= 0 || at == null) return false;
            var prefab = FindPrefab(caliber);
            if (prefab == null) return false;
            // (WW) Core.GameRefs instead of GameObject.Find; Find stays as the fallback (GameRefs stops refreshing itemNameID once the others are found)
            var counterGo = GameRefs.ItemNameId;
            if (counterGo == null) counterGo = GameObject.Find("itemNameID");
            var cf = counterGo != null ? FsmUtil.Find(counterGo, "itemNameID") : null;
            var counter = cf != null ? cf.FsmVariables.GetFsmInt("intName") : null;
            var reg = GameRefs.NewGOArrayList;
            if (reg == null) reg = GameObject.Find("NewGO_ArrayList");
            PlayMakerArrayListProxy list = null;
            if (reg != null)
                foreach (var p in reg.GetComponents<PlayMakerArrayListProxy>())
                    if ((p.referenceName ?? "").ToLowerInvariant().Contains("item")) { list = p; break; }
            if (counter == null || list == null)
            {
                GunplayLog.Warn("Cannot drop " + caliber + ": save registry/counter unavailable; retaining ammo");
                return false;
            }
            var mainCam = GameRefs.MainCamera;
            var cam = mainCam != null ? mainCam.transform : at;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = at.forward;
            var pos = cam.position + fwd * 0.7f - Vector3.up * 0.3f;
            var created = new List<GameObject>();
            var staging = new GameObject("WildWasteland.Guns.DropStaging");
            staging.SetActive(false);
            try
            {
                // Grenades and armor plates are individual items; other boxes store a count.
                int itemCount = caliber == "ammo_grenade" || caliber == "armorplate" ? rounds : 1;
                for (int i = 0; i < itemCount; i++)
                {
                    // Configure before activation so Randomize cannot overwrite the returned rounds.
                    var go = UnityEngine.Object.Instantiate(prefab, pos, UnityEngine.Random.rotation, staging.transform);
                    created.Add(go);
                    go.SetActive(false);
                    var rnd = PerGunAmmo.Find(go, "Randomize");
                    if (rnd != null) rnd.enabled = false;
                    if (caliber == "armorplate")
                    {
                        var quantityFsm = PerGunAmmo.Find(go, "Quantity");
                        var quantity = quantityFsm == null ? null : quantityFsm.FsmVariables.GetFsmInt("Quantity");
                        if (quantity != null) quantity.Value = 1;
                    }
                    else
                    {
                        var ammo = PerGunAmmo.Find(go, caliber == "ammo_battery" ? "Batteries" : "Ammo");
                        var v = ammo != null ? ammo.FsmVariables.GetFsmInt(caliber == "ammo_battery" ? "batteries" : "ammo") : null;
                        if (v == null) throw new InvalidOperationException(go.name + " has no ammo count variable");
                        v.Value = caliber == "ammo_grenade" ? 1 : rounds;
                    }
                    counter.Value += 1;
                    go.name = BoxName(caliber) + "(Clone)" + counter.Value;
                    list.arrayList.Add(go);
                }
                foreach (var go in created)
                {
                    go.transform.SetParent(null, true);
                    go.SetActive(true);
                    if (caliber == "armorplate")
                    {
                        go.layer = 9;
                        var visibility = PerGunAmmo.Find(go, "Visibility"); if (visibility != null) visibility.SendEvent("Activate");
                        var colliders = PerGunAmmo.Find(go, "Colliders"); if (colliders != null) colliders.SendEvent("CollidersActivate");
                        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
                        foreach (var collider in go.GetComponentsInChildren<Collider>(true)) { collider.enabled = true; collider.isTrigger = false; }
                    }
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb != null) { rb.isKinematic = false; if (caliber == "armorplate") rb.useGravity = true; rb.velocity = fwd * 1.5f; }
                }
                if (Log.L2) GunplayLog.Deep("Dropped " + rounds + " x " + caliber + " in " + created.Count + " save-registered item(s)");
                CombatEvents.Dropped(caliber, rounds, created.Count > 0 ? created[0] : null, pos);
                return true;
            }
            catch (Exception ex)
            {
                foreach (var go in created)
                {
                    list.arrayList.Remove(go);
                    go.SetActive(false);
                    UnityEngine.Object.Destroy(go);
                }
                GunplayLog.Warn("Could not drop " + caliber + "; retaining ammo: " + ex.Message);
                return false;
            }
            finally { UnityEngine.Object.Destroy(staging); }
        }
    }
}
