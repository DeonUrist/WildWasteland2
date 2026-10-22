using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Core
{
    // Standalone native-menu adapters. No multiplayer assembly, player identity or redirected save root.
    internal static class SaveRuntime
    {
        static readonly Regex FileName = new Regex(@"^(SaveGame([1-9]|10)|WW_(live|save|auto)-[a-fA-F0-9]{32})\.es3$", RegexOptions.IgnoreCase);
        internal static string ActiveFile, SelectedFile;
        static PlayMakerFSM _health, _inCar;
        static bool _leaving;
        static int _latestVersion = -1;
        static string _latestFile;
        internal static bool InMainMenu { get { return SceneManager.GetActiveScene().name == "MainMenu"; } }
        internal static bool InCar
        {
            get { if (_inCar == null && GameRefs.Player != null) _inCar = FsmUtil.Find(GameRefs.Player, "InCar"); return _inCar != null && _inCar.ActiveStateName != "OnFoot"; }
        }
        internal static bool IsSaveFile(string file) { return file != null && FileName.IsMatch(file); }
        internal static float Health()
        {
            if (_health == null && GameRefs.Player != null) _health = FsmUtil.Find(GameRefs.Player, "Health");
            var value = _health != null ? _health.FsmVariables.FindFsmFloat("Health") : null;
            return value != null ? value.Value : 0;
        }
        internal static float PlayerDistance(Vector3 position, Transform player) { return player != null ? Vector3.Distance(position, player.position) : float.PositiveInfinity; }
        internal static void SetActive(string file) { if (!IsSaveFile(file)) throw new IOException("Invalid save identity"); ActiveFile = file; }
        internal static void Install()
        {
            Hooks.ProcessEvent.Prefix(null, NativeMenuEvent);
            SaveBus.StateChanged += StateChanged;
            SceneManager.sceneLoaded += SceneLoaded;
            InventorySupplies.Install();
            Scheduler.Gui(null, "Native saving fallback", DrawSaving, WantsSaving);
        }
        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        { _health = _inCar = null; if (scene.name == "MainMenu") { ActiveFile = null; SaveSystem.CatalogueVersion++; } }
        static void StateChanged(string previous, string current)
        {
            if (current == "LoadGame")
            {
                string file = SelectedFile ?? SaveBus.Slot;
                if (IsSaveFile(file)) SetActive(file);
            }
            if (current == "isPlay") SelectedFile = null;
        }
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
        internal static string Latest()
        {
            if (_latestVersion == SaveSystem.CatalogueVersion) return _latestFile;
            _latestVersion = SaveSystem.CatalogueVersion;
            string file = null; DateTime best = DateTime.MinValue;
            foreach (var save in Saves()) { var stamp = File.GetLastWriteTimeUtc(SaveFiles.WorldPath(save.File)); if (stamp > best) { best = stamp; file = save.File; } }
            _latestFile = file; return file;
        }
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
        static bool NativeMenuEvent(Fsm fsm, FsmEvent ev, FsmEventData data)
        {
            if (!SaveSystem.Installed || fsm == null || ev == null || ev.Name != "Clicked" || fsm.GameObject == null || SinglePlayerSaving.Serializing) return true;
            string name = fsm.GameObject.name;
            if (SaveSystem.LocalReady && fsm.Name == "Continue" && (name.StartsWith("save_game_", StringComparison.Ordinal) || name == "savegame" || name == "Yes_Save" || name == "Save_Game_slots" || name == "Save_Game"))
            { UI.ToolWindow.OpenSaves(true); return false; }
            if (SaveSystem.LocalReady && !_leaving && name == "Yes_Quit")
            {
                SaveSystem.SaveBeforeExit(() => { _leaving = true; try { fsm.Event("Clicked"); } finally { _leaving = false; } }); return false;
            }
            if (InMainMenu && fsm.Name == "NewGame" && name == "loadgame")
            {
                if (!SaveSystem.Ready) { UI.ToolWindow.OpenSaves(false); return false; }
                var selected = fsm.Variables.FindFsmString("SaveFile");
                if (selected != null && IsSaveFile(selected.Value)) { SelectedFile = selected.Value; SetActive(selected.Value); }
            }
            if (InMainMenu && fsm.Name == "NewGame" && name == "LoadGameSlots")
            { UI.ToolWindow.OpenSaves(false); return false; }
            // Older native single-file LOAD/Continue buttons also choose the newest published SP snapshot.
            if (InMainMenu && fsm.Name == "NewGame" && name == "LoadGame" && fsm.GameObject.transform.parent != null && fsm.GameObject.transform.parent.name == "MainMenu")
            { var file = Latest(); if (file != null) Launch(file); return false; }
            return true;
        }
        static bool WantsSaving() { return SinglePlayerSaving.Serializing; }
        static void DrawSaving()
        {
            if (LoadingProgress.ScreenVisible || Event.current.type != EventType.Repaint) return;
            int depth = GUI.depth; GUI.depth = -10000; var color = GUI.color;
            GUI.color = Color.black; GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = color;
            Modules.CleanHUD.GlyphFont.DrawGame(new Rect(0, Screen.height * .42f, Screen.width, 90), SaveSystem.AutomaticSaving ? "AUTOSAVING" : "SAVING", 48 * Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), new Color(.886f, .749f, .424f), TextAnchor.MiddleCenter);
            GUI.depth = depth;
        }
        internal static bool Surface(Vector3 center, Transform player, out Vector3 position)
        {
            // Main terrain must have replaced the draft before any object/floor is used as support.
            RaycastHit terrain;
            if (!Physics.Raycast(center + Vector3.up * 1500f, Vector3.down, out terrain, 3000f, 1 << 14, QueryTriggerInteraction.Ignore) || terrain.collider.name == "Draft Terrain")
            { position = Vector3.zero; return false; }
            var tile = terrain.collider.GetComponentInParent<MapMagic.Terrains.TerrainTile>();
            if (!(terrain.collider is TerrainCollider) || !terrain.collider.enabled || !terrain.collider.gameObject.activeInHierarchy
                || tile != null && (tile.main == null || !tile.main.applyReady || tile.main.terrain == null || terrain.collider.gameObject != tile.main.terrain.gameObject))
            { position = Vector3.zero; return false; }
            for (int ring = 0; ring < 5; ring++) for (int step = 0; step < 12; step++)
            {
                float angle = step * Mathf.PI / 6f, radius = ring == 0 ? 0 : ring * 1.5f;
                var probe = center + new Vector3(Mathf.Cos(angle) * radius, 4f, Mathf.Sin(angle) * radius);
                int count = Physics.RaycastNonAlloc(probe, Vector3.down, SurfaceHits, 40f, SurfaceMask, QueryTriggerInteraction.Ignore);
                if (count >= SurfaceHits.Length) continue;
                for (int i = 0; i < count; i++)
                {
                    var hit = SurfaceHits[i]; if (hit.normal.y < .7f || hit.collider.name == "Draft Terrain" || hit.collider.transform.IsChildOf(player) || hit.point.y < terrain.point.y - .5f) continue;
                    var feet = hit.point + Vector3.up * .08f;
                    int blocked = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * .35f, feet + Vector3.up * 1.55f, .32f, Capsule, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    bool free = blocked < Capsule.Length;
                    for (int j = 0; j < blocked && j < Capsule.Length; j++) if (!Capsule[j].transform.IsChildOf(player)) { free = false; break; }
                    if (!free) continue;
                    position = hit.point + Vector3.up * 1.05f; return true;
                }
            }
            position = Vector3.zero; return false;
        }
        const int SurfaceMask = (1 << 0) | (1 << 8) | (1 << 14) | (1 << 16);
        static readonly Collider[] Capsule = new Collider[32];
        static readonly RaycastHit[] SurfaceHits = new RaycastHit[32];
    }
}
