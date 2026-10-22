using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
namespace WildWasteland.Core
{
    /// <summary>Third-party driving dashboard readings captured without exposing mod-specific types.</summary>
    public struct DrivingHudState { public float Speed, Rpm, Fraction, Scale, Gap; public string Gear; }
    /// <summary>Optional camera mod's dashboard and rendered heading, owned by Compatibility.</summary>
    public interface IDrivingHud
    {
        /// <summary>External dashboard enable setting, or null until resolved.</summary>
        ConfigEntry<bool> Enabled { get; }
        /// <summary>Captures readings, respecting the HUD's requested camera policy.</summary>
        bool TryRead(bool thirdPersonOnly, out DrivingHudState state);
        /// <summary>Hides the optional source dashboard for this captured frame.</summary>
        void HideNative();
        /// <summary>Gets the actually rendered camera heading.</summary>
        bool TryHeading(out float yaw);
        /// <summary>Retains detailed source diagnostics with native UI values.</summary>
        void Trace(Text rpm, bool visible);
        /// <summary>Records the transition to a hidden dashboard.</summary>
        void TraceHidden();
    }
    /// <summary>Extra-slot presentation state; arrays are allocated once by the consuming view.</summary>
    public sealed class PocketHudState
    {
        /// <summary>Borrowed live item objects used only for local presentation.</summary>
        public readonly GameObject[] Items = new GameObject[6];
        /// <summary>Ordinary-item classification for the borrowed objects.</summary>
        public readonly bool[] Ordinary = new bool[6];
        /// <summary>Current unlock count and selection.</summary>
        public int Count = 3, Selected = -1;
        /// <summary>True while the optional slot owner is enabled and alive.</summary>
        public bool Active;
    }
    /// <summary>Optional extra-slot visuals and input labels, with native widget capture independent of HUD implementation.</summary>
    public interface IPocketHud
    {
        /// <summary>Revision of the source widgets.</summary>
        int UiRevision { get; }
        /// <summary>Copies the stable latest slots into caller-owned storage.</summary>
        void Read(PocketHudState state);
        /// <summary>Appends graphics hidden by the replacement HUD and restores source widget shifts.</summary>
        void CollectGraphics(Canvas canvas, Transform hud, List<Graphic> into);
        /// <summary>Borrowed ordinary-item artwork.</summary>
        Texture ItemIcon(GameObject item);
        /// <summary>Optional contextual key label.</summary>
        string ActionLabel(string action);
    }
    /// <summary>Optional weapon models available to the HUD silhouette renderer.</summary>
    public interface IWeaponIconModels
    {
        /// <summary>Resolves a borrowed model and its stable cache key, or null.</summary>
        GameObject Model(GameObject item, out string key);
    }
    /// <summary>World-generator place events and banner placement, owned by Compatibility.</summary>
    public interface IPlaceBanners
    {
        /// <summary>Raised when the external world generator enters a place.</summary>
        event Action<string, string> Entered;
        /// <summary>Vertical banner placement as a fraction of screen height.</summary>
        float Height { get; }
    }
    /// <summary>Replacement banner visibility supplied by HUD so optional generators can yield their own presentation.</summary>
    public interface IPlaceBannerDisplay
    {
        /// <summary>The replacement place banner renderer is enabled.</summary>
        bool Enabled { get; }
    }
}
