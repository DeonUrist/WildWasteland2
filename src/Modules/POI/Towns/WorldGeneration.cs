using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>Optional generation facts; only Compatibility resolves the external world generator.</summary>
    internal static class WorldGeneration
    {
        internal static bool Present=>ModApi.Get<IWorldGenFacts>()?.Present??false;
        internal static string RoadIdentity=>ModApi.Get<IWorldGenFacts>()?.RoadIdentity??"";
        internal static List<Vector3[]> RoadLines {get{var lines=new List<Vector3[]>();ModApi.Get<IWorldGenFacts>()?.RoadLines(lines);return lines;}}
    }
}
