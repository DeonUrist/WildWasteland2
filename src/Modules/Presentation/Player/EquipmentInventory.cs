using System;
using System.Linq.Expressions;
using System.Reflection;
using BepInEx.Configuration;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// EquipmentInventory part (split from PlayerBody/Body/EquipmentInventory.cs in Wave A): the player's weapon slots read from the
    /// vanilla Weapons holders (Slot 1..6, drawn slot), with the Apocapocket / Apocainventory extra slots taken from the bridge when present.
    /// Target: a Core player-inventory query (B01); the bridge part (Resolve/Bridge) goes to Compatibility (B17).
    /// </summary>
    internal sealed partial class EquipmentInventory
    {
        public readonly GameObject[] Items = new GameObject[6];
        // (2.2.6) read every frame: the names and the invoke argument made once
        public int Drawn = -1;
        private Transform _camera;
        // (WW perf) the vanilla Weapons FSM's state (its state's name: the same string object until it changes), a cheap per-frame change signal
        /// <summary>Shared native weapons state used to detect held-item changes.</summary>
        public string WeaponState { get { return Core.PlayerInventory.WeaponState; } }
        // False preserves previous mount assignments during transactions and saves.
        /// <summary>Reads optional inventory ownership when available, otherwise uses shared native slot data and existing extra holders.</summary>
        public bool Read()
        {
            if (_camera != Game.PlayerCamera)
            {
                _camera = Game.PlayerCamera;
                Array.Clear(Items, 0, Items.Length); Drawn = -1;
            }
            Resolve();
            if (_instance != null)
            {
                var enabled = _enabled != null ? _enabled() as ConfigEntry<bool> : null;
                var run = _instance();
                if (run != null && (enabled == null || enabled.Value))
                {
                    if (!_ready(run)) return false;
                    var save = _save != null ? _save(run) : null;
                    if (_operation != null && _operation(run) != null
                        || save != null && (_loading != null && _loading(save) || _normalised != null && _normalised(save))) return false;
                    var slots = _slots(run) as Array;
                    Array.Clear(Items, 0, Items.Length); Drawn = -1;
                    for (int i = 0; slots != null && i < Mathf.Min(Items.Length, slots.Length); i++)
                    {
                        Items[i] = _content(slots, i);
                        if (_isDrawn != null && _isDrawn(run, i)) Drawn = i;
                    }
                    if (_handOrigin != null && _handName != null && _camera != null)
                    {
                        int origin = _handOrigin(run);
                        var hand = FirstItem(_camera.Find("Hand"));
                        if (origin >= 0 && origin < Items.Length && Items[origin] == null && hand != null && hand.name == _handName(run))
                        { Items[origin] = hand; Drawn = origin; }
                    }
                    return true;
                }
            }
            Core.PlayerInventory.Read(Items, out Drawn);
            for (int i = 3; i < Items.Length; i++)
                if (Items[i] == null && _camera != null) Items[i] = FirstItem(_camera.Find(PocketNames[i]) ?? _camera.Find(InvNames[i]));
            return true;
        }
        /// <summary>Existing physical-holder query forwarding to Core.</summary>
        [Obsolete("refactor: use Core.PlayerInventory.FirstItem")]
        public static GameObject FirstItem(Transform holder) { return Core.PlayerInventory.FirstItem(holder); }
    }
}
