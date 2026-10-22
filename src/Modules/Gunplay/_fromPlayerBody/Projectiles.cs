using System;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    // A blast lance thrown from the driver's seat starts inside her own car (the game spawns it at the first-person throw point and pushes it
    // forward) and blew up on the car's own body / doors / windows. Postfix on PlayMaker's CreateObject.OnEnter (Core hook): when a blast lance's
    // [Attack] FSM creates its projectile while she sits in a car, the projectile's colliders ignore every collider of that car (and hers),
    // so it flies out and only hits the world.
    internal static class Projectiles
    {
        // (WW) a postfix on Core.Hooks.CreateObjectEnter (the shared CreateObject.OnEnter dispatcher), subscribed in PlayerBodyModule.Init
        public static void AfterCreateObject(HutongGames.PlayMaker.Actions.CreateObject __instance)
        {
            try
            {
                if (!PlayerBodyModule.LanceIgnoresOwnCar.Value || Game.InCarFsm == null || !PlayerBodyModule.Running || !Game.InCar || __instance == null || __instance.Fsm == null || __instance.Fsm.Name != "Attack") return;
                string owner = __instance.Fsm.GameObjectName ?? "";
                if (!owner.StartsWith("blastlance", StringComparison.OrdinalIgnoreCase)) return;
                var go = __instance.storeObject != null ? __instance.storeObject.Value : null;
                var car = Game.CarRoot;
                if (go == null || car == null) return;
                var mine = go.GetComponentsInChildren<Collider>(true);
                int n = 0;
                foreach (var c in car.GetComponentsInChildren<Collider>(true))
                    foreach (var p in mine) if (c != null && p != null) { Physics.IgnoreCollision(p, c, true); n++; }
                if (Game.Player != null)
                    foreach (var c in Game.Player.GetComponentsInChildren<Collider>(true))
                        foreach (var p in mine) if (c != null && p != null) Physics.IgnoreCollision(p, c, true);
                PlayerBodyModule.Verbose("Projectiles: " + go.name + " from " + owner + " ignores " + car.name + " (" + n + " collider pairs)");
            }
            catch (Exception e) { PlayerBodyModule.Verbose("Projectiles: " + e.Message); }
        }
    }
}
