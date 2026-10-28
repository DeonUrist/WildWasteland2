using System;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>Applies one title-menu choice through native starter spawning and weapon-slot actions.</summary>
    internal sealed class NewGameSetup : IDisposable
    {
        internal static readonly string[] Vehicles = { "RANDOM", "POLOSKA", "JUNKER", "PIPERAT", "CROSSBREED", "TINYTYRANT", "HALFBREED", "MOTORCYCLE" };
        internal static readonly string[] Loadouts = { "TARNISHED", "GUNSLINGER", "ADVENTURER", "AGENT", "HUNTER", "FARMER", "GUERILLA" };
        private static readonly string[] LoadoutWeapons = { "", "22_pipe_pistol", "machete", "folk_17", "crossbow", "rochester_m24", "akm_trash" };
        private static readonly string[] LoadoutAmmo = { "", "ammo_22caliber", "", "ammo_9mm", "ammo_arrow", "ammo_12gauge", "ammo_762" };
        private static readonly int[] LoadoutRounds = { 0, 30, 0, 30, 12, 12, 30 };
        private static readonly string[] Prefabs = { "", "Poloska", "Junker", "PipeRat", "Crossbreed", "TinyTyrant", "Halfbreed", "Motorcycle" };
        private int _vehicle = -1, _loadout = -1;
        private CreateObject _carAction;
        private string _carName;
        private bool _motorcycle;

        internal NewGameSetup(IModule owner)
        {
            Hooks.CreateObjectEnter.Prefix(owner, BeforeStarterCar);
            Hooks.CreateObjectEnter.Postfix(owner, AfterStarterCar);
            SaveBus.StateChanged += StateChanged;
        }

        /// <summary>Pending choices belong only to the next explicit new-game launch.</summary>
        internal void Arm(int vehicle, int loadout)
        {
            Clear(); _vehicle = vehicle; _loadout = loadout;
            HudModule.Log("New game setup: vehicle=" + Vehicles[vehicle] + ", loadout=" + Loadouts[loadout]);
        }
        internal void Clear() { _vehicle = _loadout = -1; _carAction = null; _carName = null; _motorcycle = false; }
        public void Dispose() { SaveBus.StateChanged -= StateChanged; Clear(); }

        /// <summary>Keeps the native naming, fuel, saved-list and matching engine/spares flow.</summary>
        private bool BeforeStarterCar(CreateObject action)
        {
            if (_vehicle < 0 || action.Fsm.Name != "ItemSpawner" || action.State.Name != "spawn" || action.Fsm.GameObject.name != "CarSpawn_StarterBase") return true;
            var parent = action.Fsm.GameObject.transform.parent;
            if (parent == null || parent.name != "Starter_Area") return true;
            int choice = _vehicle; _vehicle = -1;
            var index = action.Fsm.Variables.FindFsmInt("index");
            GameObject prefab = null;
            if (choice > 0)
            {
                if (Prefabs[choice] == "Motorcycle")
                {
                    var api = ModApi.Get<IVehicleAuthoring>();
                    prefab = api != null ? api.Template("Motorcycle") : null;
                    if (index != null) index.Value = 3; // Native Crossbreed engine, motorcycle wheels and radiator.
                }
                else foreach (var list in action.Fsm.GameObject.GetComponents<PlayMakerArrayListProxy>())
                    if (list.referenceName == "CarSpawn")
                        for (int i = 0; i < list.arrayList.Count; i++)
                        {
                            var candidate = list.arrayList[i] as GameObject;
                            if (candidate == null || candidate.name != Prefabs[choice]) continue;
                            prefab = candidate; if (index != null) index.Value = i; break;
                        }
                if (prefab == null || index == null) { HudModule.Warn("New game setup: selected vehicle unavailable: " + Prefabs[choice]); return true; }
                action.gameObject.Value = prefab;
            }
            _carAction = action; _carName = choice == 0 ? "RANDOM" : Prefabs[choice]; _motorcycle = choice == 7;
            return true;
        }

        /// <summary>Factory chassis templates are inactive; native creation still owns this instance.</summary>
        private void AfterStarterCar(CreateObject action)
        {
            if (!ReferenceEquals(action, _carAction)) return;
            _carAction = null;
            var car = action.storeObject.Value;
            if (car == null) { HudModule.Warn("New game setup: starter vehicle creation failed"); return; }
            if (_motorcycle) car.SetActive(true);
            HudModule.Log("New game starter vehicle: " + _carName + " (" + car.name + ")");
            _carName = null; _motorcycle = false;
        }

        /// <summary>Existing-save launches cancel choices; the first playable state grants gear once.</summary>
        private void StateChanged(string previous, string current)
        {
            if (current == "LoadGame" || current == "generateTerrain 2") { Clear(); return; }
            if (current != "isPlay" || _loadout < 0) return;
            int choice = _loadout; _loadout = -1;
            if (choice == 0) { HudModule.Log("New game loadout: TARNISHED (nothing added)"); return; }
            GiveLoadout(choice);
        }

        /// <summary>Equips the physical weapon with the native UseWeapon recipe and the native ammo pool.</summary>
        private static void GiveLoadout(int choice)
        {
            string name = LoadoutWeapons[choice], ammoName = LoadoutAmmo[choice];
            int rounds = LoadoutRounds[choice];
            var camera = PlayerState.PlayerCamera;
            var weapons = camera != null ? camera.Find("Weapons") ?? camera.Find("HandItemUse/Weapons") : null;
            var slot = weapons != null ? weapons.Find("Slot 1") : null;
            var take = slot != null ? FsmUtil.Find(slot.gameObject, "UseWeapon") : null;
            var state = slot != null ? FsmUtil.Find(slot.gameObject, "SlotEmptyFull") : null;
            var ammo = GameRefs.GameManager != null ? FsmUtil.Find(GameRefs.GameManager, "Ammo") : null;
            var count = ammo != null && rounds > 0 ? ammo.FsmVariables.FindFsmInt(ammoName) : null;
            var prefab = PrefabCatalog.Item(name);
            if (take == null || state == null || (rounds > 0 && count == null) || prefab == null || !ItemSpawner.Available() || PlayerInventory.FirstItem(slot) != null)
            { HudModule.Warn("New game setup: cannot grant " + Loadouts[choice] + "; native inventory unavailable"); return; }
            var target = take.FsmVariables.FindFsmGameObject("Item");
            if (target == null || !take.Fsm.Initialized || !state.Fsm.Initialized)
            { HudModule.Warn("New game setup: native weapon slot is not initialized"); return; }
            if (choice == 2 && !GiveBackpack(camera)) return;
            var item = UnityEngine.Object.Instantiate(prefab, slot.position, Quaternion.identity);
            if (!ItemSpawner.RegisterSaved(item, name)) { UnityEngine.Object.Destroy(item); return; }
            target.Value = item; take.Fsm.SetState("takeWeapon"); take.Fsm.SetState("off");
            state.Fsm.SetState("full"); if (count != null) count.Value += rounds;
            HudModule.Log("New game loadout: " + Loadouts[choice] + ", " + name + " in Slot 1" + (rounds > 0 ? ", " + ammoName + " +" + rounds : ""));
        }

        /// <summary>Equips Adventurer's backpack through native TakeItem and capacity handling.</summary>
        private static bool GiveBackpack(Transform camera)
        {
            var slot = camera != null ? camera.Find("QuickItems/Backpack_Item") : null;
            var take = slot != null ? FsmUtil.Find(slot.gameObject, "TakeItem") : null;
            var state = slot != null ? FsmUtil.Find(slot.gameObject, "SlotEmptyFull") : null;
            var prefab = PrefabCatalog.Item("backpack_small");
            var target = take != null ? take.FsmVariables.FindFsmGameObject("Item") : null;
            if (target == null || state == null || prefab == null || !take.Fsm.Initialized || !state.Fsm.Initialized || PlayerInventory.FirstItem(slot) != null)
            { HudModule.Warn("New game setup: native backpack slot unavailable for ADVENTURER"); return false; }
            var item = UnityEngine.Object.Instantiate(prefab, slot.position, Quaternion.identity);
            if (!ItemSpawner.RegisterSaved(item, "backpack_small")) { UnityEngine.Object.Destroy(item); return false; }
            target.Value = item; take.Fsm.SetState("takeWeapon"); take.Fsm.SetState("off");
            state.Fsm.SetState("full");
            HudModule.Log("New game loadout: ADVENTURER, backpack_small equipped through native TakeItem");
            return true;
        }
    }
}
