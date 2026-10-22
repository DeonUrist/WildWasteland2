using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using TMPro;
using UnityEngine;
using WildWasteland.Core;
using UnityEngine.UI;

namespace WildWasteland.Modules.Compatibility
{
    // Talks to Apocalypter Talk ("Apocatalker") through reflection: its dialogue window is made of Unity UI objects and
    // all of its members are private / internal. Quest choices are copies of Talk's own choice buttons, put just above
    // its "Goodbye." button (the same way the Rumors mod adds its button).
    /// <summary>
    /// Apocatalker (Apocalypter Talk) bridge part of <see cref="ExternalQuestConversation"/> (Wave A, from Quests/TalkHook.cs; B17 Compatibility/Apocatalker):
    /// reflection access to Talk's NPC/UI fields, copied choice buttons above "Goodbye.", Talk's window skin, and the choice sync
    /// used by both the Talk window and the native conversation.
    /// </summary>
    internal sealed partial class ExternalQuestConversation
    {
        const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        const BindingFlags Stat = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        const string Prefix = "AQ|";

        public Type TalkType;
        public object Instance;
        FieldInfo fName, fNpc, fUi, fPromptContent, fTalking;
        MethodInfo mAddNpc, mAppendChatLine;
        Func<object, string> readName; Func<object, Transform> readNpc;
        Func<object, object> readUi; Func<object, bool> readTalking;
        Func<object, RectTransform> readPrompts;
        Action<object, string> addNpc, appendChat;
        readonly List<string> sigKeys = new List<string>(), sigTexts = new List<string>();
        string signature;
        /// <summary>Generated getter of one of Talk's fields (Core Access).</summary>
        static Func<object, T> Reader<T>(FieldInfo field)
        {
            return Access.Read<T>(field);
        }
        /// <summary>Generated call of one of Talk's (object, string) methods (Core Access).</summary>
        static Action<object, string> Caller(MethodInfo method)
        {
            return Access.Call<Action<object, string>>(method, typeof(void), typeof(object), typeof(string));
        }

        readonly List<GameObject> mine = new List<GameObject>();
        string mineSig = "";

        /// <summary>Resolves Talk's NPC name/NPC/UI/Talking fields and AddNpc; false (logged) when a required member is missing.</summary>
        public bool Init(Type talkType)
        {
            TalkType = talkType;
            fName = talkType.GetField("npcName", Inst);
            fNpc = talkType.GetField("currentNpc", Inst);
            fUi = talkType.GetField("dialogueUI", Inst);
            fTalking = talkType.GetField("Talking", Stat);
            mAddNpc = talkType.GetMethod("AddNpc", Inst);

            string missing = "";
            if (fName == null) missing += " npcName";
            if (fNpc == null) missing += " currentNpc";
            if (fUi == null) missing += " dialogueUI";
            if (mAddNpc == null) missing += " AddNpc";
            if (missing.Length > 0)
            {
                Core.Log.Error(CompatibilityModule.Instance,"Apocalypter Talk is missing members this mod needs (" + missing.Trim() + "). Is it a different version than the one this mod was made for (Apocatalker)? Quest dialogue is disabled.");
                return false;
            }
            readName = Reader<string>(fName); readNpc = Reader<Transform>(fNpc); readUi = Reader<object>(fUi);
            if (fTalking != null) readTalking = Reader<bool>(fTalking); addNpc = Caller(mAddNpc);
            return true;
        }

        /// <summary>True while a Talk conversation (or the native conversation) is open.</summary>
        public bool TalkingNow
        {
            get
            {
                if (fTalking == null) return false;
                try { return readTalking(Instance); } catch (Exception) { return false; }
            }
        }

        /// <summary>Binds the Talk plugin instance (closes a native conversation first).</summary>
        public void Bind(object inst) { Instance = inst; }

        /// <summary>Talk's DialogueUI object, or null.</summary>
        object Ui
        {
            get { return Instance == null ? null : readUi(Instance); }
        }

