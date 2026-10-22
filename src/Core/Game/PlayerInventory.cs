using System;
using HutongGames.PlayMaker;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Cached native player weapon holders shared by presentation and inventory consumers.</summary>
    public static class PlayerInventory
    {
        static readonly string[] SlotNames = { "Slot 1", "Slot 2", "Slot 3", "Slot 4", "Slot 5", "Slot 6" };
        static readonly GameObject[] Items = new GameObject[6];
        static Transform _camera, _weapons;
        static PlayMakerFSM _weaponFsm;
        static int _frame = -1, _drawn = -1;
        /// <summary>Forgets borrowed native inventory references on scene change.</summary>
        public static void Reset() { _camera = _weapons = null; _weaponFsm = null; _frame = -1; _drawn = -1; Array.Clear(Items, 0, Items.Length); }
        /// <summary>Reads native slot contents once in a frame; optional logical slot bridges remain Compatibility-owned.</summary>
        static void Refresh()
        {
            var camera = PlayerState.PlayerCamera;
            if (_camera != camera) { Reset(); _camera = camera; }
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            if (_weapons == null && camera != null)
            {
                _weapons = camera.Find("Weapons") ?? camera.Find("HandItemUse/Weapons");
                _weaponFsm = _weapons != null ? FsmUtil.Find(_weapons.gameObject, "Weapons") : null;
            }
            _drawn = -1;
            for (int i = 0; i < Items.Length; i++)
            {
                Items[i] = FirstItem(_weapons != null ? _weapons.Find(SlotNames[i]) : null);
                if (_weaponFsm != null && _weaponFsm.ActiveStateName == SlotNames[i]) _drawn = i;
            }
        }
        /// <summary>Native Weapons FSM state name; null while its rig is unavailable.</summary>
        public static string WeaponState { get { Refresh(); return _weaponFsm != null ? _weaponFsm.ActiveStateName : null; } }
        /// <summary>Copies the six native weapon holders and their drawn slot into the caller's reusable buffer.</summary>
        public static void Read(GameObject[] into, out int drawn)
        {
            if (into == null || into.Length < Items.Length) throw new ArgumentException("Six weapon slots are required", "into");
            Refresh(); Array.Copy(Items, into, Items.Length); drawn = _drawn;
        }
        /// <summary>First physical item under a native holder, ignoring helper transforms.</summary>
        public static GameObject FirstItem(Transform holder)
        {
            if (holder == null) return null;
            for (int i = 0; i < holder.childCount; i++)
            {
                var child = holder.GetChild(i);
                if (child.GetComponent<Rigidbody>() != null || child.GetComponentInChildren<MeshRenderer>(true) != null) return child.gameObject;
            }
            return null;
        }
    }
}
