using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Guns
{
    /// Bridges item identity to the reusable arm model before its activation actions run.
    /// Save data: ES3 key "Apocaunloader.Magazines" inside the game's own save file, injected into the ES3 cache right after the
    /// game's SaveAll("global") and again right before the cache is flushed (StoreCachedFile), read before LoadAll("global").
    /// Key, format and timing are verbatim from Gunplay 1.3.0 / Apocaunloader 1.3.0 (save compatibility both ways).
    internal static partial class PerGunAmmo
    {
        internal const string SaveKey = "Apocaunloader.Magazines";
        private static MagazineLedger _ledger = new MagazineLedger();
        private static PlayMakerFSM _reload, _handler, _saveFsm;
        private static GameObject _item;
        private static string _identity, _type, _snapshot, _saveFile;
        private static bool _loading = true, _saving, _faulted;
        private static float _scanAt;
        private static readonly List<PlayMakerFSM> _buf = new List<PlayMakerFSM>(64);

        /// <summary>Loading, saving or faulted: no ledger changes.</summary>
        internal static bool Suspended { get { return _loading || _saving || _faulted; } }
        /// <summary>[Gunplay] PerGunMagazines.</summary>
        private static bool On { get { return GunplaySettings.PerGunMagazines.Value; } }

        // ---- F7 live info ----
        /// <summary>F7: items in the ledger.</summary>
        internal static int TrackedItems { get { return _ledger.Count; } }
        /// <summary>F7: claimed weapon types.</summary>
        internal static int ClaimedTypes { get { return _ledger.ClaimedCount; } }
        internal static string Current { get { return _identity == null ? "-" : _identity + " (" + _type + ")"; } }
        /// <summary>F7: ledger state text.</summary>
        internal static string State { get { return _faulted ? "FAULTED (suspended to protect save data)" : _loading ? "loading" : _saving ? "saving" : "active"; } }

        /// <summary>Save-stable item name of the drawn gun (null when none is bound).</summary>
        internal static string CurrentItem { get { return _identity; } }
        /// <summary>Reload FSM of the drawn gun (null when none is bound).</summary>
        internal static PlayMakerFSM CurrentReload { get { return _reload; } }
        /// <summary>Item id of <paramref name="model"/> when it is the bound drawn gun, else null (stat modifiers per item).</summary>
        internal static string ItemOfModel(GameObject model) { return _reload != null && model != null && ReferenceEquals(_reload.gameObject, model) ? _identity : null; }

        /// <summary>Fresh ledger and state (module init).</summary>
        internal static void ResetAll()
        {
            _ledger = new MagazineLedger();
            _handler = null; _saveFsm = null; _scanAt = 0f;
            Release();
            _loading = true; _saving = false; _faulted = false; _snapshot = null; _saveFile = null;
        }

        /// (WW) [Guns] PerGunMagazines changed at runtime: start from a fresh ledger (as a fresh install would).
        internal static void ToggleChanged()
        {
            Release();
            _ledger = new MagazineLedger();
            _snapshot = null;
            _scanAt = 0f;
        }

        /// <summary>FSM by name on an object (Core FsmUtil).</summary>
        internal static PlayMakerFSM Find(GameObject go, string name) { return FsmUtil.Find(go, name); }

        // Vanilla's Folk 17 weaponType is "folk17", but its arm model is "folk_17".
        /// <summary>The native weaponType names this arm model (catalog alias: folk17 -> folk_17).</summary>
        private static bool MatchesModel(string type, string model)
        { return type == model || WeaponCatalog.Instance.ModelNameOf(type) == model; }

        /// <summary>The object sits under the first-person WeaponsArm.</summary>
        internal static bool ArmModel(GameObject go)
        {
            for (var p = go == null ? null : go.transform.parent; p != null; p = p.parent)
                if (p.name == "WeaponsArm") return true;
            return false;
        }

        /// <summary>Scene change: rig references dropped; the ledger of the current load is kept.</summary>
        internal static void SceneChanged()
        {
            _handler = null; _saveFsm = null; _scanAt = 0;
            // LoadAll may precede sceneLoaded. Keep the ledger from that exact load.
            Release();
        }

        /// <summary>The gun's current rounds and hammer.</summary>
        private static Magazine Read(PlayMakerFSM reload)
        {
            var rounds = reload.FsmVariables.GetFsmInt("ammo_in_gun");
            var attack = Find(reload.gameObject, "Attack");
            var hammer = attack == null ? null : attack.FsmVariables.GetFsmBool("hammer");
            return new Magazine(rounds == null ? 0 : rounds.Value, hammer != null && hammer.Value);
        }

        /// <summary>Writes a magazine into the gun (rounds and hammer).</summary>
        private static void Apply(PlayMakerFSM reload, Magazine magazine)
        {
            reload.FsmVariables.GetFsmInt("ammo_in_gun").Value = magazine.Rounds;
            var attack = Find(reload.gameObject, "Attack");
            var hammer = attack == null ? null : attack.FsmVariables.GetFsmBool("hammer");
            if (hammer != null) hammer.Value = magazine.Hammer;
        }

        /// <summary>Stores the bound gun's current magazine.</summary>
        internal static void Capture()
        {
            if (_reload == null || _identity == null || _faulted || _loading) return;
            _ledger.Capture(_identity, _type, Read(_reload));
        }

        /// <summary>Unbinds the drawn gun.</summary>
        private static void Release()
        { _reload = null; _item = null; _identity = null; _type = null; }

        /// <summary>WeaponInHand entered Deactivate: completes an unload and stores the magazine.</summary>
        internal static void Holster(Fsm fsm)
        {
            if (fsm.Name != "WeaponInHand" || Suspended) return;
            AmmoRunner.Unloader.CompleteForSwitch();
            if (!On) return;
            Capture(); Release();
        }

        /// <summary>A gun model was activated by WeaponInHand: binds it to the selected item and applies that item's magazine.</summary>
        internal static void Draw(Fsm handler, GameObject model)
        {
            if (Suspended || !On || handler == null || handler.Name != "WeaponInHand" || !ArmModel(model)) return;
            var reload = Find(model, "Reload");
            if (reload == null || reload.FsmVariables.GetFsmInt("ammo_in_gun") == null) return; // melee
            var itemVar = handler.Variables.GetFsmGameObject("Item");
            var item = itemVar == null ? null : itemVar.Value;
            var itemTypeFsm = Find(item, "weaponType");
            var itemType = itemTypeFsm == null ? null : itemTypeFsm.FsmVariables.GetFsmString("weaponType");
            // Only the physical item selected by vanilla owns this model. Save-time activation of all models is ignored.
            if (item == null || itemType == null || !MatchesModel(itemType.Value, model.name) || string.IsNullOrEmpty(item.name)) return;
            if (_item == item && _reload == reload) return;
            AmmoRunner.Unloader.CompleteForSwitch();
            Capture();
            var magazine = _ledger.Draw(item.name, itemType.Value, Read(reload));
            _item = item; _identity = item.name; _type = itemType.Value; _reload = reload; _handler = handler.FsmComponent;
            Apply(reload, magazine);
            CombatEvents.Magazine(model.name, _identity, magazine.Rounds);
            if (Log.L2) AmmoSettings.V("Magazine " + _identity + ": " + magazine.Rounds + " rounds");
        }

        // Fsm.EnterState prefix: every state entry of every FSM. Both FSM names we watch are 12 characters long, so one int compare
        // rejects almost every call before any string compare.
        /// <summary>Fsm.EnterState prefix body: holster on WeaponInHand Deactivate; SaveLoadGame Start/setSeed/LoadGame resets, wait/SaveGame begins a save (first filter: name length 12).</summary>
        internal static void BeforeState(Fsm fsm, FsmState st)
        {
            string n = fsm.Name;
            if (n == null || n.Length != 12) return;
            string state = st.Name;
            if (n == "WeaponInHand") { if (state == "Deactivate") Holster(fsm); return; }
            if (n != "SaveLoadGame") return;
            _saveFsm = fsm.FsmComponent;
            if (state == "Start" || state == "setSeed" || state == "LoadGame")
            {
                ArmorOverflow.Reset();
                AmmoRunner.Unloader.CancelForLoad();
                _ledger = new MagazineLedger(); Release();
                _loading = true; _saving = false; _faulted = false; _snapshot = null; _saveFile = null;
            }
            else if (state == "wait" || state == "SaveGame") BeginSave();
        }

        /// <summary>Fsm.EnterState postfix body: SaveLoadGame isPlay ends loading/saving and adopts the drawn gun.</summary>
        internal static void AfterState(Fsm fsm, FsmState st)
        {
            string n = fsm.Name;
            if (n == null || n.Length != 12 || n != "SaveLoadGame" || st.Name != "isPlay") return;
            _saveFsm = fsm.FsmComponent;
            _loading = false; _saving = false;
            // Native load restored only the per-type counts. Replace the selected one from our item ledger.
            Release();
            AdoptDrawn();
        }

        /// <summary>Binds the gun the player already holds (after a load, or while none is bound) from the WeaponsArm subtree.</summary>
        private static void AdoptDrawn()
        {
            if (Suspended || !On) return;
            if (_handler == null) _handler = FindHandler();
            if (_handler == null || _handler.Fsm == null || !_handler.Fsm.Initialized) return;
            // A holstered handler retains its Item variable; require a currently activated gun model.
            string state = _handler.ActiveStateName;
            if (state == "off" || state == "Deactivate" || state == "hands" || state == "Activate" || state.StartsWith("Slot ", StringComparison.Ordinal)) return;
            var type = _handler.FsmVariables.GetFsmString("weaponType");
            if (type == null) return;
            // (WW) the drawn model is an active Reload FSM under the player's WeaponsArm: search that subtree instead of the whole
            // scene every second (whole-scene scan only while the rig is not found)
            var arm = PlayerState.WeaponsArm;
            if (arm == null) return;            // (WW perf) no whole-scene FindObjectsOfType while the rig is missing: retried next second
            arm.GetComponentsInChildren(false, _buf);
            try
            {
                for (int i = 0; i < _buf.Count; i++)
                {
                    var f = _buf[i];
                    if (f != null && f.FsmName == "Reload" && MatchesModel(type.Value, f.gameObject.name) && f.gameObject.activeInHierarchy && ArmModel(f.gameObject))
                    { Draw(_handler.Fsm, f.gameObject); break; }
                }
            }
            finally { _buf.Clear(); }
        }

        // the "Weapons" object's WeaponInHand FSM: looked for under the player and the camera rig first, then (as before) in the whole
        // scene; cached until the next scene load
        /// <summary>The "Weapons" WeaponInHand FSM under the player or the camera rig (null while the rig is missing).</summary>
        private static PlayMakerFSM FindHandler()
        {
            var r = HandlerIn(GameRefs.Player);
            if (r == null) r = HandlerIn(GameRefs.PlayerCameraHolder);
            return r;                           // (WW perf) no whole-scene scan: the handler lives under the player rig

        }

        /// <summary>The "Weapons" WeaponInHand FSM below <paramref name="root"/> (active objects only).</summary>
        private static PlayMakerFSM HandlerIn(GameObject root)
        {
            if (root == null) return null;
            root.GetComponentsInChildren(false, _buf);
            try
            {
                for (int i = 0; i < _buf.Count; i++)
                {
                    var f = _buf[i];
                    if (f != null && f.FsmName == "WeaponInHand" && f.gameObject.name == "Weapons") return f;
                }
                return null;
            }
            finally { _buf.Clear(); }
        }

        /// <summary>Per frame (Ammo job): SaveLoadGame lookup, unbinds a vanished gun, adopts the drawn one once a second while none is bound.</summary>
        internal static void Tick()
        {
            if (_saveFsm == null && Time.unscaledTime >= _scanAt)
            {
                // (WW) Core.GameRefs / SaveBus instead of a FindObjectsOfType scan per second
                _scanAt = Time.unscaledTime + 1f;
                _saveFsm = GameRefs.SaveLoadGame;
                if (_saveFsm != null && SaveBus.State == "isPlay") _loading = false;
            }
            if (Suspended || !On) return;
            if (_reload != null && (_item == null || !_reload.gameObject.activeInHierarchy)) { Capture(); Release(); }
            if (_reload == null && Time.unscaledTime >= _scanAt) { _scanAt = Time.unscaledTime + 1f; AdoptDrawn(); }
        }

        /// <summary>A save starts: unload completes, the ledger snapshot is taken for the ES3 key.</summary>
        internal static void BeginSave()
        {
            if (_loading || _faulted || _saving) return;
            AmmoRunner.Unloader.CompleteForSwitch();
            if (On) { Capture(); _snapshot = _ledger.Encode(); }
            _saving = true;
        }


        /// <summary>Any ledger error: suspends per-gun magazines (save data protected) and logs it.</summary>
        internal static void Fail(Exception error)
        {
            AmmoRunner.Unloader.CancelForLoad();
            _faulted = true;
            GunplayLog.Error("Per-gun ammo suspended to preserve save data", error);
        }
    }
}
