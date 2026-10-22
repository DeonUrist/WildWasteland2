using Patches = WildWasteland.Core.Patches;
using WildWasteland.Core;
using UI = WildWasteland.Core.UI;
using Events = WildWasteland.Core.Events;
using System;
using ES3Internal;
using HarmonyLib;
using HutongGames.PlayMaker;

namespace WildWasteland.Modules.Saves
{
    /// <summary>Saves native hinge rotations under stable object identities inside the selected world file.</summary>
    internal static class HingePersistence
    {
        /// <summary>Patches ES3PlayMaker Save/Load.Enter with reversible stable-key substitutions.</summary>
        internal static void Install(IModule owner)
        {
            if (!Patches.Patch(owner, AccessTools.Method(typeof(ES3PlayMaker.Save), "Enter"), prefix: AccessTools.Method(typeof(HingePersistence), "BeforeSave"), finalizer: AccessTools.Method(typeof(HingePersistence), "AfterSave"))
                || !Patches.Patch(owner, AccessTools.Method(typeof(ES3PlayMaker.Load), "Enter"), prefix: AccessTools.Method(typeof(HingePersistence), "BeforeLoad"), finalizer: AccessTools.Method(typeof(HingePersistence), "AfterLoad"))) throw new InvalidOperationException("Stable hinge persistence hooks failed");
        }
        /// <summary>Returns a stable rotation key for a saved native hinge, retaining the existing persistence identifier.</summary>
        static string Stable(Fsm fsm)
        {
            if (fsm == null || fsm.Name != "saveItemVar" || fsm.GameObject == null || !fsm.GameObject.name.StartsWith("hinge_", StringComparison.Ordinal) || fsm.GameObject.GetComponentInParent<ES3Prefab>() == null) return null;
            var manager = ES3ReferenceMgrBase.Current; if (manager == null) return null;
            long id = manager.Get(fsm.GameObject); if (id < 0) id = manager.Add(fsm.GameObject);
            return "apocasaver.object." + id + ".hinge-rotation";
        }
        /// <summary>Swaps a hinge rotation key for its stable object key during a local save.</summary>
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
    }
}
