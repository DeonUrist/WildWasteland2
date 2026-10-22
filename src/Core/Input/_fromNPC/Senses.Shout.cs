using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Senses part owned by Core (Hotkeys): the player's shout key, its modifier and the game buttons it blocks while held. NPCAI reacts to the shout (Noise) in ShoutPressed until Wave B splits it into a PlayerShout event.</summary>
    internal static partial class Senses
    {
        // A human raider's voice (the enemy_human_single clips of a Scraffa/Flexa's Sound FSM) from the player, and a SHOUT ghost at the player
        // for every NPC hostile to the player within PlayerShoutRange. Only in play (not paused, not in a menu that stops time), once per
        // ShoutCooldown.
        private static float _nextShout;

        // (WW) Core.Hotkeys: [Keys] "Shout" pressed (not while Apocasetter blocks input / time is stopped) and ShoutAllowed()
        internal static bool ShoutAllowed() { return On && ModifierHeld(); }
        internal static void ShoutPressed()
        {
            if (!On || !ModifierHeld()) return;
            if (Time.timeScale <= 0f || Time.unscaledTime < _nextShout) return;
            FindPlayer();
            if (_player == null) return;
            _nextShout = Time.unscaledTime + Mathf.Max(0f, NpcModule.ShoutCooldown.Value);
            var clip = Voice();
            if (clip != null)
            {
                // your own voice: a 2D source on the camera, so it stays with you (PlayClipAtPoint left it hanging in the air where you shouted)
                var src = VoiceSource();
                if (src != null) src.PlayOneShot(clip, Mathf.Clamp01(NpcModule.ShoutVolume.Value));
            }
            Noise(_player, _player.transform.position, NpcModule.PlayerShoutRange.Value, Src.Shout, "the player shouting", a => a.Hostile.Contains("Player"), null, _player);
            if (NpcModule.SensesLog.Value) NpcModule.Log.LogInfo("Senses: the player shouts (" + NpcModule.PlayerShoutRange.Value.ToString("0") + " m)" + (clip == null ? ", no voice clip found" : ""));
        }

        // ShoutModifier held (an Alt key stands for either Alt). (WW) read once per frame: the button prefixes ask for every GetButton action.
        private static int _modFrame = -1; private static bool _modHeld;
        private static bool ModifierHeld()
        {
            int f = Time.frameCount;
            if (f != _modFrame) { _modFrame = f; _modHeld = ReadModifier(); }
            return _modHeld;
        }
        private static bool ReadModifier()
        {
            var m = NpcModule.ShoutModifier.Value;
            if (m == UnityEngine.InputSystem.Key.None) return true;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            if (m == UnityEngine.InputSystem.Key.LeftAlt || m == UnityEngine.InputSystem.Key.RightAlt) return kb.altKey.isPressed;
            if (m == UnityEngine.InputSystem.Key.LeftCtrl || m == UnityEngine.InputSystem.Key.RightCtrl) return kb.ctrlKey.isPressed;
            if (m == UnityEngine.InputSystem.Key.LeftShift || m == UnityEngine.InputSystem.Key.RightShift) return kb.shiftKey.isPressed;
            return kb[m].isPressed;
        }

        // the game's own buttons on the shout key do nothing while the modifier is held (Alt+Q must not kick)
        private static HashSet<string> _blocked; private static string _blockedSrc;
        private static bool Blocked(FsmString name)
        {
            // (WW) cheapest first: the module runs and the modifier is held this frame (one keyboard read per frame); then the key checks and the set
            if (!NpcModule.Running || NpcModule.ShoutModifier.Value == UnityEngine.InputSystem.Key.None || !ModifierHeld()) return false;
            if (name == null || NpcModule.ShoutKey.Value == UnityEngine.InputSystem.Key.None) return false;
            string cfg = NpcModule.ShoutBlocksButtons.Value ?? "";
            if (_blocked == null || _blockedSrc != cfg)
            {
                _blockedSrc = cfg; _blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in cfg.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)) _blocked.Add(part.Trim());
            }
            return _blocked.Contains(name.Value ?? "");
        }
        public static bool BeforeGetButton(GetButton __instance)
        {
            try { if (Blocked(__instance.buttonName)) { if (__instance.storeResult != null) __instance.storeResult.Value = false; return false; } }
            catch (Exception) { }
            return true;
        }
        public static bool BeforeGetButtonDown(GetButtonDown __instance)
        {
            try { if (Blocked(__instance.buttonName)) { if (__instance.storeResult != null) __instance.storeResult.Value = false; return false; } }
            catch (Exception) { }
            return true;
        }
        public static bool BeforeGetButtonUp(GetButtonUp __instance)
        {
            try { if (Blocked(__instance.buttonName)) { if (__instance.storeResult != null) __instance.storeResult.Value = false; return false; } }
            catch (Exception) { }
            return true;
        }
    }
}
