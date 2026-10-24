using System;
using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Loot
{
    internal static class NpcAmmoDrops
    {
        private static readonly HashSet<int> Dropped = new HashSet<int>();
        private static readonly Dictionary<string,string> Boxes = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { {"9MM","ammo_box_9mm"},{".22LR","ammo_box_22"},{"7.62MM","ammo_box_762mm"},{"5.56MM","ammo_box_556mm"},{".30-06","ammo_box_3006"},{"12GA","ammo_box_12gauge"},{"20GA","ammo_box_20gauge"},{"ARROW","ammo_arrow"} };
        internal static void Clear() { Dropped.Clear(); }
        /// <summary>One matching ammo drop per native death; corpses restored from saves never enter this living Health path.</summary>
        internal static void AfterCreate(CreateObject action)
        {
            if (action.Fsm.Name != "Health" || action.Fsm.ActiveStateName != "spawn" || action.Owner == null || action.Owner == GameRefs.Player) return;
            var corpse = action.storeObject == null ? null : action.storeObject.Value;
            if (corpse == null || corpse.name.IndexOf("_Dead",StringComparison.OrdinalIgnoreCase) < 0 || Dropped.Contains(action.Owner.GetInstanceID())) return;
            var weapon = SelectedWeapon(action.Owner); if (weapon == null) return;
            string caliber = null, box; WeaponInfo info; int key; var catalog = ModApi.Get<IWeaponCatalog>();
            if (catalog != null && catalog.TryResolve(weapon,out key) && catalog.TryGet(key,out info)) caliber = info.Caliber;
            if (caliber == null) foreach (var row in WeaponNames.BuiltIns()) if (weapon.name.StartsWith(row.Key,StringComparison.OrdinalIgnoreCase)) { caliber = row.Caliber; break; }
            if (caliber == null || !Boxes.TryGetValue(caliber,out box)) return;
            var prefab = PrefabCatalog.Find(box);
            if (prefab == null || !ItemSpawner.Available()) { Log.Warn(LootModule.Instance,"NPC ammo drop unavailable: " + box); return; }
            Dropped.Add(action.Owner.GetInstanceID());
            // Remove ammo from this NPC's random pool so its guaranteed 1-2 matching boxes are the entire ammo drop.
            var table = action.Fsm.Variables.FindFsmArray("ItemArray");
            if (table != null) { var keep = new List<object>(); foreach (var entry in table.Values) { var item = entry as GameObject; if (item == null || !item.name.StartsWith("ammo_",StringComparison.OrdinalIgnoreCase)) keep.Add(entry); } if (keep.Count > 0) { table.Values = keep.ToArray(); table.SaveChanges(); } }
            int count = UnityEngine.Random.value < .25f ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                var go = UnityEngine.Object.Instantiate(prefab,corpse.transform.position + Vector3.up * .5f + Vector3.right * (i == 0 ? -.2f : .2f),Quaternion.identity);
                if (!ItemSpawner.RegisterSaved(go,prefab.name)) { UnityEngine.Object.Destroy(go); Log.Warn(LootModule.Instance,"NPC ammo save registration failed: " + box); continue; }
                FsmUtil.Find(go,"Randomize")?.SendEvent("Randomize");
            }
            Log.Main(LootModule.Instance,"NPC ammo: " + action.Owner.name + " -> " + count + " x " + box + " (" + weapon.name + ")");
        }
        private static GameObject SelectedWeapon(GameObject npc)
        {
            var selection = FsmUtil.Find(npc,"WeaponType");
            if (selection != null && selection.Fsm.ActiveState != null) foreach (var action in selection.Fsm.ActiveState.Actions) { var set = action as ActivateGameObjects; if (set != null) for (int i = 0; i < set.gameObjects.Length && i < set.enable.Length; i++) if (set.enable[i].Value && set.gameObjects[i].Value != null) return set.gameObjects[i].Value; }
            return HeldWeaponModels.WeaponOf(npc)?.gameObject;
        }
    }
}
