using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Modules.Gunplay;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// Ammo settings (ex Gunplay.Ammo.AmmoSettings). The entries are bound by GunplaySettings.Bind in F6 order (no migration from
    /// the standalone Apocaunloader cfg: D12 fresh start). The Apocaunloader takeover (runtime patch + UnpatchSelf) is gone:
    /// GunplayModule.ConflictsWith keeps the whole module off while the standalone Apocaunloader is installed (AGENTS.md 5.8).
    /// </summary>
    internal static class AmmoSettings
    {
        internal static ConfigEntry<bool> Enabled, DropBackpackOverflow, DropArmorOverflow;
        internal static ConfigEntry<float> HoldSeconds, PhaseTimeout;
        internal static ConfigEntry<Key> FallbackKey;
        internal static int TapFrame = -100;
        /// <summary>Level-2 ammo trace (constant messages; callers guard concatenations with Log.L2).</summary>
        internal static void V(string message) { if (Log.L2) GunplayLog.Deep(message); }

        /// <summary>Reload input held this frame: the game's "Reload" axis, else the fallback key (Input System, then legacy KeyCode).</summary>
        internal static bool ReloadHeld()
        {
            // (WW perf) a game without a "Reload" input axis throws (and allocates an exception) on every call: remembered after the first
            if (!_noReloadButton) { try { return Input.GetButton("Reload"); } catch { _noReloadButton = true; } }
            try { var kb = Keyboard.current; if (kb != null) return kb[FallbackKey.Value].isPressed; } catch { }
            try
            {
                var key = FallbackKey.Value;
                if (!_fallbackParsed || key != _fallbackFor) { _fallbackFor = key; _fallbackParsed = true; _fallbackOk = Enum.TryParse(key.ToString(), out _fallbackCode); }
                if (_fallbackOk) return Input.GetKey(_fallbackCode);
            }
            catch { }
            return false;
        }
        private static bool _noReloadButton, _fallbackParsed, _fallbackOk;
        private static KeyCode _fallbackCode;
        private static Key _fallbackFor;
        /// <summary>A short Reload tap happened this frame or the previous one (the native Reload FSM gets its GetButtonDown then).</summary>
        internal static bool TapPending() { int frame = Time.frameCount; return frame == TapFrame || frame == TapFrame + 1; }
    }
}
