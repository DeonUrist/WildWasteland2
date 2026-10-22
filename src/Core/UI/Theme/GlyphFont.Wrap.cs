using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Core.UI
{
    /// <summary>Word wrap with the glyph atlas font, used by the quest journal/dialogue and menu reference pages (Wave A split; B01 -> Core/UI GlyphFont.Wrap).</summary>
    internal sealed partial class GlyphFont
    {
        /// <summary>Splits text into glyph-measured lines that fit the requested width and font size.</summary>
        internal static List<string> Wrap(string text, float width, float size = 26)
        {
            var lines = new List<string>(); var font = GlyphFont.Get();
            foreach (var paragraph in text.Replace("\r", "").Split('\n'))
            {
                var line = new StringBuilder();
                foreach (var word in paragraph.Split(' '))
                {
                    if (line.Length > 0 && font.Measure(line.ToString() + " " + word, size, 0) > width) { lines.Add(line.ToString()); line.Length = 0; }
                    if (line.Length > 0) line.Append(' '); line.Append(word);
                }
                lines.Add(line.ToString());
            }
            return lines;
        }
    }
}
