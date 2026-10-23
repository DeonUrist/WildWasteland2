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

        internal readonly EditorSession Session = new EditorSession();
        Transform _nativeNpc;
        string _nativeName = "";
        readonly List<string> _chat = new List<string>();
        readonly List<QuestChoice> _choices = new List<QuestChoice>();
        readonly Dictionary<string, Action> _buttons = new Dictionary<string, Action>();
        bool _pending;
        private IExternalQuestConversation _external;
        private IModule _owner;
        private string _choiceSignature="";
        /// <summary>Rebinds the optional provider and invalidates queued choices when its actual owner changes.</summary>
        private IExternalQuestConversation External
        {
            get
            {
                var next=ModApi.Get<IExternalQuestConversation>();if(ReferenceEquals(next,_external))return _external;
                if(_external!=null)_external.Unsubscribe(_owner);
                Generation++;_external=next;
                if(_external!=null && _owner!=null){if(NativeOpen)Close();_external.Subscribe(_owner,Plugin.I.ExternalOpened,Plugin.I.ExternalClosed,Plugin.I.OnTalkAction);}
                return _external;
            }
        }
        /// <summary>Attaches the optional provider to the current quest owner without installing foreign hooks.</summary>
        internal void InstallOptional(QuestsModule owner){_owner=owner;var current=External;}
        /// <summary>Releases native input and external choices/callbacks for this quest session.</summary>
        internal void Uninstall(){try{Close();ClearButtons();Session.Close(true);}finally{_external?.Unsubscribe(_owner);_external=null;_owner=null;}}
        /// <summary>True while Compatibility provides the actual external adapter.</summary>
        internal bool ExternalAvailable=>External!=null;
        /// <summary>Whether either native or optional conversation is currently open.</summary>
        public bool TalkingNow=>NativeOpen||External?.TalkingNow==true;
        /// <summary>Current speaker identity from the active conversation owner.</summary>
        public string NpcName=>NativeOpen?_nativeName:External?.NpcName??"";
        /// <summary>Current speaker actor from the active conversation owner.</summary>
        public Transform Npc=>NativeOpen?_nativeNpc:External?.Npc;
        /// <summary>Updates external host identity through its actual adapter.</summary>
        public void Bind(object host){if(NativeOpen)Close();External?.Bind(host);Generation++;}
        /// <summary>Appends an NPC line through the active conversation implementation.</summary>
        public void Say(string text){if(NativeOpen)AddLine(text);else External?.Say(text);}
        /// <summary>Appends a player line through the active conversation implementation.</summary>
        public void You(string text){if(NativeOpen)AddLine("You: "+text);else External?.You(text);}
        /// <summary>Queries optional skin resources without owning external reflection or textures.</summary>
        public bool WindowSkin(out Texture2D texture,out RectOffset border){var api=External;if(api!=null)return api.WindowSkin(out texture,out border);texture=null;border=null;return false;}
        /// <summary>Determines whether the visible choices differ from the current quest projection.</summary>
        public bool NeedsSync(List<QuestDialogueButton> choices){return NativeOpen?Signature(choices)!=_choiceSignature:External?.NeedsSync(choices)==true;}
        /// <summary>Publishes native choices locally or delegates external UI changes to Compatibility.</summary>
        public void SyncButtons(List<QuestDialogueButton> choices){if(NativeOpen){NativeButtons(choices);_choiceSignature=Signature(choices);}else External?.SyncButtons(choices);}
        /// <summary>Clears native action state and optional external quest choices.</summary>
        public void ClearButtons(){_choices.Clear();_buttons.Clear();_pending=false;_choiceSignature="";Revision++;External?.ClearButtons();}
        /// <summary>Compares choices by their semantic identity and text.</summary>
        private static string Signature(List<QuestDialogueButton> choices){var s=new System.Text.StringBuilder();foreach(var c in choices)s.Append(c.Key).Append('\0').Append(c.Text).Append('\0');return s.ToString();}
        /// <summary>Whether the native quest conversation is open.</summary>
        public bool Open { get { return NativeOpen; } }
        /// <summary>Current native conversation speaker name.</summary>
        public string Name { get { return _nativeName; } }
        public string Prompt { get; internal set; }
        public int Revision { get; private set; }
        /// <summary>Current native conversation chat lines.</summary>
        public IList<string> Chat { get { return _chat; } }
        /// <summary>Current typed conversation choices.</summary>
        public IList<QuestChoice> Choices { get { return _choices; } }

        /// <summary>Acquires the editor input session and opens a native conversation.</summary>
        internal bool Begin(Transform npc)
        {
            if (NativeOpen || npc == null || !Session.Acquire()) return false;
            Generation++; NativeOpen = true; _nativeNpc = npc; _nativeName = Core.Names.Base(npc.name);
            _chat.Clear(); _choices.Clear(); _buttons.Clear(); _pending = false; Prompt = ""; _choiceSignature = ""; Revision++;
            AddLine("What do you need?"); Plugin.I.OnTalkOpen(null); return true;
        }
        /// <summary>Appends a bounded native conversation chat line.</summary>
        void AddLine(string text) { if (_chat.Count >= 100) _chat.RemoveAt(0); _chat.Add(text); Revision++; }
        /// <summary>Replaces native conversation buttons with the current quest choices.</summary>
        void NativeButtons(List<QuestDialogueButton> want)
        {
            _buttons.Clear(); _choices.Clear(); _pending = false;
            foreach (var b in want) { _choices.Add(new QuestChoice { Key = b.Key, Text = b.Text }); _buttons[b.Key] = b.Click; }
            Revision++;
        }
        /// <summary>Invokes one current conversation choice at most once.</summary>
        public void Choose(string key)
        {
            Action click;
            if (!NativeOpen || _pending || !_buttons.TryGetValue(key, out click)) return;
            _pending = true; click();
        }
        /// <summary>Closes native conversation and releases its input session.</summary>
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
        /// <summary>Forwards an optional external host opening into the quest engine.</summary>
        public void ExternalOpened(object host) { if (ready && bridge.ExternalAvailable && bridge.TalkingNow) OnTalkOpen(host); }
        /// <summary>Ends the current optional external conversation session.</summary>
        public void ExternalClosed() { if (bridge.ExternalAvailable) { OnTalkClose(); bridge.Generation++; Voice.Stop(); deferred.Clear(); } }
        /// <summary>Tests current interaction admission and editor/input ownership.</summary>
        bool CanInteract() { return ready && _interaction != null && !bridge.ExternalAvailable && !bridge.TalkingNow && GameRefs.InGame && !GamePaused() && !EditorSession.Busy; }
        /// <summary>Opens the native conversation for the current aimed actor.</summary>
        void OpenInteraction() { if (CanInteract()) { showLog = false; bridge.Begin(_interaction); } }
        /// <summary>Maintains conversation lifetime and closes it when its actor or input lease is lost.</summary>
        internal void InteractionFrame()
        {
            if (!bridge.NativeOpen) return;
            if (!Application.isFocused || !ready || !bridge.Session.Open || !GameRefs.InPlay || bridge.Npc == null
                || GameRefs.Player == null || Core.WorldGeometry.HorizontalDistance(bridge.Npc.position, GameRefs.Player.transform.position) > 8f || Input.GetKeyDown(KeyCode.Escape))
            { bridge.Close(); deferred.Clear(); return; }
            bridge.Session.Maintain();
        }
        /// <summary>Finds a friendly nearby actor under the shared gameplay aim.</summary>
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
                string name = Core.Names.Base(go.name);
                if (!Person(name) || name.IndexOf("Dead", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                Vector3 aim = f.transform.position + Vector3.up * 1.2f, delta = aim - eye; float distance = delta.magnitude;
                if (GameRefs.Player == null || Core.WorldGeometry.HorizontalDistance(f.transform.position, GameRefs.Player.transform.position) > 4f || distance < .01f || Vector3.Dot(camera.transform.forward, delta / distance) < .85f) continue;
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
            if (_interaction != null) bridge.Prompt = "[" + QuestsModule.InteractKey.Value + "] Talk to " + Core.Names.Base(_interaction.name);
        }
        /// <summary>Tests whether an actor name supports quest or hiring interaction.</summary>
        static bool Person(string name)
        {
            string n = Util.Norm(name);
            if (n.Contains("merchant") && SkillPerks.Has("MerchantReroll")) return true;
            foreach (string word in Factions.PeopleWords) if (word.Length > 0 && n.Contains(word)) return true;
            return Hire.CostFor(name) >= 0 || n.Contains("gunnar") || n.Contains("mechanic");
        }
        /// <summary>Removes quest choices that do not apply to seated crew.</summary>
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

}
