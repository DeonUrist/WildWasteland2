using BepInEx.Configuration;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Traffic rider decisions and supplied lance tuning belong to their actual traffic controller.</summary>
    public sealed partial class PatrolsModule
    {
        internal static ConfigEntry<float> RiderRange, RiderIntervalMin, RiderIntervalMax, RiderLanceSpeed, RiderMaxThrowAngle, RiderJumpRange, RiderLeapSpeed, RiderBlastDamage, RiderBlastRadius, RiderLeapReachPercent, RiderBailRange;
        /// <summary>Binds rider tuning once under the owning Patrols module.</summary>
        private static void BindRiders(ModuleConfig cfg)
        {
            RiderRange = cfg.Tools.Bind(cfg.Section("Riders"), "RiderRange", 35f, new ConfigDescription("A blast-lance rider throws at a target closer than this, m (and only when a flat throw reaches it)", new AcceptableValueRange<float>(5f, 150f)));
            RiderMaxThrowAngle = cfg.Tools.Bind(cfg.Section("Riders"), "RiderMaxThrowAngle", 20f, new ConfigDescription("The rider only throws when the lance reaches the predicted spot with at most this many degrees up (a throw, not a lob)", new AcceptableValueRange<float>(0f, 45f)));
            RiderJumpRange = cfg.Tools.Bind(cfg.Section("Riders"), "RiderJumpRange", 15f, new ConfigDescription("\"Witness me!\": the rider leaps when your car is closer than this, m", new AcceptableValueRange<float>(3f, 30f)));
            RiderIntervalMin = cfg.Tools.Bind(cfg.Section("Riders"), "RiderIntervalMin", 5f, new ConfigDescription("Shortest pause between two of a rider's throws, s", new AcceptableValueRange<float>(1f, 60f)));
            RiderIntervalMax = cfg.Tools.Bind(cfg.Section("Riders"), "RiderIntervalMax", 9f, new ConfigDescription("Longest pause between two of a rider's throws, s", new AcceptableValueRange<float>(1f, 120f)));
            RiderLeapReachPercent = cfg.Tools.Bind(cfg.Section("Riders"), "RiderLeapReachPercent", 80f, new ConfigDescription("\"Witness me!\": the Warboy only leaps at a car within this % of his longest jump (RiderLeapSpeed x 1.2 s, the speeds of both cars counted)", new AcceptableValueRange<float>(10f, 100f)));
            RiderBailRange = cfg.Tools.Bind(cfg.Section("Riders"), "RiderBailRange", 6f, new ConfigDescription("A Warboy on a motorcycle who cannot turn to you jumps off when you are closer than this, m", new AcceptableValueRange<float>(1f, 30f)));
            RiderLeapSpeed = cfg.Tools.Bind(cfg.Section("Riders"), "RiderLeapSpeed", 11f, new ConfigDescription("\"Witness me!\": the leap's ground speed relative to the rider's car, m/s - a long, flat jump, no lob", new AcceptableValueRange<float>(5f, 40f)));
            RiderBlastDamage = cfg.Tools.Bind(cfg.Section("Riders"), "RiderBlastDamage", 60f, new ConfigDescription("\"Witness me!\": damage of the rider's blast at its centre (in your car too); falls to a third at the edge", new AcceptableValueRange<float>(0f, 300f)));
            RiderBlastRadius = cfg.Tools.Bind(cfg.Section("Riders"), "RiderBlastRadius", 7f, new ConfigDescription("\"Witness me!\": reach of the rider's blast, m (damage and the shove of your car)", new AcceptableValueRange<float>(1f, 20f)));
            RiderLanceSpeed = cfg.Tools.Bind(cfg.Section("Riders"), "RiderLanceSpeed", 30f, new ConfigDescription("Throw speed of a rider's lance, m/s (the player's is 30)", new AcceptableValueRange<float>(5f, 80f)));
        }
    }
}
