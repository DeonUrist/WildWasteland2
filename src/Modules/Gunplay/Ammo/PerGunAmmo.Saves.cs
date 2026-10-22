using System;
using HarmonyLib;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;
namespace WildWasteland.Modules.Gunplay
{
    /// <summary>Captures the magazine ledger through the same verified ES3 transaction as other world records.</summary>
    internal static partial class PerGunAmmo
    {
        /// <summary>Publishes the current snapshot only inside the active native save transaction.</summary>
        internal static void SaveWorld(string slot)
        {
            if(_faulted){SaveBus.ReportFailure(GunplayModule.Instance,"Magazine ledger could not be read; preserving the previous save");return;}
            BeginSave();if(_snapshot!=null)SaveBus.Key(SaveKey).Write(GunplayModule.Instance,slot,_snapshot);
        }
        /// <summary>Loads the selected native global ledger before the game applies its weapon counts.</summary>
        internal static void LoadGlobal(ES3PlayMaker.LoadAll action)
        {
            if(action.Fsm==null||action.Fsm.Name!="SaveLoadGame"||action.key.Value!="global")return;
            AmmoRunner.Unloader.CancelForLoad();Release();_loading=true;_saving=false;_snapshot=null;_faulted=false;
            string text=On?SaveBus.Key(SaveKey).Read(action.GetSettings().path):null;
            _ledger=text!=null?MagazineLedger.Decode(text):new MagazineLedger();
        }
    }
    [HarmonyPatch(typeof(ES3PlayMaker.LoadAll),"Enter")]
    internal static class MagazineLoadPatch
    {
        /// <summary>Preserves corrupt ledger protection while allowing normal native loading diagnostics.</summary>
        static void Prefix(ES3PlayMaker.LoadAll __instance){try{PerGunAmmo.LoadGlobal(__instance);}catch(Exception e){PerGunAmmo.Fail(e);}}
    }
}
