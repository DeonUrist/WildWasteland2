using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Quests engine, save part (Wave A: kept in Quests for B18, which rewrites it on Core SaveBus.Key; key ApocalypterQuests / AQ1 unchanged): Easy Save access by reflection (disk file, default cache, cached slot files), slot id, stamp pruning, SaveBook / LoadBook.</summary>
    internal sealed partial class Plugin : EngineSettings
    {
        const string SaveKey = "ApocalypterQuests";

        Type es3Type;

        bool es3Resolved;

        MethodInfo es3Save, es3Load, es3Exists, es3SaveS, es3LoadS, es3ExistsS;

        Type es3SettingsType;

        object es3FileLocation;      // ES3.Location.File

        bool ResolveEs3()
        {
            if (es3Resolved) return es3Save != null && es3Load != null && es3Exists != null;
            es3Resolved = true;
            es3Type = typeof(ES3);
            if (es3Type == null) { Error("Easy Save (ES3) was not found; quests cannot be saved."); return false; }

            foreach (MethodInfo m in es3Type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] ps = m.GetParameters();
                if (m.Name == "Save" && m.IsGenericMethodDefinition && ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType.IsGenericParameter)
                { if (es3Save == null) es3Save = m.MakeGenericMethod(typeof(string)); }
                else if (m.Name == "Load" && m.IsGenericMethodDefinition && ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType.IsGenericParameter)
                { if (es3Load == null) es3Load = m.MakeGenericMethod(typeof(string)); }
                else if (m.Name == "KeyExists" && ps.Length == 1 && ps[0].ParameterType == typeof(string))
                { if (es3Exists == null) es3Exists = m; }
            }
            foreach (MethodInfo m in es3Type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] ps = m.GetParameters();
                if (m.Name == "Save" && m.IsGenericMethodDefinition && ps.Length == 3 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType.IsGenericParameter && ps[2].ParameterType.Name == "ES3Settings")
                    es3SaveS = m.MakeGenericMethod(typeof(string));
                else if (m.Name == "Load" && m.IsGenericMethodDefinition && ps.Length == 3 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType.IsGenericParameter && ps[2].ParameterType.Name == "ES3Settings")
                    es3LoadS = m.MakeGenericMethod(typeof(string));
                else if (m.Name == "KeyExists" && !m.IsGenericMethodDefinition && ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType.Name == "ES3Settings")
                    es3ExistsS = m;
            }
            es3SettingsType = es3Type.Assembly.GetType("ES3Settings", false);
            Type locType = es3Type.GetNestedType("Location");
            if (locType != null) { try { es3FileLocation = Enum.Parse(locType, "File"); } catch (Exception) { } }
            Info("Easy Save: default " + (es3Save != null && es3Load != null) + ", straight-to-file " + (es3SaveS != null && es3LoadS != null && es3SettingsType != null && es3FileLocation != null));
            if (es3Save == null || es3Load == null || es3Exists == null)
            {
                Error("Easy Save has no Save<T>(key,value) / Load<T>(key,default) / KeyExists(key) overloads this mod can use; quests cannot be saved.");
                return false;
            }
            return true;
        }

        // ---- Easy Save's memory cache ---------------------------------------------------------------
        // The game saves through Easy Save's cached file and ends with StoreCachedFile, which rewrites the slot file from
        // memory. A value written only to the file is wiped by that, so it also has to go into the cached copy
        // (the Immersive Map mod does the same: "disk + memory").
        static object StaticMember(Type t, string name)
        {
            const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            PropertyInfo pr = t.GetProperty(name, f);
            if (pr != null) return pr.GetValue(null, null);
            FieldInfo fi = t.GetField(name, f);
            return fi != null ? fi.GetValue(null) : null;
        }

        static object InstMember(object o, string name)
        {
            const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type t = o.GetType();
            PropertyInfo pr = t.GetProperty(name, f);
            if (pr != null) return pr.GetValue(o, null);
            FieldInfo fi = t.GetField(name, f);
            return fi != null ? fi.GetValue(o) : null;
        }

        // the path Easy Save is currently using by default = the slot being played
        static string DefaultEs3Path()
        {
            try
            {
                Type st = typeof(ES3Settings);
                if (st == null) return "";
                object s = StaticMember(st, "defaultSettings");
                if (s == null) return "";
                object path = InstMember(s, "path");
                return path as string ?? "";
            }
            catch (Exception) { return ""; }
        }

        static List<object> CachedSlotFiles()
        {
            var res = new List<object>();
            try
            {
                Type ft = typeof(ES3File);
                string want = DefaultEs3Path();
                if (ft == null || want.Length == 0) return res;
                string wantName = Path.GetFileName(want);
                System.Collections.IDictionary d = StaticMember(ft, "cachedFiles") as System.Collections.IDictionary;
                if (d == null) return res;
                foreach (System.Collections.DictionaryEntry de in d)
                {
                    string k = de.Key as string;
                    if (k != null && de.Value != null && string.Equals(Path.GetFileName(k), wantName, StringComparison.OrdinalIgnoreCase)) res.Add(de.Value);
                }
            }
            catch (Exception) { }
            return res;
        }

        static int SaveToCache(string key, string text)
        {
            int n = 0;
            foreach (object file in CachedSlotFiles())
            {
                try
                {
                    foreach (MethodInfo m in file.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        ParameterInfo[] ps = m.GetParameters();
                        if (m.Name == "Save" && m.IsGenericMethodDefinition && ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType.IsGenericParameter)
                        {
                            m.MakeGenericMethod(typeof(string)).Invoke(file, new object[] { key, text });
                            n++;
                            break;
                        }
                    }
                }
                catch (Exception) { }
            }
            return n;
        }

        static string LoadFromCache(string key)
        {
            foreach (object file in CachedSlotFiles())
            {
                try
                {
                    MethodInfo ex = file.GetType().GetMethod("KeyExists", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
                    if (ex == null || !(bool)ex.Invoke(file, new object[] { key })) continue;
                    foreach (MethodInfo m in file.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        ParameterInfo[] ps = m.GetParameters();
                        if (m.Name == "Load" && m.IsGenericMethodDefinition && ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType.IsGenericParameter)
                        {
                            string s = m.MakeGenericMethod(typeof(string)).Invoke(file, new object[] { key, "" }) as string;
                            if (!string.IsNullOrEmpty(s)) return s;
                        }
                    }
                }
                catch (Exception) { }
            }
            return "";
        }

        // Settings for one save file, read and written on disk (not Easy Save's in-memory copy). Same approach as Immersive Map.
        object DiskSettings(string file)
        {
            if (es3SettingsType == null || es3FileLocation == null) return null;
            object st = null;
            foreach (ConstructorInfo c in es3SettingsType.GetConstructors())
            {
                ParameterInfo[] ps = c.GetParameters();
                if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(Enum[]))
                { st = c.Invoke(new object[] { file, new Enum[] { (Enum)es3FileLocation } }); break; }
            }
            if (st == null)
            {
                foreach (ConstructorInfo c in es3SettingsType.GetConstructors())
                {
                    ParameterInfo[] ps = c.GetParameters();
                    if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == es3SettingsType) { st = c.Invoke(new object[] { file, null }); break; }
                }
                if (st == null) return null;
                const BindingFlags I = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                FieldInfo lf = es3SettingsType.GetField("location", I);
                if (lf != null) lf.SetValue(st, es3FileLocation);
                else { PropertyInfo lp = es3SettingsType.GetProperty("location", I); if (lp != null && lp.CanWrite) lp.SetValue(st, es3FileLocation, null); else return null; }
            }
            return st;
        }

        bool SaveToDisk(string key, string value, string file)
        {
            object st = es3SaveS == null ? null : DiskSettings(file);
            if (st == null) return false;
            es3SaveS.Invoke(null, new object[] { key, value, st });
            return true;
        }

        string LoadFromDisk(string key, string file)
        {
            object st = es3LoadS == null ? null : DiskSettings(file);
            if (st == null) return null;
            if (es3ExistsS != null && !(bool)es3ExistsS.Invoke(null, new object[] { key, st })) return null;
            return (string)es3LoadS.Invoke(null, new object[] { key, "", st });
        }

        // the current save slot's file name (SaveGame1.es3 ...), asked from the game's own ES3SettingsMod
        string SlotId()
        {
            string dp = DefaultEs3Path();
            if (dp.Length > 0) return Path.GetFileName(dp);
            try
            {
                Type t = typeof(ES3SettingsMod);
                if (t != null)
                {
                    const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                    MethodInfo m = t.GetMethod("GetSavePath", f, null, Type.EmptyTypes, null);
                    if (m != null) { object r = m.Invoke(null, null); if (r != null) return r.ToString(); }
                    PropertyInfo pr = t.GetProperty("DefaultSavePath", f);
                    if (pr != null) { object r = pr.GetValue(null, null); if (r != null) return r.ToString(); }
                    FieldInfo fi = t.GetField("DefaultSavePath", f);
                    if (fi != null) { object r = fi.GetValue(null); if (r != null) return r.ToString(); }
                }
            }
            catch (Exception) { }
            return "unknown";
        }

        // Cooldown stamps that have run out are no use any more; dropping them keeps the saved data small over a long game.
        void PruneStamps()
        {
            double now = Game.GameHours();
            if (now < 0.0 || book.GiverStamp.Count == 0) return;
            List<string> drop = null;
            foreach (KeyValuePair<string, long> kv in book.GiverStamp)
            {
                double age = now - kv.Value / 1000.0;
                double keep = kv.Key.StartsWith("camp:", StringComparison.Ordinal) ? Takeover.GarrisonCooldownHours : CooldownHours;
                if (age < 0.0 || age > keep + 1.0) { if (drop == null) drop = new List<string>(); drop.Add(kv.Key); }
            }
            if (drop != null) foreach (string k in drop) book.GiverStamp.Remove(k);
        }

        void SaveBook()
        {
            if (!ready || _sessionSlot == null || _sessionSlot != SaveBus.Slot) return;
            if (saveBlocked) { SaveBus.ReportFailure("Quest data is unreadable; preserving the previous save."); Warn("Not saving quests: the data in this slot was in a format this version cannot read."); return; }
            PruneStamps();
            CaptureBook();
            string text = book.Serialize();
            bool es3ok = false, diskOk = false;
            bool haveEs3 = ResolveEs3();
            if (haveEs3)
            {
                string slotFile = DefaultEs3Path();
                if (slotFile.Length > 0)
                {
                    try { diskOk = SaveToDisk(SaveKey, text, slotFile); }
                    catch (Exception e) { Warn("Saving quests straight into " + slotFile + " failed: " + (e.InnerException ?? e).Message); }
                }
                try
                {
                    es3Save.Invoke(null, new object[] { SaveKey, text });
                    es3ok = true;
                }
                catch (Exception e) { LogOnce("SaveBook", e is TargetInvocationException && e.InnerException != null ? e.InnerException : e); }
            }
            int mem = SaveToCache(SaveKey, text);
            if (!es3ok || mem == 0) SaveBus.ReportFailure("Quest data did not reach the world cache.");
            Core.Log.Always("[Quests] Saved " + book.Active.Count + " quests (slot " + SlotId() + ", " + text.Length + " chars, game save " + (es3ok || diskOk ? "ok" : "FAILED") + (diskOk ? " (disk" + (es3ok ? " + memory)" : ")") : " (memory only)") + ", cache copies " + mem + ").");
        }

        void LoadBook()
        {
            book.Clear();
            saveBlocked = false;
            string data = "";
            string from = "";
            bool readError = false;
            bool es3ok = ResolveEs3();
            if (es3ok)
            {
                string slotFile = DefaultEs3Path();
                if (slotFile.Length > 0)
                {
                    try { string d = LoadFromDisk(SaveKey, slotFile); if (!string.IsNullOrEmpty(d)) { data = d; from = "game save file " + slotFile; } }
                    catch (Exception e) { readError = true; LogOnce("LoadDisk", e is TargetInvocationException && e.InnerException != null ? e.InnerException : e); }
                }
            }
            if (es3ok && string.IsNullOrEmpty(data))
            {
                try
                {
                    bool exists = (bool)es3Exists.Invoke(null, new object[] { SaveKey });
                    if (exists) { data = (string)es3Load.Invoke(null, new object[] { SaveKey, "" }); from = "game save"; }
                }
                catch (Exception e) { readError = true; LogOnce("LoadBook", e is TargetInvocationException && e.InnerException != null ? e.InnerException : e); }
            }
            if (string.IsNullOrEmpty(data))
            {
                string cached = LoadFromCache(SaveKey);
                if (!string.IsNullOrEmpty(cached)) { data = cached; from = "game save (memory)"; }
            }
            if (readError) { saveBlocked = true; book.Clear(); Warn("Quest data could not be read; saving is blocked for this session."); return; }
            if (string.IsNullOrEmpty(data)) { Info("No saved quests found for slot " + SlotId() + "."); return; }
            if (!book.Deserialize(data))
            {
                saveBlocked = true;
                book.Clear();
                Warn("Quest data in slot " + SlotId() + " (" + from + ") could not be read; leaving it untouched.");
                return;
            }
            RestoreBook();
            MapLink.Reset();
            for (int i = 0; i < book.Active.Count; i++)
                if (book.Active[i].Type != QType.Deliver && book.Active[i].Deadline <= 0.0) AssignDeadline(book.Active[i]);   // jobs saved by an older version get a fresh time limit
            Info("Loaded " + book.Active.Count + " quests from the " + from + " (slot " + SlotId() + ").");
        }
    }
}
