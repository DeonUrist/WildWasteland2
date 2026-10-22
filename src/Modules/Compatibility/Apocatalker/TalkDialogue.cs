using System;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Owns external Apocatalker reflection and publishes its native conversation source to HUD.</summary>
    internal static class TalkDialogue
    {
        private static object _plugin;
        private static Func<object,object> _getUi;
        private static Func<object,bool> _talking;
        private static Func<object,string> _npcName;
        private static Func<object,RectTransform> _panel,_sourceChat,_sourceChoices;
        private static Action<object> _close;
        private static int _revision;
        private static IModule _owner;
        internal static void Install(IModule owner)
        {
            _owner = owner;
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.example.apocalypter.talk", out info) || ReferenceEquals(info.Instance, null)) return;
            if (info.Metadata.Version != new Version(2, 0, 0)) Log.Warn(owner, "Apocatalker " + info.Metadata.Version + " differs from the tested 2.0.0; attempting dialogue compatibility with this version");
            _plugin = info.Instance; var p = _plugin.GetType(); var u = p.Assembly.GetType("ApocalypterTalk.DialogueUI", true);
            _getUi = Get<object>(p, "dialogueUI"); _talking = Get<bool>(p, "Talking"); _npcName = Get<string>(p, "npcName");
            _panel = Get<RectTransform>(u, "panel"); _sourceChat = Get<RectTransform>(u, "chatContent"); _sourceChoices = Get<RectTransform>(u, "promptContent");
            _close = Access.Call<Action<object>>(AccessTools.Method(p, "Close"), typeof(void), typeof(object));
            foreach (var method in new[] { "ShowDialogue", "AppendChatLine", "SetChoices" })
                if (!Core.Patches.Patch(owner, AccessTools.Method(u, method), postfix: AccessTools.Method(typeof(TalkDialogue), "Changed")))
                    throw new InvalidOperationException("Dialogue hook unavailable: " + method);
            _revision = 0;ModApi.Provide<IExternalDialogueSource>(owner,new Source());
            Log.Main(owner, "Apocatalker dialogue bridge ready; native window remains the HUD-off fallback");
        }
        /// <summary>Generated getter of a field (instance or static), converted to <typeparamref name="T"/>.</summary>
        private static Func<object, T> Get<T>(Type type, string name) { return Access.Read<T>(AccessTools.Field(type, name)); }
        /// <summary>Postfix of the source's ShowDialogue/AppendChatLine/SetChoices: the mirror rebuilds on its next tick.</summary>
        private static void Changed() { _revision++; }
        /// <summary>Invalidates the external source revision on scene boundaries.</summary>
        internal static void Hide() { _revision++; }
        /// <summary>Withdraws this source and releases external references; HUD restores its own borrowed panel state.</summary>
        internal static void Dispose() { if(_owner!=null)ModApi.Withdraw<IExternalDialogueSource>(_owner);_plugin=null;_owner=null;_getUi=null;_talking=null;_npcName=null;_panel=_sourceChat=_sourceChoices=null;_close=null; }
        /// <summary>Reads the actual external source without leaking its private types into HUD.</summary>
        private sealed class Source : IExternalDialogueSource {
            public bool Talking=>_plugin!=null&&_talking(_plugin);
            public object Session=>_plugin!=null?_getUi(_plugin):null;
            public string Speaker=>_plugin!=null?_npcName(_plugin):"";
            public int Revision=>_revision;
            public RectTransform Panel=>Session!=null?_panel(Session):null;
            public RectTransform Chat=>Session!=null?_sourceChat(Session):null;
            public RectTransform Choices=>Session!=null?_sourceChoices(Session):null;
            public void Close() { if(_plugin!=null)_close(_plugin); }
        }
    }
}
