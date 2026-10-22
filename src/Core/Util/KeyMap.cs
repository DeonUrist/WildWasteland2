using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WildWasteland.Core
{
    /// <summary>
    /// WW hotkeys are InputSystem Keys (rebindable in Apocasetter); ported code polls the legacy Input with KeyCodes
    /// (Input.GetKeyDown, the Equals/Plus/Keypad aliases of the cassette keys, the game's own InputStorage KeyCodes). One cached
    /// table converts Key -> KeyCode (built once, lookups allocation-free).
    /// </summary>
    public static class KeyMap
    {
        static KeyCode[] _map;

        /// <summary>Legacy KeyCode of an InputSystem Key (KeyCode.None when there is none).</summary>
        public static KeyCode Code(Key k)
        {
            if (k == Key.None) return KeyCode.None;
            if (_map == null) Build();
            int i = (int)k;
            return i >= 0 && i < _map.Length ? _map[i] : KeyCode.None;
        }

        /// <summary>Builds the Key -> KeyCode table once.</summary>
        static void Build()
        {
            int max = 0;
            foreach (Key k in Enum.GetValues(typeof(Key))) if ((int)k > max) max = (int)k;
            _map = new KeyCode[max + 1];
            foreach (Key k in Enum.GetValues(typeof(Key))) _map[(int)k] = Convert(k);
        }

        /// <summary>Name-based Key -> KeyCode conversion with the explicit exceptions (Enter=Return, Digit=Alpha, Numpad=Keypad...).</summary>
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
    }
}
