using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// Senses part behind the NPCAI ModAPI (INpcControl, README 6.2): behaviour mode and target of an agent, hearing policy, player
    /// hostility, NPCs near a point, the player-perception query for arbitrary eyes (patrol cars, sentries) and the NpcSpawned /
    /// NpcDespawned / NpcStateChanged / NoiseEmitted events. Every publisher checks ModEvents.HasListeners first (no work while nobody listens).
    /// </summary>
    internal static partial class Senses
    {
        /// <summary>Behaviour mode of an NPC from its senses state, refined by the brain (cover, follower) and the seat; Idle when unknown.</summary>
        internal static NpcMode ModeOf(GameObject npc)
        {
            var a = Get(npc);
            if (a == null) return Brain.IsFollowerDriven(npc) ? NpcMode.Follow : NpcMode.Idle;
            return ModeOf(a);
        }

        /// <summary>Mode of a registered agent (see <see cref="ModeOf(GameObject)"/>).</summary>
        private static NpcMode ModeOf(Agent a)
        {
            if (a.Owner == null) return NpcMode.Dead;
            if (a.T.parent != null) return NpcMode.Seated;
            if (Brain.IsFollowerDriven(a.Owner)) return NpcMode.Follow;
            if (Brain.Retreating(a.Owner)) return NpcMode.Flee;
            switch (a.State)
            {
                case State.Combat: return Brain.InCover(a.Owner) ? NpcMode.Cover : NpcMode.Combat;
                case State.Hide: return NpcMode.Cover;
                case State.Investigate: return NpcMode.Investigate;
                case State.Search: return NpcMode.Search;
                default: return Idle.Travelling(a.Owner) ? NpcMode.Travel : NpcMode.Idle;
            }
        }

        /// <summary>The NPC's current target (combat / cover target, else the object it walks to); false when it has none.</summary>
        internal static bool TryGetTarget(GameObject npc, out GameObject target)
        {
            target = null;
            var a = Get(npc);
            if (a == null) return false;
            target = (a.State == State.Combat || a.State == State.Hide) ? a.Target : a.Ghost != null ? a.Ghost.Subject : null;
            return target != null;
        }

        /// <summary>Interior / town hearing policy: deaf = ignores noises (sight still works), interior = only hears inside its structure.</summary>
        internal static void SetHearingPolicy(GameObject npc, bool deaf, bool interior)
        {
            var a = Get(npc) ?? TryRegister(npc);
            if (a == null) return;
            a.Deaf = deaf; a.Interior = interior;
            if (interior) a.AmbushDist = 0f;
            ClearDeafInvestigation(a);
        }

        /// <summary>Makes an NPC hostile / friendly to the player: the game's own PlayerIsEnemy switch plus the senses' enemy list.</summary>
        internal static void SetPlayerHostile(GameObject npc, bool hostile)
        {
            if (npc == null) return;
            foreach (var f in FsmIndex.ByOwner(npc))
                if (f != null && f.FsmName == "PlayerIsEnemy" && f.Fsm != null && f.Fsm.Initialized) { string want = hostile ? "PlayerEnemy" : "PlayerFriendly"; if (f.ActiveStateName != want) f.Fsm.SetState(want); break; }
            var a = Get(npc);
            if (a == null) return;
            if (hostile) { a.Hostile.Add("Player"); a.BaseHostile.Add("Player"); }
            else
            {
                a.Hostile.Remove("Player"); a.BaseHostile.Remove("Player");
                if ((a.State == State.Combat || a.State == State.Hide) && a.Target == _player) GiveUp(a, "made friendly");
            }
        }

        /// <summary>Fills <paramref name="into"/> (cleared first) with the registered live NPCs within <paramref name="radius"/> of a scene point.</summary>
        internal static void NpcsNear(Vector3 scene, float radius, List<GameObject> into)
        {
            into.Clear();
            float r2 = radius * radius;
            foreach (var kv in _agents)
            {
                var a = kv.Value;
                if (a.Owner != null && a.Owner.activeInHierarchy && (a.T.position - scene).sqrMagnitude <= r2) into.Add(a.Owner);
            }
        }

        /// <summary>Number of registered live NPCs of a faction tag (null = any) within a radius of a scene point.</summary>
        internal static int CountNear(Vector3 scene, float radius, string faction)
        {
            int n = 0; float r2 = radius * radius;
            foreach (var kv in _agents)
            {
                var a = kv.Value;
                if (a.Owner == null || !a.Owner.activeInHierarchy || (faction != null && a.Tag != faction)) continue;
                if ((a.T.position - scene).sqrMagnitude <= r2) n++;
            }
            return n;
        }

        /// <summary>True when the object is an NPC the senses registered.</summary>
        internal static bool Knows(GameObject go) { return Get(go) != null; }

        /// <summary>
        /// Player perception from an arbitrary eye (a patrol car roof, a sentry): sight within the profile's range (scaled by the light and
        /// sandstorms when UseLight; full range with the player's flashlight on) and field of view with a clear line to the player's head or
        /// body (the player's car and the eye's own vehicle never block), or hearing within HearingRange of a moving player or his running engine.
        /// </summary>
        internal static bool CanPerceivePlayer(Vector3 fromScene, Vector3 forward, PerceptionProfile profile)
        {
            FindPlayer();
            if (_player == null || _player.layer == 5) return false;     // God Mode: never perceived (as the senses' own sight)
            Vector3 pp = _player.transform.position, flat = pp - fromScene; flat.y = 0f;
            float d = flat.magnitude;
            if (profile.HearingRange > 0f && d <= profile.HearingRange)
            {
                var car = PlayerCar();
                if ((car != null && Nwh.EngineRunning(car)) || _playerMoving) return true;
            }
            float range = profile.SightRange;
            if (profile.UseLight && !FlashlightOn())
            {
                float dark = Mathf.Clamp01(NpcaiSettings.DarkSightRange.Value / Mathf.Max(1f, NpcaiSettings.SightRange.Value));
                range *= Mathf.Lerp(dark, 1f, LightLevel());
            }
            if (profile.UseLight && _playerInStorm) range *= Storm.SightFactor;
            if (d > range) return false;
            if (profile.FovDegrees < 360f && flat.sqrMagnitude > 0.0001f && Vector3.Angle(forward, flat) > profile.FovDegrees * 0.5f) return false;
            Vector3 head, body;
            Points(_player, true, out head, out body);
            int mask = profile.OccluderMask != 0 ? profile.OccluderMask : SightBlockers;
            return ClearFrom(fromScene, head, mask) || ClearFrom(fromScene, body, mask);
        }

        /// <summary>Line check for <see cref="CanPerceivePlayer"/>: the player, his car and anything at the eye itself (its own car) do not block.</summary>
        private static bool ClearFrom(Vector3 from, Vector3 to, int mask)
        {
            Vector3 d = to - from; float len = d.magnitude;
            if (len < 0.05f) return true;
            int count = Physics.RaycastNonAlloc(from, d / len, _perceiveHits, len, mask, QueryTriggerInteraction.Ignore);
            if (count == _perceiveHits.Length) return false;
            Transform proot = _player.transform.root, croot = _playerCar != null ? _playerCar.transform.root : null;
            for (int i = 0; i < count; i++)
            {
                var hit = _perceiveHits[i];
                if (hit.distance < 0.5f) continue;                       // the eye's own vehicle / body
                var r = hit.collider.transform.root;
                if (r == proot || (croot != null && r == croot)) continue;
                return false;
            }
            return true;
        }
        private static readonly RaycastHit[] _perceiveHits = new RaycastHit[16];

        // ---------- events ----------

        /// <summary>Publishes NpcSpawned for a freshly registered agent (Reason "registered"; Presentation decides female variants from it).</summary>
        private static void PublishSpawned(Agent a)
        {
            if (!ModEvents.HasListeners<NpcSpawned>()) return;
            a.Id = Entities.Of(a.Owner);
            var e = new NpcSpawned { Npc = a.Id, Prefab = PrefabOf(a.Owner), Faction = a.Tag, WorldPos = Origin.World(a.T.position), Reason = "registered" };
            ModEvents.Publish(ref e);
        }

        /// <summary>Publishes NpcDespawned for an agent whose object is gone (Reason Despawned; the id was taken at registration).</summary>
        private static void PublishDespawned(Agent a)
        {
            if (!a.Id.IsValid || !ModEvents.HasListeners<NpcDespawned>()) return;
            var e = new NpcDespawned { Npc = a.Id, Prefab = a.PrefabName, Faction = a.Tag, WorldPos = Origin.World(a.LastPos), Reason = NpcRemovalReason.Despawned };
            ModEvents.Publish(ref e);
        }

        /// <summary>Publishes NpcStateChanged when the agent's mode differs from the last published one (checked after each look and once a second).</summary>
        private static void PublishState(Agent a)
        {
            if (!ModEvents.HasListeners<NpcStateChanged>()) return;
            var mode = ModeOf(a);
            if (mode == a.Published) return;
            var old = a.Published; a.Published = mode;
            if (!a.Id.IsValid) a.Id = Entities.Of(a.Owner);
            GameObject target; TryGetTarget(a.Owner, out target);
            var e = new NpcStateChanged { Npc = a.Id, Old = old, New = mode, Target = target != null ? (target == _player ? Entities.Player : Entities.Of(target)) : EntityId.None };
            ModEvents.Publish(ref e);
        }

        /// <summary>Publishes NoiseEmitted for a noise the senses handle (gunshot, shout, engine, thrown item, explosion).</summary>
        private static void PublishNoise(GameObject source, Vector3 scene, float radius, Src src)
        {
            if (!ModEvents.HasListeners<NoiseEmitted>()) return;
            NoiseKind kind;
            switch (src)
            {
                case Src.Engine: kind = NoiseKind.Engine; break;
                case Src.Thrown: kind = NoiseKind.Thrown; break;
                case Src.Shout: kind = NoiseKind.Shout; break;
                default: kind = NoiseKind.Shot; break;
            }
            bool player = IsPlayer(source) || (_playerCar != null && source != null && source.transform.root == _playerCar.transform.root);
            var e = new NoiseEmitted { Source = source == null ? EntityId.None : player ? Entities.Player : Entities.Of(source), Kind = kind, WorldPos = Origin.World(scene), Radius = radius, Player = player };
            ModEvents.Publish(ref e);
        }
    }
}
