namespace WildWasteland.Core
{
    /// <summary>HUD-owned switches for location markers, queried without exposing its configuration entries.</summary>
    public interface ILocationVisibility
    {
        /// <summary>Location markers are enabled by the current HUD settings.</summary>
        bool Locations {get;}
        /// <summary>The compass itself is enabled by the current HUD settings.</summary>
        bool Compass {get;}
    }
}
