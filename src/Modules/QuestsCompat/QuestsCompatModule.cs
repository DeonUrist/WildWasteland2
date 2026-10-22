using System;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Patches = WildWasteland.Core.Patches;

namespace WildWasteland.Modules.QuestsCompat
{
    // Retain the saved module identity/enable preference; only optional Talk remains external.
    public sealed class QuestsCompatModule : ModuleBase
    {
        static QuestsCompatModule _owner;
        static object _talk;
        static Action<object> _update, _late, _close;
        static Func<object, bool> _talking;
        static Func<object, ConfigEntry<bool>> _pause;
        static bool _dispatch;
        static readonly EditorSession Session = new EditorSession();
        public override string Id { get { return "QuestsCompat"; } }
        public override string DisplayName { get { return "Apocatalker content compatibility"; } }
        public override void BindConfig(ModuleConfig cfg) { }
        public override void Init()
        {
            _owner = this;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.example.apocalypter.talk", out info) || ReferenceEquals(info.Instance, null))
            { Log.Always("[QuestsCompat] Optional Apocatalker not loaded; native Quests is independent."); return; }
            _talk = info.Instance; var type = _talk.GetType();
            _update = Binding.Call(type, "Update"); _late = Binding.Call(type, "LateUpdate"); _close = Binding.Call(type, "Close");
            _talking = Binding.Get<bool>(type, "Talking"); _pause = Binding.Get<ConfigEntry<bool>>(type, "pauseWhileTalking");
            Patch(type, "Update", "ScheduledOnly"); Patch(type, "LateUpdate", "ScheduledOnly");
            Patch(type, "Open", "BeforeOpen", "AfterOpen"); Patch(type, "Close", null, "AfterClose");
            Patch(type, "GetCam", "TalkCamera"); Patch(type, "OnDestroy", "KeepModel");
            var api = type.Assembly.GetType("ApocalypterTalk.TalkApi", true);
            PluginLifetime.Install(this, api, "LoadFile"); PluginLifetime.Install(this, api, "Reload");
            Scheduler.EveryFrame(this, "Talk.Frame", Frame);
            Scheduler.EveryFrame(this, "Talk.Cursor", Late, Phase.LateUpdateLast);
            Log.Always("[QuestsCompat] Apocatalker " + info.Metadata.Version + " scheduled; original content/profiles retained.");
        }
        static void Patch(Type type, string name, string prefix = null, string postfix = null)
        {
            if (!Patches.Patch(_owner, AccessTools.Method(type, name),
                prefix: prefix == null ? null : AccessTools.Method(typeof(QuestsCompatModule), prefix),
                postfix: postfix == null ? null : AccessTools.Method(typeof(QuestsCompatModule), postfix)))
                throw new InvalidOperationException("Talk hook failed: " + name);
        }
        static bool ScheduledOnly() { return _dispatch; }
        static bool KeepModel() { return false; }
        static bool TalkCamera(ref Camera __result) { __result = GameRefs.MainCamera; return false; }
        static bool BeforeOpen() { return GameRefs.InGame && Session.Acquire(_pause(_talk).Value); }
        static void AfterOpen()
        {
            if (!_talking(_talk)) { Session.Close(); return; }
        }
        static void AfterClose() { Session.Close(); }
        static void Run(Action<object> action)
        { _dispatch = true; try { action(_talk); } finally { _dispatch = false; } }
        static void Frame()
        {
            if (_talking(_talk) && !Application.isFocused) { _close(_talk); return; }
            if (_talking(_talk) || (!EditorSession.Busy && !ApocasetterBridge.InputBlocked && GameRefs.InPlay)) Run(_update);
        }
        static void Late() { if (_talking(_talk)) { Run(_late); Session.Maintain(); } }
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (_talk != null && _talking(_talk)) _close(_talk); }
        public override void Shutdown()
        {
            if (_talk != null && _talking != null && _talking(_talk)) _close(_talk);
            Session.Close(true); _talk = null; _owner = null; _update = _late = _close = null; _pause = null; _talking = null; _dispatch = false;
        }
    }
}
