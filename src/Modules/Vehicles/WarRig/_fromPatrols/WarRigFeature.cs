using System;
using System.Collections;
using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    // War Rig 1.1.3 as an internal feature of the Patrols module (ex WarRig.Plugin). Installed by PatrolsModule.Init, removed by
    // Shutdown (Core drops the hooks and module patches). Data: BepInEx\plugins\WildWasteland\Patrols\WarRig\ (WarRig.glb, WarRig_colliders.glb).
    internal static class WarRigFeature
    {
        internal const string Version = "1.1.3";
        internal static string Dir = ".";
        private static Core.IModule _owner;

        internal static void Install(Core.IModule owner, string dir)
        {
            _owner = owner; Dir = dir;
            // ES3: resolve the War Rig prefab id, also before the spawner was ever used this session (shared dispatcher, keyed by id)
            Core.Hooks.GetPrefab.Resolver(owner, PrefabLookup.Resolve);
            NativeStormProtection.Install(owner);
            SpawnerIntegration.Install(owner);
            DustStormBridge.Install(owner);
            // Apocapatrol templates use the rig as a trailer body ("body": "WarRig"): direct registration instead of the reflection scan
            ModVehicles.Register(Factory.Name, Factory.Template);
            PoiEncounter.InstallRetention(owner);
            Core.Services.PoiChanged += PoiEncounter.Register;
            PoiEncounter.Register(Core.Services.Poi);
            Core.Log.Info("[Patrols] War Rig " + Version + " installed; world discovery supplied by the POI module.");
        }

        // Module shutdown removes POI registration and restores storm dust. Saved rigs stay (inert).
        internal static void Uninstall()
        {
            Core.Services.PoiChanged -= PoiEncounter.Register;
            if (Core.Services.Poi != null) Core.Services.Poi.Remove("WarRig");
            PoiEncounter.Reset();
            try { NativeStormProtection.Restore(); } catch (Exception e) { Core.Log.Warn("[Patrols] War Rig storm restore: " + e.Message); }
            WorldSpawning.ResetForScene();
            _owner = null;
        }

        internal static void OnSceneLoaded() { WorldSpawning.ResetForScene(); NativeStormProtection.ResetForScene(); PoiEncounter.ResetForScene(); }

        internal static void Say(string text) { Core.Log.Always("[Patrols] " + text); }

        public static GameObject SpawnNearPlayer(bool equip)
        {
            if (!S.SpawnEnabled.Value) throw new InvalidOperationException("War Rig spawning is disabled.");
            var player = Core.GameRefs.Player;
            if (player == null) throw new InvalidOperationException("Load a game first.");
            if (player.GetComponentsInParent<NWH.VehiclePhysics2.VehicleController>().Length != 0)
                throw new InvalidOperationException("Leave your vehicle before spawning a War Rig.");
            var registry = Core.GameRefs.NewGOArrayList;
            var list = registry == null ? null : registry.GetComponents<PlayMakerArrayListProxy>()
                .FirstOrDefault(p => string.Equals(p.referenceName, "ArrayList_Cars", StringComparison.OrdinalIgnoreCase));
            var counter = FindFsm(Core.GameRefs.ItemNameId, "itemNameID");
            var id = counter == null ? null : counter.FsmVariables.GetFsmInt("intName");
            if (list == null || id == null) throw new InvalidOperationException("The game's vehicle registry is not ready yet.");
            var dir = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            if (dir.sqrMagnitude < .01f) dir = Vector3.forward;
            var pos = player.transform.position + dir * S.Distance;
            RaycastHit ground;
            if (Physics.Raycast(pos + Vector3.up * 15f, Vector3.down, out ground, 40f, (1 << 0) | (1 << 14), QueryTriggerInteraction.Ignore)) pos.y = ground.point.y;
            pos += Vector3.up * 1.5f;
            var rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir));
            if (!WorldSpawning.CanSpawnAt(ref pos, rotation))
                throw new InvalidOperationException("War Rig needs open ground outside, with space for the entire trailer. Move to an open area.");
            var rig = Factory.Create(pos, rotation, equip, () => ++id.Value);
            list.arrayList.Add(rig); Say("War Rig spawned."); return rig;
        }
        internal static PlayMakerFSM FindFsm(GameObject go, string name)
        { return go == null ? null : go.GetComponents<PlayMakerFSM>().FirstOrDefault(f => string.Equals(f.FsmName, name, StringComparison.OrdinalIgnoreCase)); }
    }

    // Saves: resolve the War Rig prefab id (Core.Hooks.GetPrefab resolver; null = not ours / let ES3 continue)
    internal static class PrefabLookup
    {
        internal static ES3Internal.ES3Prefab Resolve(long id)
        {
            if (id != Factory.PrefabId) return null;
            try { return Factory.Template().GetComponent<ES3Internal.ES3Prefab>(); }
            catch (Exception e) { Core.Log.Error("[Patrols] " + "War Rig template for a saved rig: " + e.Message); return null; }
        }
    }
}
