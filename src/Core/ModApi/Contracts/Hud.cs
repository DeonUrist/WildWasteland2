using System;
using UnityEngine;

// HUD contracts (core-modapi.md 8): map/compass/location markers, notifications, quest presentation, hit feedback, vehicle status,
// UI theme. Provider: HUD (IUiTheme: Core). Consumers: Patrols, POI, Quests, Saves, Compatibility, Gunplay. Interfaces only. FROZEN after W0.
namespace WildWasteland.Core
{
    // ------------------------------------------------------------------ moved unchanged from Services.cs / QuestServices.cs

    /// <summary>Map detail levels (Off, Ambiguous = gray/question marks, On).</summary>
    public enum MapDetail { Off, Ambiguous, On }

    /// <summary>Map markers and map state (ex CleanHUD QuestMap). Style setters live in <see cref="IMapMarkerStyle"/>.</summary>
    public interface IMapMarkers
    {
        /// <summary>Borrowed map artwork, or null while Clean HUD is off. Callers must not dispose it.</summary>
        Texture2D GetIcon(string key);
        /// <summary>Map setting "Markers".</summary>
        MapDetail Markers { get; }
        /// <summary>Map setting "Routes".</summary>
        MapDetail Routes { get; }
        /// <summary>Adds a marker; returns its id.</summary>
        int Add(string owner, Func<Vector3> position, string icon, Color color, string label);
        /// <summary>True while the marker exists.</summary>
        bool Exists(int id);
        /// <summary>Removes a marker.</summary>
        void Remove(int id);
        /// <summary>Removes every marker of an owner.</summary>
        void RemoveAll(string owner);
        /// <summary>True while the map's fog of war covers this scene position (always false with the fog off).</summary>
        bool Hidden(Vector3 scene);
        /// <summary>Saved exploration at this scene position, independent of fog display settings.</summary>
        bool Explored(Vector3 scene);
        /// <summary>True while the native map is shown (other mouse-wheel users stand down).</summary>
        bool Open { get; }
        /// <summary>Map setting "World traffic markers": moving traffic dots + names, with independent detail and fog of war.</summary>
        MapDetail TrafficMarkers { get; }
        /// <summary>Map camera zoom factor: 1 = native view, smaller = zoomed in (min about 0.667).</summary>
        float Zoom { get; }
        /// <summary>Observe a live target through binoculars, offering its colored caption; true when visible.</summary>
        bool Observe(Transform target, string name, Color color);
    }

    /// <summary>Compass markers (ex CleanHUD compass).</summary>
    public interface ICompassMarkers
    {
        /// <summary>Compass shows vehicle markers.</summary>
        bool VehiclesEnabled { get; }
        /// <summary>Adds a marker; returns its id.</summary>
        int Add(string owner, Func<Vector3> position, string icon, Color color);
        /// <summary>True while the marker exists.</summary>
        bool Exists(int id);
        /// <summary>Removes a marker.</summary>
        bool Remove(int id);
        /// <summary>Reveals the marker's real icon/color (discovered).</summary>
        void Identify(int id);
        /// <summary>Removes every marker of an owner.</summary>
        void RemoveAll(string owner);
    }

    /// <summary>Quest widgets of the HUD (tracker, journal, conversation).</summary>
    public interface IQuestPresentation
    {
        /// <summary>Tracker widget available.</summary>
        bool Tracker { get; }
        /// <summary>Journal widget available.</summary>
        bool Journal { get; }
        /// <summary>Conversation widget available.</summary>
        bool Conversation { get; }
        /// <summary>Shows a quest notice; false when not shown.</summary>
        bool Notify(string text);
    }

    /// <summary>Vehicle status line (ignition/handbrake/cassette) drawn by the HUD.</summary>
    public interface IVehicleStatusDisplay
    {
        /// <summary>Shows the status; false when the HUD does not draw it (caller falls back).</summary>
        bool TryShow(bool ignitionOff, bool handbrakeOn, bool playing, string volume);
    }

    /// <summary>Mirror of GunplayHUD.Api: hit markers/numbers.</summary>
    public interface IHitFeedback
    {
        /// <summary>Hit feedback enabled.</summary>
        bool Enabled { get; }
        /// <summary>Player hit a character.</summary>
        void PlayerHit(GameObject target, Vector3 point, float damage, bool head);
        /// <summary>Player hit a vehicle part (percent of condition).</summary>
        void PartHit(GameObject target, Vector3 point, float percent);
        /// <summary>True while the Guns module reports hits through this API, so the HUD's vanilla SetFsmFloat observer must stay quiet.</summary>
        bool ExternalReporter { get; set; }
    }

    // ------------------------------------------------------------------ new (Wave B HUD)

    /// <summary>Style changes of existing map markers (label, colour, size).</summary>
    public interface IMapMarkerStyle
    {
        /// <summary>Changes a marker's label.</summary>
        void SetLabel(int id, string label);
        /// <summary>Changes a marker's colour.</summary>
        void SetColor(int id, Color color);
        /// <summary>Changes a marker's size factor (1 = default).</summary>
        void SetSize(int id, float size);
        /// <summary>Changes a marker's icon key.</summary>
        void SetIcon(int id, string icon);
    }

    /// <summary>Location (POI/town) markers discovered by proximity/binoculars (replaces Towns' patches on LocationMarkers.Observe/QuestMap.Tick).</summary>
    public interface ILocationMarkers
    {
        /// <summary>Excludes a POI root from automatic location markers (towns draw their own).</summary>
        void Exclude(Transform root, bool excluded);
        /// <summary>Overrides the icon of a location root.</summary>
        void SetIcon(Transform root, string icon);
        /// <summary>Overrides the label of a location root.</summary>
        void SetLabel(Transform root, string label);
        /// <summary>True when the location has been discovered.</summary>
        bool Known(Transform root);
        /// <summary>True when a location key ("Label:x:z") has been discovered.</summary>
        bool Known(string locationKey);
    }

    /// <summary>Notification categories (style/priority).</summary>
    public enum NotificationKind { Info, Quest, Save, Warning, Combat, World }   // refs-ok: enum values, not module types

    /// <summary>On-screen notices (replaces direct CleanHUD MessageHook.Push calls).</summary>
    public interface INotifications
    {
        /// <summary>Shows a notice for <paramref name="seconds"/> (0 = default); false when not shown.</summary>
        bool Push(string text, NotificationKind kind, float seconds);
        /// <summary>Removes notices of a kind.</summary>
        void Clear(NotificationKind kind);
    }

    /// <summary>Shared UI theme (GlyphText, AccentTheme, panel textures). Provider: Core UI.</summary>
    public interface IUiTheme
    {
        /// <summary>Accent colour.</summary>
        Color Accent { get; }
        /// <summary>Accent name ("Yellow"...).</summary>
        string AccentName { get; }
        /// <summary>Main text font.</summary>
        Font Font { get; }
        /// <summary>Panel background texture (may be null before load).</summary>
        Texture2D Panel { get; }
        /// <summary>Muted text colour.</summary>
        Color Muted { get; }
        /// <summary>Raised when accent or font changes.</summary>
        event Action Changed;
    }
}
