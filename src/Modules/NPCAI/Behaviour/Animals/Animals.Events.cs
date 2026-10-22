using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.NPCAI
{
    /// <summary>
    /// Publishers of the NPCAI animal events (BurrowChanged, PounceLeap, DogBite) for Presentation/HUD consumers: entity ids and world
    /// positions only, built only while someone listens.
    /// </summary>
    internal static class AnimalEvents
    {
        /// <summary>Entity id of an NPC or the player (the player has its fixed id).</summary>
        private static EntityId Id(GameObject go) { return go == null ? EntityId.None : go == GameRefs.Player ? Entities.Player : Entities.Of(go); }

        /// <summary>A burrowing creature changed state (burrows, is under, rises, is out).</summary>
        internal static void Burrow(GameObject npc, BurrowState state, Vector3 scene, Vector3 targetScene)
        {
            if (npc == null || !ModEvents.HasListeners<BurrowChanged>()) return;
            var e = new BurrowChanged { Npc = Id(npc), State = state, WorldPos = Origin.World(scene), TargetWorld = Origin.World(targetScene) };
            ModEvents.Publish(ref e);
        }

        /// <summary>A pouncer leapt at its target.</summary>
        internal static void Leap(GameObject npc, GameObject target, Vector3 fromScene, Vector3 toScene)
        {
            if (npc == null || !ModEvents.HasListeners<PounceLeap>()) return;
            var e = new PounceLeap { Npc = Id(npc), Target = Id(target), FromWorld = Origin.World(fromScene), ToWorld = Origin.World(toScene) };
            ModEvents.Publish(ref e);
        }

        /// <summary>A dog's real bite landed (the Damage FSM hit).</summary>
        internal static void Bite(GameObject dog, GameObject target, Vector3 scene)
        {
            if (dog == null || !ModEvents.HasListeners<Core.Events.DogBite>()) return;
            var e = new Core.Events.DogBite { Npc = Id(dog), Target = Id(target), WorldPos = Origin.World(scene) };
            ModEvents.Publish(ref e);
        }
    }
}
