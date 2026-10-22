using System;
using System.Text;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Loot;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Towns
{
    /// <summary>
    /// Spawn part of the POI layout markers (Loot, ex Towns/PoiDressMarkers.cs): builds Hide/Exit points and Items/CarParts/Monsters
    /// groups (native ItemSpawner clones restricted to a <see cref="PoiSpawnCatalog"/> list). One-shot group progress lives in the
    /// Loot store <see cref="SpawnerProgress"/> (owner "PoiDressMarkers", ES3 key WildWasteland.Towns.PoiDressMarkers.v1, WWPM1,
    /// identity formula unchanged); Save/Load/Capture/Clear are the POI entry's call sites and forward there.
    /// Still POI/NPCAI-coupled (REFACTOR-REQUEST B15-1): the dressing site/marker types, town hash and allegiance, town floor mask
    /// and the NPCAI interior ownership of Monsters groups. Namespace kept for Wave C.
    /// </summary>
    internal sealed partial class PoiDressMarkers
    {
        /// <summary>Spawn-list catalog used by the marker groups and the POI editor.</summary>
        internal readonly PoiSpawnCatalog Catalog = new PoiSpawnCatalog();

        /// <summary>Copies live marker FSM progress into the Loot store (before unload/save).</summary>
        internal void Capture() { SpawnerProgress.CaptureMarkers(); }

        /// <summary>Stable identity of a marker group: base64 site type, world position of the site root (dm) and marker id.</summary>
        private static string Identity(PoiDressing.Site site, PoiDressProp marker) // refs-ok: REFACTOR-REQUEST B15-1
        {
            var world = Origin.World(site.Root.position);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(site.Type)) + ":" + Mathf.RoundToInt(world.x * 10) + ":" + Mathf.RoundToInt(world.z * 10) + ":" + marker.Id;
        }

        /// <summary>
        /// Builds one marker of a dressed site; false when it is not a marker (the caller places scenery). Hide: five Hide-tagged
        /// floor points; Exit: an empty holder; spawn kinds: the committed (first rolled, seeded by identity) count, a native
        /// ItemSpawner clone restricted to the profile's list with that many floor-snapped spawn points, tracked for progress.
        /// Finished groups build nothing. Throws when the list or the native template is unavailable.
        /// </summary>
        internal bool Build(PoiDressing.Site site, PoiDressProp marker) // refs-ok: REFACTOR-REQUEST B15-1
        {
            if (!marker.Marker) return false;
            var holder = new GameObject("POI " + marker.Kind + " " + marker.Id); holder.SetActive(false); holder.transform.SetParent(site.Dressing.transform, false);
            PoiPropCatalog.Pose(holder.transform, marker); // refs-ok: REFACTOR-REQUEST B15-1
            if (marker.Kind == "Hide")
            {
                // Vanilla and WW brains find Hide-tagged points, not trigger volumes. Five floor-level samples describe the area.
                foreach (var p in new[] { Vector3.zero, new Vector3(-.35f,0,-.35f), new Vector3(.35f,0,-.35f), new Vector3(-.35f,0,.35f), new Vector3(.35f,0,.35f) })
                { var point = new GameObject("Hide"); point.tag = "Hide"; point.transform.SetParent(holder.transform, false); point.transform.localPosition = p; }
                holder.SetActive(true); return true;
            }
            if (marker.Kind == "Exit") { holder.SetActive(true); return true; }
            string key = Identity(site, marker);
            var profile = Catalog.Find(marker.Kind, marker.Profile);
            if (profile == null) { Object.Destroy(holder); throw new InvalidOperationException("Unavailable marker list: " + marker.Profile); }
            var saved = SpawnerProgress.Marker(key);
            var random = new System.Random(TownPlanner.Hash(key)); // refs-ok: REFACTOR-REQUEST B15-1
            int count = random.Next(100) >= marker.Chance ? 0 : random.Next(marker.Minimum, marker.Maximum + 1);
            if (saved == null) saved = SpawnerProgress.AddMarker(key, count, count == 0 ? 1 : 0);
            count = saved.NumberEnd;
            // Every saved group is one-shot. Changing a layout/list never rerolls already committed progress.
            if (saved.OnOff > 0 || saved.Number >= count) { Object.Destroy(holder); return true; }
            var go = Object.Instantiate(profile.Template.gameObject, holder.transform, false);
            go.name = (marker.Kind == "Monsters" ? "EnemySpawn" : "ItemSpawn") + "_dress_" + marker.Id;
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            foreach (var f in go.GetComponentsInChildren<PlayMakerFSM>(true)) if (f.FsmName != "ItemSpawner") Object.DestroyImmediate(f);
            var fsm = FsmUtil.Find(go, "ItemSpawner");
            if (fsm == null) { Object.Destroy(holder); throw new InvalidOperationException("Marker template has no native ItemSpawner"); }
            var points = new GameObject("Marker spawn points"); points.transform.SetParent(holder.transform, false);
            // Native GetGameObjectSibling(+1) points to this sibling, not to unrelated POI children.
            for (int i = 0; i < count; i++)
            {
                var point = new GameObject("Spawn " + i); point.transform.SetParent(points.transform, false);
                point.transform.localPosition = new Vector3((float)random.NextDouble() - .5f, .03f, (float)random.NextDouble() - .5f);
                RaycastHit floor; var position = point.transform.position;
                if (Physics.Raycast(position + Vector3.up * .4f, Vector3.down, out floor, .8f, TownNavigation.Mask, QueryTriggerInteraction.Ignore) && floor.normal.y >= .65f) // refs-ok: REFACTOR-REQUEST B15-1
                    point.transform.position = floor.point + Vector3.up * .06f;
            }
            NativeSpawners.Select(fsm, profile.Indices, profile.Weights);
            NativeSpawners.Write(fsm, "number", saved.Number); NativeSpawners.Write(fsm, "numberEnd", count); NativeSpawners.Write(fsm, "OnOff", saved.OnOff);
            NativeSpawners.Force(fsm, count, marker.Kind == "Monsters" ? site.Population : null);
            // Ignore stale editor-template spawnList/spawn values; the native spawn action resolves its own fresh children.
            var list = fsm.FsmVariables.FindFsmGameObject("spawnList"); if (list != null) list.Value = points;
            SpawnerProgress.Track(key, fsm);
            holder.SetActive(true); InteriorPopulation.OwnMarker(fsm, site); // refs-ok: REFACTOR-REQUEST B15-1
            if (Log.L1) Log.Main(LootModule.Instance, "POI marker " + site.Type + "/" + marker.Kind + ": " + (marker.Kind == "Monsters" && site.Town != null ? "town allegiance " + site.Town.Plan.Faction : profile.Name) + "; " + saved.Number + "/" + count + ", native arrival queue; " + marker.Id);
            return true;
        }

        /// <summary>POI entry save: marker progress into the slot (forwards to <see cref="SpawnerProgress.SaveMarkers"/>).</summary>
        internal void Save(string slot) { SpawnerProgress.SaveMarkers(slot); }

        /// <summary>POI entry load/new world (null slot): marker progress of the slot (forwards to <see cref="SpawnerProgress.LoadMarkers"/>; throws on invalid data).</summary>
        internal void Load(string slot) { SpawnerProgress.LoadMarkers(slot); }

        /// <summary>Unload of the dressed sites: captures progress, stops tracking, (reset) forgets progress, drops the catalog.</summary>
        internal void Clear(bool reset) { SpawnerProgress.ClearMarkers(reset); Catalog.Clear(); }
    }
}