        /// <summary>Talk's choice list container (resolved lazily with AppendChatLine).</summary>
        RectTransform PromptContent
        {
            get
            {
                object ui = Ui;
                if (ui == null) return null;
                if (fPromptContent == null)
                {
                    fPromptContent = ui.GetType().GetField("promptContent", Inst);
                    if (fPromptContent == null) return null;
                    mAppendChatLine = ui.GetType().GetMethod("AppendChatLine", Inst);
                    readPrompts = Reader<RectTransform>(fPromptContent); if (mAppendChatLine != null) appendChat = Caller(mAppendChatLine);
                }
                return readPrompts(ui);
            }
        }

        /// <summary>Name of the NPC being talked to.</summary>
        public string NpcName
        {
            get { return Instance == null ? "" : (readName(Instance) ?? ""); }
        }

        /// <summary>Transform of the NPC being talked to.</summary>
        public Transform Npc
        {
            get { return Instance == null ? null : readNpc(Instance); }
        }

        /// <summary>Adds an NPC line to the open conversation.</summary>
        public void Say(string text)
        {
            if (Instance == null || mAddNpc == null) return;
            addNpc(Instance, text);
        }

        /// <summary>Adds a player line to the open conversation.</summary>
        public void You(string text)
        {
            object ui = Ui;
            if (ui == null) return;
            if (PromptContent == null || mAppendChatLine == null) return;      // also looks up the method
            appendChat(ui, "<color=#9ab>You:</color> " + text);
        }

        // ---- Talk's window background (window.png from Talk's theme folder, 9-sliced) ------------

        Texture2D skinTex;
        RectOffset skinBorder;
        bool skinFromPanel, skinFileTried;

        /// <summary>The image Talk draws its dialogue window with, and the size of its non-stretching edge. False when Talk uses
        /// no image (its theme/window.png is missing): it then fills the window with a plain colour (see TalkFallbackColor).</summary>
        public bool WindowSkin(out Texture2D tex, out RectOffset border)
        {
            if (!skinFromPanel) TrySkinFromPanel();
            if (skinTex == null && !skinFileTried) { skinFileTried = true; TrySkinFromFile(); }
            tex = skinTex;
            border = skinBorder;
            return tex != null;
        }

        /// <summary>Window colour Talk uses when it has no window image.</summary>
        public static readonly Color TalkFallbackColor = new Color(0.26f, 0.19f, 0.14f, 0.96f);

        // the sprite on Talk's own window, once it has been built (exactly what the dialogue box shows)
        /// <summary>Takes the sprite of Talk's built window (exactly what its dialogue box shows).</summary>
        void TrySkinFromPanel()
        {
            try
            {
                object ui = Ui;
                if (ui == null) return;
                FieldInfo f = ui.GetType().GetField("panelImage", Inst);
                Image img = f != null ? f.GetValue(ui) as Image : null;
                if (img == null || img.sprite == null || img.sprite.texture == null) return;
                Vector4 b = img.sprite.border;           // left, bottom, right, top
                skinTex = img.sprite.texture;
                skinBorder = new RectOffset((int)b.x, (int)b.z, (int)b.w, (int)b.y);
                skinFromPanel = true;
            }
            catch (Exception) { }
        }

        // before Talk has built its window: load the same file it does
        /// <summary>Before Talk has built its window: loads the same theme/window.png it does.</summary>
        void TrySkinFromFile()
        {
            try
            {
                if (TalkType == null) return;
                string dir = null;
                object ui = Ui;                                 // Talk's DialogueUI knows the folder it loads its theme from
                FieldInfo fd = ui != null ? ui.GetType().GetField("pluginDirectory", Inst) : null;
                if (fd != null) dir = fd.GetValue(ui) as string;
                if (string.IsNullOrEmpty(dir)) dir = System.IO.Path.GetDirectoryName(TalkType.Assembly.Location);
                string file = System.IO.Path.Combine(System.IO.Path.Combine(dir, "theme"), "window.png");
                if (!System.IO.File.Exists(file)) return;
                var t = Textures.FromBytes(System.IO.File.ReadAllBytes(file), new TextureSettings { Readable = true, Wrap = TextureWrapMode.Clamp, Filter = FilterMode.Bilinear, Flags = HideFlags.HideAndDontSave });
                if (t == null) return;
                skinTex = t;
                skinBorder = ParseBorder(TalkConfigString("windowBorderCfg"), 16);
            }
            catch (Exception e) { Core.Log.Warn(CompatibilityModule.Instance,"Could not load Talk's window.png for the quest log: " + e.Message); }
        }

