using UnityEngine;

namespace WildWasteland.Modules.NPCAI
{
    internal static partial class Nav
    {
        /// <summary>Tests actual terrain against an authored floor, including hills above the POI's roof.</summary>
        internal static bool TerrainMismatch(Vector3 floor, bool ground)
        {
            RaycastHit hit;
            if (!Physics.Raycast(floor + Vector3.up * 2000f, Vector3.down, out hit, 4000f, 1 << 14, QueryTriggerInteraction.Ignore)) return ground;
            return ground ? Mathf.Abs(hit.point.y - floor.y) > .2f : hit.point.y > floor.y + .2f;
        }

        /// <summary>Checks loaded walkable cells in the normal bake budget before exposing a shared map.</summary>
        private static void ValidateTerrain(Structure s, float budget)
        {
            if (s.IsolatedMap) { s.Phase = 2; s.Next = 0; return; }
            int n = s.W * s.H;
            while (s.Next < n && _sw.Elapsed.TotalMilliseconds < budget)
            {
                int i = s.Next++; float y = s.FloorY[i];
                if (float.IsNaN(y)) continue;
                Vector3 floor = CellCenter(s, i % s.W, i / s.W, y); RaycastHit support;
                if (!TerrainMismatch(floor, false) && Physics.Raycast(floor + Vector3.up * .2f, Vector3.down, out support, .4f, BakeMask, QueryTriggerInteraction.Ignore)) continue;
                s.InstanceMap = true; s.LoadedMap = s.LoadedTemplate = s.LoadedVanilla = s.MapSaved = false;
                WildWasteland.Core.Log.Main(NavRuntime.Owner, "Nav: terrain mismatch " + s.Name + " at " + floor.ToString("F1") + "; rebaking this instance only");
                BeginBake(s, false); return;
            }
            if (s.Next >= n) { s.Phase = 2; s.Next = 0; }
        }
    }
}
