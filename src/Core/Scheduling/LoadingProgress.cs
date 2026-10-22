using System;
using System.Collections.Generic;
using MapMagic.Core;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Combines native/module load progress and holds the native handoff until registered preparation is ready.</summary>
    internal static class LoadingProgress
    {
        /// <summary>Visible progress from one module's preparation work.</summary>
        internal struct Work
        {
            internal float Fraction;
            internal string Detail;
            /// <summary>Progress of one source.</summary>
            internal Work(float fraction, string detail) { Fraction = fraction; Detail = detail; }
        }
        private static readonly Dictionary<IModule, Func<Work?>> Sources = new Dictionary<IModule, Func<Work?>>();
        private static MapMagicObject _map;
        private static Hold[] _holds = new Hold[0];
        private static bool _hooked;
        private static Fsm _handoffFsm;
        private static FsmState _handoffState;

        /// <summary>One named readiness predicate, owned by a module or Core.</summary>
        private sealed class Hold
        {
            internal IModule Owner;
            internal string Name;
            internal Func<bool> Ready;
        }
        internal static bool ScreenVisible;
        /// <summary>Menus must not open (loading screen, save running).</summary>
        internal static bool MenusBlocked { get { var saves = ModApi.Get<ISaves>(); return ScreenVisible || SaveBus.Busy || saves != null && saves.Busy; } }
        /// <summary>Adds a progress source of a module.</summary>
        internal static void Register(IModule owner, Func<Work?> source) { Sources[owner] = source; }
        /// <summary>Removes a module's progress source.</summary>
        internal static void Remove(IModule owner)
        {
            Sources.Remove(owner);
            var previous = _holds;
            var remaining = new List<Hold>(previous.Length);
            for (int i = 0; i < previous.Length; i++) if (previous[i].Owner != owner) remaining.Add(previous[i]);
            _holds = remaining.ToArray();
        }

        /// <summary>Registers or replaces a named load-handoff readiness predicate; all predicates run on each handoff tick.</summary>
        internal static void RegisterHold(IModule owner, string name, Func<bool> ready)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A loading hold needs a name", "name");
            if (ready == null) throw new ArgumentNullException("ready");
            var previous = _holds;
            for (int i = 0; i < previous.Length; i++)
                if (previous[i].Owner == owner && previous[i].Name == name)
                {
                    var replacement = (Hold[])previous.Clone();
                    replacement[i] = new Hold { Owner = owner, Name = name, Ready = ready }; _holds = replacement;
                    return;
                }
            var next = new Hold[previous.Length + 1];
            Array.Copy(previous, next, previous.Length);
            next[previous.Length] = new Hold { Owner = owner, Name = name, Ready = ready }; _holds = next;
            if (_hooked) return;
            _hooked = true;
            Hooks.WaitUpdate.Prefix(null, BeforeHandoff);
        }

        /// <summary>Runs preparation gates only for the cached native SaveLoadGame handoff state.</summary>
        private static bool BeforeHandoff(Wait action)
        {
            var holds = _holds;
            if (holds.Length == 0) return true;
            var save = GameRefs.SaveLoadGame;
            if (save == null || action.Fsm != save.Fsm) return true;
            if (_handoffFsm != save.Fsm)
            {
                _handoffFsm = save.Fsm;
                _handoffState = _handoffFsm.GetState("Wait for Terrain Gen");
            }
            if (action.State != _handoffState) return true;
            bool ready = true;
            for (int i = 0; i < holds.Length; i++)
            {
                var hold = holds[i];
                if (hold.Owner != null && !ModuleHost.IsRunning(hold.Owner)) continue;
                try { if (!hold.Ready()) ready = false; }
                catch (Exception e) { ready = false; ModuleHost.ReportHookException(hold.Owner, "loading hold " + hold.Name, e); }
            }
            return ready;
        }
        /// <summary>Shows the loading screen progress from now on.</summary>
        internal static void Begin() { _map = null; _handoffFsm = null; _handoffState = null; ScreenVisible = true; }

        /// <summary>Combined progress and detail text of every source.</summary>
        internal static void Read(bool saving, out float fraction, out string detail)
        {
            string state = SaveBus.State;
            fraction = 0f;
            detail = saving ? "Core game: saving world" : "Core game: loading scene";
            if (saving)
            {
                fraction = state == "isPlay" ? 1f : state == "SaveGame" ? .5f : .1f;
                return;
            }
            switch (state)
            {
                case "Start": case "setSeed": fraction = .03f; detail = "Core game: preparing world"; break;
                case "LoadGame": fraction = .08f; detail = "Core game: reading save"; break;
                case "generateTerrain": case "generateTerrain 2":
                    var anchor = Origin.AnchorTransform;
                    if (_map == null && anchor != null) _map = anchor.GetComponent<MapMagicObject>();
                    float terrain = _map != null && _map.graph != null ? _map.GetProgress() : 0f;
                    if (float.IsNaN(terrain) || float.IsInfinity(terrain)) terrain = 0f;
                    terrain = Mathf.Clamp01(terrain);
                    fraction = .1f + .5f * terrain;
                    detail = "Core game: generating terrain (" + Mathf.RoundToInt(terrain * 100f) + "%)";
                    break;
                case "LoadVar": fraction = .62f; detail = "Core game: restoring world and items"; break;
                case "loaded": fraction = .65f; detail = "Core game: restoring vehicles and characters"; break;
                case "Wait for Terrain Gen":
                    fraction = .98f; detail = "Core game: preparing player control";
                    foreach (var pair in Sources)
                    {
                        if (!ModuleHost.IsRunning(pair.Key)) continue;
                        Work? work = pair.Value();
                        if (!work.HasValue) continue;
                        fraction = .7f + .28f * Mathf.Clamp01(work.Value.Fraction);
                        detail = pair.Key.DisplayName + ": " + work.Value.Detail;
                        break;
                    }
                    break;
                case "done": case "play": fraction = .99f; detail = "Core game: handing control to player"; break;
                case "isPlay": fraction = 1f; detail = "Ready"; break;
            }
        }
    }
}
