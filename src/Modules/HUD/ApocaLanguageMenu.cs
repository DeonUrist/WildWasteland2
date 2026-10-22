using System.Collections.Generic;
using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    /// <summary>Adapts the typed optional language menu into the HUD's existing menu rows.</summary>
    internal static class ApocaLanguageMenu
    {
        /// <summary>One menu caption, selection flag and native selection action.</summary>
        internal sealed class Entry
        {
            internal string Code, Name;
            internal bool Selected;
            internal System.Action Choose;
        }
        /// <summary>Builds rows from the current typed provider, keeping missing or disabled compatibility an empty menu.</summary>
        internal static List<Entry> Read()
        {
            var entries = new List<Entry>(); var api = ModApi.Get<ILanguageMenu>();
            if (api == null || !api.Present) return entries;
            var names = new List<string>(); api.Languages(names); string current = api.Current;
            foreach (string name in names)
            {
                string choice = name;
                entries.Add(new Entry { Code = name, Name = name, Selected = name == current, Choose = () => { var active = ModApi.Get<ILanguageMenu>(); if (active != null) active.Select(choice); } });
            }
            return entries;
        }
    }
}
