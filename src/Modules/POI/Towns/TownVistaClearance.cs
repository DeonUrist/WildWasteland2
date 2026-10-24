using System;
using System.Collections.Generic;
using System.Reflection;
using Den.Tools;
using MapMagic.Terrains;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    // The normal town path filters ObjectsOutput before apply. Late Vista towns must remove the same pooled scenery after apply.
    internal sealed class TownVistaClearance
    {
        private static readonly Type Pool = typeof(ObjectsPool).GetNestedType("Pool", BindingFlags.NonPublic);
        private static readonly Func<object, Array> Pools = Access.Read<Array>(typeof(ObjectsPool).GetField("pools", BindingFlags.Instance | BindingFlags.NonPublic));
        private static readonly Func<object, ObjectsPool.Prototype> Prototype = Access.Read<ObjectsPool.Prototype>(Pool.GetField("prototype"));
        private static readonly Func<object, List<GameObject>> Instances = Access.Read<List<GameObject>>(Pool.GetField("instances"));
        private readonly Array _pools;
        private readonly Transform _anchor;
        private readonly TownPlan _plan;
        private readonly HashSet<GameObject> _preserve;
        private int _pool, _index = -1;

        internal TownVistaClearance(TerrainTile tile, TownPlan plan, TownAssets assets)
        {
            _pools = tile.objectsPool == null ? null : Pools(tile.objectsPool); _anchor = tile.mapMagic.transform;
            _plan = plan; _preserve = new HashSet<GameObject>(assets.NativePoiPrefabs);
        }

        internal bool Step()
        {
            int budget = 64;
            while (_pools != null && _pool < _pools.Length && budget-- > 0)
            {
                var pool = _pools.GetValue(_pool); var prototype = Prototype(pool);
                if (_preserve.Contains(prototype.prefab)) { _pool++; _index = -1; continue; }
                var instances = Instances(pool); if (_index < 0) _index = instances.Count;
                if (_index == 0) { _pool++; _index = -1; continue; }
                int index = --_index; var item = instances[index]; if (item == null) continue;
                var world = _anchor.InverseTransformPoint(item.transform.position); world.y = 0;
                var center = _plan.WorldCenter; center.y = 0;
                bool clear = (world - center).sqrMagnitude <= (_plan.Radius + Mathf.Max(8, _plan.Feather)) * (_plan.Radius + Mathf.Max(8, _plan.Feather));
                for (int s = 1; !clear && s < _plan.AccessPath.Length; s++)
                {
                    var a = _plan.World(_plan.AccessPath[s - 1]); var b = _plan.World(_plan.AccessPath[s]); a.y = b.y = 0; var d = b - a;
                    clear = (world - a - d * Mathf.Clamp01(Vector3.Dot(world - a, d) / Mathf.Max(.001f, d.sqrMagnitude))).sqrMagnitude <= 64;
                }
                if (clear) { instances.RemoveAt(index); UnityEngine.Object.Destroy(item); }
            }
            return _pools != null && _pool < _pools.Length;
        }
    }
}
