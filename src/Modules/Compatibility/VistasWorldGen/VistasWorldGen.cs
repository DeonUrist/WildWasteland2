using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MapMagic.Core;
using MapMagic.Terrains;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// Vistas World Gen (World Amp, apocalypter.worldamp; tested 1.0.7) bridge, owned by the Compatibility VistasWorldGen bridge. Its
    /// obfuscated overloads are resolved by signature. It quiets the mod's routine log below Debug level 2, turns its debug overlay
    /// off, replaces its scouting/banners/dust storms with WW's, captures its replacement roads (<see cref="RoadLines"/>) and reports
    /// place entries and terrain edits (static events for the current consumers, Core TerrainEdited event, WorldTiles Edited channel).
    /// Facts are published as <see cref="IWorldGenFacts"/> by the Compatibility module.
    /// </summary>
    internal static class VistasWorldGen
    {
        /// <summary>Place entered (name, kind or "LANDMARK DISCOVERED"); HUD banners subscribe.</summary>
        internal static event Action<string, string> PlaceEntered;
        /// <summary>Vistas edited a tile's terrain (Towns/Patrols subscribe; also forwarded to WorldTiles and the Core TerrainEdited event).</summary>

        internal static event Action<TerrainTile> TerrainEdited;
        /// <summary>True while the bridge is installed.</summary>

        internal static bool Present;
        /// <summary>Changes whenever captured roads are added or cleared.</summary>

        internal static int RoadRevision;
        private static int _edits;
        private static FieldInfo _placeName, _placeKind, _landmarkName;
        private static readonly ConcurrentDictionary<string, Vector3[]> Roads = new ConcurrentDictionary<string, Vector3[]>();
        /// <summary>Captured road polylines (x,z; y = 0).</summary>

        internal static IEnumerable<Vector3[]> RoadLines { get { return Roads.Values; } }
        /// <summary>Assembly identity + terrain/road settings (route caches are keyed by it).</summary>

        internal static string RoadIdentity = "";
        private static ConfigFile _config;
        private static Assembly _assembly;
        private static ManualLogSource _logger;
        private static string _assemblyId;
        private static ConfigEntry<float> _bannerHeight;
        private static IModule _owner;
        /// <summary>Last place entered and its kind/caption.</summary>

        internal static string CurrentPlace, CurrentPlaceKind;
        /// <summary>The mod's banner height setting (0.18 when unavailable).</summary>

        internal static float BannerHeight { get { return _bannerHeight != null ? _bannerHeight.Value : .18f; } }

        /// <summary>Detects the mod and installs every hook under <paramref name="owner"/>; throws when a hook cannot be installed.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Init(IModule owner)
        {
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("apocalypter.worldamp", out info) || info.Instance == null) return;
            _owner = owner;
            Present = true;
            TerrainEdited += WorldTiles.EditedSource;   // W0B-3: Core WorldTiles "Edited" channel (no subscribers until Wave B)
            var assembly = info.Instance.GetType().Assembly;
            _config = info.Instance.Config; _assemblyId = assembly.ManifestModule.ModuleVersionId.ToString();
            SettingsChanged(null, null); _config.SettingChanged += SettingsChanged;
            if (info.Metadata.Version.ToString() != "1.0.7")
                Log.Warn(owner, "Vistas version " + info.Metadata.Version + " differs from the tested 1.0.7; attempting compatibility with this version.");
            try
            {
                _logger = (ManualLogSource)AccessTools.Property(typeof(BaseUnityPlugin), "Logger").GetValue(info.Instance, null);
                Hook(Find(typeof(ManualLogSource), "Log", typeof(void), typeof(LogLevel), typeof(object)), prefix: "QuietLog");
                _assembly = assembly;
                // MonoMod's resolver ignores field types; Vistas overloads field names by type.
                Hook(Find(typeof(MonoMod.Utils.ReflectionHelper), "_ResolveReflection", typeof(MemberInfo),
                    typeof(Mono.Cecil.MemberReference), typeof(Module[])), prefix: "ResolveField");
                ConfigEntry<bool> overlay;
                if (info.Instance.Config.TryGetEntry<bool>(new ConfigDefinition("Debug", "Overlay"), out overlay)) overlay.Value = false;
                info.Instance.Config.TryGetEntry<float>(new ConfigDefinition("Regions", "BannerHeight"), out _bannerHeight);
                var places = assembly.GetType("A.E", true);
                var place = assembly.GetType("A.c", true);
                var landmark = assembly.GetType("A.y", true);
                _placeName = Field(place, "A", typeof(string)); _placeKind = Field(place, "a", typeof(string));
                _landmarkName = Field(landmark, "A", typeof(string));
                Hook(Find(places, "A", typeof(void), place, typeof(float)), transpiler: "Entered");
                Hook(Find(places, "A", typeof(bool), landmark, typeof(bool)), prefix: "NearLandmark", transpiler: "Landmark");
                Hook(Find(places, "d", typeof(void)), prefix: "OriginalBanner");
                Hook(Find(assembly.GetType("a.t", true), "A", typeof(void), typeof(float)), prefix: "NoScouting");
                var weather = assembly.GetType("a.y", true); var kind = assembly.GetType("a.f", true);
                Hook(Find(weather, "e", typeof(bool)), postfix: "DustAllowed");
                Hook(Find(weather, "B", typeof(bool), kind), transpiler: "HaboobAllowed");
                Hook(Find(weather, "A", typeof(void), kind, typeof(float)), transpiler: "WeatherTransition");
                Hook(Find(assembly.GetType("ApocalpyterWorldAmp.SpawnerTierPatch", true), "Postfix", typeof(void), typeof(HutongGames.PlayMaker.Actions.RandomInt)), prefix: "NoSpawnBonus");
                Log.Main(owner, "Vistas elevation spawn bonuses disabled; native rolls and WW population budgets retained.");
                Hook(Find(assembly.GetType("A.q", true), "a", typeof(int), typeof(double[]), typeof(double[]), typeof(int), typeof(bool), typeof(string)), postfix: "RoadCreated");
                Hook(Find(assembly.GetType("A.S", true), "A", typeof(void), assembly.GetType("A.q", true), typeof(MapMagicObject), typeof(TerrainTile), typeof(Terrain), typeof(TerrainData), assembly.GetType("A.S+A", true), typeof(int), typeof(float)), postfix: "GroundEdited");
                Hook(Find(assembly.GetType("ApocalpyterWorldAmp.WorldAmpDriver", true), "ClearWorld", typeof(void)), postfix: "ClearRoads");
                Log.Main(owner, "Vistas " + info.Metadata.Version + " compatibility ready: overlay defaults off; WW scouting, HUD banners, WW sandstorms and replacement-road capture; its routine logging shows at Debug level 2.");
            }
            catch (Exception e) { throw new InvalidOperationException("Vistas compatibility installation failed", e); }
        }
        /// <summary>Detaches external config and terrain callbacks before module-owned patches are removed.</summary>
        internal static void Shutdown()
        {
            if (_config != null) _config.SettingChanged -= SettingsChanged;
            TerrainEdited -= WorldTiles.EditedSource;
            Present = false; _config = null; _assembly = null; _logger = null; _assemblyId = null;
            _bannerHeight = null; _placeName = _placeKind = _landmarkName = null; _owner = null;
            CurrentPlace = CurrentPlaceKind = null; RoadIdentity = ""; ClearRoads();
        }
        /// <summary>Prefix of ManualLogSource.Log (every BepInEx log call; one reference compare for other sources): the mod's Info/Debug
        /// lines only at Debug level 2 (README 5.1), its warnings/errors always.</summary>
        private static bool QuietLog(ManualLogSource __instance, LogLevel __0)
        { return !ReferenceEquals(__instance, _logger) || Log.L2 || (__0 & ~(LogLevel.Info | LogLevel.Debug)) != 0; }
        /// <summary>Prefix of MonoMod ReflectionHelper._ResolveReflection: resolves the mod's type-overloaded field names by field type.</summary>
        private static bool ResolveField(Mono.Cecil.MemberReference __0, ref MemberInfo __result)
        {
            var field = __0 as Mono.Cecil.FieldReference;
            if (field == null || _assembly == null || field.DeclaringType == null) return true;
            string scope = field.DeclaringType.Scope.Name;
            if (scope != _assembly.GetName().Name && scope != _assembly.ManifestModule.Name) return true;
            var type = _assembly.GetType(field.DeclaringType.FullName.Replace('/', '+'), false, false);
            if (type == null) return true;
            var signature = field.FieldType;
            while (signature is Mono.Cecil.RequiredModifierType || signature is Mono.Cecil.OptionalModifierType)
                signature = ((Mono.Cecil.TypeSpecification)signature).ElementType;
            var valueType = MonoMod.Utils.ReflectionHelper.ResolveReflection(signature);
            foreach (var candidate in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (candidate.Name == field.Name && candidate.FieldType == valueType)
                { __result = candidate; return false; }
            throw new MissingFieldException(type.FullName, field.Name + " : " + signature.FullName);
        }
        /// <summary>Rebuilds <see cref="RoadIdentity"/> from the mod's terrain/region/road/tile/seam settings.</summary>
        private static void SettingsChanged(object sender, SettingChangedEventArgs args)
        {
            var values = new List<string>();
            foreach (var entry in _config)
                if (entry.Key.Section == "Terrain" || entry.Key.Section == "Regions" && (entry.Key.Key == "Enabled" || entry.Key.Key == "Scale" || entry.Key.Key == "Blend" || entry.Key.Key == "ExtraTypes" || entry.Key.Key == "Landmarks") || entry.Key.Section == "Roads" || entry.Key.Section == "Tiles" || entry.Key.Section == "Seams")
                    values.Add(entry.Key.Section + ":" + entry.Key.Key + "=" + entry.Value.GetSerializedValue());
            values.Sort(StringComparer.Ordinal); RoadIdentity = _assemblyId + "|" + string.Join("|", values.ToArray());
        }
        /// <summary>Field by name and exact type (the mod overloads field names by type).</summary>
        private static FieldInfo Field(Type type, string name, Type value)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.Name == name && field.FieldType == value) return field;
            throw new MissingFieldException(type.FullName, name + " : " + value.Name);
        }
        /// <summary>Method by name, return type and exact parameter types (obfuscated overloads).</summary>
        private static MethodInfo Find(Type type, string name, Type result, params Type[] args)
        {
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != name || method.ReturnType != result) continue;
                var parameters = method.GetParameters(); if (parameters.Length != args.Length) continue;
                bool match = true; for (int i = 0; i < args.Length; i++) if (parameters[i].ParameterType != args[i]) match = false;
                if (match) return method;
            }
            throw new MissingMethodException(type.FullName, name);
        }
        /// <summary>Patches one target with this class's named prefix/postfix/transpiler; throws when refused.</summary>
        private static void Hook(MethodInfo original, string prefix = null, string postfix = null, string transpiler = null)
        {
            if (!Patches.Patch(_owner, original, prefix: prefix == null ? null : AccessTools.Method(typeof(VistasWorldGen), prefix),
                postfix: postfix == null ? null : AccessTools.Method(typeof(VistasWorldGen), postfix),
                transpiler: transpiler == null ? null : AccessTools.Method(typeof(VistasWorldGen), transpiler)))
                throw new InvalidOperationException("Cannot patch " + original.DeclaringType.FullName + "." + original.Name);
        }
        /// <summary>Prefix of the mod's banner draw: skipped while the WW HUD draws banners.</summary>
        private static bool OriginalBanner() { var display = ModApi.Get<IPlaceBannerDisplay>(); return display == null || !display.Enabled; }
        /// <summary>Prefix of the mod's scouting: always skipped (WW scouting).</summary>
        private static bool NoScouting() { return false; }
        /// <summary>Skips only Vista's post-roll population bonus; native spawning and WW's exact counts remain owned by their existing paths.</summary>
        private static bool NoSpawnBonus() { return false; }
        /// <summary>Prefix of the landmark check: skips the mod's "near" notice path.</summary>
        private static bool NearLandmark(bool __1) { return !__1; }
        /// <summary>Postfix of the mod's dust gate: no dust while WW weather runs (IWeatherRegions provided).</summary>
        private static void DustAllowed(ref bool __result) { if (ModApi.Get<IWeatherRegions>() != null) __result = false; }
        /// <summary>Haboob gate (called by the transpiled method): kind 7 only without WW weather.</summary>
        private static bool Allowed(bool original, int kind) { return original && (kind != 7 || ModApi.Get<IWeatherRegions>() == null); }
        /// <summary>Weather transition (transpiled entry): sandstorm kinds 4/7 become clear (2) while WW weather runs.</summary>
        private static int Transition(int kind) { return ModApi.Get<IWeatherRegions>() != null && (kind == 4 || kind == 7) ? 2 : kind; }
        /// <summary>Transpiler: passes every return value of the haboob gate through <see cref="Allowed"/>.</summary>
        private static IEnumerable<CodeInstruction> HaboobAllowed(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ret)
                {
                    var load = new CodeInstruction(OpCodes.Ldarg_0); load.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    yield return load; yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VistasWorldGen), "Allowed"));
                }
                yield return instruction;
            }
        }
        /// <summary>Transpiler: rewrites the transition kind argument through <see cref="Transition"/>.</summary>
        private static IEnumerable<CodeInstruction> WeatherTransition(IEnumerable<CodeInstruction> instructions)
        {
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VistasWorldGen), "Transition"));
            yield return new CodeInstruction(OpCodes.Starg_S, (byte)0);
            foreach (var instruction in instructions) yield return instruction;
        }
        /// <summary>Transpiler of the place-entry method: reports (place name, kind) before each return.</summary>
        private static IEnumerable<CodeInstruction> Entered(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ret)
                {
                    var load = new CodeInstruction(OpCodes.Ldarga_S, (byte)0); load.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    yield return load; yield return new CodeInstruction(OpCodes.Ldfld, _placeName);
                    yield return new CodeInstruction(OpCodes.Ldarga_S, (byte)0); yield return new CodeInstruction(OpCodes.Ldfld, _placeKind);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VistasWorldGen), "Notify"));
                }
                yield return instruction;
            }
        }
        /// <summary>Transpiler of the landmark check: a discovered landmark reports (name, "LANDMARK DISCOVERED").</summary>
        private static IEnumerable<CodeInstruction> Landmark(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ret)
                {
                    var skip = generator.DefineLabel(); var dup = new CodeInstruction(OpCodes.Dup);
                    dup.labels.AddRange(instruction.labels); instruction.labels.Clear(); yield return dup;
                    yield return new CodeInstruction(OpCodes.Brfalse, skip);
                    yield return new CodeInstruction(OpCodes.Ldarga_S, (byte)0); yield return new CodeInstruction(OpCodes.Ldfld, _landmarkName);
                    yield return new CodeInstruction(OpCodes.Ldstr, "LANDMARK DISCOVERED");
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VistasWorldGen), "Notify"));
                    var end = new CodeInstruction(OpCodes.Nop); end.labels.Add(skip); yield return end;
                }
                yield return instruction;
            }
        }
        /// <summary>Records the current place and raises <see cref="PlaceEntered"/>.</summary>
        private static void Notify(string name, string detail)
        { CurrentPlace = name; CurrentPlaceKind = detail; var handler = PlaceEntered; if (handler != null) handler(name, detail); }
        /// <summary>Postfix of the road builder (worker threads): captures the polyline (bounded: 20000 points, 4096 roads).</summary>
        private static void RoadCreated(double[] __0, double[] __1)
        {
            if (__0 == null || __1 == null || __0.Length < 2 || __0.Length > 20000 || __0.Length != __1.Length || Roads.Count >= 4096) return;
            var points = new Vector3[__0.Length];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector3((float)__0[i], 0, (float)__1[i]);
            string key = points[0].ToString("R") + ":" + points[points.Length - 1].ToString("R");
            Roads[key] = points; System.Threading.Interlocked.Increment(ref RoadRevision);
        }
        /// <summary>Postfix of WorldAmpDriver.ClearWorld (and shutdown): forgets captured roads.</summary>
        private static void ClearRoads() { Roads.Clear(); System.Threading.Interlocked.Increment(ref RoadRevision); }
        /// <summary>Postfix of the terrain writer (main tile terrain only): raises <see cref="TerrainEdited"/> and the Core TerrainEdited event.</summary>
        private static void GroundEdited(TerrainTile __2, Terrain __3)
        {
            if (__2 == null || __2.main == null || __2.main.terrain != __3) return;
            var handler = TerrainEdited; if (handler != null) try { handler(__2); } catch (Exception e) { Log.Error(_owner, "Vistas terrain snapshot callback", e); }
            _edits++;
            if (!ModEvents.HasListeners<Events.TerrainEdited>() || __3.terrainData == null) return;
            var corner = Origin.World(__3.GetPosition()); var size = __3.terrainData.size;
            var edited = new Events.TerrainEdited { WorldRect = new Rect(corner.x, corner.z, size.x, size.z), Source = "Vistas", Revision = _edits };
            ModEvents.Publish(ref edited);
        }
    }
}
