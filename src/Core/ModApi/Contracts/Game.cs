using System.Collections.Generic;
using UnityEngine;

// Game-level contracts provided by Core or Compatibility (core-modapi.md 8 "Game.cs"). The Core static services of that row
// (GameRefs, PlayerState, WorldIndex, WorldTiles, FsmIndex, FsmPause, PrefabCatalog, ItemSpawner, Factions, Clock, Names, Layers,
// CameraArbiter) are static classes in src/Core/Game (W0-B), not interfaces. FROZEN after W0.
namespace WildWasteland.Core
{
    /// <summary>Supply-drop / inventory supply queries of Core Fixes InventorySupplies.</summary>
    public interface IInventorySupplies
    {
        /// <summary>Supply prefabs discovered (sliced discovery finished).</summary>
        bool Ready { get; }
        /// <summary>Known supply kinds (cleared first).</summary>
        void Kinds(List<string> into);
        /// <summary>Count of a supply kind the player carries.</summary>
        int Carried(string kind);
    }

    /// <summary>Extra inventory slots (Compatibility: Apocapocket) for HUD and Presentation.</summary>
    public interface IInventorySlots
    {
        /// <summary>Number of extra slots.</summary>
        int Count { get; }
        /// <summary>Selected slot (-1 = none).</summary>
        int Selected { get; }
        /// <summary>Save-stable item id in a slot (null = empty).</summary>
        string ItemIn(int slot);
        /// <summary>True when the slot holds an ordinary (non-weapon) item.</summary>
        bool IsOrdinary(int slot);
        /// <summary>Icon of an item id (borrowed; do not destroy).</summary>
        Texture2D IconOf(string itemId);
    }

    /// <summary>Language selection (Compatibility: ApocaLanguage) for the menus.</summary>
    public interface ILanguageMenu
    {
        /// <summary>The language mod is present.</summary>
        bool Present { get; }
        /// <summary>Current language name.</summary>
        string Current { get; }
        /// <summary>Available languages (cleared first).</summary>
        void Languages(List<string> into);
        /// <summary>Selects a language.</summary>
        bool Select(string language);
    }
}
