using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>
    /// A blast lance thrown from the driver's seat starts inside the player's own car (the game spawns it at the first-person throw point
    /// and pushes it forward) and blew up on the car's own body / doors / windows. Postfix on PlayMaker's CreateObject.OnEnter (Core
    /// dispatcher): when a blast lance's [Attack] FSM creates its projectile while the player sits in a car, the projectile's colliders
    /// ignore every collider of that car (and the player's), so it flies out and only hits the world. Config [Gunplay / Projectiles]
    /// LanceIgnoresOwnCar (ex [PlayerBody / Vehicles]).
    /// </summary>
    internal static class LanceSeatCollision
    {
        static readonly List<Collider> _mine = new List<Collider>(8), _other = new List<Collider>(256);

        /// <summary>
        /// Core.Hooks.CreateObjectEnter postfix (every CreateObject of every FSM). First filter: setting, in-car state and the
        /// "Attack" FSM name; then the blastlance owner name prefix.
        /// </summary>
        public static void AfterCreateObject(HutongGames.PlayMaker.Actions.CreateObject __instance)
        {
            try
            {
                if (!GunplaySettings.LanceIgnoresOwnCar.Value || PlayerState.InCarFsm == null || !PlayerState.InCar || __instance == null || __instance.Fsm == null || __instance.Fsm.Name != "Attack") return;
                string owner = __instance.Fsm.GameObjectName ?? "";
                if (!owner.StartsWith("blastlance", StringComparison.OrdinalIgnoreCase)) return;
                var go = __instance.storeObject != null ? __instance.storeObject.Value : null;
                var car = PlayerState.CarRoot;
                if (go == null || car == null) return;
                go.GetComponentsInChildren(true, _mine);
                int n = 0;
                car.GetComponentsInChildren(true, _other);
                foreach (var c in _other)
                    foreach (var p in _mine) if (c != null && p != null) { Physics.IgnoreCollision(p, c, true); n++; }
                var player = PlayerState.Player;
                if (player != null)
                {
                    player.GetComponentsInChildren(true, _other);
                    foreach (var c in _other)
                        foreach (var p in _mine) if (c != null && p != null) Physics.IgnoreCollision(p, c, true);
                }
                _mine.Clear(); _other.Clear();
                if (Log.L2) GunplayLog.Deep("Lance: " + go.name + " from " + owner + " ignores " + car.name + " (" + n + " collider pairs)");
            }
            catch (Exception e) { _mine.Clear(); _other.Clear(); if (Log.L2) GunplayLog.Deep("Lance: " + e.Message); }
        }
    }
}
