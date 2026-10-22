using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    /// <summary>Save plumbing of the per-gun magazine ledger (Wave A split, B04): the ES3 key Apocaunloader.Magazines is cached after SaveAll("global") and before StoreCachedFile, and read before LoadAll("global").</summary>
    internal static partial class PerGunAmmo
    {
        internal static void SaveGlobal(ES3PlayMaker.SaveAll action)
        {
            if (action.Fsm == null || action.Fsm.Name != "SaveLoadGame" || action.key.Value != "global" || _faulted || _loading) return;
            BeginSave();
            var settings = action.GetSettings();
            _saveFile = settings.path;
            CacheSnapshot(settings);
        }

        private static void CacheSnapshot(ES3Settings settings)
        {
            if (_snapshot == null || string.IsNullOrEmpty(_saveFile)) return;
            var cache = new ES3Settings(settings.path, settings) { location = ES3.Location.Cache };
            ES3.Save<string>(SaveKey, _snapshot, cache);
        }

        internal static void BeforeStore(ES3Settings settings)
        {
            // Also inject into Apocasaver's final rebuilt cache, immediately before its transactional flush.
            if (settings != null && settings.path == _saveFile && !_faulted && !_loading) CacheSnapshot(settings);
        }

        internal static void LoadGlobal(ES3PlayMaker.LoadAll action)
        {
            if (action.Fsm == null || action.Fsm.Name != "SaveLoadGame" || action.key.Value != "global") return;
            AmmoRunner.Unloader.CancelForLoad(); Release();
            _loading = true; _saving = false; _snapshot = null; _saveFile = null; _faulted = false;
            var settings = action.GetSettings();
            _ledger = On && ES3.KeyExists(SaveKey, settings) ? MagazineLedger.Decode(ES3.Load<string>(SaveKey, settings)) : new MagazineLedger();
        }
    }

    [HarmonyPatch(typeof(ES3PlayMaker.SaveAll), "Enter")]
    internal static class MagazineSavePatch
    {
        static void Postfix(ES3PlayMaker.SaveAll __instance)
        { try { PerGunAmmo.SaveGlobal(__instance); } catch (Exception e) { PerGunAmmo.Fail(e); } }
    }

    [HarmonyPatch(typeof(ES3PlayMaker.LoadAll), "Enter")]
    internal static class MagazineLoadPatch
    {
        static void Prefix(ES3PlayMaker.LoadAll __instance)
        { try { PerGunAmmo.LoadGlobal(__instance); } catch (Exception e) { PerGunAmmo.Fail(e); } }
    }

    [HarmonyPatch(typeof(ES3), "StoreCachedFile", new[] { typeof(ES3Settings) })]
    internal static class MagazineStorePatch
    {
        static void Prefix(ES3Settings settings)
        { try { PerGunAmmo.BeforeStore(settings); } catch (Exception e) { PerGunAmmo.Fail(e); } }
    }
}
