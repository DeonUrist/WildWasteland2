using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>
    /// Live state of the player's drawn weapon and of NPC weapons (provider of <see cref="IWeaponState"/>), read from the parts that
    /// already track it: WeaponTransition (WeaponInHand weaponType, switch), PerGunAmmo (bound gun and its Reload FSM), Ironsights
    /// (aiming, wheel zoom), the unloader and the NPC shooter cache of Tracers. Pure queries, nothing is polled by itself.
    /// </summary>
    internal sealed class WeaponState : IWeaponState
    {
        /// <summary>The single instance.</summary>
        internal static readonly WeaponState Instance = new WeaponState();

        readonly Dictionary<Fsm, string> _calibers = new Dictionary<Fsm, string>();

        /// <summary>Private: use <see cref="Instance"/>.</summary>
        WeaponState() { }

        /// <summary>Drops per-FSM caches (scene change).</summary>
        internal void Clear() { _calibers.Clear(); }

        /// <summary>The player holds a weapon (WeaponInHand weaponType set and not "hands").</summary>
        public bool HasWeapon { get { var t = WeaponTransition.CurrentWeaponType; return !string.IsNullOrEmpty(t) && t != "hands"; } }

        /// <summary>Catalog handle of the drawn weapon (-1 = none).</summary>
        public int WeaponKey
        {
            get
            {
                var t = WeaponTransition.CurrentWeaponType;
                if (string.IsNullOrEmpty(t) || t == "hands") return -1;
                return WeaponCatalog.Instance.Handle(WeaponCatalog.Instance.ModelNameOf(t));
            }
        }

        /// <summary>Save-stable item id of the drawn gun (per-gun magazines on), else null.</summary>
        public string ItemId { get { return PerGunAmmo.CurrentItem; } }

        /// <summary>Rounds in the drawn gun (0 when no gun is bound).</summary>
        public int AmmoInGun
        {
            get
            {
                var r = PerGunAmmo.CurrentReload;
                var v = r != null ? r.FsmVariables.GetFsmInt("ammo_in_gun") : null;
                return v != null ? v.Value : 0;
            }
        }

        /// <summary>Matching rounds in the backpack (the Ammo FSM variable the gun's checkAmmo state reads).</summary>
        public int Reserve
        {
            get
            {
                var r = PerGunAmmo.CurrentReload;
                if (r == null || r.Fsm == null) return 0;
                string cal = CaliberOf(r);
                var gm = GameRefs.GameManager;
                var ammo = cal != null && gm != null ? FsmUtil.Find(gm, "Ammo") : null;
                var v = ammo != null ? ammo.FsmVariables.GetFsmInt(cal) : null;
                return v != null ? v.Value : 0;
            }
        }

        /// <summary>The drawn gun has rounds but its hammer is down (pump/bolt/hammer guns before the next shot).</summary>
        public bool NeedsCocking
        {
            get
            {
                var r = PerGunAmmo.CurrentReload;
                if (r == null) return false;
                var attack = FsmUtil.Find(r.gameObject, "Attack");
                var hammer = attack != null ? attack.FsmVariables.FindFsmBool("hammer") : null;
                return hammer != null && !hammer.Value && AmmoInGun > 0;
            }
        }

        /// <summary>The drawn gun's Reload FSM is not idle, or the unloader runs.</summary>
        public bool Reloading
        {
            get
            {
                if (AmmoRunner.Unloader.Busy) return true;
                var r = PerGunAmmo.CurrentReload;
                return r != null && r.Fsm != null && r.Fsm.Initialized && r.ActiveStateName != "idle";
            }
        }

        /// <summary>Aiming down sights.</summary>
        public bool Aiming { get { return Ironsights.Aiming; } }

        /// <summary>Aiming with a scoped weapon (catalog Scoped).</summary>
        public bool Scoped
        {
            get
            {
                if (!Ironsights.Aiming) return false;
                WeaponInfo info;
                return WeaponCatalog.Instance.TryGet(WeaponKey, out info) && info.Scoped;
            }
        }

        /// <summary>Wheel zoom factor while aiming (1 = none).</summary>
        public float ZoomFactor { get { return Ironsights.Aiming ? Ironsights.ZoomFactor : 1f; } }

        /// <summary>A smooth weapon switch is in progress.</summary>
        public bool Switching { get { return WeaponTransition.Switching; } }

        /// <summary>Catalog handle of the gun an NPC/turret holds (Tracers shooter cache; false for monsters/unarmed).</summary>
        public bool TryGetNpcWeapon(GameObject npc, out int weaponKey)
        {
            weaponKey = -1;
            Tracers.Kind kind; string held;
            if (npc == null || !Tracers.HeldWeaponName(npc, out kind, out held)) return false;
            weaponKey = WeaponCatalog.Instance.Handle(held);
            return weaponKey >= 0;
        }

        /// <summary>Backpack variable name of a Reload FSM's caliber (its checkAmmo state's GetFsmInt from the Ammo FSM), cached.</summary>
        string CaliberOf(PlayMakerFSM reload)
        {
            string cal;
            if (_calibers.TryGetValue(reload.Fsm, out cal)) return cal;
            cal = null;
            foreach (var st in reload.FsmStates)
            {
                if (st.Name != "checkAmmo") continue;
                foreach (var a in st.Actions)
                {
                    var g = a as GetFsmInt;
                    if (g != null && g.fsmName != null && g.fsmName.Value == "Ammo" && g.variableName != null) { cal = g.variableName.Value; break; }
                }
                break;
            }
            if (_calibers.Count > 128) _calibers.Clear();
            _calibers[reload.Fsm] = cal;
            return cal;
        }
    }
}
