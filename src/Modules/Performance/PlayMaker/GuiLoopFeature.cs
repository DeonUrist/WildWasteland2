using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Log = WildWasteland.Core.Log;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// PM5 (plan §4, research A §1.7 / R6). Three exact replacements around PlayMaker's IMGUI and visibility callbacks:
    ///
    /// 1. PlayMakerGUI.OnGUI (PlayMakerGUI.cs:349-399) copies PlayMakerFSM.FsmList and, for EVERY enabled FSM, checks Active /
    ///    ActiveState / HandleOnGUI and calls OnGUI on the active state's Active actions (CallOnGUI) and on its sub-FSMs, once
    ///    per IMGUI event (2-3 per frame). Only an action type that overrides FsmStateAction.OnGUI (base is empty) or a sub-FSM
    ///    can make that loop do anything, so the replacement iterates only "candidate" FSMs (any state has such an action, or
    ///    sub-FSMs exist), kept in FsmList order (appended in PlayMakerFSM.OnEnable after vanilla appends, removed in
    ///    OnDisable/OnDestroy: same relative order as FsmList), snapshotted before the loop like vanilla. The loop body and
    ///    the rest of OnGUI (skin/colors, cursor texture, state labels, Cursor.lockState/visible on Repaint) are copied 1:1.
    ///    In the game only ActivateGameObjects (107 uses, always on FSMs with HandleOnGUI = skipped here) and SetMouseCursor
    ///    (5 uses) override OnGUI, so the loop goes from ~N FSMs to a handful.
    /// 2. The 47 PlayMakerOnGUI proxy components (PlayMakerOnGUI.cs) call Fsm.OnGUI on FSMs whose only OnGUI actions are
    ///    ActivateGameObjects; that action always Finish()es in OnEnter, so the call normally does nothing but
    ///    FsmState.OnGUI's RemoveFinishedActions(). Those proxies are disabled after their Start() and their OnGUI is emulated
    ///    from the PlayMakerGUI pass (same number of IMGUI events): Fsm.OnGUI() is called exactly when it would have an effect
    ///    (finished actions pending, or an OnGUI-overriding action still active). Restored (enabled) on switch-off; if
    ///    PlayMakerGUI stops running (no OnGUI for ~1 s) the proxies are re-enabled.
    /// 3. PlayMakerFSM.OnBecameVisible/OnBecameInvisible (PMF.cs:540-548) send BECAME VISIBLE/INVISIBLE through the full
    ///    Event path on every renderer visibility flip; no game FSM handles them. Skipped exactly per <see cref="FsmEventSkip"/>
    ///    (vanilla kept when the FSM is not started, has a transition / OnEvent handler / output listener, or targets elsewhere).
    /// </summary>
    public sealed class GuiLoopFeature : Feature
    {
        public override string PlanId { get { return "PM5"; } }
        public override string Key { get { return "GUI loop"; } }
        public override string Title { get { return "PlayMaker GUI loop and visibility events"; } }
        public override string Category { get { return Categories.PlayMaker; } }
        public override int Order { get { return 5; } }
        public override string Help { get { return "PlayMaker's GUI pass visits only the few FSMs that draw anything, unused OnGUI helpers stop running, and unhandled became-visible events are skipped. Same result."; } }

        internal static bool On;
        static bool _suspended;          // proxies left enabled because PlayMakerGUI is not running
        static int _lastGuiFrame;
        static Job _guard;
        public static long GuiPasses, CandidateCalls, ProxyEmulated, VisibilitySkipped;

        static readonly Func<PlayMakerFSM, Fsm> PmfFsm = FastReflect.Getter<PlayMakerFSM, Fsm>(typeof(PlayMakerFSM), "fsm");
        static readonly Func<FsmState, List<FsmStateAction>> FinishedActions = FastReflect.Getter<FsmState, List<FsmStateAction>>(typeof(FsmState), "_finishedActions");
        static readonly Func<Fsm, List<Fsm>> SubFsms = FastReflect.Getter<Fsm, List<Fsm>>(typeof(Fsm), "subFsmList");
        static readonly FieldInfo VanillaScratch = AccessTools.Field(typeof(PlayMakerGUI), "fsmList");
        static Action<PlayMakerGUI> _drawStateLabels;

        // ---- candidates (FsmList order) ----
        static readonly List<PlayMakerFSM> _cands = new List<PlayMakerFSM>(64);
        static readonly HashSet<PlayMakerFSM> _candSet = new HashSet<PlayMakerFSM>(RefComparer<PlayMakerFSM>.Instance);
        static readonly Dictionary<Fsm, bool> _isCand = new Dictionary<Fsm, bool>(RefComparer<Fsm>.Instance);
        static bool _dirty = true;
        static PlayMakerFSM[] _snap = new PlayMakerFSM[64];
        static bool _inPass;

        // ---- disabled PlayMakerOnGUI proxies (emulated) ----
        static readonly List<PlayMakerOnGUI> _proxies = new List<PlayMakerOnGUI>(64);
        static readonly HashSet<PlayMakerOnGUI> _proxySet = new HashSet<PlayMakerOnGUI>(RefComparer<PlayMakerOnGUI>.Instance);

        static readonly Dictionary<Type, bool> _guiType = new Dictionary<Type, bool>();

        // PlayMakerFSM.OnEnable is shared with Patrols and the registry: Core.Hooks.FsmEnable postfix
        public override void SubscribeHooks() { Hooks.FsmEnable.Postfix(Module, OnFsmEnabled); }
        static void OnFsmEnabled(PlayMakerFSM f) { if (On) Added(f); }
        protected override void OnEnable(FeatureMode mode)
        {
            if (!EnsurePatched(typeof(Patch))) throw new Exception("PlayMakerGUI / visibility patches failed");
            if (_drawStateLabels == null)
            {
                var m = AccessTools.Method(typeof(PlayMakerGUI), "DrawStateLabels");
                if (m == null) throw new Exception("PlayMakerGUI.DrawStateLabels not found");
                _drawStateLabels = AccessTools.MethodDelegate<Action<PlayMakerGUI>>(m);
            }
            if (_guard == null || _guard.Removed) _guard = Scheduler.Interval(Module, "Performance.GuiProxyGuard", 1f, GuardTick);
            _isCand.Clear(); _dirty = true; _suspended = false;
            _lastGuiFrame = Time.frameCount;
            // vanilla's scratch copy of FsmList is no longer refreshed: drop the references it holds
            try { var l = VanillaScratch != null ? VanillaScratch.GetValue(null) as List<PlayMakerFSM> : null; if (l != null) l.Clear(); } catch { }
            On = true;
            DisableExistingProxies();
        }

        protected override void OnDisable()
        {
            On = false;
            RestoreProxies();
            _cands.Clear(); _candSet.Clear(); _isCand.Clear(); _dirty = true;
            Array.Clear(_snap, 0, _snap.Length);
        }

        public override void OnSceneLoaded() { _isCand.Clear(); _dirty = true; _suspended = false; PruneProxies(); }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("GUI candidates / disabled OnGUI helpers", _cands.Count + " / " + _proxies.Count + (_suspended ? " (suspended)" : ""));
            l.Value("GUI passes / FSM GUI calls / helper calls", GuiPasses + " / " + CandidateCalls + " / " + ProxyEmulated);
            l.Value("Visibility events skipped", VisibilitySkipped.ToString());
        }

        // ---------------------------------------------------------------- candidates

        static bool OverridesOnGui(Type t)
        {
            bool v;
            if (_guiType.TryGetValue(t, out v)) return v;
            try
            {
                var m = t.GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                v = m == null || m.DeclaringType != typeof(FsmStateAction);
            }
            catch (Exception) { v = true; }   // ambiguous: be conservative
            _guiType[t] = v;
            return v;
        }

        /// <summary>Could PlayMakerGUI's loop ever do anything for this FSM? (OnGUI-overriding action in any state, unloaded
        /// actions, or sub-FSMs). Cached per Fsm; sub-FSM creation re-marks the host.</summary>
        static bool IsCandidate(Fsm fsm)
        {
            if (fsm == null) return false;
            bool v;
            if (_isCand.TryGetValue(fsm, out v)) return v;
            v = Compute(fsm);
            _isCand[fsm] = v;
            return v;
        }

        static bool Compute(Fsm fsm)
        {
            var subs = SubFsms(fsm);
            if (subs != null && subs.Count > 0) return true;
            var states = fsm.States;
            if (states == null) return false;
            for (int i = 0; i < states.Length; i++)
            {
                var st = states[i];
                if (st == null) continue;
                if (!st.ActionsLoaded) return true;          // vanilla would load them in CallOnGUI: keep it in the loop
                var acts = st.Actions;
                for (int j = 0; j < acts.Length; j++) if (acts[j] != null && OverridesOnGui(acts[j].GetType())) return true;
            }
            return false;
        }

        /// <summary>Every OnGUI-overriding action of the FSM is an ActivateGameObjects (and no sub-FSMs).</summary>
        static bool OnlyActivateGameObjects(Fsm fsm)
        {
            var subs = SubFsms(fsm);
            if (subs != null && subs.Count > 0) return false;
            var states = fsm.States;
            if (states == null) return false;
            for (int i = 0; i < states.Length; i++)
            {
                var st = states[i];
                if (st == null) continue;
                if (!st.ActionsLoaded) return false;
                var acts = st.Actions;
                for (int j = 0; j < acts.Length; j++)
                {
                    var a = acts[j];
                    if (a == null) continue;
                    var t = a.GetType();
                    if (t != typeof(ActivateGameObjects) && OverridesOnGui(t)) return false;
                }
            }
            return true;
        }

        static void Rebuild()
        {
            _dirty = false;
            _cands.Clear(); _candSet.Clear();
            var list = PlayMakerFSM.FsmList;
            for (int i = 0; i < list.Count; i++)
            {
                var pmf = list[i];
                if (ReferenceEquals(pmf, null) || _candSet.Contains(pmf)) continue;
                Fsm fsm;
                try { fsm = PmfFsm(pmf); } catch { continue; }
                if (IsCandidate(fsm)) { _cands.Add(pmf); _candSet.Add(pmf); }
            }
        }

        static void Added(PlayMakerFSM pmf)
        {
            var fsm = PmfFsm(pmf);
            if (fsm == null) return;
            if (fsm.HandleOnGUI && !_suspended) Adopt(pmf, fsm);
            if (_dirty || _candSet.Contains(pmf)) return;   // a pending rebuild picks it up from FsmList
            if (IsCandidate(fsm)) { _cands.Add(pmf); _candSet.Add(pmf); }
        }

        static void Removed(PlayMakerFSM pmf)
        {
            if (!_candSet.Remove(pmf)) return;
            for (int i = _cands.Count - 1; i >= 0; i--) if (ReferenceEquals(_cands[i], pmf)) { _cands.RemoveAt(i); break; }
        }

        // ---------------------------------------------------------------- the GUI pass (exact copy of PlayMakerGUI.OnGUI)

        static void CallOnGUI(Fsm fsm)
        {
            if (fsm.ActiveState == null) return;
            var actions = fsm.ActiveState.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                var a = actions[i];
                if (a.Active) a.OnGUI();
            }
        }

        static void Pass(PlayMakerGUI gui)
        {
            GuiPasses++;
            gui.useGUILayout = gui.enableGUILayout;
            if (PlayMakerGUI.GUISkin != null) GUI.skin = PlayMakerGUI.GUISkin;
            GUI.color = PlayMakerGUI.GUIColor;
            GUI.backgroundColor = PlayMakerGUI.GUIBackgroundColor;
            GUI.contentColor = PlayMakerGUI.GUIContentColor;

            if (_dirty) Rebuild();
            // snapshot (vanilla iterates a copy of FsmList; FSMs enabled during the pass are not visited)
            int n = _cands.Count;
            bool nested = _inPass;   // IMGUI is not re-entrant, but be safe: a nested pass uses its own copy
            PlayMakerFSM[] snap;
            if (nested) snap = new PlayMakerFSM[n];
            else
            {
                if (_snap.Length < n) _snap = new PlayMakerFSM[Mathf.NextPowerOfTwo(n)];
                snap = _snap;
            }
            _cands.CopyTo(snap, 0);
            _inPass = true;
            try
            {
                for (int i = 0; i < n; i++)
                {
                    var pmf = snap[i];
                    if (!(pmf == null) && pmf.Active && pmf.Fsm.ActiveState != null && !pmf.Fsm.HandleOnGUI)
                    {
                        CandidateCalls++;
                        CallOnGUI(pmf.Fsm);
                        var subs = pmf.Fsm.SubFsmList;
                        for (int j = 0; j < subs.Count; j++) CallOnGUI(subs[j]);
                    }
                }
            }
            finally
            {
                if (!nested) { Array.Clear(_snap, 0, n); _inPass = false; }
            }

            EmulateProxies();

            if (Application.isPlaying && Event.current.type == EventType.Repaint)
            {
                Matrix4x4 matrix = GUI.matrix;
                GUI.matrix = Matrix4x4.identity;
                var cursor = PlayMakerGUI.MouseCursor;
                if (cursor != null)
                {
                    GUI.DrawTexture(new Rect(Input.mousePosition.x - (float)cursor.width * 0.5f, (float)Screen.height - Input.mousePosition.y - (float)cursor.height * 0.5f, cursor.width, cursor.height), cursor);
                }
                if (gui.drawStateLabels && PlayMakerGUI.EnableStateLabels) _drawStateLabels(gui);
                GUI.matrix = matrix;
                PlayMakerGUI.GUIMatrix = Matrix4x4.identity;
                if (gui.controlMouseCursor)
                {
                    UnityEngine.Cursor.lockState = PlayMakerGUI.LockCursor ? CursorLockMode.Locked : CursorLockMode.None;
                    UnityEngine.Cursor.visible = !PlayMakerGUI.HideCursor;
                }
            }
        }

        // ---------------------------------------------------------------- PlayMakerOnGUI proxies

        static bool TryDisable(PlayMakerOnGUI p)
        {
            if (p == null || !p.enabled || _proxySet.Contains(p)) return false;
            var pmf = p.playMakerFSM;
            if (pmf == null) return false;
            var fsm = PmfFsm(pmf);
            // HandleOnGUI already true = the proxy's Start() effect (Fsm.HandleOnGUI = true) is in place
            if (fsm == null || !fsm.HandleOnGUI || !OnlyActivateGameObjects(fsm)) return false;
            if (!p.gameObject.scene.IsValid()) return false;   // never touch prefab assets
            p.enabled = false;
            _proxies.Add(p); _proxySet.Add(p);
            return true;
        }

        /// <summary>A clone of an object whose proxy we disabled carries enabled = false (vanilla never disables them).</summary>
        static void Adopt(PlayMakerFSM pmf, Fsm fsm)
        {
            var p = pmf.GetComponent<PlayMakerOnGUI>();
            if (p == null || p.enabled || _proxySet.Contains(p)) return;
            if (!ReferenceEquals(p.playMakerFSM, pmf) || !OnlyActivateGameObjects(fsm)) return;
            _proxies.Add(p); _proxySet.Add(p);
        }

        static void DisableExistingProxies()
        {
            if (_suspended) return;
            PlayMakerOnGUI[] all;
            try { all = Resources.FindObjectsOfTypeAll<PlayMakerOnGUI>(); } catch (Exception e) { Log.Warn("[Performance] PM5 proxy scan: " + e.Message); return; }
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (TryDisable(all[i])) n++;
            if (n > 0 && Log.Verbose) Log.Info("[Performance] PM5 disabled " + n + " PlayMakerOnGUI helpers (emulated)");
        }

        static void RestoreProxies()
        {
            for (int i = 0; i < _proxies.Count; i++)
            {
                var p = _proxies[i];
                if (p != null && !p.enabled) p.enabled = true;
            }
            _proxies.Clear(); _proxySet.Clear();
        }

        static void PruneProxies()
        {
            for (int i = _proxies.Count - 1; i >= 0; i--)
                if (_proxies[i] == null) { _proxySet.Remove(_proxies[i]); _proxies.RemoveAt(i); }
        }

        /// <summary>PlayMakerOnGUI.OnGUI for every disabled proxy that Unity would have called (GameObject active), but only
        /// when Fsm.OnGUI would change something: FsmState.OnGUI = OnGUI on ActiveActions + RemoveFinishedActions().</summary>
        static void EmulateProxies()
        {
            for (int i = 0; i < _proxies.Count; i++)
            {
                var p = _proxies[i];
                if (ReferenceEquals(p, null)) continue;
                var pmf = p.playMakerFSM;
                if (ReferenceEquals(pmf, null)) continue;
                var fsm = PmfFsm(pmf);
                if (fsm == null) continue;
                var st = fsm.ActiveState;
                if (st == null) continue;
                var fin = FinishedActions(st);
                bool needed = fin != null && fin.Count > 0;
                if (!needed)
                {
                    var acts = st.ActiveActions;
                    for (int j = 0; j < acts.Count; j++) if (acts[j] != null && OverridesOnGui(acts[j].GetType())) { needed = true; break; }
                }
                if (!needed) continue;
                if (p == null) continue;                              // destroyed
                if (p.enabled || !p.gameObject.activeInHierarchy) continue;   // Unity calls it itself / would not call it
                ProxyEmulated++;
                // PlayMakerOnGUI.OnGUI (play mode branch)
                if (p.playMakerFSM != null && p.playMakerFSM.Fsm != null && p.playMakerFSM.Fsm.HandleOnGUI) p.playMakerFSM.Fsm.OnGUI();
            }
        }

        static void GuardTick()
        {
            if (!On || _proxies.Count == 0) return;
            if (Time.frameCount - _lastGuiFrame > 60)
            {
                _suspended = true;
                RestoreProxies();
                Log.Info("[Performance] PM5: PlayMakerGUI is not running; OnGUI helpers re-enabled");
            }
        }

        // ---------------------------------------------------------------- visibility events

        static bool Visibility(PlayMakerFSM pmf, FsmEvent e)
        {
            var fsm = PmfFsm(pmf);
            if (fsm == null || !FsmEventSkip.Unhandled(fsm, e)) return true;
            FsmEventSkip.Apply(fsm);
            VisibilitySkipped++;
            return false;
        }

        [HarmonyPatch]
        static class Patch
        {
            [HarmonyPatch(typeof(PlayMakerGUI), "OnGUI"), HarmonyPrefix]
            static bool OnGuiPre(PlayMakerGUI __instance)
            {
                if (!On || !Application.isPlaying) return true;
                _lastGuiFrame = Time.frameCount;
                if (_suspended && _proxies.Count == 0) _suspended = false;   // PlayMakerGUI runs again: new helpers may be disabled
                Pass(__instance);
                return false;
            }


            [HarmonyPatch(typeof(PlayMakerFSM), "OnDisable"), HarmonyPrefix]
            static void OnDisablePre(PlayMakerFSM __instance) { if (On) Removed(__instance); }

            [HarmonyPatch(typeof(PlayMakerFSM), "OnDestroy"), HarmonyPrefix]
            static void OnDestroyPre(PlayMakerFSM __instance)
            {
                if (!On) return;
                Removed(__instance);
                var f = PmfFsm(__instance);
                if (f != null) _isCand.Remove(f);
            }

            [HarmonyPatch(typeof(Fsm), "CreateSubFsm"), HarmonyPostfix]
            static void CreateSubFsmPost(Fsm __instance) { if (On) { _isCand[__instance] = true; _dirty = true; } }

            [HarmonyPatch(typeof(PlayMakerOnGUI), "Start"), HarmonyPostfix]
            static void ProxyStartPost(PlayMakerOnGUI __instance) { if (On && !_suspended) TryDisable(__instance); }

            [HarmonyPatch(typeof(PlayMakerFSM), "OnBecameVisible"), HarmonyPrefix]
            static bool BecameVisiblePre(PlayMakerFSM __instance) { return !On || Visibility(__instance, FsmEvent.BecameVisible); }

            [HarmonyPatch(typeof(PlayMakerFSM), "OnBecameInvisible"), HarmonyPrefix]
            static bool BecameInvisiblePre(PlayMakerFSM __instance) { return !On || Visibility(__instance, FsmEvent.BecameInvisible); }
        }
    }
}
