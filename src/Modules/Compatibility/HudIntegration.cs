using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;
using WildWasteland.Modules.HUD;
namespace WildWasteland.Modules.Compatibility
{
    /// <summary>Typed facade of the optional driving source, preserving its own visibility and diagnostics.</summary>
    internal sealed class DrivingHudAdapter : IDrivingHud
    {
        /// <summary>Source dashboard setting.</summary>
        public ConfigEntry<bool> Enabled { get { return ChaseCameraHud.HudEnabled; } }
        /// <summary>Copies the native snapshot after the source validates it.</summary>
        public bool TryRead(bool thirdPersonOnly, out DrivingHudState state)
        {
            state = default(DrivingHudState);
            if (!ChaseCameraHud.Refresh(thirdPersonOnly)) return false;
            state = new DrivingHudState { Speed = ChaseCameraHud.Speed, Rpm = ChaseCameraHud.Rpm, Fraction = ChaseCameraHud.Fraction, Scale = ChaseCameraHud.Scale, Gap = ChaseCameraHud.Gap, Gear = ChaseCameraHud.Gear }; return true;
        }
        /// <summary>Yields the source's duplicate overlay for this frame.</summary>
        public void HideNative() { ChaseCameraHud.HideNative(); }
        /// <summary>Returns the source camera's rendered heading.</summary>
        public bool TryHeading(out float yaw) { return ChaseCameraHud.TryHeading(out yaw); }
        /// <summary>Records unchanged dashboard diagnostics.</summary>
        public void Trace(Text rpm, bool visible) { ChaseCameraHud.TraceState(rpm, visible); }
        /// <summary>Records the hidden transition.</summary>
        public void TraceHidden() { ChaseCameraHud.TraceHidden(); }
    }
    /// <summary>Typed optional inventory UI adapter with one reused reader.</summary>
    internal sealed class PocketHudAdapter : IPocketHud
    {
        readonly ApocapocketInventory _reader = new ApocapocketInventory();
        /// <summary>Current source-widget revision.</summary>
        public int UiRevision { get { return ApocapocketInventory.UiRevision; } }
        /// <summary>Copies slot objects and kinds without allocating.</summary>
        public void Read(PocketHudState state)
        {
            _reader.Read(); state.Count = _reader.Count; state.Selected = _reader.Selected; state.Active = _reader.Active;
            Array.Copy(_reader.Items, state.Items, 6); Array.Copy(_reader.Ordinary, state.Ordinary, 6);
        }
        /// <summary>Collects borrowed graphics and restores the optional source's widget shifts.</summary>
        public void CollectGraphics(Canvas canvas, Transform hud, List<Graphic> into) { ApocapocketInventory.CaptureUi(canvas, hud, into); }
        /// <summary>Source-owned item icon.</summary>
        public Texture ItemIcon(GameObject item) { return ApocapocketInventory.ItemIcon(item); }
        /// <summary>Source-owned key caption.</summary>
        public string ActionLabel(string action) { return ApocapocketInventory.ActionLabel(action); }
    }
    /// <summary>Optional Weapons Pack model identity and borrowed visual source.</summary>
    internal sealed class WeaponIconModels : IWeaponIconModels
    {
        /// <summary>Looks up supported held models through the installed pack accessors.</summary>
        public GameObject Model(GameObject item, out string key) { return WeaponsPackIcons.Model(item, out key); }
    }
    /// <summary>Generator banner event adapter with the bridge's own lifetime.</summary>
    internal sealed class PlaceBanners : IPlaceBanners
    {
        /// <summary>Forwards live generator place events.</summary>
        public event Action<string, string> Entered { add { VistasWorldGen.PlaceEntered += value; } remove { VistasWorldGen.PlaceEntered -= value; } }
        /// <summary>Source-configured banner height.</summary>
        public float Height { get { return VistasWorldGen.BannerHeight; } }
    }
}
