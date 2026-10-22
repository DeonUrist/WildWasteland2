using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Copied quest NPC identity for Debug's authoring recorder; no quest engine or scene object is retained.</summary>
    public struct NpcDiagnosticIdentity
    {
        public string Name, Location, Faction;
        public float Distance;
        public Vector3 World;
    }
    /// <summary>Debug owns whether quest identity observations are requested and consumes the copied values.</summary>
    public static class QuestDiagnosticState
    {
        public static bool RecordNpcNames;
        public static event Action<NpcDiagnosticIdentity> NpcIdentified;
        /// <summary>Publishes an explicitly requested identity record without a module-to-module dependency.</summary>
        public static void Publish(NpcDiagnosticIdentity identity)
        {
            if (!RecordNpcNames) return;
            var handler = NpcIdentified; if (handler != null) handler(identity);
        }
    }
    /// <summary>Quest-private context for the on-demand faction dump; core global/native object details remain Debug-owned.</summary>
    public interface IQuestDiagnosticSource
    {
        /// <summary>Copy of the configured NPC name classification words.</summary>
        string[] PeopleWords { get; }
        /// <summary>Formats the existing takeover/override/near-POI diagnostic details around a transient scene-space point.
        /// The point is never persisted or retained; exported positions in identity payloads are world-space.</summary>
        string DescribeFactionContext(Vector3 sceneCenter);
    }
    /// <summary>Debug-owned display choices read by Patrols without depending on the Debug module.</summary>
    public static class RuntimeDiagnosticState
    {
        public static bool AiOverlay, TrafficDebugMap = true;
    }
    /// <summary>Patrols-private diagnostic selectors; Debug owns the screen labels, controls and output.</summary>
    public interface IPatrolDiagnosticSource
    {
        /// <summary>Current pilot/raider counts and automatic-spawn status.</summary>
        string Status { get; }
        /// <summary>Copies the existing pilot lines or the no-pilot convoy fallback in their original order.</summary>
        void CollectOverlayLines(List<string> into);
    }
}
