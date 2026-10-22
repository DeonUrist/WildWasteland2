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
        /// <summary>Suppresses native look input only while offline serialization freezes the world.</summary>
        static bool Look(MouseLook action) { return !Serializing; }
        /// <summary>Clears native button results while offline serialization owns input.</summary>
        static bool Block(FsmBool result) { if (!Serializing) return true; if (result != null) result.Value = false; return false; }
        /// <summary>Routes native button-down queries through the serialization input barrier.</summary>
        static bool ButtonDown(GetButtonDown action) { return Block(action.storeResult); }
        /// <summary>Routes native held-button queries through the serialization input barrier.</summary>
        static bool Button(GetButton action) { return Block(action.storeResult); }
        /// <summary>Routes native button-up queries through the serialization input barrier.</summary>
        static bool ButtonUp(GetButtonUp action) { return Block(action.storeResult); }
        /// <summary>Routes native mouse-down queries through the serialization input barrier.</summary>
        static bool MouseDown(GetMouseButtonDown action) { return Block(action.storeResult); }
        /// <summary>Assigns a fresh live identity when a new native singleplayer world starts.</summary>
        static void StateChanged(string previous, string current)
        {
            if (current != "setSeed" || SaveRuntime.SelectedFile != null) return;
            // A new offline world must never inherit the previous world's cache/lineage or overwrite its slot.
            string file = "WW_live-" + Guid.NewGuid().ToString("N") + ".es3";
            ES3Settings.defaultSettings.path = file; SaveRuntime.SetActive(file);
            var variable = GameRefs.SaveLoadGame != null ? GameRefs.SaveLoadGame.FsmVariables.FindFsmString("SaveFile") : null;
            if (variable != null) variable.Value = file;
        }
        /// <summary>Queues one tracked save iterator and initializes its coarse progress state.</summary>
        internal static bool Request()
        {
            if (_requested || Serializing || Loading || !SaveSystem.LocalReady) return false;
            _requested = true; Progress = 0; ProgressText = "Preparing save"; SaveRoutines.Start(Save()); return true;
        }
        /// <summary>Freezes, serializes and publishes a native singleplayer world with unconditional rollback and thaw.</summary>
        static IEnumerator Save()
        {
            float scale = Time.timeScale; int generation = SaveBus.Generation; bool completed = false, blocked = false, began = false, serialized = false;
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
                Presentation(Core.Events.SavePresentationPhase.Begin, .1f, "Waiting for terrain to settle");
                var terrain = SaveTerrain.Prepare(); while (terrain.MoveNext()) yield return terrain.Current;
                if (generation != SaveBus.Generation || !SaveSystem.LocalReady) yield break;
                Presentation(Core.Events.SavePresentationPhase.Progress, .25f, "Serializing world");
                SaveSystem.Begin();
                began = true;
                var before = new Core.Events.BeforeSerialize { Slot = file, Generation = generation, Kind = SaveSystem.Exiting ? SaveKind.Exit : SaveSystem.AutomaticSaving ? SaveKind.Auto : SaveKind.Manual };
                ModEvents.Publish(ref before);
                SaveSystem.Stamp();
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
                completed = SaveSystem.Complete();
            }
            finally
            {
                try { if (!completed && (SaveSystem.Busy || SaveSystem.Requested)) SaveSystem.Abort(new IOException("Offline save did not finish; previous files retained")); }
                finally
                {
                    SaveTerrain.Release(); Serializing = _requested = false;
                    if (began && SaveBus.Generation == generation)
                    {
                        var after = new Core.Events.AfterSerialize { Slot = SaveSystem.ActiveFile, Generation = generation, Kind = SaveSystem.Exiting ? SaveKind.Exit : SaveSystem.AutomaticSaving ? SaveKind.Auto : SaveKind.Manual, Ok = serialized };
                        ModEvents.Publish(ref after);
                    }
                    Presentation(Core.Events.SavePresentationPhase.End, 0, null);
                    if (SaveBus.Generation == generation) Time.timeScale = scale;
                    if (blocked) ApocasetterBridge.SetInputBlock(false);
                }
                if (completed) SaveSystem.Notify(SaveSystem.Status);
            }
        }
        /// <summary>Publishes coarse serializer milestones; no native percentage is inferred.</summary>
        static void Presentation(Core.Events.SavePresentationPhase phase, float fraction, string text)
        {
            Progress = fraction; ProgressText = text;
            var update = new Core.Events.SavePresentation { Phase = phase, Operation = SavePhase.Saving, Fraction = fraction, Text = text };
            ModEvents.Publish(ref update);
        }
        /// <summary>Validates a chosen snapshot before native loading or a tracked return-to-menu flow.</summary>
        internal static void Load(string file)
        {
            if (!SaveSystem.Installed || !SaveSystem.Ready || SaveSystem.RecoveryError != null || SaveSystem.Busy || SaveSystem.Requested || Serializing || Loading) return;
            if (!SaveRuntime.IsSaveFile(file) || !File.Exists(SaveFiles.WorldPath(file))) return;
            if (!SaveRuntime.InMainMenu)
            { if (SaveSystem.LocalReady) SaveRoutines.Start(ReturnAndLoad(file)); return; }
            if (!SaveRuntime.Launch(file)) SaveSystem.Notify("The save could not be loaded. Check the log.");
        }
        /// <summary>Waits for the title scene before launching the selected snapshot and clears loading on cancellation.</summary>
        static IEnumerator ReturnAndLoad(string file)
        {
            Loading = true;
            try
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                while (!SaveRuntime.InMainMenu) yield return null;
                yield return null; yield return null;
                if (!SaveRuntime.Launch(file)) SaveSystem.Notify("The save could not be loaded. Open LOAD GAME to retry.");
            }
            finally { Loading = false; }
        }
    }
}
