using System;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Quests
{
    /// <summary>Quest entry points forwarding shared world/player/save/clock queries to canonical Core services.</summary>
    internal static partial class Game
    {
        public static Vector3 Origin() { return Core.Origin.Anchor; }
        public static Vector3 Rel(Vector3 scene) { return Core.Origin.World(scene); }
        public static Vector3 Abs(Vector3 world) { return Core.Origin.Scene(world); }
        public static Transform Player() { return PlayerState.Transform; }
        public static float HDist(Vector3 a, Vector3 b) { return WorldGeometry.HorizontalDistance(a, b); }
        public static string RegisterSpawned(GameObject gameObject, string baseName)
        {
            try
            {
                if (ItemSpawner.Counter() == null) return "itemNameID counter not found";
                if (ItemSpawner.List(ItemSpawner.SaveList.Items) == null) return "ArrayList_Items not found on NewGO_ArrayList";
                ItemSpawner.RegisterSaved(gameObject, baseName, ItemSpawner.SaveList.Items, ItemSpawner.Numbering.UseThenIncrement);
                InvalidateLiveNames(); return "registered as " + gameObject.name;
            }
            catch (Exception error) { return "registration failed: " + error.Message; }
        }
        public static bool GroundAt(float x, float z, out Vector3 position) { return WorldGeometry.GroundAt(x, z, out position); }
        public static FsmInt MoneyVar(string configured) { return Money.MoneyVar(configured); }
        public static int GetMoney(string configured) { return Money.GetMoney(configured); }
        public static bool AddMoney(int amount, string configured) { return Money.AddMoney(amount, configured); }
        private static bool UnderPlayer(Transform transform) { return PlayerState.UnderPlayer(transform); }
        public static FsmInt FindInt(PlayMakerFSM fsm, string name) { return FsmIndex.Int(fsm, name); }
        public static FsmFloat FindFloat(PlayMakerFSM fsm, string name) { return FsmIndex.Float(fsm, name); }
        public static double GameHours() { return Clock.GameHours; }
        public static string FindString(PlayMakerFSM fsm, string name) { var value = FsmIndex.String(fsm, name); return value != null ? value.Value ?? "" : ""; }
        public static string BaseName(string name) { return Names.Base(name); }
    }
}