        /// <summary>Value of one of Talk's string config entries, or null.</summary>
        string TalkConfigString(string field)
        {
            try
            {
                if (Instance == null) return null;
                FieldInfo f = TalkType.GetField(field, Inst);
                object entry = f != null ? f.GetValue(Instance) : null;
                PropertyInfo v = entry != null ? entry.GetType().GetProperty("Value") : null;
                return v != null ? v.GetValue(entry, null) as string : null;
            }
            catch (Exception) { return null; }
        }

        // "16" or "left,right,top,bottom", like Talk's WindowBorder setting
        /// <summary>Parses "16" or "left,right,top,bottom" like Talk's WindowBorder setting.</summary>
        static RectOffset ParseBorder(string text, int def)
        {
            var r = new RectOffset(def, def, def, def);
            if (string.IsNullOrEmpty(text)) return r;
            string[] p = text.Split(',');
            int[] n = new int[p.Length];
            for (int i = 0; i < p.Length; i++)
                if (!int.TryParse(p[i].Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n[i])) return r;
            if (n.Length == 1) return new RectOffset(n[0], n[0], n[0], n[0]);
            if (n.Length == 4) return new RectOffset(n[0], n[1], n[2], n[3]);
            return r;
        }

        // ---- the extra choices ----------------------------------------------------------------

        /// <summary>Cached signature of the wanted quest choices (rebuilt only when keys/texts change).</summary>
        string Signature(List<QuestDialogueButton> want) // refs-ok: REFACTOR-REQUEST B17-12
        {
            bool changed = signature == null || sigKeys.Count != want.Count;
            if (!changed) for (int i = 0; i < want.Count; i++) if (sigKeys[i] != want[i].Key || sigTexts[i] != want[i].Text) { changed = true; break; }
            if (!changed) return signature;
            sigKeys.Clear(); sigTexts.Clear(); var sb = new System.Text.StringBuilder();
            foreach (var b in want) { sigKeys.Add(b.Key); sigTexts.Add(b.Text); sb.Append(b.Key).Append('\u0001').Append(b.Text).Append('\u0002'); }
            return signature = sb.ToString();
        }

        // True when the choices on screen are not the wanted ones: the list changed, or Talk rebuilt its own list (which
        // throws away everything in it, ours included) after the player picked one of its prompts.
        /// <summary>True when the choices on screen are not the wanted ones (list changed, or Talk rebuilt its own list).</summary>
        public bool NeedsSync(List<QuestDialogueButton> want) // refs-ok: REFACTOR-REQUEST B17-12
        {
            RectTransform content = PromptContent;
            if (content == null) return false;
            if (Signature(want) != mineSig) return true;
            for (int i = 0; i < mine.Count; i++)
            {
                GameObject go = mine[i];
                if (go == null || go.transform.parent != content || !go.activeSelf) return true;
            }
            return false;
        }

