using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using AccessTools = HarmonyLib.AccessTools;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility.Apocasaver
{
    /// <summary>Stands down the standalone save engine for the whole process; ordinary Compatibility switches cannot revive it safely.</summary>
    internal static class ApocasaverSession
    {
        private const string Guid = "com.denis.apocalypter.apocasaver";

        /// <summary>Resolves every target before patching, then commits the irreversible save-engine takeover after guards are installed.</summary>
        internal static void Install()
        {
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out info) || info.Instance == null) return;
            var plugin = info.Instance.GetType(); var runner = plugin.Assembly.GetType("Apocasaver.Runner");
            if (runner == null) throw new InvalidOperationException("Cannot safely disable this Apocasaver version: Runner not found");
            var targets = new List<KeyValuePair<MethodInfo, MethodInfo>>();
            var standDown = AccessTools.Method(typeof(ApocasaverSession), "StandDown");
            foreach (var type in new[] { plugin, runner })
                foreach (string name in type == plugin ? new[] { "Awake", "ApplyPatches", "EnsureRunner", "OnSceneLoaded" } : new[] { "Update", "LateUpdate", "OnGUI", "OnDestroy" })
                    targets.Add(new KeyValuePair<MethodInfo, MethodInfo>(Require(type, name), standDown));
            var poses = plugin.Assembly.GetType("Apocasaver.HandPose");
            if (poses != null)
            {
                targets.Add(new KeyValuePair<MethodInfo, MethodInfo>(Require(poses, "Set", new[] { typeof(string), typeof(string) }), AccessTools.Method(typeof(ApocasaverSession), "SetPose")));
                targets.Add(new KeyValuePair<MethodInfo, MethodInfo>(Require(poses, "Get", new[] { typeof(string) }), AccessTools.Method(typeof(ApocasaverSession), "GetPose")));
            }
            var field = AccessTools.Field(plugin, "_runnerGo");
            var go = field != null ? field.GetValue(null) as GameObject : null;
            var behaviours = go != null ? go.GetComponents<Behaviour>() : new Behaviour[0];
            var attempted = new List<KeyValuePair<MethodInfo, MethodInfo>>();
            bool irreversible = false;
            try
            {
                // Null owner explicitly means the Core/session Harmony id, never Compatibility's removable module id.
                foreach (var target in targets)
                {
                    attempted.Add(target);
                    if (!Patches.Patch(null, target.Key, prefix: target.Value))
                        throw new InvalidOperationException("Cannot install Apocasaver session guard: " + target.Key.Name);
                }
                irreversible = true;
                Patches.RemoveForeignOwner(Guid);
                Patches.RemoveForeignOwner(Guid + ".history");
                info.Instance.enabled = false;
                foreach (var behaviour in behaviours) behaviour.enabled = false;
                Log.Always("[System] Standalone Apocasaver suppressed for this session: runner, scene callbacks and save/history patches disabled; files/settings preserved");
            }
            catch
            {
                if (!irreversible)
                {
                    for (int i = attempted.Count - 1; i >= 0; i--)
                        if (!Patches.UnpatchMethod(null, attempted[i].Key, attempted[i].Value))
                            Log.Error("[Session] Could not roll back Apocasaver guard " + attempted[i].Key.Name + "; restart is required");
                }
                else Log.Error("[Session] Apocasaver takeover failed after foreign patches were removed; session guards retained, restart is required");
                throw;
            }
        }

        /// <summary>Resolves a required standalone method before any takeover side effects occur.</summary>
        private static MethodInfo Require(Type type, string name, Type[] args = null)
        {
            var method = args == null ? AccessTools.Method(type, name) : AccessTools.Method(type, name, args);
            if (method == null) throw new InvalidOperationException("Apocasaver required method not found: " + type.FullName + "." + name);
            return method;
        }

        /// <summary>Suppresses standalone plugin and runner entry points for the remainder of the process.</summary>
        private static bool StandDown() { return false; }

        /// <summary>Replaces HandPose.Set with session metadata under the native stable pose key and skips the standalone implementation.</summary>
        private static bool SetPose(string itemName, string pose)
        { if (!string.IsNullOrEmpty(itemName)) SessionMetadata.Set("apocasaver.pose." + itemName, pose); return false; }

        /// <summary>Replaces HandPose.Get with the session/cache/file lookup while retaining null for an empty or missing item.</summary>
        private static bool GetPose(string itemName, ref string __result)
        { __result = string.IsNullOrEmpty(itemName) ? null : SessionMetadata.Get("apocasaver.pose." + itemName); return false; }
    }
}
