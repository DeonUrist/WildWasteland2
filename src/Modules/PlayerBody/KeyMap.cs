using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WildWasteland.Modules.PlayerBody
{
    // WW hotkeys are InputSystem Keys (rebindable in Apocasetter); the ported Apocaplayer code polls the legacy Input with KeyCodes
    // (Input.GetKeyDown, the Equals/Plus/Keypad aliases of the cassette keys, the game's own InputStorage KeyCodes). One cached table
    // converts Key -> KeyCode, and the old cfg's KeyCode names -> Key for the first-run migration.
    internal static class KeyMap
    {
        static KeyCode[] _map;

        internal static KeyCode Code(Key k)
        {
            if (k == Key.None) return KeyCode.None;
            if (_map == null) Build();
            int i = (int)k;
            return i >= 0 && i < _map.Length ? _map[i] : KeyCode.None;
        }

        static void Build()
        {
            int max = 0;
            foreach (Key k in Enum.GetValues(typeof(Key))) if ((int)k > max) max = (int)k;
            _map = new KeyCode[max + 1];
            foreach (Key k in Enum.GetValues(typeof(Key))) _map[(int)k] = Convert(k);
        }

        static KeyCode Convert(Key k)
        {
            switch (k)
            {
                case Key.None: return KeyCode.None;
                case Key.Enter: return KeyCode.Return;
                case Key.LeftCtrl: return KeyCode.LeftControl;
                case Key.RightCtrl: return KeyCode.RightControl;
                case Key.Backquote: return KeyCode.BackQuote;
                case Key.ContextMenu: return KeyCode.Menu;
                case Key.PrintScreen: return KeyCode.Print;
                case Key.NumpadEnter: return KeyCode.KeypadEnter;
                case Key.NumpadDivide: return KeyCode.KeypadDivide;
                case Key.NumpadMultiply: return KeyCode.KeypadMultiply;
                case Key.NumpadPlus: return KeyCode.KeypadPlus;
                case Key.NumpadMinus: return KeyCode.KeypadMinus;
                case Key.NumpadPeriod: return KeyCode.KeypadPeriod;
                case Key.NumpadEquals: return KeyCode.KeypadEquals;
                case Key.LeftMeta: return KeyCode.LeftWindows;
                case Key.RightMeta: return KeyCode.RightWindows;
            }
            string n = k.ToString();
            if (n.StartsWith("Digit", StringComparison.Ordinal)) n = "Alpha" + n.Substring(5);
            else if (n.StartsWith("Numpad", StringComparison.Ordinal)) n = "Keypad" + n.Substring(6);
            try { return (KeyCode)Enum.Parse(typeof(KeyCode), n, true); } catch { return KeyCode.None; }
        }

        /// <summary>Old Apocaplayer cfg value (a KeyCode name) -> Key.</summary>
        internal static bool FromKeyCodeName(string raw, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrEmpty(raw)) return false;
            string n = raw.Trim();
            if (n.Equals("None", StringComparison.OrdinalIgnoreCase)) return true;
            if (n.StartsWith("Alpha", StringComparison.OrdinalIgnoreCase)) n = "Digit" + n.Substring(5);
            else if (n.StartsWith("Keypad", StringComparison.OrdinalIgnoreCase)) n = "Numpad" + n.Substring(6);
            else if (n.Equals("Return", StringComparison.OrdinalIgnoreCase)) n = "Enter";
            else if (n.Equals("LeftControl", StringComparison.OrdinalIgnoreCase)) n = "LeftCtrl";
            else if (n.Equals("RightControl", StringComparison.OrdinalIgnoreCase)) n = "RightCtrl";
            else if (n.Equals("Menu", StringComparison.OrdinalIgnoreCase)) n = "ContextMenu";
            else if (n.Equals("Print", StringComparison.OrdinalIgnoreCase)) n = "PrintScreen";
            else if (n.Equals("LeftWindows", StringComparison.OrdinalIgnoreCase) || n.Equals("LeftCommand", StringComparison.OrdinalIgnoreCase)) n = "LeftMeta";
            else if (n.Equals("RightWindows", StringComparison.OrdinalIgnoreCase) || n.Equals("RightCommand", StringComparison.OrdinalIgnoreCase)) n = "RightMeta";
            try { key = (Key)Enum.Parse(typeof(Key), n, true); return true; } catch { return false; }
        }
    }
}
