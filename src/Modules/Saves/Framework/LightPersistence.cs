using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ES3Internal;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using Patches = WildWasteland.Core.Patches;

namespace WildWasteland.Modules.Saves
{
    /// <summary>Retains native light switches alongside each serialized prefab and restores them after fuel loading.</summary>
    internal static class LightPersistence
    {
        static readonly HashSet<GameObject> Pending = new HashSet<GameObject>();
        const string Prefix = "WildWasteland.LightSwitches.";
        const string FlashlightKey = Prefix + "Player";
        /// <summary>Observes the existing item serializers, including streamed world-history captures.</summary>
        internal static void Install(IModule owner)
        {
            foreach (var type in new[] { typeof(ES3PlayMaker.Save), typeof(ES3PlayMaker.SaveAll), typeof(ES3PlayMaker.Load), typeof(ES3PlayMaker.LoadAll) })
                if (!Patches.Patch(owner, AccessTools.Method(type, "Enter"), postfix: AccessTools.Method(typeof(LightPersistence), type == typeof(ES3PlayMaker.Save) || type == typeof(ES3PlayMaker.SaveAll) ? "Saved" : "Loaded")))
                    throw new InvalidOperationException("Light switch persistence hook failed");
            ModEvents.Subscribe<Core.Events.BeforeSerialize>(owner, BeforeSerialize);
            ModEvents.Subscribe<Core.Events.WorldReady>(owner, WorldReady);
        }
        /// <summary>Releases references at a world boundary or module shutdown.</summary>
        internal static void Clear() { Pending.Clear(); }
        /// <summary>Accepts only native saveable roots, excluding their separately saved hinges and liquid children.</summary>
        static GameObject Root(ES3PlayMaker.SettingsAction action)
        {
            var go = action.Owner;
            return action.Fsm.Name == "saveItemVar" && go != null && go.GetComponent<ES3Prefab>() != null ? go : null;
        }
        /// <summary>Uses the same persistent root identity as native ES3 object serialization.</summary>
        static string Key(GameObject root, bool saving)
        {
            var manager = ES3ReferenceMgrBase.Current; if (manager == null) return null;
            long id = manager.Get(root); if (id < 0 && saving) id = manager.Add(root);
            return id < 0 ? null : Prefix + id;
        }
        /// <summary>Builds an authored child path within the saved prefab without changing names or identifiers.</summary>
        static string Path(Transform node, Transform root)
        {
            string path = "";
            for (; node != root; node = node.parent) path = node.name + (path.Length == 0 ? "" : "/" + path);
            return path;
        }
        /// <summary>Captures the enabled half of paired native stove, can-light and vehicle switches.</summary>
        static void Saved(ES3PlayMaker.SettingsAction __instance)
        {
            var root = Root(__instance); if (root == null) return;
            var rows = new StringBuilder(); var prefab = root.GetComponent<ES3Prefab>();
            foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (fsm.FsmName != "Off" && fsm.FsmName != "LightOn" || fsm.GetComponentInParent<ES3Prefab>() != prefab) continue;
                string other = fsm.FsmName == "Off" ? "On" : "LightOff";
                if (FsmUtil.Find(fsm.gameObject, other) == null) continue;
                rows.Append(Path(fsm.transform, root.transform)).Append('\t').Append(fsm.FsmName).Append('\t').Append(fsm.enabled ? '1' : '0').Append('\n');
            }
            if (rows.Length == 0) return;
            string key = Key(root, true); if (key == null) throw new InvalidOperationException("Light switch root has no ES3 identity");
            ES3.Save<string>(key, rows.ToString(), __instance.GetSettings());
        }
        /// <summary>Defers switches until every root's saved fuel and equipment state has loaded.</summary>
        static void Loaded(ES3PlayMaker.SettingsAction __instance)
        { var root = Root(__instance); if (SaveBus.Loading && root != null) Pending.Add(root); }
        /// <summary>Snapshots the equipped flashlight into the same protected native save cache.</summary>
        static void BeforeSerialize(ref Core.Events.BeforeSerialize save)
        {
            var camera = PlayerState.PlayerCamera; var fsm = camera != null ? FsmUtil.Find(camera.gameObject, "Flashlight") : null;
            ES3.Save<string>(FlashlightKey, fsm != null && fsm.ActiveStateName == "on" ? "1" : "0", new ES3Settings(save.Slot) { location = ES3.Location.Cache });
        }
        /// <summary>Replays only mismatched switches through native ignition, headlight and battery logic.</summary>
        static void WorldReady(ref Core.Events.WorldReady ready)
        {
            if (!ready.FromLoad) { Clear(); return; }
            SaveRoutines.Start(Restore(ready.Slot, ready.Generation));
        }
        /// <summary>Allows native equipment FSMs to finish their first update before replaying the switches.</summary>
        static IEnumerator Restore(string slot, int generation)
        {
            yield return null;
            try
            {
                if (generation != SaveBus.Generation || !SaveSystem.LocalReady) yield break;
                int count = 0;
                foreach (var root in Pending)
                {
                    if (root == null) continue;
                    string key = Key(root, false); if (key == null) continue;
                    string data = SaveBus.Key(key).Read(slot); if (data == null) continue;
                    foreach (string row in data.Split('\n'))
                    {
                        if (row.Length == 0) continue;
                        var fields = row.Split('\t'); if (fields.Length != 3) throw new InvalidOperationException("Invalid saved light switch");
                        var node = fields[0].Length == 0 ? root.transform : root.transform.Find(fields[0]); if (node == null) continue;
                        var on = FsmUtil.Find(node.gameObject, fields[1]); if (on == null) continue;
                        bool desired = fields[2] == "1"; if (on.enabled == desired) continue;
                        var sender = desired ? FsmUtil.Find(node.gameObject, fields[1] == "Off" ? "On" : "LightOff") : on;
                        if (sender != null) { sender.SendEvent(fields[1] == "Off" ? "UseItem" : "useDoor"); count++; }
                    }
                }
                var camera = PlayerState.PlayerCamera; var flashlight = camera != null ? FsmUtil.Find(camera.gameObject, "Flashlight") : null;
                string saved = SaveBus.Key(FlashlightKey).Read(slot);
                if (flashlight != null && saved != null)
                {
                    if (saved == "1" && flashlight.ActiveStateName == "off") flashlight.SendEvent("next");
                    else if (saved == "0" && flashlight.ActiveStateName == "on") flashlight.SendEvent("back");
                }
                Log.Main(SavesModule.Instance, "Load: restored native light switches (" + count + " toggled); flashlight=" + (saved ?? "legacy"));
            }
            finally { Clear(); }
        }
    }
}
