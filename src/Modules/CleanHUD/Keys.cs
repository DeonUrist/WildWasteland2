using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using InsaneSystems.InputManager;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.CleanHUD
{
    // One key box of a binding row.
    internal sealed class KeySlot
    {
        internal KeyCode Key;                  // for display and duplicate checks (None = unbound)
        internal string Text;                  // display override (shortcut modifiers, axis names)
        internal Func<KeyCode, KeyCode[], string> Set;   // (key, held modifiers) -> error or null; null = fixed
        internal bool Mouse = true;            // accepts mouse buttons
        internal bool Shortcut;                // KeyboardShortcut: held Ctrl/Shift/Alt are recorded
        internal bool NoDup;                   // axis names, unused game keys
        internal readonly List<string> Clash = new List<string>();
    }

    // One binding row: a game action, a hardcoded game key, a mod config key or a key hardcoded in a mod.
    internal sealed class KeyBind
    {
        internal string Tab, Group, Label, Ctx, Note, Mirror, Why;   // Why: shown when a fixed key is picked
        internal bool Mod, Fixed;
        internal int Order;
        internal readonly List<KeySlot> Slots = new List<KeySlot>();
        internal string Who { get { return (Mod ? Group : "GAME") + ": " + Label; } }
    }

    // Everything that reads a key: the game's rebindable actions (InsaneSystems InputManager), keys the game's FSMs read
    // directly (F9, F10, F11, ...), every KeyCode / KeyboardShortcut / InputSystem.Key setting of every loaded plugin, and
    // keys hardcoded in plugin code (Input.GetKey*(KeyCode.X) / Keyboard.current.xKey, found by reading the plugin DLLs).
    internal static class KeyCatalog
    {
        internal static readonly string[] Tabs = { "MOVEMENT", "ACTIONS", "WEAPONS", "VEHICLE", "ADJUST", "DEBUG", "MODS" };

        // ---- game action names (inputstorage resource) and where they go
        private static readonly HashSet<string> Vanilla = new HashSet<string>(new[] {
            "Jump", "Crouch", "Crawl", "Run", "Use", "Zoom", "Flashlight", "Headlight", "Horn", "Handbrake", "ShiftUp", "ShiftDown",
            "Fire", "Aim Down Sights", "Reload", "Weapon off", "Weapon 1", "Weapon 2", "Weapon 3", "Drop Weapon",
            "ShiftIntoR", "ShiftIntoN", "ShiftInto1", "ShiftInto2", "ShiftInto3", "ShiftInto4", "ShiftInto5", "ShiftInto6", "Change Camera",
            "Use Bandage", "Use First Aid", "Submit", "Cancel", "Binocular", "Rotate Item", "Player Sheet", "Smoke", "Kick",
            "Cruise Control", "Mouse Steering", "Throw Grenade", "TrailerAttachDetach", "Use_2", "Map", "Home",
            "Steering", "Throttle", "Brakes", "Clutch", "Horizontal", "Vertical", "Ignition" });
        private static readonly HashSet<string> Movement = new HashSet<string>(new[] { "Horizontal", "Vertical", "Mouse X", "Mouse Y", "Jump", "Crouch", "Crawl", "Run", "Kick" });
        private static readonly HashSet<string> Weapons = new HashSet<string>(new[] { "Fire", "Aim Down Sights", "Reload", "Weapon off", "Weapon 1", "Weapon 2", "Weapon 3", "Drop Weapon", "Throw Grenade" });
        private static readonly HashSet<string> Vehicle = new HashSet<string>(new[] { "Steering", "Throttle", "Brakes", "Clutch", "Ignition", "Handbrake", "Headlight", "Horn", "ShiftUp", "ShiftDown",
            "ShiftIntoR", "ShiftIntoN", "ShiftInto1", "ShiftInto2", "ShiftInto3", "ShiftInto4", "ShiftInto5", "ShiftInto6", "Change Camera", "Cruise Control", "Mouse Steering", "TrailerAttachDetach" });
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string> {
            { "Horizontal", "MOVE RIGHT / LEFT" }, { "Vertical", "MOVE FORWARD / BACK" }, { "Steering", "STEER RIGHT / LEFT" },
            { "Throttle", "THROTTLE" }, { "Brakes", "BRAKE" }, { "Clutch", "CLUTCH" }, { "Use_2", "USE 2" }, { "Weapon off", "WEAPON OFF" },
            { "TrailerAttachDetach", "TRAILER ATTACH / DETACH" }, { "Mouse X", "LOOK LEFT / RIGHT" }, { "Mouse Y", "LOOK UP / DOWN" },
            { "Mouse ScrollWheel", "MOUSE WHEEL" }, { "Player Sheet", "PLAYER SHEET" }, { "Home", "HOME" } };

        private static void Place(string name, KeyBind b)
        {
            if (name.StartsWith("Adjust", StringComparison.OrdinalIgnoreCase)) { b.Tab = "ADJUST"; b.Ctx = "adjust"; }
            else if (Vehicle.Contains(name)) { b.Tab = "VEHICLE"; b.Ctx = "vehicle"; }
            else if (Movement.Contains(name)) { b.Tab = "MOVEMENT"; b.Ctx = "foot"; }
            else if (Weapons.Contains(name) || name.StartsWith("Item ", StringComparison.Ordinal)) { b.Tab = "WEAPONS"; b.Ctx = "foot"; }
            else if (name == "Submit" || name == "Cancel") { b.Tab = "ACTIONS"; b.Ctx = "menu"; }
            else { b.Tab = "ACTIONS"; b.Ctx = "foot"; }
        }
        private static string GameName(string name)
        {
            var pocket = ApocapocketInventory.ActionLabel(name); if (pocket != null) return pocket;
            string s;
            if (Names.TryGetValue(name, out s)) return s;
            if (name.StartsWith("Adjust-", StringComparison.OrdinalIgnoreCase)) name = name.Substring(7);
            return Pretty(name);
        }

        // ---- keys the game's FSMs read directly (from the level1 FSM dump); runtime scan adds anything else
        private struct Hard { internal KeyCode Key; internal string Label, Tab, Ctx, Fsm; }
        private static readonly Hard[] HardKeys = {
            new Hard { Key = KeyCode.F9, Label = "HIDE / SHOW HUD", Tab = "ACTIONS", Ctx = "global", Fsm = "HideUI" },
            new Hard { Key = KeyCode.Return, Label = "SAVE (AT A SAVE POINT)", Tab = "ACTIONS", Ctx = "foot", Fsm = "UseSave" },
            new Hard { Key = KeyCode.F10, Label = "GOD HANDS", Tab = "DEBUG", Ctx = "global", Fsm = "GodHands" },
            new Hard { Key = KeyCode.F11, Label = "GOD MODE (FLY, NO CLIP)", Tab = "DEBUG", Ctx = "global", Fsm = "GODMODE" },
            new Hard { Key = KeyCode.Space, Label = "GOD MODE: FLY UP", Tab = "DEBUG", Ctx = "noclip", Fsm = "GODMODEMovement" },
            new Hard { Key = KeyCode.LeftControl, Label = "GOD MODE: FLY DOWN", Tab = "DEBUG", Ctx = "noclip", Fsm = "GODMODEMovement" },
            new Hard { Key = KeyCode.LeftShift, Label = "GOD MODE: FLY FAST", Tab = "DEBUG", Ctx = "noclip", Fsm = "GODMODEMovement" },
            new Hard { Key = KeyCode.End, Label = "SPAWN ITEM (OFF IN THIS VERSION)", Tab = "DEBUG", Ctx = "off", Fsm = "SpawnItem" },
        };

        private static string HardFile { get { return Path.Combine(WildWasteland.Core.Assets.ModuleConfigDir("CleanHUD"), "game_keys.txt"); } }
        private static readonly List<string[]> _fsmExtra = new List<string[]>();
        private static readonly HashSet<string> _fsmSeen = new HashSet<string>();
        internal static void SceneReset()
        {
            _fsmExtra.Clear(); _fsmSeen.Clear();
            try { if (File.Exists(HardFile)) foreach (var line in File.ReadAllLines(HardFile)) { var parts = line.Split('\t'); if (parts.Length == 3) _fsmExtra.Add(parts); } }
            catch { }
        }
        internal static void BeginFsms()
        {
            _fsmExtra.Clear(); _fsmSeen.Clear();
            foreach (var h in HardKeys) _fsmSeen.Add(h.Key + "|" + h.Fsm);
        }
        internal static void ObserveState(PlayMakerFSM f, FsmState state)
        {
            FsmStateAction[] actions;
            try { actions = state != null ? state.Actions : null; } catch { return; }
            if (actions == null) return;
            foreach (var a in actions)
            {
                KeyCode k;
                var gd = a as HutongGames.PlayMaker.Actions.GetKeyDown; var gu = a as HutongGames.PlayMaker.Actions.GetKeyUp; var gk = a as HutongGames.PlayMaker.Actions.GetKey;
                if (gd != null) k = gd.key; else if (gu != null) k = gu.key; else if (gk != null) k = gk.key; else continue;
                if (k == KeyCode.None || !_fsmSeen.Add(k + "|" + f.FsmName)) continue;
                _fsmExtra.Add(new[] { k.ToString(), f.FsmName, f.gameObject.name });
            }
        }
        internal static void FinishFsms()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var p in _fsmExtra) sb.Append(p[0]).Append('\t').Append(p[1]).Append('\t').Append(p[2]).Append('\n');
                Directory.CreateDirectory(Path.GetDirectoryName(HardFile)); File.WriteAllText(HardFile, sb.ToString());
            }
            catch (Exception e) { CleanHudModule.Warn("Keys: FSM cache write failed: " + e.Message); }
        }
        // ---- collect
        internal static int ClashCount;

        internal static List<KeyBind> Collect()
        {
            var all = new List<KeyBind>();
            int order = 0;
            try { AddGame(all, ref order); } catch (Exception e) { CleanHudModule.Warn("Keys: game controls unavailable: " + e.Message); }
            foreach (var h in HardKeys)
            {
                var b = new KeyBind { Tab = h.Tab, Label = h.Label, Ctx = h.Ctx, Fixed = true, Note = h.Ctx == "off" ? "NOT ACTIVE" : "HARDCODED", Order = 10000 + order++ };
                b.Slots.Add(new KeySlot { Key = h.Key, NoDup = h.Ctx == "off" });
                all.Add(b);
            }
            foreach (var p in _fsmExtra)
            {
                KeyCode k; if (!TryKeyCode(p[0], out k)) continue;
                var b = new KeyBind { Tab = "ACTIONS", Label = Pretty(p[1]) + " (" + Pretty(p[2]) + ")", Ctx = "global", Fixed = true, Note = "HARDCODED", Order = 10000 + order++ };
                b.Slots.Add(new KeySlot { Key = k });
                all.Add(b);
            }
            try { AddMods(all, ref order); } catch (Exception e) { CleanHudModule.Warn("Keys: mod settings: " + e.Message); }
            try { AddModCode(all, ref order); } catch (Exception e) { CleanHudModule.Warn("Keys: mod code scan: " + e.Message); }
            all.Sort((a, b) => a.Order.CompareTo(b.Order));
            ClashCount = FindClashes(all);
            return all;
        }

        private static void AddGame(List<KeyBind> all, ref int order)
        {
            var st = InputStorage.Singleton;
            if (st == null) return;
            if (st.KeyAxes != null)
                foreach (var ax in st.KeyAxes)
                {
                    if (ax == null) continue;
                    var a = ax;
                    var b = new KeyBind { Label = GameName(a.Name), Order = order++ };
                    Place(a.Name, b);
                    b.Slots.Add(new KeySlot { Key = a.PositiveKey, Set = (k, m) => { a.SetPositiveKey(k); SaveGameKeys(); return null; } });
                    b.Slots.Add(new KeySlot { Key = a.NegativeKey, Set = (k, m) => { a.SetNegativeKey(k); SaveGameKeys(); return null; } });
                    all.Add(b);
                }
            if (st.Keys != null)
                foreach (var ka in st.Keys)
                {
                    if (ka == null || ka.Name == null) continue;
                    var a = ka;
                    var b = new KeyBind { Label = GameName(a.Name), Order = order++ };
                    Place(a.Name, b);
                    if (!Vanilla.Contains(a.Name) && !a.Name.StartsWith("Adjust", StringComparison.OrdinalIgnoreCase))
                    { b.Note = "ADDED BY A MOD"; b.Mirror = Norm(a.Name); }
                    b.Slots.Add(new KeySlot { Key = a.Key, Set = (k, m) => { SetField(a, "key", k); SaveGameKeys(); return null; } });
                    b.Slots.Add(new KeySlot { Key = a.AlternativeKey, Set = (k, m) => { SetField(a, "alternativeKey", k); SaveGameKeys(); return null; } });
                    all.Add(b);
                }
            if (st.Axis != null)
                foreach (var ax in st.Axis)
                {
                    if (ax == null || ax.Name == null) continue;
                    var b = new KeyBind { Label = GameName(ax.Name), Fixed = true, Note = "AXIS", Order = order++ };
                    Place(ax.Name, b);
                    var n = ax.AxisName;
                    b.Slots.Add(new KeySlot { Text = string.IsNullOrEmpty(n) ? "—" : n.ToUpperInvariant(), NoDup = true });
                    all.Add(b);
                }
        }

        private static void SetField(KeyAction ka, string field, KeyCode v)
        {
            var f = typeof(KeyAction).GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) throw new MissingFieldException("KeyAction", field);
            f.SetValue(ka, v);
        }
        private static void SaveGameKeys()
        {
            InputStorage.SaveToPlayerPrefs();
            PlayerPrefs.Save();
            try
            {
                var s = InsaneSystems.InputManager.UI.Settings.singleton;
                if (s == null) return;
                var m = typeof(InsaneSystems.InputManager.UI.Settings).GetMethod("RefreshUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m != null) m.Invoke(s, null);
            }
            catch { }
        }

        // ---- mod settings
        private static void AddMods(List<KeyBind> all, ref int order)
        {
            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddModEntries(all, ref order, covered);
            AddModFiles(all, ref order, covered);
        }

        // Config files of loaded plugins that no live ConfigFile covers (a mod's second file kept out of reach): listed from the
        // file itself, read only.
        private static void AddModFiles(List<KeyBind> all, ref int order, HashSet<string> covered)
        {
            string[] files;
            try { files = Directory.GetFiles(Paths.ConfigPath, "*.cfg"); } catch { return; }
            var infos = new List<PluginInfo>(Chainloader.PluginInfos.Values);
            foreach (var path in files)
            {
                if (covered.Contains(Path.GetFullPath(path))) continue;
                var fn = Path.GetFileNameWithoutExtension(path);
                PluginInfo owner = null;
                foreach (var info in infos)
                {
                    var g = info.Metadata.GUID;
                    if (ReferenceEquals(info.Instance, null) || string.IsNullOrEmpty(g)) continue;
                    if (fn.Equals(g, StringComparison.OrdinalIgnoreCase) || fn.StartsWith(g + ".", StringComparison.OrdinalIgnoreCase)) { owner = info; break; }
                }
                if (owner == null) continue;
                string mod = (owner.Metadata.Name ?? owner.Metadata.GUID).ToUpperInvariant();
                string[] lines;
                try { lines = File.ReadAllLines(path); } catch { continue; }
                string section = "", type = null;
                foreach (var raw in lines)
                {
                    var l = raw.Trim();
                    if (l.StartsWith("[", StringComparison.Ordinal) && l.EndsWith("]", StringComparison.Ordinal)) { section = l.Substring(1, l.Length - 2); type = null; continue; }
                    if (l.StartsWith("# Setting type:", StringComparison.Ordinal)) { type = l.Substring(15).Trim(); continue; }
                    if (l.Length == 0 || l.StartsWith("#", StringComparison.Ordinal)) continue;
                    int eq = l.IndexOf('=');
                    if (eq <= 0 || type == null) { type = null; continue; }
                    var t = type; type = null;
                    if (t != "KeyCode" && t != "KeyboardShortcut" && t != "Key") continue;
                    var key = l.Substring(0, eq).Trim(); var val = l.Substring(eq + 1).Trim();
                    var parts = val.Split('+');
                    KeyCode k = KeyCode.None;
                    if (t == "Key") FromKeyName(parts[0].Trim(), out k); else TryKeyCode(parts[0].Trim(), out k);
                    var b = new KeyBind { Tab = "MODS", Group = mod, Mod = true, Ctx = "global", Order = 20000 + order++, Label = Pretty(key), Mirror = Norm(key),
                        Note = "IN " + Path.GetFileName(path).ToUpperInvariant(), Why = "SET IT IN BEPINEX\\CONFIG\\" + Path.GetFileName(path).ToUpperInvariant() + " ([" + section.ToUpperInvariant() + "] " + key.ToUpperInvariant() + ")" };
                    var slot = new KeySlot { Key = k };
                    if (parts.Length > 1) { var sb = new StringBuilder(); for (int i = 1; i < parts.Length; i++) { KeyCode m; if (TryKeyCode(parts[i].Trim(), out m)) sb.Append(ModName(m)).Append('+'); } slot.Text = sb + KeyText(k); }
                    b.Slots.Add(slot);
                    all.Add(b);
                }
            }
        }

        private static void AddModEntries(List<KeyBind> all, ref int order, HashSet<string> covered)
        {
            var infos = new List<PluginInfo>(Chainloader.PluginInfos.Values);
            infos.Sort((a, b) => string.Compare(a.Metadata.Name, b.Metadata.Name, StringComparison.OrdinalIgnoreCase));
            foreach (var info in infos)
            {
                var inst = info.Instance;
                if (ReferenceEquals(inst, null)) continue;
                var files = new List<ConfigFile>();
                try { if (inst.Config != null) files.Add(inst.Config); } catch { }
                FindConfigFiles(inst, files);
                string mod = (info.Metadata.Name ?? info.Metadata.GUID).ToUpperInvariant();
                var done = new HashSet<ConfigEntryBase>();
                foreach (var cf in files)
                {
                    try { covered.Add(Path.GetFullPath(cf.ConfigFilePath)); } catch { }
                    var entries = new List<ConfigEntryBase>();
                    try { foreach (var kv in (IDictionary<ConfigDefinition, ConfigEntryBase>)cf) entries.Add(kv.Value); } catch { continue; }
                    foreach (var e in entries)
                    {
                        if (e == null || !done.Add(e)) continue;
                        var t = e.SettingType;
                        bool kc = t == typeof(KeyCode), sc = t == typeof(KeyboardShortcut), isk = t.FullName == "UnityEngine.InputSystem.Key";
                        if (!kc && !sc && !isk) continue;
                        var entry = e;
                        var sec = e.Definition.Section ?? "";
                        var b = new KeyBind { Tab = "MODS", Group = mod, Mod = true, Ctx = "global", Order = 20000 + order++,
                            Label = Pretty(e.Definition.Key), Mirror = Norm(e.Definition.Key),
                            Note = sec.Length > 0 && !sec.Equals("General", StringComparison.OrdinalIgnoreCase) && !sec.Equals("Keys", StringComparison.OrdinalIgnoreCase) ? Pretty(sec) : null };
                        var slot = new KeySlot();
                        if (kc)
                        {
                            slot.Key = (KeyCode)entry.BoxedValue;
                            slot.Set = (k, m) => { entry.BoxedValue = k; return null; };
                        }
                        else if (sc)
                        {
                            var ks = (KeyboardShortcut)entry.BoxedValue;
                            slot.Key = ks.MainKey; slot.Shortcut = true;
                            var sb = new StringBuilder();
                            foreach (var m in ks.Modifiers) sb.Append(ModName(m)).Append('+');
                            if (sb.Length > 0) slot.Text = sb + KeyText(ks.MainKey);
                            slot.Set = (k, m) => { entry.BoxedValue = new KeyboardShortcut(k, m ?? new KeyCode[0]); return null; };
                        }
                        else
                        {
                            var name = entry.BoxedValue != null ? entry.BoxedValue.ToString() : "None";
                            KeyCode k0; slot.Key = FromKeyName(name, out k0) ? k0 : KeyCode.None;
                            if (slot.Key == KeyCode.None && name != "None") slot.Text = Pretty(name);
                            slot.Mouse = false;
                            var et = t;
                            slot.Set = (k, m) =>
                            {
                                if (k == KeyCode.None) { entry.BoxedValue = Enum.Parse(et, "None"); return null; }
                                var kn = ToKeyName(k);
                                try { entry.BoxedValue = Enum.Parse(et, kn, true); return null; }
                                catch { return "THIS SETTING CAN'T TAKE " + KeyText(k); }
                            };
                        }
                        b.Slots.Add(slot);
                        all.Add(b);
                    }
                }
            }
        }

        private static void FindConfigFiles(BaseUnityPlugin inst, List<ConfigFile> files)
        {
            const BindingFlags SF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            const BindingFlags IF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            try
            {
                foreach (var f in inst.GetType().GetFields(IF)) Take(f.GetValue(inst), files);
            }
            catch { }
            // static fields: the plugin class (already initialised) and classes without a static constructor
            // (reading a static field must not run a mod's type initialiser)
            Type[] types;
            try { types = inst.GetType().Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            catch { return; }
            foreach (var t in types)
            {
                if (t == null || t.ContainsGenericParameters) continue;
                if (t != inst.GetType() && t.TypeInitializer != null) continue;
                FieldInfo[] fs;
                try { fs = t.GetFields(SF); } catch { continue; }
                foreach (var f in fs)
                {
                    if (f.IsLiteral) continue;
                    var ft = f.FieldType;
                    if (ft != typeof(ConfigFile) && !typeof(ConfigEntryBase).IsAssignableFrom(ft)) continue;
                    try { Take(f.GetValue(null), files); } catch { }
                }
            }
        }
        private static void Take(object o, List<ConfigFile> files)
        {
            var cf = o as ConfigFile;
            if (cf == null) { var e = o as ConfigEntryBase; if (e != null) cf = e.ConfigFile; }
            if (cf != null && !files.Contains(cf)) files.Add(cf);
        }

        // ---- keys hardcoded in plugin code (read from the DLLs with Mono.Cecil on a worker thread, once)
        private sealed class CodeKey { internal string Mod, Type, Method; internal KeyCode Key; }
        private static readonly object _sync = new object();
        private static Thread _scan;
        private static List<CodeKey> _codeKeys;
        private static List<string> _scanWarnings;

        internal static bool ScanDone { get { return _scan != null && !_scan.IsAlive; } }
        internal static bool ScanStarted { get { return _scan != null; } }

        internal static void StartScan()
        {
            if (_scan != null) return;
            var jobs = new List<KeyValuePair<string, string>>();
            try
            {
                foreach (var info in Chainloader.PluginInfos.Values)
                {
                    if (info.Metadata.GUID == CleanHudModule.GUID || string.IsNullOrEmpty(info.Location) || !File.Exists(info.Location)) continue;
                    var name = (info.Metadata.Name ?? info.Metadata.GUID).ToUpperInvariant();
                    bool dup = false; foreach (var j in jobs) if (j.Value == info.Location) dup = true;
                    if (!dup) jobs.Add(new KeyValuePair<string, string>(name, info.Location));
                }
            }
            catch { }
            _scan = new Thread(() =>
            {
                var res = new List<CodeKey>();
                var warnings = new List<string>();
                foreach (var j in jobs)
                {
                    try { CodeScan.Run(j.Key, j.Value, res); }
                    catch (Exception e) { warnings.Add("Keys: could not read " + Path.GetFileName(j.Value) + ": " + e.Message); }
                }
                lock (_sync) { _codeKeys = res; _scanWarnings = warnings; }
            });
            _scan.IsBackground = true;
            _scan.Start();
        }

        private static void AddModCode(List<KeyBind> all, ref int order)
        {
            StartScan();
            if (!ScanDone) return;
            List<CodeKey> keys;
            lock (_sync)
            {
                keys = _codeKeys;
                if (_scanWarnings != null) { foreach (var warning in _scanWarnings) CleanHudModule.Warn(warning); _scanWarnings = null; }
            }
            if (keys == null) return;
            foreach (var c in keys)
            {
                var b = new KeyBind { Tab = "MODS", Group = c.Mod, Mod = true, Fixed = true, Ctx = "global", Order = 30000 + order++,
                    Label = Pretty(c.Type) + " · " + Pretty(c.Method), Note = "HARDCODED IN THE MOD" };
                b.Slots.Add(new KeySlot { Key = c.Key });
                all.Add(b);
            }
        }

        // isolated so a missing Mono.Cecil only fails this call
        private static class CodeScan
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            internal static void Run(string mod, string path, List<CodeKey> res)
            {
                var seen = new HashSet<string>();
                using (var m = Mono.Cecil.ModuleDefinition.ReadModule(path))
                {
                    foreach (var t in m.GetTypes())
                        foreach (var md in t.Methods)
                        {
                            if (t.Namespace != null && t.Namespace.StartsWith("WildWasteland.Modules.CleanHUD", StringComparison.Ordinal)) continue;
                            if (!md.HasBody || md.Name.IndexOf("Rebind", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                            var ins = md.Body.Instructions;
                            for (int i = 1; i < ins.Count; i++)
                            {
                                var mr = ins[i].Operand as Mono.Cecil.MethodReference;
                                if (mr == null || mr.DeclaringType == null) continue;
                                var dt = mr.DeclaringType.FullName;
                                KeyCode k = KeyCode.None;
                                if (dt == "UnityEngine.Input" && (mr.Name == "GetKey" || mr.Name == "GetKeyDown" || mr.Name == "GetKeyUp")
                                    && mr.Parameters.Count == 1 && mr.Parameters[0].ParameterType.Name == "KeyCode")
                                {
                                    int v;
                                    if (!Const(ins[i - 1], out v)) continue;
                                    k = (KeyCode)v;
                                }
                                else if (dt == "UnityEngine.InputSystem.Keyboard" && mr.Name.StartsWith("get_", StringComparison.Ordinal) && mr.Name.EndsWith("Key", StringComparison.Ordinal) && mr.Name != "get_anyKey")
                                {
                                    var kn = mr.Name.Substring(4, mr.Name.Length - 7);
                                    if (!FromKeyName(kn, out k)) continue;
                                }
                                else continue;
                                if (k == KeyCode.None || k == KeyCode.Escape || IsModifier(k)) continue;
                                var tn = t.Name; int p = tn.IndexOf('<'); if (p > 0) tn = tn.Substring(0, p);
                                if (t.IsNested && (tn.StartsWith("<", StringComparison.Ordinal) || tn.Length == 0) && t.DeclaringType != null) tn = t.DeclaringType.Name;
                                var mn = md.Name; if (mn.StartsWith("<", StringComparison.Ordinal)) { int q = mn.IndexOf('>'); mn = q > 1 ? mn.Substring(1, q - 1) : mn; }
                                if (!seen.Add(k + "|" + tn + "|" + mn)) continue;
                                res.Add(new CodeKey { Mod = mod, Type = tn, Method = mn, Key = k });
                            }
                        }
                }
            }
            private static bool Const(Mono.Cecil.Cil.Instruction i, out int v)
            {
                v = 0;
                switch (i.OpCode.Code)
                {
                    case Mono.Cecil.Cil.Code.Ldc_I4: v = (int)i.Operand; return true;
                    case Mono.Cecil.Cil.Code.Ldc_I4_S: v = (sbyte)i.Operand; return true;
                    case Mono.Cecil.Cil.Code.Ldc_I4_0: v = 0; return true;
                    case Mono.Cecil.Cil.Code.Ldc_I4_1: v = 1; return true;
                    case Mono.Cecil.Cil.Code.Ldc_I4_2: v = 2; return true;
                    case Mono.Cecil.Cil.Code.Ldc_I4_3: v = 3; return true;
                    case Mono.Cecil.Cil.Code.Ldc_I4_4: v = 4; return true;
                    case Mono.Cecil.Cil.Code.Ldc_I4_5: v = 5; return true;
                    case Mono.Cecil.Cil.Code.Ldc_I4_6: v = 6; return true;
                    case Mono.Cecil.Cil.Code.Ldc_I4_7: v = 7; return true;
                    case Mono.Cecil.Cil.Code.Ldc_I4_8: v = 8; return true;
                }
                return false;
            }
        }

        // ---- duplicates
        // Two bindings clash on the same key when either one is a mod's, either one works everywhere (F9, F10, F11,
        // mod keys), or both work in the same situation (on foot / driving / part adjust / menus / god mode).
        // The game's own keys may share a key across situations by design (W = forward and throttle).
        private static bool Clashes(KeyBind a, KeyBind b)
        {
            if (a.Mirror != null && a.Mirror == b.Mirror) return false;   // a mod's action and its own setting for it
            if (a.Mod || b.Mod) return true;
            if (a.Ctx == "global" || b.Ctx == "global") return true;
            return a.Ctx == b.Ctx;
        }

        private static int FindClashes(List<KeyBind> all)
        {
            var map = new Dictionary<KeyCode, List<KeyValuePair<KeyBind, KeySlot>>>();
            foreach (var b in all)
                foreach (var s in b.Slots)
                {
                    s.Clash.Clear();
                    if (s.Key == KeyCode.None || s.NoDup || b.Ctx == "off") continue;
                    List<KeyValuePair<KeyBind, KeySlot>> l;
                    if (!map.TryGetValue(s.Key, out l)) map[s.Key] = l = new List<KeyValuePair<KeyBind, KeySlot>>();
                    l.Add(new KeyValuePair<KeyBind, KeySlot>(b, s));
                }
            int n = 0;
            var logged = new StringBuilder();
            foreach (var kv in map)
            {
                var l = kv.Value;
                for (int i = 0; i < l.Count; i++)
                    for (int j = i + 1; j < l.Count; j++)
                    {
                        var a = l[i].Key; var b = l[j].Key;
                        if (ReferenceEquals(a, b) || !Clashes(a, b)) continue;
                        if (!l[i].Value.Clash.Contains(b.Who)) l[i].Value.Clash.Add(b.Who);
                        if (!l[j].Value.Clash.Contains(a.Who)) l[j].Value.Clash.Add(a.Who);
                    }
                bool any = false;
                foreach (var p in l) if (p.Value.Clash.Count > 0) any = true;
                if (any)
                {
                    n++;
                    logged.Append("\n  ").Append(KeyText(kv.Key)).Append(": ");
                    bool first = true;
                    foreach (var p in l) if (p.Value.Clash.Count > 0) { if (!first) logged.Append(" / "); logged.Append(p.Key.Who); first = false; }
                }
            }
            var text = logged.ToString();
            if (text != _lastLog) { _lastLog = text; if (n > 0) CleanHudModule.Warn("Keys used twice (" + n + "):" + text); }
            return n;
        }
        private static string _lastLog = "";

        // ---- names
        internal static bool IsModifier(KeyCode k)
        {
            return k == KeyCode.LeftShift || k == KeyCode.RightShift || k == KeyCode.LeftControl || k == KeyCode.RightControl
                || k == KeyCode.LeftAlt || k == KeyCode.RightAlt || k == KeyCode.AltGr || k == KeyCode.LeftWindows || k == KeyCode.RightWindows
                || k == KeyCode.LeftCommand || k == KeyCode.RightCommand;
        }
        private static string ModName(KeyCode k)
        {
            if (k == KeyCode.LeftControl || k == KeyCode.RightControl) return "CTRL";
            if (k == KeyCode.LeftShift || k == KeyCode.RightShift) return "SHIFT";
            if (k == KeyCode.LeftAlt || k == KeyCode.RightAlt || k == KeyCode.AltGr) return "ALT";
            return KeyText(k);
        }

        internal static string KeyText(KeyCode k)
        {
            if (k == KeyCode.None) return "—";
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((int)(k - KeyCode.Alpha0)).ToString(CultureInfo.InvariantCulture);
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "NUM " + (int)(k - KeyCode.Keypad0);
            if (k >= KeyCode.Mouse3 && k <= KeyCode.Mouse6) return "MOUSE " + (int)(k - KeyCode.Mouse0 + 1);
            if (k >= KeyCode.JoystickButton0 && k <= KeyCode.JoystickButton19) return "PAD " + (int)(k - KeyCode.JoystickButton0);
            switch (k)
            {
                case KeyCode.Mouse0: return "LMB";
                case KeyCode.Mouse1: return "RMB";
                case KeyCode.Mouse2: return "MMB";
                case KeyCode.KeypadPlus: return "NUM +";
                case KeyCode.KeypadMinus: return "NUM -";
                case KeyCode.KeypadMultiply: return "NUM *";
                case KeyCode.KeypadDivide: return "NUM /";
                case KeyCode.KeypadPeriod: return "NUM .";
                case KeyCode.KeypadEnter: return "NUM ENTER";
                case KeyCode.KeypadEquals: return "NUM =";
                case KeyCode.Return: return "ENTER";
                case KeyCode.Escape: return "ESC";
                case KeyCode.Delete: return "DEL";
                case KeyCode.Insert: return "INS";
                case KeyCode.PageUp: return "PG UP";
                case KeyCode.PageDown: return "PG DN";
                case KeyCode.UpArrow: return "UP";
                case KeyCode.DownArrow: return "DOWN";
                case KeyCode.LeftArrow: return "LEFT";
                case KeyCode.RightArrow: return "RIGHT";
                case KeyCode.LeftControl: return "L CTRL";
                case KeyCode.RightControl: return "R CTRL";
                case KeyCode.LeftShift: return "L SHIFT";
                case KeyCode.RightShift: return "R SHIFT";
                case KeyCode.LeftAlt: return "L ALT";
                case KeyCode.RightAlt: return "R ALT";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.Plus: return "+";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.CapsLock: return "CAPS";
                case KeyCode.Backspace: return "BACKSPACE";
            }
            return k.ToString().ToUpperInvariant();
        }

        // Unity InputSystem Key names <-> KeyCode (Apocasetter, FsmInspector and WildWasteland store Key settings).
        private static readonly Dictionary<string, KeyCode> KeyNames = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase) {
            { "Enter", KeyCode.Return }, { "LeftCtrl", KeyCode.LeftControl }, { "RightCtrl", KeyCode.RightControl }, { "Ctrl", KeyCode.LeftControl },
            { "Shift", KeyCode.LeftShift }, { "Alt", KeyCode.LeftAlt }, { "Backquote", KeyCode.BackQuote }, { "LeftMeta", KeyCode.LeftWindows },
            { "RightMeta", KeyCode.RightWindows }, { "LeftApple", KeyCode.LeftWindows }, { "LeftCommand", KeyCode.LeftWindows },
            { "RightApple", KeyCode.RightWindows }, { "RightCommand", KeyCode.RightWindows }, { "ContextMenu", KeyCode.Menu },
            { "PrintScreen", KeyCode.Print }, { "NumLock", KeyCode.Numlock }, { "NumpadEnter", KeyCode.KeypadEnter },
            { "NumpadDivide", KeyCode.KeypadDivide }, { "NumpadMultiply", KeyCode.KeypadMultiply }, { "NumpadPlus", KeyCode.KeypadPlus },
            { "NumpadMinus", KeyCode.KeypadMinus }, { "NumpadPeriod", KeyCode.KeypadPeriod }, { "NumpadEquals", KeyCode.KeypadEquals } };

        internal static bool FromKeyName(string n, out KeyCode k)
        {
            k = KeyCode.None;
            if (string.IsNullOrEmpty(n) || n == "None") return false;
            if (KeyNames.TryGetValue(n, out k)) return true;
            if (n.StartsWith("Digit", StringComparison.OrdinalIgnoreCase) && n.Length == 6) n = "Alpha" + n[5];
            else if (n.StartsWith("Numpad", StringComparison.OrdinalIgnoreCase) && n.Length == 7) n = "Keypad" + n[6];
            try { k = (KeyCode)Enum.Parse(typeof(KeyCode), n, true); return Enum.IsDefined(typeof(KeyCode), k); }
            catch { return false; }
        }
        internal static string ToKeyName(KeyCode k)
        {
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return "Digit" + (int)(k - KeyCode.Alpha0);
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "Numpad" + (int)(k - KeyCode.Keypad0);
            foreach (var kv in KeyNames)
                if (kv.Value == k && kv.Key != "Ctrl" && kv.Key != "Shift" && kv.Key != "Alt" && !kv.Key.EndsWith("Apple", StringComparison.Ordinal) && !kv.Key.EndsWith("Command", StringComparison.Ordinal))
                    return kv.Key;
            return k.ToString();
        }
        internal static bool TryKeyCode(string s, out KeyCode k)
        {
            k = KeyCode.None;
            try { k = (KeyCode)Enum.Parse(typeof(KeyCode), s, true); return true; } catch { return false; }
        }

        // "TrailerAttachDetach" -> "TRAILER ATTACH DETACH", "ShiftInto1" -> "SHIFT INTO 1", "HUDScale" -> "HUD SCALE"
        internal static string Pretty(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i], p = i > 0 ? s[i - 1] : ' ', nx = i + 1 < s.Length ? s[i + 1] : ' ';
                if (c == '_' || c == '-') { sb.Append(' '); continue; }
                if (c == '/') { sb.Append(" / "); continue; }
                if (i > 0 && ((char.IsUpper(c) && char.IsLower(p)) || (char.IsDigit(c) && char.IsLetter(p)) || (char.IsUpper(c) && char.IsDigit(p)) || (char.IsUpper(c) && char.IsUpper(p) && char.IsLower(nx))))
                    sb.Append(' ');
                sb.Append(c);
            }
            var r = sb.ToString().ToUpperInvariant();
            while (r.Contains("  ")) r = r.Replace("  ", " ");
            return r.Trim();
        }
        // mirror id: "Item 4", "Item4Key" and "Item4AltKey" are the same binding
        private static string Norm(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s ?? "") if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            var r = sb.ToString();
            if (r.EndsWith("key", StringComparison.Ordinal)) r = r.Substring(0, r.Length - 3);
            if (r.EndsWith("alt", StringComparison.Ordinal)) r = r.Substring(0, r.Length - 3);
            return r;
        }
    }
}
