using WildWasteland.Core;
using UI = WildWasteland.Core.UI;
using Events = WildWasteland.Core.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.Saves
{
    // Standalone native-menu adapters. No multiplayer assembly, player identity or redirected save root.
    // Canonical player/placement queries belong to Core; native save menu interception belongs to Saves.
    internal static class SaveRuntime
    {
        static readonly Regex FileName = new Regex(@"^(SaveGame([1-9]|10)|WW_(live|save|auto)-[a-fA-F0-9]{32})\.es3$", RegexOptions.IgnoreCase);
        internal static string ActiveFile, SelectedFile;
        static readonly FsmEvent Clicked = FsmEvent.GetFsmEvent("Clicked");
        static bool _leaving;
        static int _latestVersion = -1;
        static string _latestFile;
        static int _menuFrame = -1;
        static bool _inMenu;
        /// <summary>True in the MainMenu scene; the scene name (an allocating native call) is read at most once per frame.</summary>
        internal static bool InMainMenu
        {
            get
            {
                int frame = Time.frameCount;
                if (frame != _menuFrame) { _menuFrame = frame; _inMenu = SceneManager.GetActiveScene().name == "MainMenu"; }
                return _inMenu;
            }
        }
        /// <summary>True for WW save identities (SaveGame1..10.es3, WW_live/save/auto-<32 hex>.es3).</summary>
        internal static bool IsSaveFile(string file) { return file != null && FileName.IsMatch(file); }
        /// <summary>Native player in-vehicle transition state (canonical Core PlayerState).</summary>
        internal static bool InCar { get { return PlayerState.InVehicleTransition; } }
        /// <summary>Current player health from the shared cached native state (canonical Core PlayerState).</summary>
        internal static float Health() { return PlayerState.Health; }
        /// <summary>Distance from position to the player (infinity without a player).</summary>
        internal static float PlayerDistance(Vector3 position, Transform player) { return player != null ? Vector3.Distance(position, player.position) : float.PositiveInfinity; }
        /// <summary>Sets the active save file (validated identity).</summary>
        internal static void SetActive(string file) { if (!IsSaveFile(file)) throw new IOException("Invalid save identity"); ActiveFile = file; }
        /// <summary>Installs module-owned native-menu/scene observers and the temporary HUD presentation adapter.</summary>
        internal static void Install(IModule owner)
        {
            ActiveFile = SaveBus.Slot; SelectedFile = null; _latestVersion = -1;
            Hooks.ProcessEvent.Prefix(owner, NativeMenuEvent);
            SaveBus.StateChanged += StateChanged;
            SceneManager.sceneLoaded += SceneLoaded;
            Core.SaveRuntime.InstallPresentation(owner);
        }
        /// <summary>Detaches scene/native state observation; current-world identity survives a runtime toggle.</summary>
        internal static void Shutdown() { SaveBus.StateChanged -= StateChanged; SceneManager.sceneLoaded -= SceneLoaded; SelectedFile = null; _leaving = false; _latestVersion = -1; }
        /// <summary>Clears cached player FSMs; the main menu clears the active file and bumps the save catalogue.</summary>
        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        { _menuFrame = -1; if (scene.name == "MainMenu") { ActiveFile = null; SaveSystem.CatalogueVersion++; } }
        /// <summary>LoadGame makes the selected (or native slot) file active; isPlay clears the selection.</summary>
        static void StateChanged(string previous, string current)
        {
            if (current == "LoadGame")
            {
                string file = SelectedFile ?? SaveBus.Slot;
                if (IsSaveFile(file)) SetActive(file);
            }
            if (current == "isPlay") SelectedFile = null;
        }
        /// <summary>Published save snapshots in the ES3 folder (no live files, no near-empty files).</summary>
        internal static IEnumerable<SaveSystem.SaveInfo> Saves()
        {
            if (!Directory.Exists(SaveFiles.WorldRoot)) yield break;
            foreach (string path in Directory.GetFiles(SaveFiles.WorldRoot, "*.es3"))
            {
                string file = Path.GetFileName(path);
                if (!IsSaveFile(file) || file.StartsWith("WW_live-", StringComparison.OrdinalIgnoreCase) || new FileInfo(path).Length <= 1024) continue;
                yield return SaveSystem.Info(file);
            }
        }
        /// <summary>Newest published save (cached per catalogue version).</summary>
        internal static string Latest()
        {
            if (_latestVersion == SaveSystem.CatalogueVersion) return _latestFile;
            _latestVersion = SaveSystem.CatalogueVersion;
            string file = null; DateTime best = DateTime.MinValue;
            foreach (var save in Saves()) { var stamp = File.GetLastWriteTimeUtc(SaveFiles.WorldPath(save.File)); if (stamp > best) { best = stamp; file = save.File; } }
            _latestFile = file; return file;
        }
        /// <summary>Loads file through the native loadgame FSM (main menu only).</summary>
        internal static bool Launch(string file)
        {
            if (!InMainMenu || !IsSaveFile(file) || !File.Exists(SaveFiles.WorldPath(file))) return false;
            // Use the native loadgame FSM: it owns scene loading, global restoration and terrain admission.
            var canvas = GameObject.Find("LoadGame_Canvas");
            var node = canvas != null ? canvas.transform.Find("LoadGame/loadgame") : null;
            var fsm = node != null ? FsmUtil.Find(node.gameObject, "NewGame") : null;
            if (fsm == null || !fsm.Fsm.Initialized) return false;
            var variable = fsm.FsmVariables.FindFsmString("SaveFile"); if (variable == null) return false;
            SelectedFile = file; SetActive(file); ES3Settings.defaultSettings.path = file; variable.Value = file;
            fsm.gameObject.SetActive(true); fsm.enabled = true; fsm.SendEvent("Clicked"); return true;
        }
        /// <summary>ProcessEvent prefix: redirects native save/load/quit menu clicks to the WW save library, exit save and newest-snapshot load.</summary>
        static bool NativeMenuEvent(Fsm fsm, FsmEvent ev, FsmEventData data)
        {
            if (!SaveSystem.Installed || fsm == null || ev == null || !ReferenceEquals(ev, Clicked) || fsm.GameObject == null || SinglePlayerSaving.Serializing) return true;
            string name = fsm.GameObject.name;
            if (SaveSystem.LocalReady && fsm.Name == "Continue" && (name.StartsWith("save_game_", StringComparison.Ordinal) || name == "savegame" || name == "Yes_Save" || name == "Save_Game_slots" || name == "Save_Game"))
            { WildWasteland.Modules.Saves.SaveLibraryPage.Open(true); return false; }
            if (SaveSystem.LocalReady && !_leaving && name == "Yes_Quit")
            {
                SaveSystem.SaveBeforeExit(() => { _leaving = true; try { fsm.Event("Clicked"); } finally { _leaving = false; } }); return false;
            }
            if (InMainMenu && fsm.Name == "NewGame" && name == "loadgame")
            {
                if (!SaveSystem.Ready) { WildWasteland.Modules.Saves.SaveLibraryPage.Open(false); return false; }
                var selected = fsm.Variables.FindFsmString("SaveFile");
                if (selected != null && IsSaveFile(selected.Value)) { SelectedFile = selected.Value; SetActive(selected.Value); }
            }
            if (InMainMenu && fsm.Name == "NewGame" && name == "LoadGameSlots")
            { WildWasteland.Modules.Saves.SaveLibraryPage.Open(false); return false; }
            // Older native single-file LOAD/Continue buttons also choose the newest published SP snapshot.
            if (InMainMenu && fsm.Name == "NewGame" && name == "LoadGame" && fsm.GameObject.transform.parent != null && fsm.GameObject.transform.parent.name == "MainMenu")
            { var file = Latest(); if (file != null) Launch(file); return false; }
            return true;
        }
    }
}
