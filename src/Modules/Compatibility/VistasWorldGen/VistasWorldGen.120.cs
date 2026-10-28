using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MapMagic.Terrains;
using UnityEngine;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Core
{
    internal static partial class VistasWorldGen
    {
        // Vista 1.2.0 has a different obfuscation map and asynchronous terrain commits. Keep 1.0.7's bindings separate.
        internal static bool Is120;
        internal const string Network120 = "_PmolccZA32cCgjl13XVdhbIhh8N";
        internal const string Ground120 = "_muIMY4vZuKPgg1kBWlkhzHmd6b";
        internal const string Track120 = "_3xGtiaxddQCDh0y1rGbA91voIDaA";
        internal const string Gate120 = "_cm9iZJLa8WGOxB6XtxLE4Vt9BKf";
        private static Func<object, TerrainTile> _jobTile120;
        private static Func<object, Terrain> _jobTerrain120;
        private static Func<object, bool> _gateActive120;
        private static Func<object, float> _progress120, _released120;
        private static Func<object, string> _stage120;
        private static readonly Dictionary<string, MethodInfo> _globalMethods120 = new Dictionary<string, MethodInfo>();

        private static void Install120(Assembly assembly)
        {
            // MonoMod's global-method signature matcher loses Vista's generic string-decoder calls. Resolve the original
            // definitions by their own metadata tokens, then close each generic call with its original type arguments.
            using (var metadata = Mono.Cecil.ModuleDefinition.ReadModule(assembly.Location))
            {
                if (metadata.Mvid != assembly.ManifestModule.ModuleVersionId) throw new InvalidOperationException("Vista metadata differs from the loaded assembly");
                foreach (var method in metadata.GetType("<Module>").Methods)
                {
                    if (method.IsConstructor) continue;
                    if (_globalMethods120.ContainsKey(method.Name)) throw new AmbiguousMatchException("Vista global method " + method.Name);
                    _globalMethods120.Add(method.Name, (MethodInfo)assembly.ManifestModule.ResolveMethod(method.MetadataToken.ToInt32()));
                }
            }
            var places = assembly.GetType("_jj3t1AeP8UFu9aqe1JPXBR1oUsd", true);
            var place = assembly.GetType("_iPCmOBuc9hNgsJz1zprCBuOoMCM", true);
            var landmark = assembly.GetType("_INu2HYvi1N2zOoYB5xgb095W2Ur", true);
            _placeName = Field(place, "_wTuBzEr8tO9rcf1bpKxeR9Esydo", typeof(string));
            _placeKind = Field(place, "_PEFjz9LIOvpsWwLtdCuWQi69T4g", typeof(string));
            _landmarkName = Field(landmark, "_wTuBzEr8tO9rcf1bpKxeR9Esydo", typeof(string));
            Hook(Find(places, "_90wRkZO3BsWL3pFrJZLPe4P2fcq", typeof(void), place, typeof(float)), transpiler: "Entered");
            Hook(Find(places, "_5q7EkcRBnODMG2Xt1uzh3Y5xZrg", typeof(bool), landmark, typeof(bool)), prefix: "NearLandmark", transpiler: "Landmark");
            Hook(Find(places, "_sGGH3Zpyzdp7wFRrKrFAYYqPHYp", typeof(void)), prefix: "OriginalBanner");
            Hook(Find(assembly.GetType("_XfuIG5jhSwyEKPdfJL645p0ayLp", true), "_uDSqvFcMdfhpKmYBDy1hGgaZUIl", typeof(void), typeof(float)), prefix: "NoScouting");
            var weather = assembly.GetType("_pTwXbYJ3EryjTebATfRdWIVH2B3", true);
            var kind = assembly.GetType("ApocalpyterWorldAmp.WeatherKind", true);
            Hook(AccessTools.PropertyGetter(weather, "_rPSQIdqRDm5by1HcLiKro65iyPQ"), postfix: "DustAllowed");
            Hook(Find(weather, "_m4mzAffglZTGjqzk66XbCdphsIe", typeof(bool), kind), transpiler: "HaboobAllowed");
            Hook(Find(weather, "_ft1bBYVeHo0Q1LuGQSTaUk5U2LD", typeof(void), kind, typeof(float)), transpiler: "WeatherTransition");
            Hook(Find(assembly.GetType("ApocalpyterWorldAmp.SpawnerTierPatch", true), "Postfix", typeof(void), typeof(HutongGames.PlayMaker.Actions.RandomInt)), prefix: "NoSpawnBonus");
            Log.Main(_owner, "Vistas elevation spawn bonuses disabled; native rolls and WW population budgets retained.");
            Hook(Find(assembly.GetType(Network120, true), "_Ub8zoH3AAR3DGM1SNvZpR6o32Mu", typeof(int), typeof(double[]), typeof(double[]), typeof(int), typeof(bool), typeof(string), typeof(long)), postfix: "RoadCreated");
            var ground = assembly.GetType(Ground120, true);
            var job = assembly.GetType(Ground120 + "+_HsFivv3Dr88FNEYS3DgTKw3sqSf", true);
            _jobTile120 = Access.Read<TerrainTile>(Field(job, "_6Byu7rQnmAmBnEma5Ptqs8nWaJB", typeof(TerrainTile)));
            _jobTerrain120 = Access.Read<Terrain>(Field(job, "_CfpWWMBZbtgMlswIlsleBdcW45f", typeof(Terrain)));
            Hook(Find(ground, "_enZRdwlwEIwhGvcrxdQGz9SBN1M", typeof(void), assembly.GetType(Network120, true), job, typeof(float)), postfix: "GroundCommitted120");
            var gate = assembly.GetType(Gate120, true);
            _gateActive120 = Access.Read<bool>(gate.GetField("_qPlA3qLGZtIounCYgkXuhx7LuYo", BindingFlags.Static | BindingFlags.NonPublic));
            _progress120 = Access.Read<float>(gate.GetField("_dOJmTbBVIU0uKs6FFkxBGAtnOCW", BindingFlags.Static | BindingFlags.NonPublic));
            _released120 = Access.Read<float>(gate.GetField("_niqaoD1KdhtKLscZrW7d7DwjPBW", BindingFlags.Static | BindingFlags.NonPublic));
            _stage120 = Access.Read<string>(gate.GetField("_gGUtICsBER6Wembqa4KFKYvfz9l", BindingFlags.Static | BindingFlags.NonPublic));
            Hook(Find(gate, "_y7TsiWDFJIbMXNCOBel5vmUTcrG", typeof(void)), prefix: "LoadingDraw120");
        }

        internal static void Loading120(IWorldGenRoads roads)
        {
            if (!Is120) return;
            LoadingProgress.Register(CompatibilityModule.Instance, Progress120);
            Log.Main(_owner, "Vistas 1.2.0 loading: native stage/progress displayed; Vista owns generation completion, then WW towns.");
        }

        internal static bool NativeLoadingFinished120 { get { return _released120 != null && !_gateActive120(null) && _released120(null) >= 0f; } }

        private static LoadingProgress.Work? Progress120()
        {
            if (!_gateActive120(null)) return null;
            return new LoadingProgress.Work(.35f * Mathf.Clamp01(_progress120(null)), "Vista: " + _stage120(null));
        }

        private static bool LoadingDraw120() { return !LoadingProgress.ScreenVisible; }
        private static void GroundCommitted120(object __1) { if (__1 != null) GroundEdited(_jobTile120(__1), _jobTerrain120(__1)); }

        private static void Clear120()
        {
            if (Is120)
            {
                LoadingProgress.Remove(CompatibilityModule.Instance);
            }
            _jobTile120 = null; _jobTerrain120 = null; _gateActive120 = null; _progress120 = _released120 = null; _stage120 = null;
            _globalMethods120.Clear();
        }
    }
}
