using System;
using ES3Internal;
using HarmonyLib;
using HutongGames.PlayMaker;

namespace WildWasteland.Core
{
    // Preserve Apocasaver's stable hinge keys when its standalone save engine is disabled.
    // Compatibility owns the external HandPose adapter; Core transports its metadata with the original full ES3 keys.
    internal static class SaveCompatibility
    {
        /// <summary>Patches ES3PlayMaker Save/Load.Enter (prefix + finalizer) and clears the pose table on load/new game.</summary>
        internal static void Install(IModule owner)
        {
            if (!Patches.Patch(owner, AccessTools.Method(typeof(ES3PlayMaker.Save), "Enter"), prefix: AccessTools.Method(typeof(SaveCompatibility), "BeforeSave"), finalizer: AccessTools.Method(typeof(SaveCompatibility), "AfterSave"))
                || !Patches.Patch(owner, AccessTools.Method(typeof(ES3PlayMaker.Load), "Enter"), prefix: AccessTools.Method(typeof(SaveCompatibility), "BeforeLoad"), finalizer: AccessTools.Method(typeof(SaveCompatibility), "AfterLoad"))) throw new InvalidOperationException("Stable hinge compatibility hooks failed");
            SaveBus.StateChanged += StateChanged;
        }
        /// <summary>Detaches load resets; the current world's pose values remain valid across a toggle.</summary>
        internal static void Shutdown() { SaveBus.StateChanged -= StateChanged; }
        /// <summary>Clears the session pose table on LoadGame/Start/setSeed.</summary>
        static void StateChanged(string previous, string current) { if (current == "LoadGame" || current == "Start" || current == "setSeed") SessionMetadata.Clear(); }
        /// <summary>Apocasaver stable ES3 key for a hinge saveItemVar FSM on a saved prefab, or null.</summary>
        static string Stable(Fsm fsm)
        {
            if (fsm == null || fsm.Name != "saveItemVar" || fsm.GameObject == null || !fsm.GameObject.name.StartsWith("hinge_", StringComparison.Ordinal) || fsm.GameObject.GetComponentInParent<ES3Prefab>() == null) return null;
            var manager = ES3ReferenceMgrBase.Current; if (manager == null) return null;
            long id = manager.Get(fsm.GameObject); if (id < 0) id = manager.Add(fsm.GameObject);
            return "apocasaver.object." + id + ".hinge-rotation";
        }
        /// <summary>Swaps a hinge rotation key for its stable Apocasaver key during a local save.</summary>
        static void BeforeSave(ES3PlayMaker.Save __instance, out FsmString __state)
        {
            __state = null; if (!SaveSystem.LocalWorld || __instance.value.Type != VariableType.Vector3) return;
            string key = Stable(__instance.Fsm); if (key == null) return;
            __state = __instance.key; __instance.key = new FsmString { Value = key };
        }
        /// <summary>Restores the original key; a save exception fails the save transaction.</summary>
        static Exception AfterSave(ES3PlayMaker.Save __instance, FsmString __state, Exception __exception)
        { if (__state != null) __instance.key = __state; if (__exception != null) SaveSystem.Fail(__exception.Message); return __exception; }
        /// <summary>Loads a hinge rotation from its stable key when the save has one.</summary>
        static void BeforeLoad(ES3PlayMaker.Load __instance, out FsmString __state)
        {
            __state = null; if (__instance.value.Type != VariableType.Vector3) return;
            string key = Stable(__instance.Fsm); if (key == null || !ES3.KeyExists(key, __instance.GetSettings())) return;
            __state = __instance.key; __instance.key = new FsmString { Value = key };
        }
        /// <summary>Restores the original key and lets VehicleLoadPlacement see the loaded stance.</summary>
        static Exception AfterLoad(ES3PlayMaker.Load __instance, FsmString __state, Exception __exception)
        { if (__state != null) __instance.key = __state; if (__exception == null) VehicleLoadPlacement.LoadedStance(__instance); return __exception; }
        /// <summary>Writes the session HandPose table into the active file's ES3 cache.</summary>
        internal static void SavePoses()
        { SessionMetadata.Save(SaveSystem.ActiveFile); }
    }
}
