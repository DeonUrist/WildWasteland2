using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// Publishers of the character presentation events (Core.Events PresentationEvents.cs): CharacterAttached/Detached when the ModAPI
    /// driver starts/stops animating a character, CharacterModelChanged for the player's character choice and Women of Wasteland bodies,
    /// CharacterVoice for the female player pain/death cues. Every publisher first checks ModEvents.HasListeners (no cost without one).
    /// </summary>
    internal static class PresentationEvents
    {
        /// <summary>Entity of an animated character: the Rigidbody root above its Animator (NPC body), else the Animator's object.</summary>
        internal static EntityId EntityOf(Animator a)
        {
            if (a == null) return EntityId.None;
            var rb = a.GetComponentInParent<Rigidbody>();
            return Entities.Of(rb != null ? rb.gameObject : a.gameObject);
        }

        /// <summary>Publishes CharacterAttached for an Animator the ModAPI driver now animates.</summary>
        internal static void Attached(Animator a, CharacterKind kind)
        {
            if (!ModEvents.HasListeners<CharacterAttached>()) return;
            var e = new CharacterAttached { Character = kind == CharacterKind.Player ? Entities.Player : EntityOf(a), Kind = kind, ModelId = null };
            ModEvents.Publish(ref e);
        }

        /// <summary>Publishes CharacterDetached for an Animator the ModAPI driver released.</summary>
        internal static void Detached(Animator a, CharacterKind kind)
        {
            if (!ModEvents.HasListeners<CharacterDetached>()) return;
            var e = new CharacterDetached { Character = kind == CharacterKind.Player ? Entities.Player : EntityOf(a), Kind = kind };
            ModEvents.Publish(ref e);
        }

        /// <summary>Publishes CharacterModelChanged (player choice or a Women of Wasteland body swap).</summary>
        internal static void ModelChanged(EntityId id, string modelId, string variant)
        {
            if (!ModEvents.HasListeners<CharacterModelChanged>()) return;
            var e = new CharacterModelChanged { Character = id, ModelId = modelId, Variant = variant };
            ModEvents.Publish(ref e);
        }

        /// <summary>Publishes CharacterModelChanged for a scene object (its entity id is only looked up when someone listens).</summary>
        internal static void ModelChanged(GameObject go, string modelId, string variant)
        {
            if (go == null || !ModEvents.HasListeners<CharacterModelChanged>()) return;
            ModelChanged(Entities.Of(go), modelId, variant);
        }

        /// <summary>Publishes CharacterVoice for a cue played at <paramref name="scenePos"/> (converted to world space).</summary>
        internal static void Voice(EntityId id, string clipId, Vector3 scenePos)
        {
            if (!ModEvents.HasListeners<CharacterVoice>()) return;
            var e = new CharacterVoice { Character = id, ClipId = clipId, WorldPos = Origin.World(scenePos) };
            ModEvents.Publish(ref e);
        }
    }
}
