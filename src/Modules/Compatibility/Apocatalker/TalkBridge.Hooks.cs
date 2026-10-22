using System;
using BepInEx.Bootstrap;
using HarmonyLib;
using WildWasteland.Core;
namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Owns external dialogue reflection, hooks and callback subscriptions under Compatibility.</summary>
    internal sealed partial class ExternalQuestConversation : IExternalQuestConversation
    {
        static ExternalQuestConversation _active;
        IModule _listenerOwner;
        Action<object> _opened;
        Action _closed;
        Action<string,string> _action;
        /// <summary>Installs only against the available optional plugin under the supplied bridge scope.</summary>
        internal static ExternalQuestConversation Start(IModule scope)
        {
            BepInEx.PluginInfo info;
            if(!Chainloader.PluginInfos.TryGetValue("com.example.apocalypter.talk",out info)||ReferenceEquals(info.Instance,null))return null;
            var api=new ExternalQuestConversation();if(!api.Init(info.Instance.GetType()))return null;
            api.Bind(info.Instance);
            if(!Core.Patches.Patch(scope,AccessTools.Method(api.TalkType,"Open"),postfix:AccessTools.Method(typeof(ExternalQuestConversation),"Opened"))||!Core.Patches.Patch(scope,AccessTools.Method(api.TalkType,"Close"),postfix:AccessTools.Method(typeof(ExternalQuestConversation),"Closed")))throw new InvalidOperationException("Optional Talk conversation hooks unavailable");
            _active=api;TalkApiLink.Init(info.Instance.GetType().Assembly,api.ActionTriggered);ModApi.Provide<IExternalQuestConversation>(scope,api);return api;
        }
        /// <summary>Hooks only forward into a currently running quest owner.</summary>
        private bool Listening=>_listenerOwner!=null&&ModuleHost.IsRunning(_listenerOwner);
        /// <summary>Notifies the current quest owner of external conversation opening.</summary>
        static void Opened(object __instance){if(_active!=null&&_active.Listening)_active._opened?.Invoke(__instance);}
        /// <summary>Notifies the current quest owner of external conversation closing.</summary>
        static void Closed(){if(_active!=null&&_active.Listening)_active._closed?.Invoke();}
        /// <summary>Forwards optional dialogue actions to the active typed quest subscriber.</summary>
        void ActionTriggered(string action,string npc){if(Listening)_action?.Invoke(action,npc);}
        /// <summary>Registers exactly one active quest session owner.</summary>
        public void Subscribe(IModule owner,Action<object> opened,Action closed,Action<string,string> action){if(Listening&&!ReferenceEquals(owner,_listenerOwner))throw new InvalidOperationException("Quest conversation already subscribed");_listenerOwner=owner;_opened=opened;_closed=closed;_action=action;}
        /// <summary>Removes generated choices and drops callbacks from the exact session owner.</summary>
        public void Unsubscribe(IModule owner){if(!ReferenceEquals(owner,_listenerOwner))return;ClearButtons();_listenerOwner=null;_opened=null;_closed=null;_action=null;}
        /// <summary>Restores external UI and releases all callbacks and optional API state.</summary>
        internal void Stop(){ClearButtons();_listenerOwner=null;_opened=null;_closed=null;_action=null;TalkApiLink.Shutdown();if(skinTex!=null&&!skinFromPanel)UnityEngine.Object.Destroy(skinTex);skinTex=null;Instance=null;TalkType=null;if(ReferenceEquals(_active,this))_active=null;}
    }
}