        /// <summary>Replaces the quest choices: copies of Talk's own choice button placed above "Goodbye." (native conversation: its own buttons).</summary>
        public void SyncButtons(List<QuestDialogueButton> want) // refs-ok: REFACTOR-REQUEST B17-12
        {
            RectTransform content = PromptContent;
            if (content == null) return;
            ClearButtons();
            mineSig = Signature(want);
            if (want.Count == 0) return;

            // a button of Talk's own to copy
            GameObject template = null;
            int goodbye = -1;
            for (int i = 0; i < content.childCount; i++)
            {
                Transform ch = content.GetChild(i);
                if (!ch.gameObject.activeInHierarchy || ch.name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                TMP_Text t = ch.GetComponentInChildren<TMP_Text>(true);
                if (t == null || ch.GetComponent<Button>() == null) continue;
                if (template == null) template = ch.gameObject;
                if (t.text == "Goodbye.") goodbye = i;
            }
            if (template == null) { mineSig = ""; return; }       // Talk's list is not built yet; try again next frame

            int at = goodbye;
            for (int i = 0; i < want.Count; i++)
            {
                QuestDialogueButton b = want[i]; // refs-ok: REFACTOR-REQUEST B17-12
                GameObject go = UnityEngine.Object.Instantiate(template, content, false);
                go.name = Prefix + b.Key;
                go.SetActive(true);
                Button btn = go.GetComponent<Button>();
                TMP_Text txt = go.GetComponentInChildren<TMP_Text>(true);
                if (btn == null || txt == null) { UnityEngine.Object.Destroy(go); continue; }
                btn.onClick.RemoveAllListeners();
                Action click = b.Click;
                btn.onClick.AddListener(delegate { if (click != null) click(); });
                txt.text = b.Text;
                if (at >= 0) { go.transform.SetSiblingIndex(at); at++; }
                mine.Add(go);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Removes every quest choice this bridge added.</summary>
        public void ClearButtons()
        {
            for (int i = 0; i < mine.Count; i++)
                if (mine[i] != null) UnityEngine.Object.Destroy(mine[i]);
            mine.Clear();
            mineSig = "";
        }
    }

    // Optional link to Talk's public API (flags and the "action = ..." events), through reflection so this mod does not
    // need to be built against a particular version of Talk.
    /// <summary>Optional link to Talk's public TalkApi (flags, "action = ..." events) by reflection (Wave A, from Quests/TalkHook.cs; B17).</summary>
    internal static class TalkApiLink
    {
        /// <summary>Exposes the active optional flag adapter without leaking third-party reflection into Quests.</summary>
        private sealed class FlagsApi : IConversationFlags
        {
            /// <summary>Writes through the resolved external API.</summary>
            public void Set(string name, bool value) { SetFlag(name, value); }
        }
        static Type api;
        static EventInfo actionEvent; static Delegate actionHandler;
        static MethodInfo getFlag, setFlag;
        /// <summary>True when Talk's TalkApi was found.</summary>
        public static bool Available { get { return api != null; } }

        /// <summary>Links TalkApi (flags) and subscribes the listener method to its ActionTriggered event.</summary>
        public static void Init(Assembly talkAssembly, Action<string,string> listener)
        {
            try
            {
                api = talkAssembly.GetType("ApocalypterTalk.TalkApi", false);
                if (api == null) return;
                getFlag = api.GetMethod("GetFlag", new Type[] { typeof(string) });
                setFlag = api.GetMethod("SetFlag", new Type[] { typeof(string), typeof(bool) });
                if (setFlag != null) ModApi.Provide<IConversationFlags>(CompatibilityModule.Instance, new FlagsApi());
                EventInfo ev = api.GetEvent("ActionTriggered");
                MethodInfo m = listener.Method;
                if (ev != null && m != null)
                {
                    Delegate d = Delegate.CreateDelegate(ev.EventHandlerType, listener.Target, m);
                    ev.AddEventHandler(null, d); actionEvent = ev; actionHandler = d;
                }
                else Core.Log.Warn(CompatibilityModule.Instance,"Talk's ActionTriggered event was not found; 'action = quest.give:...' lines in dialogue files will not work.");
            }
            catch (Exception e) { api = null; Core.Log.Warn(CompatibilityModule.Instance,"Could not link to Talk's API: " + e.Message); }
        }

        /// <summary>Unsubscribes from ActionTriggered and forgets TalkApi.</summary>
        internal static void Shutdown() { ModApi.Withdraw<IConversationFlags>(CompatibilityModule.Instance); if (actionEvent != null && actionHandler != null) actionEvent.RemoveEventHandler(null, actionHandler); actionEvent = null; actionHandler = null; api = null; getFlag = setFlag = null; }

        /// <summary>Sets a Talk dialogue flag (ignored when TalkApi is absent).</summary>
        public static void SetFlag(string name, bool value)
        {
            if (api == null || setFlag == null || string.IsNullOrEmpty(name)) return;
            try { setFlag.Invoke(null, new object[] { name, value }); } catch (Exception) { }
        }

        /// <summary>Reads a Talk dialogue flag (false when TalkApi is absent).</summary>
        public static bool GetFlag(string name)
        {
            if (api == null || getFlag == null || string.IsNullOrEmpty(name)) return false;
            try { return (bool)getFlag.Invoke(null, new object[] { name }); } catch (Exception) { return false; }
        }
    }
}
