using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Core
{
    // Loaded plugins keep runners/coroutines/patches: disable through the installer's pre-load operation.
    internal static partial class ReplacementGuard
    {
        // Wave A split (refactor): the title notice (GUI job, text, drawing with the CleanHUD theme) lives in the HUD part
        // src/Modules/HUD/_fromCore/ReplacementGuard.HUD.cs; detection and disabling stay in Core.
        /// <summary>HUD part: registers the title-notice GUI job.</summary>
        static partial void InstallNotice();
        /// <summary>HUD part: rebuilds the restart notice text from the replaced-mod list.</summary>
        static partial void RebuildNotice();

        static readonly Dictionary<string, string> _status = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly List<string> _names = new List<string>();
        static bool _title;
        internal static bool MenuModalOpen;
        static int _failed;
        static readonly string[,] Recommended = {
            { "apocalypter.worldamp", "Vista World Gen", "73" },
            { "com.fived.apocalypter.fallout_nv_radio", "Apocalypter Radio", "74" },
            { "local.apocalypter.chasecamera", "ApocaChaseCam", "44" },
            { "apocalypter.dogcompanion", "Dog Companion", "55" },
            { "com.denis.apocalypter.apocapocket", "Apocapocket", "7" }
        };
        static readonly List<int> _missing = new List<int>();
        static bool _checked;
        static ConfigEntry<bool> _showRecommended;

        /// <summary>Before modules start: binds [Debug] Recommended mods, queues the disable of every loaded replaced standalone (ConflictsWith GUIDs + Immersive Map, Women of Wasteland, HealthStacks, iTweenGuard) and starts the notice.</summary>
        internal static void Prepare()
        {
            _showRecommended = TunedSettings.Bind(Plugin.Settings, "Debug", "Recommended mods", false, "Show optional mod recommendations at the title menu.");
            foreach (var entry in ModuleHost.Entries)
            {
                var guids = entry.Module.ConflictsWith;
                if (guids != null) foreach (string guid in guids) Disable(guid);
            }
            Disable("local.immersivemap");
            Disable("com.denis.apocalypter.womenofwasteland");
            Disable(HealthStacks.GUID);
            Disable(ITweenGuard.GUID);
            Scheduler.Interval(null, "Core.Replacement notice", .5f, Tick).RunNow();
            InstallNotice();
            RebuildNotice();
        }
        /// <summary>A conflict found after Prepare (late-loaded plugin): disable it and refresh the notice.</summary>
        internal static void LateConflict(string guid) { Disable(guid); RebuildNotice(); }

        /// <summary>Queues the standalone plugin guid for disable through Apocasetter (configs kept) and records its status once.</summary>
        static void Disable(string guid)
        {
            if (_status.ContainsKey(guid)) return;
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(guid, out info) || ReferenceEquals(info.Instance, null)) return;
            string error;
            bool queued = ApocasetterBridge.QueueDisable(info, out error);
            _status[guid] = queued ? "disable queued in Apocasetter; restart the game for Wild Wasteland to take over"
                : "automatic disable unavailable (" + error + "); disable the standalone mod and restart";
            _names.Add(info.Metadata.Name + (queued ? "" : " (disable manually)"));
            if (!queued) _failed++;
            Log.Warn("[Core] Replacing " + info.Metadata.Name + ": " + _status[guid] + ". Configs are preserved.");
        }

        /// <summary>Status text for a replaced standalone (Modules page "Standalone replacement" line).</summary>
        internal static string Status(string guid)
        {
            string status;
            return _status.TryGetValue(guid, out status) ? status : "disable the standalone mod and restart";
        }

        /// <summary>Tracks whether the title scene is active (notice visibility).</summary>
        internal static void SceneLoaded(Scene scene) { _title = scene.buildIndex == 0; }
        /// <summary>0.5 s job: title-scene flag; once after chainloading, counts the recommended mods that are not active.</summary>
        static void Tick()
        {
            SceneLoaded(SceneManager.GetActiveScene());
            if (_checked) return;
            _checked = true;
            for (int i = 0; i < Recommended.GetLength(0); i++)
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue(Recommended[i, 0], out info) || ReferenceEquals(info.Instance, null)) _missing.Add(i);
            }
            Log.Always("[Core] Recommended mods not active: " + _missing.Count);
        }
    }
}
