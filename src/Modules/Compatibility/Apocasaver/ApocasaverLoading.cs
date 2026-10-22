using System;
using HarmonyLib;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Owns standalone Apocasaver reflection and delegates all UI to the current HUD provider.</summary>
    internal static class ApocasaverLoading
    {
        private static IExternalSavePresentation _screen;
        private static bool _active;
        private static bool _ready;
        private static IModule _owner;
        private static Action _confirmName, _cancelName;

        /// <summary>Resolves the standalone's coordinator/presentation/naming types and patches them under <paramref name="owner"/> (the HUD).</summary>
        internal static void Install(IModule owner)
        {
            _ready=false;_owner=owner;_active=false;
            ModApi.Changed-=ApiChanged;ModApi.Changed+=ApiChanged;
            InstallNaming(owner);
            var type = AccessTools.TypeByName("Apocasaver.AutosavePresentation");
            if (type == null) return;
            bool begin = Core.Patches.Patch(owner, AccessTools.Method(type, "Begin"), postfix: AccessTools.Method(typeof(ApocasaverLoading), "AfterBegin"));
            bool end = Core.Patches.Patch(owner, AccessTools.Method(type, "End"), postfix: AccessTools.Method(typeof(ApocasaverLoading), "AfterEnd"));
            _ready = begin && end;
            if (_ready) Log.Main(owner, "Apocasaver autosave loading screen hooks ready");
        }
        /// <summary>Forgets the screen, delegates and styles (the owner's shutdown removes the patches).</summary>
        internal static void Clear()
        {
            ModApi.Changed-=ApiChanged;
            if(_active) _screen?.End();
            _active=false;_owner=null;_screen=null;_ready=false;_confirmName=_cancelName=null;
        }
        /// <summary>Transfers an active screen to a restarted HUD provider and releases the previous one.</summary>
        private static void ApiChanged(Type type)
        {
            if(type!=typeof(IExternalSavePresentation)) return;
            if(_active) _screen?.End();
            _screen=ModApi.Get<IExternalSavePresentation>();
            if(_active) _screen?.Begin();
        }
        /// <summary>Retains original naming UI when HUD is absent or declines the request.</summary>
        private static bool BeforeNameGui(bool ____open, ref string ____text, float ____openedAt, ref bool ____focusPending)
        {
            var view=ModApi.Get<IExternalSavePresentation>();
            return !____open || view==null || view.DrawName(ref ____text,____openedAt,ref ____focusPending,_confirmName,_cancelName);
        }
        /// <summary>Patches the standalone's SaveNaming.OnGUI when its private popup fields exist.</summary>
        private static void InstallNaming(IModule owner)
        {
            var type = AccessTools.TypeByName("Apocasaver.SaveNaming");
            if (type == null) return;
            try
            {
                foreach (string field in new[] { "_open", "_text", "_openedAt", "_focusPending" })
                    if (AccessTools.Field(type, field) == null) return;
                var confirm = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(type, "Confirm"));
                var cancel = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(type, "Cancel"));
                if (!Core.Patches.Patch(owner, AccessTools.Method(type, "OnGUI"), prefix: AccessTools.Method(typeof(ApocasaverLoading), "BeforeNameGui"))) return;
                _confirmName = confirm; _cancelName = cancel;
                Log.Main(owner, "Apocasaver save-name window ready; Clean menus off restores the original popup");
            }
            catch (Exception e) { Log.Warn(owner, "Apocasaver save-name window hooks: " + e.Message); }
        }
        /// <summary>Postfix of AutosavePresentation.Begin: shows the WW saving screen.</summary>
        private static void AfterBegin()
        {
            if (!_ready) return;
            try { _active=true;_screen=ModApi.Get<IExternalSavePresentation>();_screen?.Begin(); } catch (Exception e) { Log.Warn(_owner, "Apocasaver autosave screen: " + e.Message); }
        }
        /// <summary>Postfix of AutosavePresentation.End: ends the WW saving screen.</summary>
        private static void AfterEnd()
        {
            if (!_ready) return;
            try { _active=false;_screen?.End();_screen=null; } catch (Exception e) { Log.Warn(_owner, "Apocasaver autosave screen end: " + e.Message); }
        }
    }
}
