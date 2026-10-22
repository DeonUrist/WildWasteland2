using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// <see cref="ITraversal"/> provider of the Presentation World part (README P3): NPC traversal leases over the shared ladder / climb
    /// animations, the player's ladder state, NPC ladder occupancy and the ladder animation metrics. Replaces the raw
    /// <c>Services.PlayerLadder*</c> / <c>LadderClimbers</c> fields (still written / read for consumers that were not converted yet).
    /// Player climbs and ladders publish <see cref="TraversalStarted"/> / <see cref="TraversalEnded"/> through <see cref="PublishStarted"/> / <see cref="PublishEnded"/>.
    /// </summary>
    internal sealed class TraversalApi : ITraversal
    {
        struct Occupant { public Vector3 Feet; public float At; }
        /// <summary>An occupant not refreshed for this long (seconds) is dropped (NPCAI refreshes every physics step).</summary>
        const float OccupantTimeout = .25f;
        readonly Dictionary<EntityId, TraversalLease> _leases = new Dictionary<EntityId, TraversalLease>();
        readonly Dictionary<EntityId, Occupant> _occupants = new Dictionary<EntityId, Occupant>();
        readonly List<EntityId> _stale = new List<EntityId>();

        /// <summary>Starts an NPC ladder / climb animation lease; null for other kinds, a busy character, a non-humanoid rig or a missing clip.</summary>
        public ITraversalLease Begin(EntityId character, Animator animator, TraversalKind kind)
        {
            if (!PresentationModule.WorldRunning || animator == null || kind == TraversalKind.Drop || kind == TraversalKind.Crawl) return null;
            TraversalLease old;
            if (_leases.TryGetValue(character, out old)) { if (old.Valid) return null; old.Dispose(); }   // an animation that ended on its own still publishes TraversalEnded
            var animation = NpcTraversal.Begin(animator, kind == TraversalKind.Ladder ? "Ladder" : kind == TraversalKind.ClimbHigh ? "ClimbHigh" : "ClimbWaist");
            if (animation == null) return null;
            var lease = new TraversalLease(this, character, kind, animator, animation);
            _leases[character] = lease;
            var at = Origin.World(animator.transform.position);
            var e = new TraversalStarted { Character = character, Kind = kind, StartWorld = at, EndWorld = at };
            ModEvents.Publish(ref e);
            return lease;
        }

        /// <summary>Active lease of a character, or null.</summary>
        public ITraversalLease LeaseOf(EntityId character)
        {
            TraversalLease l;
            return _leases.TryGetValue(character, out l) && l.Valid ? l : null;
        }

        /// <summary>Drops a disposed lease (called by the lease).</summary>
        internal void Forget(TraversalLease lease)
        {
            TraversalLease l;
            if (_leases.TryGetValue(lease.Character, out l) && ReferenceEquals(l, lease)) _leases.Remove(lease.Character);
        }

        /// <summary>Ends every lease and forgets every occupant (World part shutdown / scene load).</summary>
        internal void Clear()
        {
            var all = new List<TraversalLease>(_leases.Values);
            for (int i = 0; i < all.Count; i++) all[i].Dispose();
            _leases.Clear(); _occupants.Clear();
        }

        /// <summary>True while the player climbs a ladder.</summary>
        public bool PlayerOnLadder { get { return LadderController.Active; } }

        /// <summary>Ladder trigger the player climbs, his climbing side, slide pose phase and top slide speed.</summary>
        public bool TryPlayerLadder(out Collider ladder, out Vector3 side, out float slidePhase, out float slideSpeed)
        {
            slidePhase = TraversalSettings.LadderSlidePose != null ? TraversalSettings.LadderSlidePose.Value : 0f;
            slideSpeed = TraversalSettings.LadderSlideSpeed != null ? TraversalSettings.LadderSlideSpeed.Value : 6f;
            return LadderController.TryPlayerLadder(out ladder, out side);
        }

        /// <summary>Reports (on) or clears (off) an NPC's feet on a ladder, scene space.</summary>
        public void SetLadderOccupant(EntityId npc, Vector3 feetScene, bool on)
        {
            if (on) _occupants[npc] = new Occupant { Feet = feetScene, At = Time.time };
            else _occupants.Remove(npc);
        }

        /// <summary>Feet of every NPC on a ladder (cleared first): reported occupants refreshed recently, plus the legacy Services.LadderClimbers list.</summary>
        public void LadderOccupants(List<Vector3> into)
        {
            into.Clear();
            if (_occupants.Count > 0)
            {
                float now = Time.time;
                foreach (var kv in _occupants)
                {
                    if (now - kv.Value.At > OccupantTimeout) { _stale.Add(kv.Key); continue; }
                    into.Add(kv.Value.Feet);
                }
                for (int i = 0; i < _stale.Count; i++) _occupants.Remove(_stale[i]);
                _stale.Clear();
            }
#pragma warning disable 618
            var legacy = Services.LadderClimbers;
#pragma warning restore 618
            for (int i = 0; i < legacy.Count; i++) into.Add(legacy[i]);
        }

        /// <summary>Ladder animation: height climbed per cycle (two rungs), metres (1.24 without the clip).</summary>
        public float LadderCycleRise { get { var c = PoseClip.Shared("ClimbingLadder"); return c != null ? c.Rise : 1.24f; } }

        /// <summary>Ladder animation: hips-to-rung distance, metres (0.44 without the clip).</summary>
        public float LadderToeDepth { get { var c = PoseClip.Shared("ClimbingLadder"); return c != null ? c.ToeDepth : .44f; } }

        /// <summary>True while the player crawls (Low Crawl).</summary>
        public bool PlayerCrawling { get { return Crawling.Active; } }

        /// <summary>Publishes <see cref="TraversalStarted"/> for the player (climb or ladder), positions in scene space converted to world.</summary>
        internal static void PublishStarted(TraversalKind kind, Vector3 startScene, Vector3 endScene)
        {
            var e = new TraversalStarted { Character = Entities.Player, Kind = kind, StartWorld = Origin.World(startScene), EndWorld = Origin.World(endScene) };
            ModEvents.Publish(ref e);
        }

        /// <summary>Publishes <see cref="TraversalEnded"/> for the player.</summary>
        internal static void PublishEnded(TraversalKind kind, Vector3 endScene, bool completed)
        {
            var e = new TraversalEnded { Character = Entities.Player, Kind = kind, EndWorld = Origin.World(endScene), Completed = completed };
            ModEvents.Publish(ref e);
        }
    }
}
