using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using TMPro;
using UnityEngine;
using WildWasteland.Core;
using UnityEngine.UI;

namespace WildWasteland.Modules.Quests
{
    // One extra choice in Talk's list of things to say.
    internal class TalkBtn
    {
        public string Key;          // identifies it (the list is rebuilt only when the keys or texts change)
        public string Text;
        public Action Click;
    }

    // Talks to Apocalypter Talk ("Apocatalker") through reflection: its dialogue window is made of Unity UI objects and
    // all of its members are private / internal. Quest choices are copies of Talk's own choice buttons, put just above
    // its "Goodbye." button (the same way the Rumors mod adds its button).
    internal partial class TalkBridge : IQuestConversation
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
        static Func<object, T> Reader<T>(FieldInfo field)
        {
            return OptionalAccess.Read<T>(field);
        }
        static Action<object, string> Caller(MethodInfo method)
        {
            return OptionalAccess.Call<Action<object, string>>(method, typeof(void), typeof(object), typeof(string));
        }

        readonly List<GameObject> mine = new List<GameObject>();
        string mineSig = "";

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
                Plugin.Log.LogError("Apocalypter Talk is missing members this mod needs (" + missing.Trim() + "). Is it a different version than the one this mod was made for (Apocatalker)? Quest dialogue is disabled.");
                return false;
            }
            readName = Reader<string>(fName); readNpc = Reader<Transform>(fNpc); readUi = Reader<object>(fUi);
            if (fTalking != null) readTalking = Reader<bool>(fTalking); addNpc = Caller(mAddNpc);
            return true;
        }

        public bool TalkingNow
        {
            get
            {
                if (NativeOpen) return true;
                if (fTalking == null) return false;
                try { return readTalking(Instance); } catch (Exception) { return false; }
            }
        }

        public void Bind(object inst) { if (NativeOpen) Close(); Instance = inst; Generation++; }

        object Ui
        {
            get { return Instance == null ? null : readUi(Instance); }
        }

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

        public string NpcName
        {
            get { return NativeOpen ? _nativeName : Instance == null ? "" : (readName(Instance) ?? ""); }
        }

        public Transform Npc
        {
            get { return NativeOpen ? _nativeNpc : Instance == null ? null : readNpc(Instance); }
        }

        public void Say(string text)
        {
            if (NativeOpen) { AddLine(text); return; }
            if (Instance == null || mAddNpc == null) return;
            addNpc(Instance, text);
        }

        public void You(string text)
        {
            if (NativeOpen) { AddLine("You: " + text); return; }
            object ui = Ui;
            if (ui == null) return;
            if (PromptContent == null || mAppendChatLine == null) return;      // also looks up the method
            appendChat(ui, "<color=#9ab>You:</color> " + text);
        }

        // ---- Talk's window background (window.png from Talk's theme folder, 9-sliced) ------------

        Texture2D skinTex;
        RectOffset skinBorder;
        bool skinFromPanel, skinFileTried;

        /// The image Talk draws its dialogue window with, and the size of its non-stretching edge. False when Talk uses
        /// no image (its theme/window.png is missing): it then fills the window with a plain colour (see TalkFallbackColor).
        public bool WindowSkin(out Texture2D tex, out RectOffset border)
        {
            if (!skinFromPanel) TrySkinFromPanel();
            if (skinTex == null && !skinFileTried) { skinFileTried = true; TrySkinFromFile(); }
            tex = skinTex;
            border = skinBorder;
            return tex != null;
        }

        public static readonly Color TalkFallbackColor = new Color(0.26f, 0.19f, 0.14f, 0.96f);

        // the sprite on Talk's own window, once it has been built (exactly what the dialogue box shows)
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
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!t.LoadImage(System.IO.File.ReadAllBytes(file))) { UnityEngine.Object.Destroy(t); return; }
                t.filterMode = FilterMode.Bilinear;
                t.wrapMode = TextureWrapMode.Clamp;
                t.hideFlags = HideFlags.HideAndDontSave;
                skinTex = t;
                skinBorder = ParseBorder(TalkConfigString("windowBorderCfg"), 16);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Could not load Talk's window.png for the quest log: " + e.Message); }
        }

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

        string Signature(List<TalkBtn> want)
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
        public bool NeedsSync(List<TalkBtn> want)
        {
            if (NativeOpen) return Signature(want) != mineSig;
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

        public void SyncButtons(List<TalkBtn> want)
        {
            if (NativeOpen) { NativeButtons(want); mineSig = Signature(want); return; }
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
                TalkBtn b = want[i];
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

        public void ClearButtons()
        {
            if (NativeOpen) { _choices.Clear(); _buttons.Clear(); _pending = false; Revision++; }
            for (int i = 0; i < mine.Count; i++)
                if (mine[i] != null) UnityEngine.Object.Destroy(mine[i]);
            mine.Clear();
            mineSig = "";
        }
    }

    // Optional link to Talk's public API (flags and the "action = ..." events), through reflection so this mod does not
    // need to be built against a particular version of Talk.
    internal static class TalkApiLink
    {
        static Type api;
        static EventInfo actionEvent; static Delegate actionHandler;
        static MethodInfo getFlag, setFlag;
        public static bool Available { get { return api != null; } }

        public static void Init(Assembly talkAssembly, object listener, string method)
        {
            try
            {
                api = talkAssembly.GetType("ApocalypterTalk.TalkApi", false);
                if (api == null) return;
                getFlag = api.GetMethod("GetFlag", new Type[] { typeof(string) });
                setFlag = api.GetMethod("SetFlag", new Type[] { typeof(string), typeof(bool) });
                EventInfo ev = api.GetEvent("ActionTriggered");
                MethodInfo m = listener.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (ev != null && m != null)
                {
                    Delegate d = Delegate.CreateDelegate(ev.EventHandlerType, listener, m);
                    ev.AddEventHandler(null, d); actionEvent = ev; actionHandler = d;
                }
                else Plugin.Log.LogWarning("Talk's ActionTriggered event was not found; 'action = quest.give:...' lines in dialogue files will not work.");
            }
            catch (Exception e) { api = null; Plugin.Log.LogWarning("Could not link to Talk's API: " + e.Message); }
        }

        internal static void Shutdown() { if (actionEvent != null && actionHandler != null) actionEvent.RemoveEventHandler(null, actionHandler); actionEvent = null; actionHandler = null; api = null; getFlag = setFlag = null; }

        public static void SetFlag(string name, bool value)
        {
            if (api == null || setFlag == null || string.IsNullOrEmpty(name)) return;
            try { setFlag.Invoke(null, new object[] { name, value }); } catch (Exception) { }
        }

        public static bool GetFlag(string name)
        {
            if (api == null || getFlag == null || string.IsNullOrEmpty(name)) return false;
            try { return (bool)getFlag.Invoke(null, new object[] { name }); } catch (Exception) { return false; }
        }
    }

}
