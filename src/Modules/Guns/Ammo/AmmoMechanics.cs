using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    /// Was a MonoBehaviour on Gunplay.Runner; now the scheduler job "Guns.Ammo" (every frame: the hold/tap state machine needs frames).
    internal static class AmmoRunner
    {
        private static bool _held;
        private static float _pressStart;
        private static bool _consumed;
        internal static Unloader Unloader = new Unloader();

        internal static void ResetAll()
        {
            _held = false; _pressStart = 0f; _consumed = false;
            Unloader.CancelForLoad();
            AmmoSettings.TapFrame = -100;
            GetButtonDown_Patch.Reset();
        }

        internal static void Tick()
        {
            try { PerGunAmmo.Tick(); } catch (Exception e) { PerGunAmmo.Fail(e); }
            BackpackOverflow.Tick();
            if (!AmmoSettings.Enabled.Value) { Unloader.Tick(); return; }

            bool down = AmmoSettings.ReloadHeld();
            float now = Time.unscaledTime;

            if (down && !_held)
            {
                _held = true; _pressStart = now; _consumed = false;
            }
            else if (down && _held && !_consumed && now - _pressStart >= AmmoSettings.HoldSeconds.Value)
            {
                _consumed = true;
                if (Time.timeScale > 0f && !Unloader.Busy) Unloader.TryStart();
            }
            else if (!down && _held)
            {
                _held = false;
                if (!_consumed && !Unloader.Busy) { AmmoSettings.TapFrame = Time.frameCount; AmmoSettings.V("Reload tap"); }
            }

            Unloader.Tick();
        }
    }

    /// Preserve ammo before the vanilla per-frame clamp discards it after a capacity reduction.
    internal static class BackpackOverflow
    {
        // (WW) Per IntClamp action: is it one of the __GameManager__ Ammo FSM's ammo_X <= ammo_X_capacity clamps (decided once: the
        // variable names and the owner never change; GameObject.name and the "_capacity" concatenation allocate) + its transfer state.
        private sealed class Clamp { public bool Ours; public OverflowTransfer State; }
        private static readonly Dictionary<IntClamp, Clamp> _clamps = new Dictionary<IntClamp, Clamp>();
        private static Fsm _emptySlot;
        private static int _removalFrame;
        internal static bool Enabled { get { return AmmoSettings.DropBackpackOverflow.Value; } }
        internal static int Tracked { get { int n = 0; foreach (var c in _clamps.Values) if (c.Ours && c.State != null) n++; return n; } }

        internal static void Reset() { _clamps.Clear(); _emptySlot = null; }

        private static bool Classify(IntClamp action, Fsm fsm)
        {
            if (fsm.GameObject.name != "__GameManager__") return false;
            var count = action.intVariable;
            var max = action.maxValue;
            return count != null && max != null && count.Name.StartsWith("ammo_", StringComparison.Ordinal) && max.Name == count.Name + "_capacity";
        }

        internal static bool BeforeClamp(IntClamp action)
        {
            var fsm = action.Fsm;
            if (fsm == null) return true;
            string fn = fsm.Name;                          // cheap first filter: IntClamp runs every frame in many FSMs
            if (fn == null || fn.Length != 4 || fn != "Ammo" || fsm.GameObject == null) return true;
            Clamp c;
            if (!_clamps.TryGetValue(action, out c)) { c = new Clamp { Ours = Classify(action, fsm) }; _clamps[action] = c; }
            if (!c.Ours) return true;
            var count = action.intVariable;
            var max = action.maxValue;
            if (!Enabled) { c.State = null; return true; }

            int capacity = Math.Max(0, max.Value);
            OverflowTransfer state = c.State;
            if (state == null)
            {
                // Establish a baseline without spilling ammo during scene initialization/save loading.
                c.State = new OverflowTransfer(capacity);
                return true;
            }
            if (state.Idle(capacity)) return true;
            int retained;
            bool allowClamp = Transfer(state, count.Value, capacity, count.Name, fsm.GameObject.transform, out retained);
            if (retained != count.Value)
            {
                int excess = count.Value - retained;
                count.Value = retained;
                if (Core.Log.Verbose) Core.Log.Info("[Guns] Backpack capacity reduced: dropped " + excess + " x " + count.Name + "; retained " + count.Value + "/" + capacity);
            }
            // Letting vanilla run here would delete the ammo we could not put into a world item.
            return allowClamp;
        }

        // (WW) the drop closure lives here, not in BeforeClamp: a lambda capturing BeforeClamp's locals made the compiler allocate
        // its closure on every IntClamp call game-wide, before the first filter
        private static bool Transfer(OverflowTransfer state, int count, int capacity, string caliber, Transform at, out int retained)
        {
            return state.BeforeClamp(count, capacity, Time.unscaledTime, excess => AmmoBox.Drop(caliber, excess, at), out retained);
        }

        internal static void OnSlotEnter(FsmState state)
        {
            var fsm = state.Fsm;
            if (fsm == null) return;
            string fn = fsm.Name;                           // cheap first filter: every state entry of every FSM lands here
            if (fn == null || fn.Length != 13 || !Enabled || fn != "SlotEmptyFull" || fsm.GameObject == null || fsm.GameObject.name != "Backpack_Item") return;
            if (state.Name == "full") { _emptySlot = null; return; }
            var item = fsm.Variables.GetFsmGameObject("item");
            if (state.Name != "empty" || item == null || item.Value == null) return;
            _emptySlot = fsm;
            _removalFrame = Time.frameCount;
        }

        internal static void Tick()
        {
            // (old: Reset() every frame while off; BeforeClamp already drops each clamp's state while off)
            if (!Enabled) { _emptySlot = null; return; }
            if (_emptySlot == null || Time.frameCount <= _removalFrame) return;
            var slot = _emptySlot.GameObject;
            if (slot == null || _emptySlot.ActiveStateName != "empty" || slot.transform.childCount != 0) { _emptySlot = null; return; }
            var take = FsmUtil.Find(slot, "TakeItem");
            // Swapping packs temporarily empties the slot while dropCurrent waits one frame.
            if (take != null && (take.ActiveStateName == "dropCurrent" || take.ActiveStateName == "takeWeapon")) return;

            var gm = GameRefs.GameManager;
            var backpack = gm != null ? FsmUtil.Find(gm, "Backpack") : null;
            var ammo = gm != null ? FsmUtil.Find(gm, "Ammo") : null;
            var armor = gm != null ? FsmUtil.Find(gm, "ArmorBackpack") : null;
            if (backpack == null || ammo == null || armor == null) return;
            FsmState set = null;
            foreach (var st in backpack.FsmStates) if (st.Name == "set") { set = st; break; }
            if (set == null) return;
            // Read vanilla's base capacities instead of duplicating its caliber table.
            foreach (var a in set.Actions)
            {
                var action = a as SetIntValue;
                if (action == null) continue;
                if (!action.Enabled || action.intVariable == null || action.intValue == null) continue;
                string name = action.intVariable.Name;
                if (!name.StartsWith("ammo_", StringComparison.Ordinal) || !name.EndsWith("_capacity", StringComparison.Ordinal)) continue;
                var capacity = ammo.FsmVariables.GetFsmInt(name);
                if (capacity != null) capacity.Value = action.intValue.Value;
            }
            var packCapacity = armor.FsmVariables.GetFsmFloat("backpack_capacity");
            var multiply = backpack.FsmVariables.GetFsmFloat("multiply");
            if (packCapacity != null) packCapacity.Value = 0f;
            if (multiply != null) multiply.Value = 1f;
            _emptySlot = null;
            AmmoSettings.V("Backpack removed: restored base ammo capacities");
        }
    }

    /// Drives the unload sequence by replaying the weapon's own Reload FSM events.
    internal class Unloader
    {
        private enum Phase { Idle, WaitAnimFinished, WaitAnimStart, WaitAnimEnd }
        private Phase _phase = Phase.Idle;
        private PlayMakerFSM _reload;
        private GameObject _weapon;
        private PlayMakerFSM _ammoFsm;
        private string _caliber;
        private float _deadline;
        private bool _moved;

        public bool Busy { get { return _phase != Phase.Idle; } }
        public string PhaseName { get { return _phase.ToString(); } }
        public string WeaponName { get { return _weapon != null ? _weapon.name : "-"; } }

        public void TryStart()
        {
            if (PerGunAmmo.Suspended) return;
            var reload = FindActiveReloadFsm();
            if (reload == null) { AmmoSettings.V("No active weapon Reload FSM in idle"); return; }
            var inGun = reload.FsmVariables.GetFsmInt("ammo_in_gun");
            if (inGun == null) { AmmoSettings.V("Reload FSM has no ammo_in_gun"); return; }
            if (inGun.Value <= 0) { AmmoSettings.V("Gun already empty"); return; }

            // Caliber variable name from the checkAmmo state's GetFsmInt action.
            string caliber = null;
            var check = FindState(reload, "checkAmmo");
            if (check != null)
                foreach (var a in check.Actions)
                {
                    var g = a as GetFsmInt;
                    if (g != null && g.fsmName != null && g.fsmName.Value == "Ammo") { caliber = g.variableName.Value; break; }
                }
            if (string.IsNullOrEmpty(caliber)) { Core.Log.Warn("[Guns] " + "Could not determine caliber for " + reload.gameObject.name); return; }

            var gm = GameRefs.GameManager;
            var ammoFsm = gm != null ? FsmUtil.Find(gm, "Ammo") : null;
            if (ammoFsm == null || ammoFsm.FsmVariables.GetFsmInt(caliber) == null) { Core.Log.Warn("[Guns] " + "Ammo FSM / variable " + caliber + " not found"); return; }

            // The state the game enters after checkAmmo passes: it sends Deactivate/Reloading/animation events.
            FsmState start = null;
            if (check != null)
                foreach (var t in check.Transitions) if (t.EventName == "next") { start = FindState(reload, t.ToState); break; }
            if (start == null) { Core.Log.Warn("[Guns] " + "No start state found for " + reload.gameObject.name); return; }

            _reload = reload; _weapon = reload.gameObject; _ammoFsm = ammoFsm; _caliber = caliber; _moved = false;
            bool threePhase = false;
            foreach (var t in start.Transitions) if (t.EventName == "AnimStart") { threePhase = true; break; }
            if (Core.Log.Verbose) Core.Log.Info("[Guns] Unloading " + _weapon.name + ": " + inGun.Value + " x " + caliber + (threePhase ? " (3-phase)" : ""));

            ReplaySendEvents(start);
            _phase = threePhase ? Phase.WaitAnimStart : Phase.WaitAnimFinished;
            _deadline = Time.unscaledTime + AmmoSettings.PhaseTimeout.Value;
        }

        /// Called from the Harmony hook on Fsm.Event.
        public void OnFsmEvent(Fsm fsm, string evName)
        {
            if (_phase == Phase.Idle || fsm == null || fsm.GameObject != _weapon) return;
            switch (_phase)
            {
                case Phase.WaitAnimFinished: if (evName == "AnimFinished") { AmmoSettings.V("AnimFinished"); Finish(); } break;
                case Phase.WaitAnimStart: if (evName == "AnimStart") { AmmoSettings.V("AnimStart"); AfterAnimStart(); } break;
                case Phase.WaitAnimEnd: if (evName == "AnimEnd") { AmmoSettings.V("AnimEnd"); Finish(); } break;
            }
        }

        public void Tick()
        {
            if (_phase == Phase.Idle) return;
            if (_weapon == null || !_weapon.activeInHierarchy) { Core.Log.Warn("[Guns] Weapon vanished during unload; aborting"); Finish(); return; }
            if (Time.unscaledTime < _deadline) return;
            Core.Log.Warn("[Guns] Unload phase " + _phase + " timed out; continuing");
            if (_phase == Phase.WaitAnimStart) AfterAnimStart(); else Finish();
        }

        private void AfterAnimStart()
        {
            MoveAmmo();
            // Replay the "empty shell" sound the game plays between the open and close animations.
            foreach (var st in _reload.FsmStates)
            {
                if (st.Name.IndexOf("shell", StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (var a in st.Actions)
                {
                    var ps = a as PlaySound;
                    if (ps == null || ps.clip == null) continue;
                    var clip = ps.clip.Value as AudioClip;
                    if (clip != null && EmptyGunSound.Before(ps)) AudioSource.PlayClipAtPoint(clip, _weapon.transform.position + ps.position.Value, ps.volume.Value);
                }
            }
            // State that waits for AnimEnd sends ReloadEnd (close animation).
            FsmState endState = null;
            foreach (var s in _reload.FsmStates)
            {
                foreach (var t in s.Transitions) if (t.EventName == "AnimEnd") { endState = s; break; }
                if (endState != null) break;
            }
            if (endState != null) ReplaySendEvents(endState);
            _phase = Phase.WaitAnimEnd;
            _deadline = Time.unscaledTime + AmmoSettings.PhaseTimeout.Value;
        }

        private void MoveAmmo()
        {
            if (_moved) return;
            _moved = true;
            var inGun = _reload.FsmVariables.GetFsmInt("ammo_in_gun");
            var store = _ammoFsm.FsmVariables.GetFsmInt(_caliber);
            if (inGun == null || store == null) return;
            int n = inGun.Value;
            var capVar = _ammoFsm.FsmVariables.GetFsmInt(_caliber + "_capacity");
            int cap = capVar != null ? capVar.Value : int.MaxValue;
            int space = Math.Max(0, cap - store.Value);
            int toStore = Math.Min(n, space);
            int leftover = n - toStore;

            store.Value += toStore;
            inGun.Value = leftover;
            if (leftover > 0)
            {
                if (AmmoBox.Drop(_caliber, leftover, _weapon.transform)) inGun.Value = 0;
                else Core.Log.Warn("[Guns] Could not spawn an ammo box; " + leftover + " rounds left in the gun");
            }
            var mirror = _reload.FsmVariables.GetFsmInt("ammo");
            if (mirror != null) mirror.Value = store.Value;
            if (inGun.Value == 0)
            {
                var attack = PerGunAmmo.Find(_weapon, "Attack");
                var hammer = attack == null ? null : attack.FsmVariables.GetFsmBool("hammer");
                if (hammer != null) hammer.Value = false;
            }
            if (Core.Log.Verbose) Core.Log.Info("[Guns] Returned " + toStore + " x " + _caliber + " to backpack (now " + store.Value + "/" + cap + ")" + (leftover > 0 ? ", " + leftover + " dropped as a box" : ""));
        }

        private void Finish()
        {
            MoveAmmo();
            _phase = Phase.Idle; // Event replay can synchronously re-enter this hook.
            var idle = FindState(_reload, "idle");
            if (idle != null && _weapon != null && _weapon.activeInHierarchy) ReplaySendEvents(idle); // MenuOut / Activate: re-enable firing etc.
            _reload = null; _weapon = null; _ammoFsm = null;
        }

        internal void CompleteForSwitch() { if (Busy) Finish(); }
        internal void CancelForLoad() { _phase = Phase.Idle; _reload = null; _weapon = null; _ammoFsm = null; }

        private void ReplaySendEvents(FsmState st)
        {
            foreach (var a in st.Actions)
            {
                var se = a as SendEvent;
                if (se == null || !se.Enabled || se.sendEvent == null) continue;
                AmmoSettings.V("  replay " + st.Name + ": " + se.sendEvent.Name);
                try { _reload.Fsm.Event(se.eventTarget, se.sendEvent); }
                catch (Exception e) { Core.Log.Warn("[Guns] SendEvent replay failed: " + e.Message); }
            }
        }

        private static FsmState FindState(PlayMakerFSM f, string name)
        {
            foreach (var s in f.FsmStates) if (s.Name == name) return s;
            return null;
        }

        // (WW) Searches the player's WeaponsArm subtree (active objects only, as FindObjectsOfType did); the whole-scene scan is the
        // fallback when the rig is not found. Every model accepted below must sit under a WeaponsArm anyway.
        private static readonly List<PlayMakerFSM> _armFsms = new List<PlayMakerFSM>(64);
        private static PlayMakerFSM FindActiveReloadFsm()
        {
            var arm = PlayerRig.WeaponsArm;
            if (arm != null)
            {
                arm.GetComponentsInChildren(false, _armFsms);
                try { var r = PickReload(_armFsms); if (r != null) return r; }
                finally { _armFsms.Clear(); }
            }
            return PickReload(UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>());
        }

        private static PlayMakerFSM PickReload(IList<PlayMakerFSM> fsms)
        {
            for (int i = 0; i < fsms.Count; i++)
            {
                var f = fsms[i];
                if (f == null) continue;
                if (f.FsmName != "Reload" || !f.gameObject.activeInHierarchy || !f.enabled) continue;
                if (f.Fsm == null || !f.Fsm.Initialized) continue;
                if (f.FsmVariables.GetFsmInt("ammo_in_gun") == null) continue;
                var p = f.transform.parent;
                bool underArm = false;
                while (p != null) { if (p.name == "WeaponsArm") { underArm = true; break; } p = p.parent; }
                if (!underArm) continue;
                if (f.ActiveStateName != "idle") { AmmoSettings.V(f.gameObject.name + " Reload busy (" + f.ActiveStateName + ")"); continue; }
                return f;
            }
            return null;
        }
    }

    /// Spawns an ammo box item (same save-compatible recipe as Apocaspawner) holding the rounds that
    /// did not fit in the backpack.
    internal static class AmmoBox
    {
        private static readonly Dictionary<string, string> DefaultBoxes = new Dictionary<string, string>
        {
            { "ammo_762", "ammo_box_762mm" }, { "ammo_556", "ammo_box_556mm" }, { "ammo_9mm", "ammo_box_9mm" },
            { "ammo_12gauge", "ammo_box_12gauge" }, { "ammo_20gauge", "ammo_box_20gauge" }, { "ammo_22caliber", "ammo_box_22" },
            { "ammo_3006", "ammo_box_3006" }, { "ammo_arrow", "ammo_arrow" },
            { "ammo_battery", "flashlight_batteries" }, { "ammo_grenade", "grenade" },
            { "armorplate", "armorplate" },
        };
        private static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

        private static string BoxName(string caliber)
        {
            string d;
            return DefaultBoxes.TryGetValue(caliber, out d) ? d : null;
        }

        private static GameObject FindPrefab(string caliber)
        {
            GameObject cached;
            if (_prefabs.TryGetValue(caliber, out cached) && cached != null) return cached;
            string name = BoxName(caliber);
            if (name == null) return null;
            GameObject asset = null, instance = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.transform.parent != null) continue;
                if (go.name != name && !go.name.StartsWith(name + "(Clone)", StringComparison.Ordinal)) continue;
                string fsmName = caliber == "armorplate" ? "ID" : caliber == "ammo_battery" ? "Batteries" : "Ammo";
                if (PerGunAmmo.Find(go, fsmName) == null) continue;
                if (!go.scene.IsValid()) { if (go.name == name && asset == null) asset = go; }
                else if (instance == null && go.name.StartsWith(name + "(Clone)", StringComparison.Ordinal)) instance = go;
            }
            var found = asset ?? instance;
            if (found != null) { _prefabs[caliber] = found; AmmoSettings.V("Ammo box template for " + caliber + ": " + found.name + (asset != null ? " (prefab)" : " (scene copy)")); }
            else Core.Log.Warn("[Guns] No ammo box prefab named '" + name + "' is loaded");
            return found;
        }

        internal static bool Drop(string caliber, int rounds, Transform at)
        {
            if (rounds <= 0 || at == null) return false;
            var prefab = FindPrefab(caliber);
            if (prefab == null) return false;
            // (WW) Core.GameRefs instead of GameObject.Find; Find stays as the fallback (GameRefs stops refreshing itemNameID once the others are found)
            var counterGo = GameRefs.ItemNameId;
            if (counterGo == null) counterGo = GameObject.Find("itemNameID");
            var cf = counterGo != null ? FsmUtil.Find(counterGo, "itemNameID") : null;
            var counter = cf != null ? cf.FsmVariables.GetFsmInt("intName") : null;
            var reg = GameRefs.NewGOArrayList;
            if (reg == null) reg = GameObject.Find("NewGO_ArrayList");
            PlayMakerArrayListProxy list = null;
            if (reg != null)
                foreach (var p in reg.GetComponents<PlayMakerArrayListProxy>())
                    if ((p.referenceName ?? "").ToLowerInvariant().Contains("item")) { list = p; break; }
            if (counter == null || list == null)
            {
                Core.Log.Warn("[Guns] Cannot drop " + caliber + ": save registry/counter unavailable; retaining ammo");
                return false;
            }
            var mainCam = GameRefs.MainCamera;
            var cam = mainCam != null ? mainCam.transform : at;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = at.forward;
            var pos = cam.position + fwd * 0.7f - Vector3.up * 0.3f;
            var created = new List<GameObject>();
            var staging = new GameObject("WildWasteland.Guns.DropStaging");
            staging.SetActive(false);
            try
            {
                // Grenades and armor plates are individual items; other boxes store a count.
                int itemCount = caliber == "ammo_grenade" || caliber == "armorplate" ? rounds : 1;
                for (int i = 0; i < itemCount; i++)
                {
                    // Configure before activation so Randomize cannot overwrite the returned rounds.
                    var go = UnityEngine.Object.Instantiate(prefab, pos, UnityEngine.Random.rotation, staging.transform);
                    created.Add(go);
                    go.SetActive(false);
                    var rnd = PerGunAmmo.Find(go, "Randomize");
                    if (rnd != null) rnd.enabled = false;
                    if (caliber == "armorplate")
                    {
                        var quantityFsm = PerGunAmmo.Find(go, "Quantity");
                        var quantity = quantityFsm == null ? null : quantityFsm.FsmVariables.GetFsmInt("Quantity");
                        if (quantity != null) quantity.Value = 1;
                    }
                    else
                    {
                        var ammo = PerGunAmmo.Find(go, caliber == "ammo_battery" ? "Batteries" : "Ammo");
                        var v = ammo != null ? ammo.FsmVariables.GetFsmInt(caliber == "ammo_battery" ? "batteries" : "ammo") : null;
                        if (v == null) throw new InvalidOperationException(go.name + " has no ammo count variable");
                        v.Value = caliber == "ammo_grenade" ? 1 : rounds;
                    }
                    counter.Value += 1;
                    go.name = BoxName(caliber) + "(Clone)" + counter.Value;
                    list.arrayList.Add(go);
                }
                foreach (var go in created)
                {
                    go.transform.SetParent(null, true);
                    go.SetActive(true);
                    if (caliber == "armorplate")
                    {
                        go.layer = 9;
                        var visibility = PerGunAmmo.Find(go, "Visibility"); if (visibility != null) visibility.SendEvent("Activate");
                        var colliders = PerGunAmmo.Find(go, "Colliders"); if (colliders != null) colliders.SendEvent("CollidersActivate");
                        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
                        foreach (var collider in go.GetComponentsInChildren<Collider>(true)) { collider.enabled = true; collider.isTrigger = false; }
                    }
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb != null) { rb.isKinematic = false; if (caliber == "armorplate") rb.useGravity = true; rb.velocity = fwd * 1.5f; }
                }
                if (Core.Log.Verbose) Core.Log.Info("[Guns] Dropped " + rounds + " x " + caliber + " in " + created.Count + " save-registered item(s)");
                return true;
            }
            catch (Exception ex)
            {
                foreach (var go in created)
                {
                    list.arrayList.Remove(go);
                    go.SetActive(false);
                    UnityEngine.Object.Destroy(go);
                }
                Core.Log.Warn("[Guns] Could not drop " + caliber + "; retaining ammo: " + ex.Message);
                return false;
            }
            finally { UnityEngine.Object.Destroy(staging); }
        }
    }

    // ---------------------------------------------------------------------------------
    // Harmony patches
    // ---------------------------------------------------------------------------------

    [HarmonyPatch(typeof(IntClamp), "DoClamp")]
    internal static class AmmoCapacity_Patch
    {
        static bool Prefix(IntClamp __instance) { return BackpackOverflow.BeforeClamp(__instance); }
    }

    [HarmonyPatch(typeof(FsmState), "OnEnter")]
    internal static class BackpackSlot_Patch
    {
        static void Postfix(FsmState __instance) { BackpackOverflow.OnSlotEnter(__instance); }
    }

    /// The game's Reload FSMs poll GetButtonDown("Reload"). We take over that button: it no longer fires on press;
    /// instead we fire the action's event on a short tap (released before HoldSeconds).
    /// (WW) Handler of Core.Hooks.GetButtonDownUpdate (prefix); same semantics: the original is skipped for every Reload poll.
    internal static class GetButtonDown_Patch
    {
        private static readonly Dictionary<GetButtonDown, int> _lastTap = new Dictionary<GetButtonDown, int>();
        internal static void Reset() { _lastTap.Clear(); }

        internal static bool Prefix(GetButtonDown __instance)
        {
            if (!AmmoSettings.Enabled.Value) return true;
            if (__instance.buttonName == null || __instance.buttonName.Value != "Reload") return true;
            if (AmmoSettings.TapPending())
            {
                int last;
                if (!_lastTap.TryGetValue(__instance, out last) || last != AmmoSettings.TapFrame)
                {
                    _lastTap[__instance] = AmmoSettings.TapFrame;
                    if (__instance.storeResult != null) __instance.storeResult.Value = true;
                    if (__instance.Fsm != null && __instance.sendEvent != null) __instance.Fsm.Event(__instance.sendEvent);
                    return false;
                }
            }
            if (__instance.storeResult != null) __instance.storeResult.Value = false;
            return false;
        }
    }

    /// (WW) Handler of Core.Hooks.ProcessEvent (observer, never skips).
    internal static class Fsm_Event_Patch
    {
        internal static bool Prefix(Fsm fsm, FsmEvent fsmEvent, FsmEventData data)
        {
            if (!AmmoRunner.Unloader.Busy || fsmEvent == null) return true;
            AmmoRunner.Unloader.OnFsmEvent(fsm, fsmEvent.Name);
            return true;
        }
    }
}
