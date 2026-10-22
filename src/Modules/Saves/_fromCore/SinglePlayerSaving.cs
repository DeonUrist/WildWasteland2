using System;
using System.Collections;
using System.IO;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Core
{
    // Offline uses the native serializer and the same journal/publication path, without network ownership steps.
    internal static class SinglePlayerSaving
    {
        internal static bool Serializing { get; private set; }
        static bool _requested;
        internal static void Install()
        {
            SaveBus.StateChanged += StateChanged;
            Hooks.MouseLookDo.Prefix(null, Look);
            Hooks.GetButtonDownUpdate.Prefix(null, ButtonDown);
            Hooks.GetButtonDo.Prefix(null, Button);
            Hooks.GetButtonUpUpdate.Prefix(null, ButtonUp);
            Hooks.GetMouseButtonDownUpdate.Prefix(null, MouseDown);
        }
        static bool Look(MouseLook action) { return !Serializing; }
        static bool Block(FsmBool result) { if (!Serializing) return true; if (result != null) result.Value = false; return false; }
        static bool ButtonDown(GetButtonDown action) { return Block(action.storeResult); }
        static bool Button(GetButton action) { return Block(action.storeResult); }
        static bool ButtonUp(GetButtonUp action) { return Block(action.storeResult); }
        static bool MouseDown(GetMouseButtonDown action) { return Block(action.storeResult); }
        static void StateChanged(string previous, string current)
        {
            if (current != "setSeed" || SaveRuntime.SelectedFile != null) return;
            // A new offline world must never inherit the previous world's cache/lineage or overwrite its slot.
            string file = "WW_live-" + Guid.NewGuid().ToString("N") + ".es3";
            ES3Settings.defaultSettings.path = file; SaveRuntime.SetActive(file);
            var variable = GameRefs.SaveLoadGame != null ? GameRefs.SaveLoadGame.FsmVariables.FindFsmString("SaveFile") : null;
            if (variable != null) variable.Value = file;
        }
        internal static bool Request()
        {
            if (_requested || Serializing || !SaveSystem.LocalReady) return false;
            _requested = true; Scheduler.StartCoroutine(Save()); return true;
        }
        static IEnumerator Save()
        {
            float scale = Time.timeScale; int generation = SaveBus.Generation; bool completed = false, blocked = false;
            try
            {
                // Let an originating native multi-root save action finish being suppressed before starting ours.
                yield return null;
                if (generation != SaveBus.Generation || !SaveSystem.LocalReady) yield break;
                if (SaveSystem.DeferScheduledSave()) yield break;
                string file = SaveSystem.ActiveFile;
                var registry = GameRefs.NewGOArrayList != null ? FsmUtil.Find(GameRefs.NewGOArrayList, "Save_NewGO_ArrayList") : null;
                if (registry == null) throw new IOException("Native object registry is unavailable");
                GameRefs.SaveLoadGame.FsmVariables.FindFsmString("SaveFile").Value = file;
                registry.FsmVariables.FindFsmString("SaveFile").Value = file;
                ES3Settings.defaultSettings.path = file;
                Serializing = true; Time.timeScale = 0;
                blocked = !ApocasetterBridge.InputBlocked && ApocasetterBridge.SetInputBlock(true);
                var terrain = SaveTerrain.Prepare(); while (terrain.MoveNext()) yield return terrain.Current;
                if (generation != SaveBus.Generation || !SaveSystem.LocalReady) yield break;
                SaveSystem.Begin(); SaveSystem.Stamp();
                // Activate objects exactly as the native save menu does before serializing all roots.
                var activate = FsmUtil.Find(GameRefs.GameManager, "SaveGameActivateItems"); if (activate != null) activate.SendEvent("Activate");
                PlayMakerFSM.BroadcastEvent("SaveGame");
                float deadline = Time.unscaledTime + 120f; bool observed = false;
                while (SaveBus.Generation == generation && Time.unscaledTime < deadline)
                {
                    Time.timeScale = 0;
                    if (SaveBus.Saving || SaveBus.State == "SaveGame") observed = true;
                    if (observed && SaveBus.State == "isPlay" && registry.ActiveStateName == "off" && !SaveBus.PendingAfterSave) break;
                    yield return null;
                }
                if (!observed || SaveBus.Generation != generation || SaveBus.State != "isPlay" || registry.ActiveStateName != "off" || SaveBus.PendingAfterSave)
                    throw new IOException("Offline serialization did not finish; previous files retained");
                completed = SaveSystem.Complete();
            }
            finally
            {
                try { if (!completed && (SaveSystem.Busy || SaveSystem.Requested)) SaveSystem.Abort(new IOException("Offline save did not finish; previous files retained")); }
                finally
                {
                    SaveTerrain.Release(); Serializing = _requested = false;
                    if (SaveBus.Generation == generation) Time.timeScale = scale;
                    if (blocked) ApocasetterBridge.SetInputBlock(false);
                }
                if (completed) SaveSystem.Notify(SaveSystem.Status);
            }
        }
        internal static void Load(string file)
        {
            if (!SaveSystem.Ready || SaveSystem.RecoveryError != null || SaveSystem.Busy || SaveSystem.Requested) return;
            if (!SaveRuntime.IsSaveFile(file) || !File.Exists(SaveFiles.WorldPath(file))) return;
            if (!SaveRuntime.InMainMenu)
            { if (SaveSystem.LocalReady) Scheduler.StartCoroutine(ReturnAndLoad(file)); return; }
            if (!SaveRuntime.Launch(file)) SaveSystem.Notify("The save could not be loaded. Check the log.");
        }
        static IEnumerator ReturnAndLoad(string file)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            while (!SaveRuntime.InMainMenu) yield return null;
            yield return null; yield return null;
            if (!SaveRuntime.Launch(file)) SaveSystem.Notify("The save could not be loaded. Open LOAD GAME to retry.");
        }
    }
}
