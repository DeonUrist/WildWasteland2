using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core.Events;
namespace WildWasteland.Core
{
    /// <summary>Owner-scoped shout input, modifier handling and native button suppression; NPCAI consumes PlayerShout.</summary>
    public sealed class PlayerShoutInput
    {
        readonly ConfigEntry<Key> _key, _modifier;
        readonly ConfigEntry<string> _buttons;
        readonly ConfigEntry<float> _cooldown, _radius;
        readonly Func<bool> _enabled;
        readonly HashSet<string> _blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string _blockedText;
        int _frame = -1;
        bool _held;
        float _next;
        /// <summary>Registers a configured shout and its native button guards under the module's existing lifecycle.</summary>
        public PlayerShoutInput(IModule owner, ConfigEntry<Key> key, ConfigEntry<Key> modifier, ConfigEntry<string> buttons, ConfigEntry<float> cooldown, ConfigEntry<float> radius, Func<bool> enabled)
        {
            _key = key; _modifier = modifier; _buttons = buttons; _cooldown = cooldown; _radius = radius; _enabled = enabled;
            Hotkeys.Register(owner, key, Press, Allowed);
            Hooks.GetButtonDo.Prefix(owner, BeforeGetButton);
            Hooks.GetButtonDownUpdate.Prefix(owner, BeforeGetButtonDown);
            Hooks.GetButtonUpUpdate.Prefix(owner, BeforeGetButtonUp);
        }
        /// <summary>Whether the configured owner is active and its modifier is held.</summary>
        bool Allowed() { return _enabled() && ModifierHeld(); }
        /// <summary>Reads the modifier once per frame, treating either side of Alt/Ctrl/Shift identically.</summary>
        bool ModifierHeld()
        {
            if (_frame == Time.frameCount) return _held;
            _frame = Time.frameCount;
            var modifier = _modifier.Value; var keyboard = Keyboard.current;
            if (modifier == Key.None) return _held = true;
            if (keyboard == null) return _held = false;
            if (modifier == Key.LeftAlt || modifier == Key.RightAlt) return _held = keyboard.altKey.isPressed;
            if (modifier == Key.LeftCtrl || modifier == Key.RightCtrl) return _held = keyboard.ctrlKey.isPressed;
            if (modifier == Key.LeftShift || modifier == Key.RightShift) return _held = keyboard.shiftKey.isPressed;
            return _held = keyboard[modifier].isPressed;
        }
        /// <summary>Publishes an admitted shout using the shared player's world position and existing cooldown.</summary>
        void Press()
        {
            if (!Allowed() || Time.timeScale <= 0 || Time.unscaledTime < _next) return;
            var player = PlayerState.Player; if (player == null) return;
            _next = Time.unscaledTime + Mathf.Max(0, _cooldown.Value);
            var shout = new PlayerShout { WorldPos = Origin.World(player.transform.position), Radius = _radius.Value };
            Core.ModEvents.Publish(ref shout);
        }
        /// <summary>Whether this native button would conflict with the active shout modifier.</summary>
        bool Blocked(FsmString name)
        {
            if (!_enabled() || _modifier.Value == Key.None || !ModifierHeld() || name == null || _key.Value == Key.None) return false;
            string text = _buttons.Value ?? "";
            if (_blockedText != text)
            {
                _blockedText = text; _blocked.Clear();
                foreach (string item in text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)) _blocked.Add(item.Trim());
            }
            return _blocked.Contains(name.Value ?? "");
        }
        /// <summary>Makes a blocked native held-button action read as released.</summary>
        bool BeforeGetButton(GetButton action) { if (!Blocked(action.buttonName)) return true; if (action.storeResult != null) action.storeResult.Value = false; return false; }
        /// <summary>Suppresses a blocked native button-down action while the modifier is held.</summary>
        bool BeforeGetButtonDown(GetButtonDown action) { if (!Blocked(action.buttonName)) return true; if (action.storeResult != null) action.storeResult.Value = false; return false; }
        /// <summary>Suppresses a blocked native button-up action while the modifier is held.</summary>
        bool BeforeGetButtonUp(GetButtonUp action) { if (!Blocked(action.buttonName)) return true; if (action.storeResult != null) action.storeResult.Value = false; return false; }
    }
}
