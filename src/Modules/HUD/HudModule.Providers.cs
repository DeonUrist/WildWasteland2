using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.HUD
{
    /// <summary>Registers HUD-owned presentation services under the unified lifecycle.</summary>
    public sealed partial class HudModule : INotifications, ILocationMarkers, IPlaceBannerDisplay, ILocationVisibility
    {
        /// <summary>Publishes typed providers; Core removes all of them when this HUD stops.</summary>
        public override void ProvideApis()
        {
            ModApi.Provide<IPlaceBannerDisplay>(this, this);
            ModApi.Provide<IHitFeedback>(this, Core.ModApi.Get<Core.IHitFeedback>());
            ModApi.Provide<IMapMarkers>(this, _questMap);
            ModApi.Provide<IMapMarkerStyle>(this, _questMap);
            ModApi.Provide<ICompassMarkers>(this, this);
            ModApi.Provide<IVehicleStatusDisplay>(this, this);
            ModApi.Provide<IQuestPresentation>(this, Core.ModApi.Get<Core.IQuestPresentation>());
            ModApi.Provide<INotifications>(this, this);
            ModApi.Provide<ILocationMarkers>(this, this);
            ModApi.Provide<ILocationVisibility>(this,this);
        }
        /// <summary>Respects the existing location-marker switch for external POI markers.</summary>
        bool INotifications.Enabled=>Running&&CleanHudEnabled.Value;
        bool ILocationVisibility.Locations=>Running&&CleanHudEnabled.Value&&ShowLocations.Value;
        /// <summary>Respects the existing master compass switch for external markers.</summary>
        bool ILocationVisibility.Compass=>Running&&CleanHudEnabled.Value&&ShowCompass.Value;
        /// <summary>Whether the replacement generator banner is enabled.</summary>
        bool IPlaceBannerDisplay.Enabled { get { return Running && CleanHudEnabled.Value; } }
        /// <summary>Queues a notice only while the clean HUD can present it.</summary>
        public bool Push(string text, NotificationKind kind, float seconds)
        {
            if (!Running || !CleanHudEnabled.Value || !MessageHook.Active || string.IsNullOrWhiteSpace(text)) return false;
            MessageHook.Push(text, kind, seconds); return true;
        }
        /// <summary>Removes pending and visible notices of one category.</summary>
        public void Clear(NotificationKind kind) { MessageHook.ClearKind(kind); }
        /// <summary>Excludes an owned location from automatic HUD discovery markers.</summary>
        public void Exclude(Transform root, bool excluded) { LocationMarkers.Exclude(root, excluded); }
        /// <summary>Overrides a location's icon without exposing renderer internals.</summary>
        public void SetIcon(Transform root, string icon) { LocationMarkers.SetIcon(root, icon); }
        /// <summary>Overrides a location caption without changing its discovery identity.</summary>
        public void SetLabel(Transform root, string label) { LocationMarkers.SetLabel(root, label); }
        /// <summary>Reports discovery for a live structure.</summary>
        public bool Known(Transform root) { return LocationMarkers.Known(root); }
        /// <summary>Reports discovery for the persisted location identity.</summary>
        public bool Known(string key) { return LocationMarkers.Known(key); }
    }
}
