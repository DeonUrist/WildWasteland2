using WildWasteland.Core;
using UI = WildWasteland.Core.UI;
using Events = WildWasteland.Core.Events;
using System;
using System.Collections;
using System.IO;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.Saves
{
    // Offline uses the native serializer and the same journal/publication path, without network ownership steps.
    internal static class SinglePlayerSaving
    {
        internal static bool Serializing { get; private set; }
        static bool _requested;
        /// <summary>True while returning to the menu to launch a chosen snapshot.</summary>
        internal static bool Loading { get; private set; }
        /// <summary>Fraction of the completed save milestones, not a native serializer percentage.</summary>
        internal static float Progress { get; private set; }
        /// <summary>Current save milestone shown through ISaves.</summary>
        internal static string ProgressText { get; private set; }
        /// <summary>Owns native input suppression and new-world identity selection while Saves runs.</summary>
        internal static void Install(IModule owner)
        {
            SaveBus.StateChanged += StateChanged;
            Hooks.MouseLookDo.Prefix(owner, Look);
            Hooks.GetButtonDownUpdate.Prefix(owner, ButtonDown);
            Hooks.GetButtonDo.Prefix(owner, Button);
            Hooks.GetButtonUpUpdate.Prefix(owner, ButtonUp);
            Hooks.GetMouseButtonDownUpdate.Prefix(owner, MouseDown);
        }
        /// <summary>Detaches new-world identity selection after running routines have been disposed.</summary>
        internal static void Shutdown() { SaveBus.StateChanged -= StateChanged; _requested = false; }
        static bool Look(MouseLook action) { return !Serializing; }
        static bool Block(FsmBool result) { if (!Serializing) return true; if (result != null) result.Value = false; return false; }
        static bool ButtonDown(GetButtonDown action) { return Block(action.storeResult); }
        static bool Button(GetButton action) { return Block(action.storeResult); }
        static bool ButtonUp(GetButtonUp action) { return Block(action.storeResult); }
        static bool MouseDown(GetMouseButtonDown action) { return Block(action.storeResult); }
        static void StateChanged(string previous, string current)
        {
            if (current != "setSeed" || SavesRuntime.SelectedFile != null) return;
            // A new offline world must never inherit the previous world's cache/lineage or overwrite its slot.
            string file = "WW_live-" + Guid.NewGuid().ToString("N") + ".es3";
            ES3Settings.defaultSettings.path = file; SavesRuntime.SetActive(file);
            var variable = GameRefs.SaveLoadGame != null ? GameRefs.SaveLoadGame.FsmVariables.FindFsmString("SaveFile") : null;
            if (variable != null) variable.Value = file;
        }
        internal static bool Request()
        {
            if (_requested || Serializing || Loading || !SavesSystem.LocalReady) return false;
            _requested = true; Progress = 0; ProgressText = "Preparing save"; SaveRoutines.Start(Save()); return true;
        }
        static IEnumerator Save()
        {
            float scale = Time.timeScale; int generation = SaveBus.Generation; bool completed = false, blocked = false, began = false, serialized = false;
            try
            {
                // Let an originating native multi-root save action finish being suppressed before starting ours.
                yield return null;
                if (generation != SaveBus.Generation || !SavesSystem.LocalReady) yield break;
                if (SavesSystem.DeferScheduledSave()) yield break;
                string file = SavesSystem.ActiveFile;
                var registry = GameRefs.NewGOArrayList != null ? FsmUtil.Find(GameRefs.NewGOArrayList, "Save_NewGO_ArrayList") : null;
                if (registry == null) throw new IOException("Native object registry is unavailable");
                GameRefs.SaveLoadGame.FsmVariables.FindFsmString("SaveFile").Value = file;
                registry.FsmVariables.FindFsmString("SaveFile").Value = file;
                ES3Settings.defaultSettings.path = file;
                Serializing = true; Time.timeScale = 0;
                blocked = !ApocasetterBridge.InputBlocked && ApocasetterBridge.SetInputBlock(true);
                Presentation(Core.Events.SavePresentationPhase.Begin, .1f, "Waiting for terrain to settle");
                var terrain = SaveTerrain.Prepare(); while (terrain.MoveNext()) yield return terrain.Current;
                if (generation != SaveBus.Generation || !SavesSystem.LocalReady) yield break;
                Presentation(Core.Events.SavePresentationPhase.Progress, .25f, "Serializing world");
                SavesSystem.Begin();
                began = true;
                var before = new Core.Events.BeforeSerialize { Slot = file, Generation = generation, Kind = SavesSystem.Exiting ? SaveKind.Exit : SavesSystem.AutomaticSaving ? SaveKind.Auto : SaveKind.Manual };
                ModEvents.Publish(ref before);
                SavesSystem.Stamp();
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
                serialized = true;
                Presentation(Core.Events.SavePresentationPhase.Progress, .75f, "Verifying and publishing save");
                completed = SavesSystem.Complete();
            }
            finally
            {
                try { if (!completed && (SavesSystem.Busy || SavesSystem.Requested)) SavesSystem.Abort(new IOException("Offline save did not finish; previous files retained")); }
                finally
                {
                    SaveTerrain.Release(); Serializing = _requested = false;
                    if (began && SaveBus.Generation == generation)
                    {
                        var after = new Core.Events.AfterSerialize { Slot = SavesSystem.ActiveFile, Generation = generation, Kind = SavesSystem.Exiting ? SaveKind.Exit : SavesSystem.AutomaticSaving ? SaveKind.Auto : SaveKind.Manual, Ok = serialized };
                        ModEvents.Publish(ref after);
                    }
                    Presentation(Core.Events.SavePresentationPhase.End, 0, null);
                    if (SaveBus.Generation == generation) Time.timeScale = scale;
                    if (blocked) ApocasetterBridge.SetInputBlock(false);
                }
                if (completed) SavesSystem.Notify(SavesSystem.Status);
            }
        }
        /// <summary>Publishes coarse serializer milestones; no native percentage is inferred.</summary>
        static void Presentation(Core.Events.SavePresentationPhase phase, float fraction, string text)
        {
            Progress = fraction; ProgressText = text;
            var update = new Core.Events.SavePresentation { Phase = phase, Operation = SavePhase.Saving, Fraction = fraction, Text = text };
            ModEvents.Publish(ref update);
        }
        internal static void Load(string file)
        {
            if (!SavesSystem.Installed || !SavesSystem.Ready || SavesSystem.RecoveryError != null || SavesSystem.Busy || SavesSystem.Requested || Serializing || Loading) return;
            if (!SavesRuntime.IsSaveFile(file) || !File.Exists(SaveFiles.WorldPath(file))) return;
            if (!SavesRuntime.InMainMenu)
            { if (SavesSystem.LocalReady) SaveRoutines.Start(ReturnAndLoad(file)); return; }
            if (!SavesRuntime.Launch(file)) SavesSystem.Notify("The save could not be loaded. Check the log.");
        }
        static IEnumerator ReturnAndLoad(string file)
        {
            Loading = true;
            try
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                while (!SavesRuntime.InMainMenu) yield return null;
                yield return null; yield return null;
                if (!SavesRuntime.Launch(file)) SavesSystem.Notify("The save could not be loaded. Open LOAD GAME to retry.");
            }
            finally { Loading = false; }
        }
    }
}
