using System;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Quests
{
    /// <summary>Quest entry points forwarding shared world/player/save/clock queries to canonical Core services.</summary>
    internal static partial class Game
    {
        /// <summary>Forwards the shared stable-world anchor.</summary>
        [Obsolete("refactor: use Core.Origin.Anchor")]
        public static Vector3 Origin() { return Core.Origin.Anchor; }
        /// <summary>Converts a scene position into the shared stable-world frame.</summary>
        [Obsolete("refactor: use Core.Origin.World")]
        public static Vector3 Rel(Vector3 scene) { return Core.Origin.World(scene); }
        /// <summary>Converts a stable-world position into the current scene frame.</summary>
        [Obsolete("refactor: use Core.Origin.Scene")]
        public static Vector3 Abs(Vector3 world) { return Core.Origin.Scene(world); }
        /// <summary>Returns the shared native player root.</summary>
        [Obsolete("refactor: use Core.PlayerState.Transform")]
        public static Transform Player() { return PlayerState.Transform; }
        /// <summary>Computes horizontal distance through the shared geometry utility.</summary>
        [Obsolete("refactor: use Core.WorldGeometry.HorizontalDistance")]
        public static float HDist(Vector3 a, Vector3 b) { return WorldGeometry.HorizontalDistance(a, b); }
        /// <summary>Registers a quest-created native item with existing naming order and returns a diagnostic outcome.</summary>
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
        /// <summary>Queries shared native terrain ground at the supplied horizontal coordinates.</summary>
        [Obsolete("refactor: use Core.WorldGeometry.GroundAt")]
        public static bool GroundAt(float x, float z, out Vector3 position) { return WorldGeometry.GroundAt(x, z, out position); }
        /// <summary>Forwards canonical native currency lookup with the quest-configured variable name.</summary>
        [Obsolete("refactor: use Core.Money.MoneyVar")]
        public static FsmInt MoneyVar(string configured) { return Money.MoneyVar(configured); }
        /// <summary>Reads shared native currency using the quest-configured name.</summary>
        [Obsolete("refactor: use Core.Money.GetMoney")]
        public static int GetMoney(string configured) { return Money.GetMoney(configured); }
        /// <summary>Changes shared native currency and publishes its canonical change event.</summary>
        [Obsolete("refactor: use Core.Money.AddMoney")]
        public static bool AddMoney(int amount, string configured) { return Money.AddMoney(amount, configured); }
        /// <summary>Uses the shared player-hierarchy exclusion query.</summary>
        private static bool UnderPlayer(Transform transform) { return PlayerState.UnderPlayer(transform); }
        /// <summary>Reads a named integer through the shared native FSM index.</summary>
        [Obsolete("refactor: use Core.FsmIndex.Int")]
        public static FsmInt FindInt(PlayMakerFSM fsm, string name) { return FsmIndex.Int(fsm, name); }
        /// <summary>Reads a named float through the shared native FSM index.</summary>
        [Obsolete("refactor: use Core.FsmIndex.Float")]
        public static FsmFloat FindFloat(PlayMakerFSM fsm, string name) { return FsmIndex.Float(fsm, name); }
        /// <summary>Reads a named string through the shared native FSM index with an empty fallback.</summary>
        [Obsolete("refactor: use Core.FsmIndex.String")]
        public static string FindString(PlayMakerFSM fsm, string name) { var value = FsmIndex.String(fsm, name); return value != null ? value.Value ?? "" : ""; }
        /// <summary>Uses canonical native clone-name normalization.</summary>
        [Obsolete("refactor: use Core.Names.Base")]
        public static string BaseName(string name) { return Names.Base(name); }
    }
}
