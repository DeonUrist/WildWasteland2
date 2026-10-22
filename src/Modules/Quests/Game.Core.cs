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

        /// <summary>Converts a scene position into the shared stable-world frame.</summary>

        /// <summary>Converts a stable-world position into the current scene frame.</summary>

        /// <summary>Returns the shared native player root.</summary>

        /// <summary>Computes horizontal distance through the shared geometry utility.</summary>

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

        /// <summary>Forwards canonical native currency lookup with the quest-configured variable name.</summary>

        /// <summary>Reads shared native currency using the quest-configured name.</summary>

        /// <summary>Changes shared native currency and publishes its canonical change event.</summary>

        /// <summary>Uses the shared player-hierarchy exclusion query.</summary>
        private static bool UnderPlayer(Transform transform) { return PlayerState.UnderPlayer(transform); }
        /// <summary>Reads a named integer through the shared native FSM index.</summary>

        /// <summary>Reads a named float through the shared native FSM index.</summary>

        /// <summary>Reads a named string through the shared native FSM index with an empty fallback.</summary>

        /// <summary>Uses canonical native clone-name normalization.</summary>

    }
}
