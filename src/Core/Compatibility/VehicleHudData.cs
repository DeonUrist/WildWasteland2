using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Read-only cockpit presentation data published by Vehicles; HUD owns hint settings and drawing.</summary>
    public interface IVehicleHudData
    {
        bool Active { get; }
        bool IgnitionOff { get; }
        bool HandbrakeOn { get; }
        bool Playing { get; }
        string Volume { get; }
        string Hint { get; }
        Font Font { get; }
        FontStyle FontStyle { get; }
        Color HintColor { get; }
        int FontSize { get; }
    }
}
