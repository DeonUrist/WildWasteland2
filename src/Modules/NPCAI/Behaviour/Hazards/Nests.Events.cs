using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Nests part publishing the NPCAI hazard events NestBuilt / NestFreed / NestAlarm / WebTouched (only while someone listens).</summary>
    internal static partial class Nests
    {
        /// <summary>A nest finished building (webs placed, sitters seated).</summary>
        private static void PublishBuilt(Nest n)
        {
            if (!ModEvents.HasListeners<NestBuilt>()) return;
            var e = new NestBuilt { NestKey = n.Key, WorldPos = Origin.World(n.C) };
            ModEvents.Publish(ref e);
        }

        /// <summary>A nest was freed (its place is gone or the player went far).</summary>
        private static void PublishFreed(Nest n)
        {
            if (!ModEvents.HasListeners<NestFreed>()) return;
            var e = new NestFreed { NestKey = n.Key, WorldPos = Origin.World(n.C) };
            ModEvents.Publish(ref e);
        }

        /// <summary>The player touched a web of the nest: WebTouched, then the nest's NestAlarm.</summary>
        private static void PublishTouched(Nest n, Vector3 webScene, GameObject intruder)
        {
            bool touched = ModEvents.HasListeners<WebTouched>(), alarm = ModEvents.HasListeners<NestAlarm>();
            if (!touched && !alarm) return;
            var who = intruder == null ? EntityId.None : intruder == GameRefs.Player ? Entities.Player : Entities.Of(intruder);
            if (touched) { var t = new WebTouched { Toucher = who, WorldPos = Origin.World(webScene), NestKey = n.Key }; ModEvents.Publish(ref t); }
            if (alarm) { var a = new NestAlarm { NestKey = n.Key, WorldPos = Origin.World(n.C), Intruder = who }; ModEvents.Publish(ref a); }
        }
    }
}
