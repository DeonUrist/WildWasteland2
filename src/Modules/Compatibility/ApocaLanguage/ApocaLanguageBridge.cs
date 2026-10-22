using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Resolves the optional language mod and exposes its choices through the shared menu contract.</summary>
    internal sealed class ApocaLanguageBridge : ILanguageMenu
    {
        private ConfigEntry<string> _language;
        private Func<object> _languages;
        private Func<object, string> _code;
        private Action<string> _select;
        private readonly Dictionary<string, string> _codes = new Dictionary<string, string>(StringComparer.Ordinal);
        private bool _failed;

        /// <summary>Detects the optional plugin lazily, after the chainloader has finished constructing its plugins.</summary>
        private bool Resolve()
        {
            if (_language != null) return true;
            if (_failed) return false;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.denis.apocalypter.apocalanguage", out info) || info.Instance == null) return false;
            try
            {
                var assembly = info.Instance.GetType().Assembly;
                var plugin = assembly.GetType("ApocaLanguage.Plugin", true);
                var translator = assembly.GetType("ApocaLanguage.Translator", true);
                var language = assembly.GetType("ApocaLanguage.LanguageInfo", true);
                var entry = Access.Get<ConfigEntry<string>>(plugin, "LanguageEntry")(null);
                var choices = Access.Get<object>(translator, "Languages");
                var code = Access.Get<string>(language, "Code");
                var select = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), plugin.GetMethod("SelectLanguage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
                if (entry == null || choices(null) == null) throw new InvalidOperationException("Language settings unavailable");
                _language = entry; _languages = () => choices(null); _code = code; _select = select;
                return true;
            }
            catch (Exception error) { Fail(error); return false; }
        }
        /// <summary>Reports whether the optional language provider can currently serve its native menu.</summary>
        public bool Present { get { return Resolve(); } }
        /// <summary>Returns the English menu caption of the selected native language.</summary>
        public string Current { get { return Resolve() ? Label(_language.Value) : null; } }
        /// <summary>Copies current native choices as stable menu captions while retaining their native selection codes privately.</summary>
        public void Languages(List<string> into)
        {
            into.Clear(); _codes.Clear(); if (!Resolve()) return;
            try
            {
                foreach (var item in (IEnumerable)_languages())
                {
                    string code = _code(item); if (string.IsNullOrEmpty(code)) continue;
                    string label = Label(code); if (_codes.ContainsKey(label)) continue;
                    _codes.Add(label, code); into.Add(label);
                }
            }
            catch (Exception error) { into.Clear(); Fail(error); }
        }
        /// <summary>Selects one advertised language through the native plugin's own persisted selection method.</summary>
        public bool Select(string language)
        {
            if (!Resolve() || string.IsNullOrEmpty(language)) return false;
            if (!_codes.ContainsKey(language)) { var names = new List<string>(); Languages(names); }
            string code; if (!_codes.TryGetValue(language, out code)) return false;
            try { _select(code); return true; } catch (Exception error) { Fail(error); return false; }
        }
        /// <summary>Preserves the existing simplified/traditional Chinese captions and native culture fallback.</summary>
        private static string Label(string code)
        {
            if (string.IsNullOrEmpty(code)) return code ?? "";
            if (code == "ZH") return "SIMPLIFIED CHINESE";
            if (code == "ZH-TW") return "TRADITIONAL CHINESE";
            try { return CultureInfo.GetCultureInfo(code).EnglishName.ToUpperInvariant(); }
            catch (CultureNotFoundException) { return code; }
        }
        /// <summary>Stops repeated reflective failures and leaves the original language mod menu available.</summary>
        private void Fail(Exception error)
        {
            _failed = true; _language = null; _languages = null; _code = null; _select = null; _codes.Clear();
            Log.Warn(CompatibilityModule.Instance, "ApocaLanguage menu bridge unavailable: " + error.GetBaseException().Message);
        }
    }
}
