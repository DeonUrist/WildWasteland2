using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    internal static class PoiEncounter
    {
        private static CarTemplate _template;
        private static GameObject _zombie, _deadLugnut;
        private static readonly HashSet<GetDistance> CleanupChecks = new HashSet<GetDistance>();
        internal static void InstallRetention(Core.IModule owner)
        { Core.Patches.Patch(owner, AccessTools.Method(typeof(GetDistance), "OnEnter"), prefix: AccessTools.Method(typeof(PoiEncounter), nameof(BeforeCleanupDistance))); }
        internal static bool Protected(GameObject go)
        {
            if (go == null) return false;
            if (go.GetComponentInParent<WarRigUnit>() != null) return true;
            var marker = go.GetComponentInParent<PatrolMarker>();
            return marker != null && marker.TemplateName == "Abandoned War Rig" && marker.SetupOnly;
        }
        private static bool BeforeCleanupDistance(GetDistance __instance)
        {
            if (!PatrolsModule.Running || !CleanupChecks.Contains(__instance)) return true;
            var target = __instance.target != null ? __instance.target.Value : null;
            if (Protected(target) || WorldTraffic.RetainsTown(target))
            { __instance.Fsm.Event("FINISHED"); __instance.Finish(); return false; }
            var player = Core.GameRefs.Player;
            if (target != null && player != null && (target.transform.position - player.transform.position).sqrMagnitude > 5000f * 5000f)
                DetachTankers(target);
            return true;
        }
        internal static void DetachTankers(GameObject truck)
        {
            var body = truck.GetComponent<Rigidbody>(); if (body == null) return;
            foreach (var rig in WarRigUnit.All)
                if (rig != null) foreach (var joint in rig.GetComponents<ConfigurableJoint>())
                    if (joint.connectedBody == body)
                    {
                        UnityEngine.Object.Destroy(joint);
                        var leg = rig.transform.Find("leg"); if (leg != null) leg.gameObject.SetActive(true);
                    }
        }
        internal static void Register(Core.IPoiApi api)
        {
            if (api == null) return;
            api.Register(new Core.PoiDefinition { Id = "WarRig", Radius = 65f, Enabled = Enabled, Spawn = Spawn, Mark = Mark });
        }
        private static bool Enabled() { return PatrolsModule.Running && S.SpawnEnabled.Value; }
        private static void Mark(GameObject marker) { WorldSpawning.NaturalRigs.Add(marker); }
        internal static void Reset() { _template = null; _zombie = _deadLugnut = null; CleanupChecks.Clear(); }
        internal static void ResetForScene() { CleanupChecks.Clear(); _zombie = _deadLugnut = null; }
        internal static void Prepare()
        {
            CleanupChecks.Clear();
            var cleanup = Core.FsmUtil.Find(Core.GameRefs.GameManager, "DeleteFarAwayItems");
            if (cleanup != null) foreach (var state in cleanup.FsmStates) foreach (var action in state.Actions)
                if (action is GetDistance) CleanupChecks.Add((GetDistance)action);
            _zombie = Prefabs.Exact("Zombie_Runner");
            _deadLugnut = Prefabs.Exact("Lugnut_Dead");
            if (_zombie == null) Core.Log.Warn("[Patrols] War Rig POI: Zombie_Runner prefab is missing.");
            if (_deadLugnut == null) Core.Log.Warn("[Patrols] War Rig POI: Lugnut_Dead prefab is missing.");
            if (_template != null) { VehicleBuildCache.Warm(_template); return; }
            using (var stream = WildWasteland.Core.DiskResources.Open("Patrols.DefaultTemplates.Truck_War_Rig.json"))
            using (var reader = new StreamReader(stream))
            {
                var file = TemplateFile.FromJson(reader.ReadToEnd());
                file.name = "Abandoned War Rig"; file.kind = "truck"; file.driver = file.passenger = "";
                file.fillFuel = false; file.releaseHandbrake = false; file.explodeChance = 0f;
                for (int i = 0; i < 4; i++) { file.SetTurret(i, ""); file.trailer.SetTurret(i, ""); }
                var parts = new List<TemplatePart>(); var indices = new Dictionary<int, int>();
                for (int i = 0; i < file.parts.Length; i++)
                {
                    var part = file.parts[i];
                    if (part.hinge == "hinge_engine" || (part.parent >= 0 && !indices.ContainsKey(part.parent))) continue;
                    int original = part.parent; part.parent = original < 0 ? -1 : indices[original];
                    indices[i] = parts.Count; parts.Add(part);
                }
                file.parts = parts.ToArray();
                _template = CarTemplates.Parse(file.ToJson(), file.name, "POI", "embedded War Rig POI");
                if (_template == null) throw new InvalidOperationException("War Rig POI template could not be prepared");
                _template.SetupOnly = true; _template.SpawnLoot = new Dictionary<string, int>();
                VehicleBuildCache.Warm(_template);
            }
        }
        private static void Spawn(Vector3 position, Quaternion rotation, Action<GameObject> onDone)
        {
            if (!Enabled() || _template == null || _zombie == null || _deadLugnut == null || PatrolsModule.Instance == null)
            { onDone(null); return; }
            var template = _template.CloneForSpawn();
            string ammo = UnityEngine.Random.value < .5f ? "ammo_box_9mm" : "ammo_box_762mm";
            template.Trailer.SpawnLoot = new Dictionary<string, int> { { "bandage_1", UnityEngine.Random.Range(1, 3) },
                { "tin_can_nuts", UnityEngine.Random.Range(1, 3) }, { ammo, UnityEngine.Random.Range(1, 3) } };
            PatrolsModule.Instance.SpawnPoi(template, position + Vector3.up * 1.5f, rotation,
                new Completion { Done = onDone }.Finished);
        }
        private sealed class Completion
        {
            internal Action<GameObject> Done;
            internal void Finished(GameObject truck)
            {
                if (truck == null) { Done(null); return; }
                var rig = TrailerSupport.Attached(truck);
                if (rig == null) { Core.Log.Warn("[Patrols] War Rig POI: truck was built without its tanker."); Done(null); return; }
                Wheels(truck, new[] { "hinge_wheel_FL", "hinge_wheel_FR", "hinge_wheel_RL", "hinge_wheel_RR" });
                Wheels(rig, Factory.Hinges);
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
                        WildWasteland.Modules.Patrols.Register.Name(actor, prefab.name);
                        WildWasteland.Modules.Patrols.Register.Add(actor, false); actor.SetActive(true); placed = true;
                    }
                    if (!placed) Core.Log.Warn("[Patrols] War Rig POI: no clear ground for " + prefab.name);
                }
                WorldSpawning.NaturalNoticeUntil = Time.unscaledTime + 10f;
                Core.Log.Always("[Patrols] WAR RIG SPAWNED: abandoned Rustchief + tanker, worn wheels, no engine, 3 zombies, 2 dead Lugnuts, tanker supplies.");
                Done(truck);
            }
        }
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
