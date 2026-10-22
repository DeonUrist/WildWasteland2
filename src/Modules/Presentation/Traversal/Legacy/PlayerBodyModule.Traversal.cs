using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Old PlayerBody entry part (Wave A split) for climbing and ladders. Wave B (B06): binding, clip loading, jobs, shutdown and origin shifts are
    /// owned by the Presentation module (World part, <see cref="TraversalSettings"/>); the partial methods are empty and the old config statics
    /// forward to the new entries until Wave C deletes them.
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        /// <summary>Forwarder to <see cref="TraversalSettings.ClimbingOn"/> (PLAYER settings page).</summary>
        [Obsolete("refactor: use TraversalSettings / ITraversal")] internal static ConfigEntry<bool> ClimbingOn { get { return TraversalSettings.ClimbingOn; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.JumpTriggersClimbing"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static ConfigEntry<bool> JumpTriggersClimbing { get { return TraversalSettings.JumpTriggersClimbing; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.Ladders"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static ConfigEntry<bool> Ladders { get { return TraversalSettings.Ladders; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.ClimbKey"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static ConfigEntry<Key> ClimbKey { get { return TraversalSettings.ClimbKey; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.ClimbKeyCode"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static KeyCode ClimbKeyCode { get { return TraversalSettings.ClimbKeyCode; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.ClimbMinHeight"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static ConfigEntry<float> ClimbMinHeight { get { return TraversalSettings.ClimbMinHeight; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.ClimbMaxHeight"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static ConfigEntry<float> ClimbMaxHeight { get { return TraversalSettings.ClimbMaxHeight; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.ClimbReach"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static ConfigEntry<float> ClimbReach { get { return TraversalSettings.ClimbReach; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.ClimbWaistLimit"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static ConfigEntry<float> ClimbWaistLimit { get { return TraversalSettings.ClimbWaistLimit; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.ClimbSpeed"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static ConfigEntry<float> ClimbSpeed { get { return TraversalSettings.ClimbSpeed; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.LadderSpeed"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static ConfigEntry<float> LadderSpeed { get { return TraversalSettings.LadderSpeed; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.LadderSlideSpeed"/>.</summary>
        [Obsolete("refactor: use TraversalSettings / ITraversal.TryPlayerLadder")] internal static ConfigEntry<float> LadderSlideSpeed { get { return TraversalSettings.LadderSlideSpeed; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.LadderSlidePose"/>.</summary>
        [Obsolete("refactor: use TraversalSettings / ITraversal.TryPlayerLadder")] internal static ConfigEntry<float> LadderSlidePose { get { return TraversalSettings.LadderSlidePose; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.LadderOffset"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static ConfigEntry<float> LadderOffset { get { return TraversalSettings.LadderOffset; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.LadderEyeForward"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static ConfigEntry<float> LadderEyeForward { get { return TraversalSettings.LadderEyeForward; } }
        /// <summary>Forwarder to <see cref="TraversalSettings.LadderNeckBack"/>.</summary>
        [Obsolete("refactor: use TraversalSettings")] internal static ConfigEntry<float> LadderNeckBack { get { return TraversalSettings.LadderNeckBack; } }

        /// <summary>Old bind step ([PlayerBody / Climbing] switches); empty, Presentation binds <see cref="TraversalSettings"/>.</summary>
        partial void BindTraversal(ModuleConfig cfg) { }
        /// <summary>Old bind step ([Keys] Climb); empty, Presentation binds <see cref="TraversalSettings"/>.</summary>
        partial void BindTraversalKeys(ModuleConfig cfg) { }
        /// <summary>Old bind step ([PlayerBody / Climbing] tools); empty, Presentation binds <see cref="TraversalSettings"/>.</summary>
        partial void BindTraversalTools(ModuleConfig cfg) { }
        /// <summary>Old init step (climbing / ladder clips); empty, Presentation loads them.</summary>
        partial void InitTraversal() { }
        /// <summary>Old init step (climbing / ladder jobs); empty, Presentation registers them.</summary>
        partial void InitTraversalJobs() { }
        /// <summary>Old init step (NPC traversal poses job); empty, Presentation registers it.</summary>
        partial void InitTraversalNpcJobs() { }
        /// <summary>Old shutdown step; empty, Presentation stops the traversals.</summary>
        partial void ShutdownTraversal() { }
        /// <summary>Old origin-shift step; empty, Presentation shifts the climb / ladder paths.</summary>
        partial void OriginShiftTraversal(Vector3 delta) { }
    }
}
