using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.Patrols.WarRig
{
    /// <summary>
    /// War Rig 1.1.3 as a feature of the Vehicles module (ex WarRig.Plugin, then a Patrols feature): ES3 prefab resolver, template body
    /// registration for vehicle templates ("body": "WarRig"), spawning. Started by VehiclesModule.Init, stopped by its Shutdown (Core drops
    /// the hooks and module patches). Model files: plugins/WildWasteland/Vehicles/WarRig/ (WarRig.glb, WarRig_colliders.glb).
    /// The storm protection (Survival), the ApocaDustStorm/Apocaspawner bridges (Compatibility) and the POI encounter (POI) are installed by
    /// their own modules; the rig's shelter reaches them through IShelterRegistry (WarRigShelter).
    /// </summary>
    internal static class WarRigFeature
    {
        internal const string Version = "1.1.3";
        /// <summary>Folder of the War Rig model files.</summary>
        internal static string Dir = ".";
        private static IModule _owner;

        /// <summary>
        /// Old Patrols entry point (PatrolsModule.InitInner). The War Rig itself is started by the Vehicles module (<see cref="Start"/>); this
        /// transitional forwarder keeps installing, under the Patrols owner, the parts other modules own until they install them themselves
        /// (REFACTOR-REQUEST B12-4..B12-6): Survival storm protection, Compatibility Apocaspawner/ApocaDustStorm bridges, POI encounter.
        /// </summary>
        [Obsolete("refactor: the Vehicles module starts the War Rig (WarRigFeature.Start); owners install their own War Rig parts")]
        internal static void Install(IModule owner, string dir)
        {
            SpawnerIntegration.Install(owner);                    // refs-ok: REFACTOR-REQUEST B12-5 (Compatibility Apocaspawner bridge)
            DustStormBridge.Install(owner);                       // refs-ok: REFACTOR-REQUEST B12-5 (Compatibility ApocaDustStorm bridge)
            PoiEncounter.InstallRetention(owner);                 // refs-ok: REFACTOR-REQUEST B12-6 (POI War Rig encounter)
            Services.PoiChanged -= PoiEncounter.Register;         // refs-ok: REFACTOR-REQUEST B12-6
            Services.PoiChanged += PoiEncounter.Register;         // refs-ok: REFACTOR-REQUEST B12-6
            PoiEncounter.Register(Services.Poi);                  // refs-ok: REFACTOR-REQUEST B12-6
        }

        /// <summary>Old Patrols shutdown entry point: undoes the transitional foreign parts of <see cref="Install"/> (the Vehicles module stops the War Rig itself).</summary>
        [Obsolete("refactor: the Vehicles module stops the War Rig (WarRigFeature.Stop); owners remove their own War Rig parts")]
        internal static void Uninstall()
        {
            Services.PoiChanged -= PoiEncounter.Register;         // refs-ok: REFACTOR-REQUEST B12-6
            if (Services.Poi != null) Services.Poi.Remove("WarRig");   // refs-ok: REFACTOR-REQUEST B12-6
            PoiEncounter.Reset();                                 // refs-ok: REFACTOR-REQUEST B12-6
        }

        /// <summary>Old Patrols scene entry point: per-scene reset of the transitional foreign parts (the Vehicles module resets the War Rig itself).</summary>
        [Obsolete("refactor: the Vehicles module resets the War Rig per scene (WarRigFeature.ResetForScene); owners reset their own parts")]
        internal static void OnSceneLoaded() { PoiEncounter.ResetForScene(); }   // refs-ok: REFACTOR-REQUEST B12-4, B12-6

        /// <summary>Installs the War Rig prefab-id resolver and registers the rig as a template body.</summary>
        internal static void Start(IModule owner, string dir)
        {
            _owner = owner; Dir = dir;
            // ES3: resolve the War Rig prefab id, also before the spawner was ever used this session (shared dispatcher, keyed by id)
            Hooks.GetPrefab.Resolver(owner, PrefabLookup.Resolve);
            // vehicle templates use the rig as a trailer body ("body": "WarRig"): direct registration instead of the reflection scan
            ModVehicles.Register(Factory.Name, Factory.Template);
            if (Log.L1) Log.Main(owner, "War Rig " + Version + " installed; world discovery supplied by the POI module.");
        }

        /// <summary>Module shutdown: removes the template-body registration. Saved rigs stay (inert).</summary>
        internal static void Stop()
        {
            ModVehicles.Unregister(Factory.Name);
            WorldSpawning.ResetForScene();
            _owner = null;
        }

        /// <summary>Scene change: placement clearance cache and natural-rig notices are dropped.</summary>
        internal static void ResetForScene() { WorldSpawning.ResetForScene(); }

        /// <summary>Old log helper (always logged); Vehicles logs at level 1.</summary>
        [Obsolete("refactor: log through Log.Main(VehiclesModule.Instance, ...)")]
        internal static void Say(string text) { Log.Main(VehiclesModule.Instance, text); }

        /// <summary>Spawns a War Rig on open ground 14 m in front of the player (F3 tool); throws with a player-readable reason when it cannot.</summary>
        public static GameObject SpawnNearPlayer(bool equip)
        {
            if (!S.SpawnEnabled.Value) throw new InvalidOperationException("War Rig spawning is disabled.");
            var player = GameRefs.Player;
            if (player == null) throw new InvalidOperationException("Load a game first.");
            if (player.GetComponentsInParent<NWH.VehiclePhysics2.VehicleController>().Length != 0)
                throw new InvalidOperationException("Leave your vehicle before spawning a War Rig.");
            if (!ItemSpawner.Available(ItemSpawner.SaveList.Cars)) throw new InvalidOperationException("The game's vehicle registry is not ready yet.");
            var dir = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            if (dir.sqrMagnitude < .01f) dir = Vector3.forward;
            var pos = player.transform.position + dir * S.Distance;
            RaycastHit ground;
            if (Physics.Raycast(pos + Vector3.up * 15f, Vector3.down, out ground, 40f, (1 << 0) | (1 << 14), QueryTriggerInteraction.Ignore)) pos.y = ground.point.y;
            pos += Vector3.up * 1.5f;
            var rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir));
            if (!WorldSpawning.CanSpawnAt(ref pos, rotation))
                throw new InvalidOperationException("War Rig needs open ground outside, with space for the entire trailer. Move to an open area.");
            var rig = SpawnAt(pos, rotation, equip);
            if (rig == null) throw new InvalidOperationException("The game's vehicle registry is not ready yet.");
            VehiclesModule.FactoryApi.Record(rig, Factory.Name);
            VehicleFactory.Spawned(rig, Factory.Name, Factory.Name, new VehicleBuildOptions { Saved = true });
            Log.Main(VehiclesModule.Instance, "War Rig spawned.");
            return rig;
        }

        /// <summary>Creates a War Rig at a scene pose, numbered from the game's item counter and added to its saved car list; null when the registry is missing.</summary>
        internal static GameObject SpawnAt(Vector3 position, Quaternion rotation, bool equip)
        {
            var list = ItemSpawner.List(ItemSpawner.SaveList.Cars);
            if (list == null || ItemSpawner.Counter() == null) return null;
            var rig = Factory.Create(position, rotation, equip, NextId);
            list.arrayList.Add(rig);
            return rig;
        }

        /// <summary>Next native item number (counter incremented first, as the old spawner did).</summary>
        private static int NextId() { return ItemSpawner.NextId(); }

        /// <summary>First PlayMaker FSM of that name on the object (case-insensitive), or null.</summary>
        internal static PlayMakerFSM FindFsm(GameObject go, string name)
        {
            if (go == null) return null;
            foreach (var f in go.GetComponents<PlayMakerFSM>()) if (string.Equals(f.FsmName, name, StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }
    }
}
