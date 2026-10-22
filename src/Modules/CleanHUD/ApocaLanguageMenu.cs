using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class ApocaLanguageMenu
    {
        internal sealed class Entry
        {
            internal string Code, Name;
            internal bool Selected;
            internal Action Choose;
        }
        private static string Label(string code)
        {
            if (code == "ZH") return "SIMPLIFIED CHINESE";
            if (code == "ZH-TW") return "TRADITIONAL CHINESE";
            try { return CultureInfo.GetCultureInfo(code).EnglishName.ToUpperInvariant(); }
            catch (CultureNotFoundException) { return code; }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static List<Entry> Read()
        {
            var entries = new List<Entry>(); BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.denis.apocalypter.apocalanguage", out info) || ReferenceEquals(info.Instance, null)) return entries;
            var assembly = info.Instance.GetType().Assembly;
            var plugin = assembly.GetType("ApocaLanguage.Plugin", true);
            var translator = assembly.GetType("ApocaLanguage.Translator", true);
            var language = assembly.GetType("ApocaLanguage.LanguageInfo", true);
            var codeField = language.GetField("Code");
            var select = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), plugin.GetMethod("SelectLanguage"));
            string current = ((ConfigEntry<string>)plugin.GetField("LanguageEntry").GetValue(null)).Value;
            foreach (var item in (IEnumerable)translator.GetField("Languages").GetValue(null))
            {
                string code = (string)codeField.GetValue(item);
                entries.Add(new Entry { Code = code, Name = Label(code), Selected = code == current, Choose = () => select(code) });
            }
            return entries;
        }
    }
}
