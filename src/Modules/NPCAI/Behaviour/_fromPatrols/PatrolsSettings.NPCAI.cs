using System;
using BepInEx.Configuration;
using WildWasteland.Core;
using WildWasteland.Modules.NPC;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// Former NPCAI part of the Patrols settings (Wave A split). Since B07 the blast-lance rider tuning is bound by NPCAI in
    /// [NPCAI / Riders] (NpcaiSettings); the old PatrolsModule.Rider* statics forward there for readers outside NPCAI (Gunplay lance).
    /// </summary>
    public sealed partial class PatrolsModule
    {
        /// <summary>[Obsolete] Forwarder to NpcaiSettings.RiderRange.</summary>
        [Obsolete("refactor: NPCAI config [NPCAI / Riders]")] internal static ConfigEntry<float> RiderRange { get { return NpcaiSettings.RiderRange; } }
        /// <summary>[Obsolete] Forwarder to NpcaiSettings.RiderIntervalMin.</summary>
        [Obsolete("refactor: NPCAI config [NPCAI / Riders]")] internal static ConfigEntry<float> RiderIntervalMin { get { return NpcaiSettings.RiderIntervalMin; } }
        /// <summary>[Obsolete] Forwarder to NpcaiSettings.RiderIntervalMax.</summary>
        [Obsolete("refactor: NPCAI config [NPCAI / Riders]")] internal static ConfigEntry<float> RiderIntervalMax { get { return NpcaiSettings.RiderIntervalMax; } }
        /// <summary>[Obsolete] Forwarder to NpcaiSettings.RiderLanceSpeed (Gunplay lance throw).</summary>
        [Obsolete("refactor: NPCAI config [NPCAI / Riders]")] internal static ConfigEntry<float> RiderLanceSpeed { get { return NpcaiSettings.RiderLanceSpeed; } }
        /// <summary>[Obsolete] Forwarder to NpcaiSettings.RiderMaxThrowAngle.</summary>
        [Obsolete("refactor: NPCAI config [NPCAI / Riders]")] internal static ConfigEntry<float> RiderMaxThrowAngle { get { return NpcaiSettings.RiderMaxThrowAngle; } }
        /// <summary>[Obsolete] Forwarder to NpcaiSettings.RiderJumpRange.</summary>
        [Obsolete("refactor: NPCAI config [NPCAI / Riders]")] internal static ConfigEntry<float> RiderJumpRange { get { return NpcaiSettings.RiderJumpRange; } }
        /// <summary>[Obsolete] Forwarder to NpcaiSettings.RiderLeapSpeed.</summary>
        [Obsolete("refactor: NPCAI config [NPCAI / Riders]")] internal static ConfigEntry<float> RiderLeapSpeed { get { return NpcaiSettings.RiderLeapSpeed; } }
        /// <summary>[Obsolete] Forwarder to NpcaiSettings.RiderBlastDamage (Gunplay rider blast).</summary>
        [Obsolete("refactor: NPCAI config [NPCAI / Riders]")] internal static ConfigEntry<float> RiderBlastDamage { get { return NpcaiSettings.RiderBlastDamage; } }
        /// <summary>[Obsolete] Forwarder to NpcaiSettings.RiderBlastRadius (Gunplay rider blast).</summary>
        [Obsolete("refactor: NPCAI config [NPCAI / Riders]")] internal static ConfigEntry<float> RiderBlastRadius { get { return NpcaiSettings.RiderBlastRadius; } }
        /// <summary>[Obsolete] Forwarder to NpcaiSettings.RiderLeapReachPercent.</summary>
        [Obsolete("refactor: NPCAI config [NPCAI / Riders]")] internal static ConfigEntry<float> RiderLeapReachPercent { get { return NpcaiSettings.RiderLeapReachPercent; } }
        /// <summary>[Obsolete] Forwarder to NpcaiSettings.RiderBailRange.</summary>
        [Obsolete("refactor: NPCAI config [NPCAI / Riders]")] internal static ConfigEntry<float> RiderBailRange { get { return NpcaiSettings.RiderBailRange; } }

        /// <summary>Empty (B07): the rider decision tuning is bound by NpcaiSettings in [NPCAI / Riders].</summary>
        partial void BindNpcaiRiderTuning(ModuleConfig cfg) { }

        /// <summary>Empty (B07): the rider leap / lance tuning is bound by NpcaiSettings in [NPCAI / Riders].</summary>
        partial void BindNpcaiRiderLeap(ModuleConfig cfg) { }
    }
}
