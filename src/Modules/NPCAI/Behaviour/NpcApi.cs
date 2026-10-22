using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Guns;
using WildWasteland.Modules.Patrols;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// The NPCAI ModAPI provider (README 6.2): <see cref="INpcApi"/> (NPCAI.Api v1, same members and behaviour, plus the nav structure
    /// list Apocapatrol read by reflection) and <see cref="INpcControl"/> (seating, fright, perception, mode/target, hearing policy,
    /// hostility, enumeration, bosses). Provided by NPCAIModule.ProvideApis, withdrawn by ModuleHost when the module stops.
    /// </summary>
    internal sealed class NpcApi : INpcApi, INpcControl
    {
        /// <summary>Contract version (1 = NPCAI.Api v1).</summary>
        public int ContractVersion { get { return 1; } }
        /// <summary>Marks an NPC as a simple convoy bandit (Patrols crews on foot).</summary>
        public void SetSimpleConvoyBandit(GameObject npc, bool enabled) { SimpleBandits.Set(npc, enabled); }
        /// <summary>Simple convoy bandit membership and combat state.</summary>
        public void SetSimpleConvoyBanditState(GameObject npc, bool enabled, bool combat) { SimpleBandits.Set(npc, enabled, combat); }
        /// <summary>Simple convoy bandit goal.</summary>
        public void SetSimpleConvoyBanditTarget(GameObject npc, GameObject target) { SimpleBandits.Target(npc, target); }
        /// <summary>A gun was fired (Gunplay): NPCs within the kind's noise range hear it (weaponKind 0..5 = Pistol..Crossbow).</summary>
        public void Shot(GameObject shooter, Vector3 position, int weaponKind, bool player)
        { if (weaponKind >= 0 && weaponKind <= 5) Senses.Shot(shooter, position, (WeaponRanges.Kind)weaponKind, player); }
        /// <summary>A creature was hit (Gunplay): it knows where the hit came from.</summary>
        public void Hurt(GameObject victim, GameObject attacker) { Senses.Hurt(victim, attacker); }
        /// <summary>NPC aim spread factor at a distance (1 when [NPCAI] NpcAim is off).</summary>
        public float SpreadFactor(float distance) { return NpcaiSettings.AimEnabled.Value ? Aim.SpreadFactor(distance) : 1f; }
        /// <summary>Tells an NPC where a target is (now, or the moment it registers).</summary>
        public void Inform(GameObject npc, GameObject target) { Senses.Inform(npc, target); }
        /// <summary>Hands the NPC's movement to a follower driver (Quests hire) or back to the brain / idle behaviour.</summary>
        public void SetFollowerDriven(GameObject npc, bool enabled, float reloadUntil = 0f) { Brain.SetFollowerDriven(npc, enabled, reloadUntil); Idle.SetFollowerDriven(npc, enabled); }
        /// <summary>Fills <paramref name="into"/> (cleared first) with the current nav structures.</summary>
        public void Structures(List<NavStructure> into) { Nav.Structures(into); }
        /// <summary>Temporary map diagnostic: highlight a POI type with no captured persistent bake.</summary>
        public bool HighlightMissingPoiBake(string prefabName) { return Nav.HighlightMissingPoiBake(prefabName); }
        /// <summary>Walks an NPC to a scene point (an Idle travel leg); null cancels.</summary>
        public void Travel(GameObject npc, Vector3? scenePoint, float arriveRadius) { Idle.Travel(npc, scenePoint, arriveRadius); }
        /// <summary>0 = no order, 1 = walking, 2 = arrived, 3 = failed.</summary>
        public int TravelState(GameObject npc) { return Idle.TravelState(npc); }
        /// <summary>True while the NPC has no target, ghost or search.</summary>
        public bool IsIdle(GameObject npc) { return Senses.IsIdle(npc); }
        /// <summary>The NPC knows where target is for seconds (fought-player memory).</summary>
        public void Remember(GameObject npc, GameObject target, float seconds) { Senses.Remember(npc, target, seconds); }
        /// <summary>Assigns a crew's attacker or suspends combat while it boards; null/false releases the order.</summary>
        public bool CrewOrder(GameObject npc, GameObject enemy, bool boarding) { return Senses.CrewOrder(npc, enemy, boarding); }

        // ------------------------------------------------------------------ INpcControl

        /// <summary>Seated crew AI: mutes the AI / mover FSMs of a seated NPC (Patrols Crew.MuteAi rule) or switches them back on.</summary>
        public void SetSeated(GameObject npc, bool seated) { NpcSeatState.SetSeated(npc, seated); }
        /// <summary>Retreat from a threat for seconds (infinity = until told otherwise, 0 = ends the fright).</summary>
        public void Frighten(GameObject npc, Vector3 threatScene, float seconds, string cause) { Brain.FrightenFor(npc, threatScene, seconds, cause); }
        /// <summary>Player perception from an arbitrary eye (sight by light/storm/FOV/line, hearing of a moving player or his engine).</summary>
        public bool CanPerceivePlayer(Vector3 fromScene, Vector3 forward, PerceptionProfile profile) { return Senses.CanPerceivePlayer(fromScene, forward, profile); }
        /// <summary>Current behaviour mode of an NPC.</summary>
        public NpcMode ModeOf(GameObject npc) { return Senses.ModeOf(npc); }
        /// <summary>Current target of an NPC.</summary>
        public bool TryGetTarget(GameObject npc, out GameObject target) { return Senses.TryGetTarget(npc, out target); }
        /// <summary>Hearing policy for interior / town populations.</summary>
        public void SetHearingPolicy(GameObject npc, bool deaf, bool interior) { Senses.SetHearingPolicy(npc, deaf, interior); }
        /// <summary>Makes an NPC hostile / friendly to the player.</summary>
        public void SetPlayerHostile(GameObject npc, bool hostile) { Senses.SetPlayerHostile(npc, hostile); }
        /// <summary>Live NPCs within a radius of a scene point.</summary>
        public void NpcsNear(Vector3 scene, float radius, List<GameObject> into) { Senses.NpcsNear(scene, radius, into); }
        /// <summary>True when the object is an NPC known to NPCAI (senses agent or brain).</summary>
        public bool IsNpc(GameObject go) { return go != null && (Senses.Knows(go) || Brain.Knows(go)); }
        /// <summary>Faction tag of an NPC (the senses' tag, else the object's tag; null when unknown).</summary>
        public string FactionOf(GameObject npc) { return npc == null ? null : Senses.TagOf(npc) ?? npc.tag; }
        /// <summary>True for a human boss whose health NPCAI caps (Duke Ironjaw, Buzzgut).</summary>
        public bool IsBoss(GameObject npc) { return Bosses.IsTracked(npc); }
    }
}
