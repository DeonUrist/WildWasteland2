using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core.UI;

namespace WildWasteland.Core
{
    /// <summary>
    /// Contract every Wild Wasteland module implements (AGENTS.md 5.3). Modules derive from <see cref="ModuleBase"/>
    /// and override only what they need. Every hook is invoked by <see cref="ModuleHost"/> inside its own try/catch.
    /// </summary>
    public interface IModule
    {
        /// <summary>Short stable id, also the config section prefix and the Harmony id suffix ("PlayerBody", "NPC", "Guns", "GunplayHUD", "Patrols").</summary>
        string Id { get; }
        string DisplayName { get; }
        /// <summary>GUIDs of the standalone plugins this module replaces. If one is loaded the module stays OFF (AGENTS.md 5.8).</summary>
        string[] ConflictsWith { get; }
        bool DefaultEnabled { get; }
        /// <summary>True while the module is initialised and running. Dispatcher handlers of a module are removed on Shutdown, so hot paths need no extra check.</summary>
        bool Active { get; }

        /// <summary>Bind EVERY config entry here (player-facing on cfg.Main, tuning/debug on cfg.Tools, hotkeys via cfg.Hotkey). Called for every module, enabled or not, before any Init, in module order.</summary>
        void BindConfig(ModuleConfig cfg);
        /// <summary>Once, after BindConfig, only when enabled and no conflicting plugin is present. Apply patches, register scheduler jobs and hooks, load assets.</summary>
        void Init();
        void OnSceneLoaded(Scene scene, LoadSceneMode mode);
        /// <summary>SaveLoadGame reached isPlay after a new game / load / scene reset (first isPlay after a LoadGame or setSeed).</summary>
        void OnWorldReady();
        /// <summary>SaveLoadGame entered "SaveGame". Slot = SaveGameN.es3 file name (may be null if unknown).</summary>
        void OnSaveBegin(string slot);
        /// <summary>SaveLoadGame returned to isPlay after a save. Called immediately; sidecar writes should go through SaveBus.AfterSave (≈1 s later, ES3 cache rule).</summary>
        void OnSaveEnd(string slot);
        /// <summary>SaveLoadGame entered "LoadGame" (a load is starting; cancel pending restores).</summary>
        void OnLoadBegin(string slot);
        /// <summary>FloatingOrigin jumped; delta = oldTotalOffset - newTotalOffset (add it to cached world positions).</summary>
        void OnOriginShift(Vector3 delta);
        /// <summary>Runtime disable (Enabled set to false in the menu), repeated failures, or application quit. Must undo game-state side effects and stop every loop. Patches, jobs and hook handlers registered through Core are removed by Core after this call.</summary>
        void Shutdown();
        /// <summary>Pages shown in the F7 tool window (debug/config tools). May return null.</summary>
        IEnumerable<ToolPage> ToolPages();
    }

    public abstract class ModuleBase : IModule
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public virtual string[] ConflictsWith { get { return Empty; } }
        public virtual bool DefaultEnabled { get { return true; } }
        public bool Active { get; internal set; }
        /// <summary>Config helper for this module; set by Core before BindConfig.</summary>
        public ModuleConfig Config { get; internal set; }
        /// <summary>Asset directory of this module: BepInEx\plugins\WildWasteland\&lt;Id&gt;\ (see Assets).</summary>
        public string Dir { get { return Assets.ModuleDir(Id); } }
        static readonly string[] Empty = new string[0];

        public abstract void BindConfig(ModuleConfig cfg);
        public abstract void Init();
        public virtual void OnSceneLoaded(Scene scene, LoadSceneMode mode) { }
        public virtual void OnWorldReady() { }
        public virtual void OnSaveBegin(string slot) { }
        public virtual void OnSaveEnd(string slot) { }
        public virtual void OnLoadBegin(string slot) { }
        public virtual void OnOriginShift(Vector3 delta) { }
        public virtual void Shutdown() { }
        public virtual IEnumerable<ToolPage> ToolPages() { return null; }
    }
}
