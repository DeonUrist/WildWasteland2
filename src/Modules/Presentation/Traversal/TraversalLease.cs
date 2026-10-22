using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>One NPC's traversal lease handed out by <see cref="TraversalApi.Begin"/>: wraps the NPC traversal animation and publishes <see cref="TraversalEnded"/> on Dispose.</summary>
    internal sealed class TraversalLease : ITraversalLease
    {
        readonly INpcTraversalAnimation _animation;
        readonly Animator _animator;
        readonly TraversalApi _owner;
        bool _ended;

        /// <summary>Wraps a started animation of <paramref name="character"/>.</summary>
        internal TraversalLease(TraversalApi owner, EntityId character, TraversalKind kind, Animator animator, INpcTraversalAnimation animation)
        { _owner = owner; Character = character; Kind = kind; _animator = animator; _animation = animation; }

        /// <summary>Character holding the lease.</summary>
        public EntityId Character { get; private set; }
        /// <summary>Traversal kind.</summary>
        public TraversalKind Kind { get; private set; }
        /// <summary>False once ended or when the animator died.</summary>
        public bool Valid { get { return !_ended && _animation.Valid; } }
        /// <summary>Phase (cycles for ladders, 0..1 for climbs).</summary>
        public float Phase { get { return _animation.Phase; } set { _animation.Phase = value; } }
        /// <summary>Natural length of one cycle or the clip, seconds.</summary>
        public float Length { get { return _animation.Length; } }

        /// <summary>Ends the animation, forgets the lease and publishes <see cref="TraversalEnded"/> (Completed = the phase reached the clip end).</summary>
        public void Dispose()
        {
            if (_ended) return;
            _ended = true;
            bool completed = Kind != TraversalKind.Ladder && _animation.Phase >= 1f;
            _animation.Dispose();
            _owner.Forget(this);
            var e = new TraversalEnded { Character = Character, Kind = Kind, EndWorld = _animator != null ? Origin.World(_animator.transform.position) : Vector3.zero, Completed = completed };
            ModEvents.Publish(ref e);
        }
    }
}
