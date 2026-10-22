using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Patches = WildWasteland.Core.Patches;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Apocatalker install part of TalkBridge (Wave A, from Quests/NativeConversation.cs; B17): detects Talk, patches Talk Open/Close, links TalkApi and undoes it all.</summary>
    internal partial class TalkBridge : IQuestConversation
    {
        internal void InstallOptional(QuestsModule owner)
        {
            Prompt = ""; ExternalAvailable = false;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(Plugin.TalkGuid, out info) || ReferenceEquals(info.Instance, null)) return;
            if (!Init(info.Instance.GetType())) { Plugin.Warn("[Quests] Talk adapter unavailable; using native quest interaction."); return; }
            Bind(info.Instance); ExternalAvailable = true;
            if (!Patches.Patch(owner, AccessTools.Method(TalkType, "Open"), postfix: AccessTools.Method(typeof(TalkBridge), "Opened"))
                || !Patches.Patch(owner, AccessTools.Method(TalkType, "Close"), postfix: AccessTools.Method(typeof(TalkBridge), "Closed")))
                throw new InvalidOperationException("Optional Talk conversation hooks unavailable.");
            TalkApiLink.Init(info.Instance.GetType().Assembly, Plugin.I, "OnTalkAction");
            Core.Log.Always("[Quests] Optional Apocatalker content adapter ready.");
        }

        static void Opened(object __instance) { if (Plugin.I != null) Plugin.I.ExternalOpened(__instance); }

        static void Closed() { if (Plugin.I != null) Plugin.I.ExternalClosed(); }

        internal void Uninstall()
        {
            Close(); ClearButtons(); Session.Close(true); TalkApiLink.Shutdown(); ExternalAvailable = false;
            if (skinTex != null && !skinFromPanel) UnityEngine.Object.Destroy(skinTex);
            skinTex = null; Instance = null; TalkType = null;
        }
    }
}
