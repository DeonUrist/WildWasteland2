using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Core
{
    /// <summary>
    /// Provider of an exact, hook-maintained FSM index (Performance PM1 FsmRegistry registers one in Wave A).
    /// </summary>
    public interface IFsmIndexProvider
    {
        /// <summary>True while the index is tracking (PM1 has users); otherwise FsmIndex falls back.</summary>
        bool Ready { get; }
        /// <summary>Enabled FSMs on exactly go in PlayMakerFSM.FsmList order (live list; null when none). Must not allocate.</summary>
        List<PlayMakerFSM> On(GameObject go);
    }

    /// <summary>
    /// Shared PlayMaker FSM lookups replacing 9 full PlayMakerFSM.FsmList scans (Quests QuestWorld/CrewHistory/FactionDump,
    /// Towns InteriorPopulation/TownGasStation, Patrols TownTraffic, Weather WeatherNative, Core SaveCombat) and the FSM
    /// variable helpers (Quests Game.FindInt/FindFloat).
    /// - <see cref="ByOwner"/>: enabled FSMs of one GameObject; served by the registered <see cref="IFsmIndexProvider"/>
    ///   (PM1) when ready, otherwise GetComponents filtered to isActiveAndEnabled (same membership as FsmList).
    /// - <see cref="ByName"/>: every FsmList entry with a tracked FSM name ("Health", "ItemName", "ID", ...). Names are
    ///   tracked by <see cref="Track"/>; one Core sliced job ("Core.FsmIndex") walks FsmList in slices (QuestWorld style:
    ///   256 entries per step) and publishes complete lists atomically, then waits <see cref="RefreshSeconds"/> before the next
    ///   pass. Lists are cleared on scene load. Published lists are reused: do not keep them across frames, copy if needed.
    /// </summary>
    public static class FsmIndex
    {
        static IFsmIndexProvider _provider;
        static readonly List<PlayMakerFSM> _buf = new List<PlayMakerFSM>(8);
        static readonly List<PlayMakerFSM> _empty = new List<PlayMakerFSM>(0);

        /// <summary>Seconds between two ByName passes (QuestWorld used 0.5 s).</summary>
        public static float RefreshSeconds = 0.5f;
        /// <summary>FsmList entries examined per sliced step.</summary>
        public static int EntriesPerStep = 256;
        /// <summary>Incremented after each published ByName pass.</summary>
        public static int Revision;
        /// <summary>True once the first ByName pass after a scene load has been published.</summary>
        public static bool Ready;

        /// <summary>One tracked name: owners, the published list and the list being built.</summary>
        private sealed class Tracked
        {
            public readonly List<IModule> Owners = new List<IModule>(2);
            public List<PlayMakerFSM> Published = new List<PlayMakerFSM>();
            public List<PlayMakerFSM> Next = new List<PlayMakerFSM>();
        }

        static readonly Dictionary<string, Tracked> _names = new Dictionary<string, Tracked>(StringComparer.Ordinal);
        static Tracked _all;
        static readonly List<Tracked> _trackedList = new List<Tracked>();
        static int _scan;
        static float _nextPass;
        static bool _installed;

        /// <summary>Registers (or, with null, unregisters) the exact index provider. Only one provider; the last call wins.</summary>
        public static void SetProvider(IFsmIndexProvider provider) { _provider = provider; }

        /// <summary>True when the PM1 provider currently serves ByOwner.</summary>
        public static bool ProviderReady { get { var p = _provider; return p != null && p.Ready; } }

        /// <summary>
        /// Enabled FSMs of go (FsmList order with the provider; component order without it). The returned list is shared
        /// and only valid until the next call: iterate it immediately, never store or modify it.
        /// </summary>
        public static List<PlayMakerFSM> ByOwner(GameObject go)
        {
            if (go == null) return _empty;
            var p = _provider;
            if (p != null && p.Ready) { var l = p.On(go); return l ?? _empty; }
            _buf.Clear();
            go.GetComponents(_buf);
            for (int i = _buf.Count - 1; i >= 0; i--) if (_buf[i] == null || !_buf[i].isActiveAndEnabled) _buf.RemoveAt(i);
            return _buf;
        }

        /// <summary>First FSM named fsmName on go, enabled or not (FsmUtil.Find semantics: GetComponents, no FindFsmOnGameObject).</summary>
        public static PlayMakerFSM Find(GameObject go, string fsmName) { return FsmUtil.Find(go, fsmName); }

        /// <summary>Starts tracking FSMs named fsmName for owner (null name = every FSM, QuestWorld "All").</summary>
        public static void Track(IModule owner, string fsmName)
        {
            Tracked t;
            if (fsmName == null) { if (_all == null) { _all = new Tracked(); _trackedList.Add(_all); } t = _all; }
            else if (!_names.TryGetValue(fsmName, out t)) { _names[fsmName] = t = new Tracked(); _trackedList.Add(t); }
            if (!t.Owners.Contains(owner)) t.Owners.Add(owner);
            Install();
            _nextPass = 0f;
        }

        /// <summary>Stops every tracking of owner (module shutdown); names nobody tracks any more are dropped.</summary>
        public static void RemoveAll(IModule owner)
        {
            _drop.Clear();
            foreach (var pair in _names) { pair.Value.Owners.Remove(owner); if (pair.Value.Owners.Count == 0) _drop.Add(pair.Key); }
            for (int i = 0; i < _drop.Count; i++) { _trackedList.Remove(_names[_drop[i]]); _names.Remove(_drop[i]); }
            _drop.Clear();
            if (_all != null) { _all.Owners.Remove(owner); if (_all.Owners.Count == 0) { _trackedList.Remove(_all); _all = null; } }
        }
        static readonly List<string> _drop = new List<string>();

        /// <summary>
        /// Published FSMs named fsmName (empty until tracked and the first pass finished). Shared list: read it, never modify
        /// it; it may contain FSMs destroyed since the pass (null-check entries).
        /// </summary>
        public static List<PlayMakerFSM> ByName(string fsmName)
        {
            Tracked t;
            return fsmName != null && _names.TryGetValue(fsmName, out t) ? t.Published : _empty;
        }

        /// <summary>Every FSM of the last pass (requires Track(owner, null)).</summary>
        public static List<PlayMakerFSM> All { get { return _all != null ? _all.Published : _empty; } }

        /// <summary>Installs the sliced job and the scene-load reset once.</summary>
        static void Install()
        {
            if (_installed) return;
            _installed = true;
            SceneManager.sceneLoaded += SceneLoaded;
            Scheduler.Sliced(null, "Core.FsmIndex", 300, Step);
        }

        /// <summary>Scene load: drop all published and partial lists; the next pass starts at once.</summary>
        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            for (int i = 0; i < _trackedList.Count; i++) { _trackedList[i].Published.Clear(); _trackedList[i].Next.Clear(); }
            _scan = 0; _nextPass = 0f; Ready = false;
        }

        /// <summary>Sliced step: scans EntriesPerStep FsmList entries into the Next lists; publishes (swaps) when the list end is reached.</summary>
        static bool Step()
        {
            if (_trackedList.Count == 0) return false;
            if (_scan == 0 && Time.unscaledTime < _nextPass) return false;
            var source = PlayMakerFSM.FsmList;
            if (source == null) return false;
            for (int end = Math.Min(source.Count, _scan + EntriesPerStep); _scan < end; )
            {
                var f = source[_scan++];
                if (ReferenceEquals(f, null)) continue;
                if (_all != null) _all.Next.Add(f);
                if (_names.Count == 0) continue;
                Tracked t;
                string n = f.FsmName;
                if (n != null && _names.TryGetValue(n, out t)) t.Next.Add(f);
            }
            if (_scan < source.Count) return true;
            for (int i = 0; i < _trackedList.Count; i++)
            {
                var t = _trackedList[i];
                var old = t.Published; t.Published = t.Next; t.Next = old; old.Clear();
            }
            _scan = 0; _nextPass = Time.unscaledTime + RefreshSeconds; Ready = true; Revision++;
            return false;
        }

        // ------------------------------------------------------------------ FSM variables (no FindFsm*: they log per miss in some versions)

        /// <summary>The FsmInt named name of f, or null (Quests Game.FindInt).</summary>
        public static FsmInt Int(PlayMakerFSM f, string name)
        {
            if (f == null || f.FsmVariables == null) return null;
            var a = f.FsmVariables.IntVariables; if (a == null) return null;
            for (int i = 0; i < a.Length; i++) if (a[i] != null && a[i].Name == name) return a[i];
            return null;
        }

        /// <summary>The FsmFloat named name of f, or null (Quests Game.FindFloat).</summary>
        public static FsmFloat Float(PlayMakerFSM f, string name)
        {
            if (f == null || f.FsmVariables == null) return null;
            var a = f.FsmVariables.FloatVariables; if (a == null) return null;
            for (int i = 0; i < a.Length; i++) if (a[i] != null && a[i].Name == name) return a[i];
            return null;
        }

        /// <summary>The FsmString named name of f, or null.</summary>
        public static FsmString String(PlayMakerFSM f, string name)
        {
            if (f == null || f.FsmVariables == null) return null;
            var a = f.FsmVariables.StringVariables; if (a == null) return null;
            for (int i = 0; i < a.Length; i++) if (a[i] != null && a[i].Name == name) return a[i];
            return null;
        }

        /// <summary>The FsmBool named name of f, or null.</summary>
        public static FsmBool Bool(PlayMakerFSM f, string name)
        {
            if (f == null || f.FsmVariables == null) return null;
            var a = f.FsmVariables.BoolVariables; if (a == null) return null;
            for (int i = 0; i < a.Length; i++) if (a[i] != null && a[i].Name == name) return a[i];
            return null;
        }

        /// <summary>The FsmGameObject named name of f, or null.</summary>
        public static FsmGameObject Go(PlayMakerFSM f, string name)
        {
            if (f == null || f.FsmVariables == null) return null;
            var a = f.FsmVariables.GameObjectVariables; if (a == null) return null;
            for (int i = 0; i < a.Length; i++) if (a[i] != null && a[i].Name == name) return a[i];
            return null;
        }
    }
}
