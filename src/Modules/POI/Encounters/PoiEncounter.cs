using WildWasteland.Core;
using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.POI
{
    internal static class PoiEncounter
    {
        private static string _template;
        private static GameObject _zombie, _deadLugnut;
        /// <summary>Registers validated data under its owning module.</summary>
        internal static void Register(Core.IPoiApi api)
        {
            if (api == null) return;
            api.Register(new Core.PoiDefinition { Id = "WarRig", Radius = 65f, Enabled = Enabled, Spawn = Spawn, Mark = Mark });
        }
        /// <summary>Reports whether the required live providers and feature switches allow this operation.</summary>
        private static bool Enabled() { return ModApi.Get<IWarRigWorld>()?.Running==true && ModApi.Get<IAuthoredVehicleRequests>()!=null; }
        /// <summary>Remembers the live encounter location for map presentation.</summary>
        private static void Mark(GameObject marker) { EncounterNotices.Rigs.Add(marker); }
        /// <summary>Drops per-world observations and invalidates cached query revisions.</summary>
        internal static void Reset() { _template = null; _zombie = _deadLugnut = null; }
        /// <summary>Drops scene objects while retaining reusable authored definitions.</summary>
        internal static void ResetForScene() { _zombie = _deadLugnut = null; }
        /// <summary>Captures main-thread inputs before worker generation or staged preparation.</summary>
        internal static void Prepare()
        {
           
            _zombie = PrefabCatalog.Exact("Zombie_Runner");
            _deadLugnut = PrefabCatalog.Exact("Lugnut_Dead");
            if (_zombie == null) Core.Log.Warn(null,"[Patrols] War Rig POI: Zombie_Runner prefab is missing.");
            if (_deadLugnut == null) Core.Log.Warn(null,"[Patrols] War Rig POI: Lugnut_Dead prefab is missing.");
            if (_template == null) using(var stream=DiskResources.Open("POI.Encounters.AbandonedWarRig.json")) using(var reader=new StreamReader(stream)) _template=reader.ReadToEnd();
            ModApi.Get<IAuthoredVehicleRequests>()?.Prepare(_template);
        }
        /// <summary>Requests a saved encounter through the authored vehicle provider.</summary>
        private static void Spawn(Vector3 position, Quaternion rotation, Action<GameObject> onDone)
        {
            if (!Enabled() || _template == null || _zombie == null || _deadLugnut == null)
            { onDone(null); return; }
            string ammo=UnityEngine.Random.value<.5f?"ammo_box_9mm":"ammo_box_762mm";
            var request=new AuthoredVehicleSpawn{Definition=_template,SetupOnly=true,Loot=new Dictionary<string,int>(),TrailerLoot=new Dictionary<string,int>{{"bandage_1",UnityEngine.Random.Range(1,3)},{"tin_can_nuts",UnityEngine.Random.Range(1,3)},{ammo,UnityEngine.Random.Range(1,3)}}};
            ModApi.Get<IAuthoredVehicleRequests>().Spawn(request,position+Vector3.up*1.5f,rotation,new Completion{Done=onDone}.Finished);
        }
        private sealed class Completion
        {
            internal Action<GameObject> Done;
            /// <summary>Completes the authored encounter with worn wheels and saved native actors.</summary>
            internal void Finished(GameObject truck)
            {
                if (truck == null) { Done(null); return; }
                var rig = ModApi.Get<IVehicleAttachments>()?.Attached(truck);
                if (rig == null) { Core.Log.Warn(null,"[Patrols] War Rig POI: truck was built without its tanker."); Done(null); return; }
                Wheels(truck, new[] { "hinge_wheel_FL", "hinge_wheel_FR", "hinge_wheel_RL", "hinge_wheel_RR" });
                Wheels(rig, new[] {"hinge_wheel_L","hinge_wheel_R","hinge_wheel_L2","hinge_wheel_R2","hinge_wheel_L3","hinge_wheel_R3"});
                // The builder can span an origin jump; derive positions from the live truck.
                var center = truck.transform.position; var rotation = truck.transform.rotation;
                float phase = UnityEngine.Random.Range(0f, 360f);
                for (int i = 0; i < 5; i++)
                {
                    var prefab = i < 3 ? _zombie : _deadLugnut; bool placed = false;
                    for (int attempt = 0; attempt < 16 && !placed; attempt++)
                    {
                        float angle = (phase + i * 120f + UnityEngine.Random.Range(-40f, 40f)) * Mathf.Deg2Rad;
                        var offset = i < 3 ? new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * UnityEngine.Random.Range(12f, 24f)
                            : new Vector3(i == 3 ? -4f : 4f, 0f, UnityEngine.Random.Range(-2f, 3f));
                        var point = center + rotation * offset; RaycastHit ground;
                        if (!Physics.Raycast(point + Vector3.up * 30f, Vector3.down, out ground, 60f, Physics.AllLayers, QueryTriggerInteraction.Ignore) || !(ground.collider is TerrainCollider)) continue;
                        var actor = UnityEngine.Object.Instantiate(prefab, ground.point + Vector3.up * .5f, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                        if(!ItemSpawner.RegisterSaved(actor,prefab.name)){UnityEngine.Object.Destroy(actor);throw new InvalidOperationException("Native save registry unavailable for encounter actor");} actor.SetActive(true); placed = true;
                    }
                    if (!placed) Core.Log.Warn(null,"[Patrols] War Rig POI: no clear ground for " + prefab.name);
                }
                EncounterNotices.NoticeUntilValue = Time.unscaledTime + 10f;
                Core.Log.Main(null, "[Patrols] WAR RIG SPAWNED: abandoned Rustchief + tanker, worn wheels, no engine, 3 zombies, 2 dead Lugnuts, tanker supplies.");
                Done(truck);
            }
        }
        /// <summary>Applies the encounter wheel-condition rule at the actual fitted hinges.</summary>
        private static void Wheels(GameObject vehicle, string[] hinges)
        {
            foreach (string name in hinges)
            {
                var hinge = vehicle.transform.Find(name); if (hinge == null) continue;
                foreach (Transform part in hinge)
                    foreach (var fsm in part.GetComponents<PlayMakerFSM>())
                        if (fsm.FsmName == "Condition" || fsm.FsmName == "Repair")
                        { var condition = fsm.FsmVariables.GetFsmFloat("Condition"); if (condition != null) condition.Value = 5f; }
            }
        }
    }
}
