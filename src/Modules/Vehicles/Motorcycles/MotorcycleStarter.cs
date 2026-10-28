using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using WildWasteland.Core;

namespace WildWasteland.Modules.Vehicles
{
    internal static class MotorcycleStarter
    {
        internal static void Hook(IModule owner)
        {
            Hooks.CreateObjectEnter.Prefix(owner, BeforeItem);
            Hooks.EnterState.Prefix(owner, BeforeState);
        }

        private static bool IsStarter(Fsm fsm, string name)
        {
            if (fsm.Name != "ItemSpawner" || fsm.GameObject.name != name) return false;
            var area = fsm.GameObject.transform.parent;
            if (area == null || area.name != "Starter_Area") return false;
            var spawn = area.Find("CarSpawn_StarterBase");
            var car = spawn != null ? FsmUtil.Find(spawn.gameObject, "ItemSpawner") : null;
            var item = car != null ? car.FsmVariables.FindFsmGameObject("Item") : null;
            return item != null && item.Value != null && item.Value.name.StartsWith("Motorcycle(Clone)", System.StringComparison.Ordinal);
        }

        private static bool BeforeItem(CreateObject action)
        {
            if (action.State.Name != "spawn" || !IsStarter(action.Fsm, "ItemSpawn_StarterBase")) return true;
            var source = action.gameObject.Value;
            if (source == null || source.name != "poloska_seat_front_homemade") return true;
            action.gameObject.Value = Motorcycles.Factory.Source("seat_motorcycle");
            Log.Main(VehiclesModule.Instance, "Motorcycle starter supplies: seat_motorcycle replaces the homemade Poloska seat.");
            return true;
        }

        private static bool BeforeState(Fsm fsm, FsmState state)
        {
            if (state.Name != "spawned" || fsm.PreviousActiveState == null || fsm.PreviousActiveState.Name != "starterAdd 6"
                || !IsStarter(fsm, "EngineSpawn_StarterBase")) return true;
            if (fsm.GetState("motorcycle radiator") == null) return true;
            Log.Main(VehiclesModule.Instance, "Motorcycle starter supplies: spawning radiator_motorcycle through the native radiator state.");
            fsm.SetState("motorcycle radiator");
            return false;
        }
    }
}
