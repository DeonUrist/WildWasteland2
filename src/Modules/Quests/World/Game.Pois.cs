using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Quests world helpers, POI part (Wave A, from Quests/Game.cs; B13): camps/caves/buildings/wrecks in the loaded tiles, the human faction that owns a camp's spawners, and visited map icons.</summary>
    internal static partial class Game
    {
        // ---- points of interest and enemies -------------------------------------------

        /// <summary>Resolves camp or town ownership through the owning world services.</summary>
        internal static string SpawnFactionOf(Transform poi)
        {
            Vector3 world = Core.Origin.World(poi.position);
            var towns = ModApi.Get<ITownsApi>(); TownInfo town;
            if (towns != null && towns.IsTownBuilding(poi) && towns.TryTownAt(world, out town)) return town.Faction ?? "";
            var locations = ModApi.Get<IWorldLocations>(); WorldLocation location;
            string key = Names.Prefab(poi.name) + ":" + Mathf.RoundToInt(world.x) + ":" + Mathf.RoundToInt(world.z);
            return locations != null && locations.TryGet(key, out location) ? location.Faction ?? "" : "";
        }

        // Camps, caves, buildings and wrecks in the loaded MapMagic tiles whose horizontal distance
        // from `center` is between minD and maxD. kindFilter ("" = any) is a word like "cave".
        /// <summary>Returns loaded quest POIs matching kind and horizontal range.</summary>
        public static List<Poi> FindPois(Vector3 center, float minD, float maxD, string kindFilter)
        {
            var list = new List<Poi>(); string filter = Util.Norm(kindFilter);
            var towns = ModApi.Get<ITownsApi>();
            foreach (var p in QuestWorld.Pois)
            {
                if (p.Obj == null || !p.Obj.gameObject.activeInHierarchy) continue;
                if (towns != null && towns.IsTownBuilding(p.Obj)) continue;
                p.Pos = p.Obj.position;
                p.Faction = SpawnFactionOf(p.Obj);
                if (filter.Length > 0 && !Util.Norm(p.Name).Contains(filter) && !Util.Norm(p.Kind).Contains(filter)) continue;
                float d = Core.WorldGeometry.HorizontalDistance(p.Pos, center); if (d >= minD && d <= maxD) list.Add(p);
            }
            if (towns != null && (filter.Length == 0 || filter == "camp" || filter == "town"))
            {
                var known = new List<TownInfo>(); towns.Towns(known);
                foreach (var town in known)
                {
                    Vector3 scene = Core.Origin.Scene(town.WorldCenter); float d = Core.WorldGeometry.HorizontalDistance(scene, center);
                    if (d >= minD && d <= maxD) list.Add(new Poi { Name = town.Key, Kind = "Town", Pos = scene, Faction = town.Faction ?? "" });
                }
            }
            return list;
        }

        // ---- places the player has been to ----------------------------------------------------------
        // World positions of the map markers that the game has turned yellow = places the player has visited (the game's
        // Visited FSM does this; Immersive Map reads it the same way). Found by type name so the TextMeshPro DLL is not needed.

        /// <summary>Copies native visited map-icon positions for exploration objectives.</summary>
        public static List<Vector3> VisitedIcons()
        {
            var result = new List<Vector3>();
            foreach (var icon in QuestWorld.Icons) if (icon != null) { var c = icon.color; if (c.r > .8f && c.g > .8f && c.b < .5f) result.Add(icon.transform.position); }
            return result;
        }

        /// <summary>Tests whether a point lies near any supplied visited position.</summary>
        public static bool NearAny(List<Vector3> points, Vector3 p, float radius)
        {
            for (int i = 0; i < points.Count; i++) if (Core.WorldGeometry.HorizontalDistance(points[i], p) <= radius) return true;
            return false;
        }
    }
}
