using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using Patches = WildWasteland.Core.Patches;

namespace WildWasteland.Modules.Quests
{
    /// <summary>
    /// The quest conversation (<see cref="IQuestConversation"/>), Quests part: the native talk session (begin, lines, choices, close)
    /// used when Apocatalker is not installed. Wave A split: Talk patches/install -> Compatibility/Apocatalker/_fromQuests.
    /// </summary>
    internal partial class TalkBridge : IQuestConversation
    {
        internal bool NativeOpen;
        internal int Generation;
        internal bool ExternalAvailable;
        internal readonly EditorSession Session = new EditorSession();
        Transform _nativeNpc;
        string _nativeName = "";
        readonly List<string> _chat = new List<string>();
        readonly List<QuestChoice> _choices = new List<QuestChoice>();
        readonly Dictionary<string, Action> _buttons = new Dictionary<string, Action>();
        bool _pending;
        public bool Open { get { return NativeOpen; } }
        public string Name { get { return _nativeName; } }
        public string Prompt { get; internal set; }
        public int Revision { get; private set; }
        public IList<string> Chat { get { return _chat; } }
        public IList<QuestChoice> Choices { get { return _choices; } }

        internal bool Begin(Transform npc)
        {
            if (NativeOpen || npc == null || !Session.Acquire()) return false;
            Generation++; NativeOpen = true; _nativeNpc = npc; _nativeName = Game.BaseName(npc.name);
            _chat.Clear(); _choices.Clear(); _buttons.Clear(); _pending = false; Prompt = ""; mineSig = ""; Revision++;
            AddLine("What do you need?"); Plugin.I.OnTalkOpen(null); return true;
        }
        void AddLine(string text) { if (_chat.Count >= 100) _chat.RemoveAt(0); _chat.Add(text); Revision++; }
        void NativeButtons(List<TalkBtn> want)
        {
            _buttons.Clear(); _choices.Clear(); _pending = false;
            foreach (var b in want) { _choices.Add(new QuestChoice { Key = b.Key, Text = b.Text }); _buttons[b.Key] = b.Click; }
            Revision++;
        }
        public void Choose(string key)
        {
            Action click;
            if (!NativeOpen || _pending || !_buttons.TryGetValue(key, out click)) return;
            _pending = true; click();
        }
        public void Close()
        {
            if (!NativeOpen) { Prompt = ""; return; }
            if (Plugin.I != null) Plugin.I.OnTalkClose();
            Generation++; NativeOpen = false; _nativeNpc = null; _pending = false; Prompt = "";
            _buttons.Clear(); _choices.Clear(); Voice.Stop(); Session.Close(); Revision++;
        }
    }

    /// <summary>Quests engine, interaction part: the aim-based "talk to" finder, opening the native conversation and filtering crew buttons.</summary>
    internal sealed partial class Plugin
    {
        Transform _interaction;
        readonly RaycastHit[] _interactionHits = new RaycastHit[32];
        public void ExternalOpened(object host) { if (ready && bridge.ExternalAvailable && bridge.TalkingNow) OnTalkOpen(host); }
        public void ExternalClosed() { if (bridge.ExternalAvailable) { OnTalkClose(); bridge.Generation++; Voice.Stop(); deferred.Clear(); } }
        bool CanInteract() { return ready && _interaction != null && !bridge.ExternalAvailable && !bridge.TalkingNow && GameRefs.InGame && !GamePaused() && !EditorSession.Busy; }
        void OpenInteraction() { if (CanInteract()) { showLog = false; bridge.Begin(_interaction); } }
        internal void InteractionFrame()
        {
            if (!bridge.NativeOpen) return;
            if (!Application.isFocused || !ready || !bridge.Session.Open || !GameRefs.InPlay || bridge.Npc == null
                || GameRefs.Player == null || Game.HDist(bridge.Npc.position, GameRefs.Player.transform.position) > 8f || Input.GetKeyDown(KeyCode.Escape))
            { bridge.Close(); deferred.Clear(); return; }
            bridge.Session.Maintain();
        }
        internal void FindInteraction()
        {
            _interaction = null; bridge.Prompt = "";
            if (bridge.ExternalAvailable || bridge.TalkingNow || GamePaused() || EditorSession.Busy || ApocasetterBridge.InputBlocked || !Application.isFocused) return;
            var camera = GameRefs.MainCamera; if (camera == null) return;
            Vector3 eye = camera.transform.position; float best = float.PositiveInfinity;
            foreach (var f in QuestWorld.People)
            {
                if (f == null || f.FsmName != "Health" || f.ActiveStateName != "health" || !f.gameObject.activeInHierarchy) continue;
                var go = f.gameObject; if (go == GameRefs.Player || Game.IsPlayerEnemy(go) || (ModApi.Get<IFollowerVehicles>() != null && ModApi.Get<IFollowerVehicles>().IsSeated(go))) continue;
                string name = Game.BaseName(go.name);
                if (!Person(name) || name.IndexOf("Dead", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                Vector3 aim = f.transform.position + Vector3.up * 1.2f, delta = aim - eye; float distance = delta.magnitude;
                if (GameRefs.Player == null || Game.HDist(f.transform.position, GameRefs.Player.transform.position) > 4f || distance < .01f || Vector3.Dot(camera.transform.forward, delta / distance) < .85f) continue;
                var hp = f.FsmVariables.GetFsmFloat("Health"); if (hp == null || hp.Value <= 0) continue;
                bool blocked = false;
                int count = Physics.RaycastNonAlloc(eye, delta / distance, _interactionHits, distance, ~0, QueryTriggerInteraction.Ignore);
                if (count == _interactionHits.Length) continue;
                for (int i = 0; i < count; i++)
                {
                    var hit = _interactionHits[i]; if (hit.collider == null || hit.transform.IsChildOf(f.transform)) continue;
                    if (GameRefs.Player != null && hit.transform.IsChildOf(GameRefs.Player.transform)) continue;
                    blocked = true; break;
                }
                float score = 1f - Vector3.Dot(camera.transform.forward, delta / distance) + distance * .01f;
                if (!blocked && score < best) { best = score; _interaction = f.transform; }
            }
            if (_interaction != null) bridge.Prompt = "[" + QuestsModule.InteractKey.Value + "] Talk to " + Game.BaseName(_interaction.name);
        }
        static bool Person(string name)
        {
            string n = Util.Norm(name);
            foreach (string word in Factions.PeopleWords) if (word.Length > 0 && n.Contains(word)) return true;
            return Hire.CostFor(name) >= 0 || n.Contains("gunnar") || n.Contains("mechanic");
        }
        void FilterCrewButtons()
        {
            var tr = bridge.Npc; bool crew = CrewHistory.Blocked(tr);
            bool seated = tr != null && ModApi.Get<IFollowerVehicles>() != null && ModApi.Get<IFollowerVehicles>().IsSeated(tr.gameObject);
            for (int i = wantButtons.Count - 1; i >= 0; i--)
            {
                string key = wantButtons[i].Key;
                if ((crew && (key == "work" || key == "accept")) || (seated && key == "hire")) wantButtons.RemoveAt(i);
            }
        }
    }

    // One extra choice in Talk's list of things to say.
    internal class TalkBtn
    {
        public string Key;          // identifies it (the list is rebuilt only when the keys or texts change)
        public string Text;
        public Action Click;
    }
}
