using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPCAI
{
    internal static partial class PoiNav
    {
        /// <summary>Validates walk segments and traversal endpoints, one terrain sample per scheduler step.</summary>
        private static bool ValidateTerrainStep(Inst inst)
        {
            if (inst.CheckEdge + 1 < inst.Data.Edges.Count)
            {
                int a = inst.Data.Edges[inst.CheckEdge], b = inst.Data.Edges[inst.CheckEdge + 1];
                int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(inst.World[a], inst.World[b]) / .5f));
                Vector3 point = Vector3.Lerp(inst.World[a], inst.World[b], (float)++inst.CheckSample / samples);
                _buildRays++;
                if (Nav.TerrainMismatch(point, inst.Data.Nodes[a].Terrain && inst.Data.Nodes[b].Terrain)) inst.NeedsBake = true;
                if (inst.CheckSample >= samples) { inst.CheckEdge += 2; inst.CheckSample = 0; }
                return false;
            }
            if (inst.CheckLink < inst.Data.Links.Count * 2)
            {
                int index = inst.CheckLink++; var link = inst.Data.Links[index / 2];
                if (!link.Off)
                {
                    _buildRays++;
                    if (Nav.TerrainMismatch(inst.W(index % 2 == 0 ? link.From : link.To), false)) inst.NeedsBake = true;
                }
                return false;
            }
            return true;
        }

        /// <summary>Runs the existing baker without saving or invalidating any other instance's authored graph.</summary>
        private static bool RebuildTerrain(Inst inst)
        {
            if (inst.Bake == null)
            {
                inst.Bake = NavigationAuthoringApi.CreateBake(inst.Root, inst.Source);
                Log.Main(NavRuntime.Owner, "POI navigation: terrain mismatch " + inst.Poi + " at " + inst.RootPos.ToString("F1")
                    + (inst.Bake == null ? "; waiting for Tools baker; keeping authored graph" : "; rebaking this instance only; keeping authored graph"));
                if (inst.Bake == null) { _builds.Dequeue(); inst.Queued = false; return _builds.Count > 0; }
            }
            if (!NavigationAuthoringApi.BakerAvailable) { inst.Bake = null; _builds.Dequeue(); inst.Queued = false; return _builds.Count > 0; }
            inst.Bake.Step(.5f);
            if (inst.Bake.Done)
            {
                var result = inst.Bake.Result;
                if (result != null)
                {
                    var pending = new Inst { Root = inst.Root, Poi = inst.Poi, Id = inst.Id, Source = inst.Source };
                    Build(pending, result, true); inst.Pending = pending;
                    inst.Bake = null; inst.NeedsBake = false; inst.Queued = false; _builds.Dequeue();
                }
                else
                {
                    inst.Bake = null; inst.BakeFailed = true; _builds.Dequeue(); inst.Queued = false;
                    Log.Warn(NavRuntime.Owner, "POI navigation: instance terrain bake failed " + inst.Poi + "; keeping authored graph");
                }
            }
            return false;
        }
    }
}
