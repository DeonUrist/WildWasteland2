using UnityEngine;

namespace WildWasteland.Modules.NPCAI
{
    internal static partial class Brain
    {
        /// <summary>True while a fright keeps the NPC in its retreat cover.</summary>
        internal static bool Retreating(GameObject owner)
        {
            Npc n;
            return On && owner != null && _npcs.TryGetValue(owner.GetInstanceID(), out n) && Time.time < n.FrightUntil && n.Mode == Mode.Cover;
        }
        /// <summary>Weapons Pack burning fear: on = retreat to cover away from the threat until told otherwise; off = the burning fear ends (a chainsaw fright continues).</summary>
        internal static void FireRetreat(GameObject owner, Vector3 threat, bool burning)
        {
            if (!On || owner == null) return;
            var n = Of(owner);
            if (n == null) return;
            if (!burning) { if (n.BurningFear) { n.BurningFear = false; n.FrightUntil = n.SawFrightUntil; n.CoverUntil = n.FrightUntil; } return; }
            if (n.FollowerDriven || n.Travel || n.T.parent != null || n.HpVar == null || n.HpVar.Value < 1f) return;
            if (burning)
            {
                Frighten(n, threat, "burning debuff");
                n.BurningFear = true; n.FrightUntil = float.PositiveInfinity;
            }
        }

        /// <summary>A chainsaw kill was witnessed: hostile humans within 25 m with a line of sight to the corpse flee for 6 s.</summary>
        internal static void ChainsawWitnesses(GameObject corpse)
        {
            if (!On) return;
            Vector3 victim = corpse.transform.position;
            foreach (var pair in _npcs)
            {
                var n = pair.Value;
                if (n.Owner == null || n.FollowerDriven || n.Travel || n.T.parent != null || n.HpVar == null || n.HpVar.Value < 1f) continue;
                var agent = Senses.AgentFor(n.Owner);
                if (agent == null || !agent.Human || !agent.Hostile.Contains("Player") || (n.T.position - victim).sqrMagnitude > 625f) continue;
                Vector3 eye = n.Col != null ? n.Col.bounds.center : n.T.position + Vector3.up;
                RaycastHit obstruction;
                if (Physics.Linecast(eye, victim + Vector3.up, out obstruction, LosMask, QueryTriggerInteraction.Ignore)
                    && !obstruction.transform.IsChildOf(corpse.transform)) continue;
                Frighten(n, victim, "witnessed chainsaw kill");
                n.SawFrightUntil = Time.time + 6f;
            }
        }

        /// <summary>Frightens a brain: retreat away from the threat (6 s, or for ever while burning); a fresh fright thinks at once.</summary>
        private static void Frighten(Npc n, Vector3 threat, string why)
        {
            bool fresh = Time.time >= n.FrightUntil;
            n.FrightUntil = n.BurningFear ? float.PositiveInfinity : Time.time + 6f; n.FrightFrom = threat;
            if (fresh)
            {
                n.NextFrightCover = 0f; n.NextTick = 0f;
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Brain: " + n.Owner.name + " retreat: " + why);
            }
        }

        /// <summary>Fright think: finds cover away from the threat (camp cover objects, else a map spot) and runs there until the fright ends.</summary>
        private static bool FrightThink(Npc n, float now)
        {
            if (now >= n.FrightUntil) return false;
            if (n.HpVar == null || n.HpVar.Value < 1f) { n.FrightUntil = 0f; return false; }
            if (n.Mode != Mode.Cover && now >= n.NextFrightCover)
            {
                n.NextFrightCover = now + 2f;
                float range = NpcaiSettings.CoverRange.Value, best = range * range;
                float clearance = Mathf.Max(25f, (n.T.position - n.FrightFrom).sqrMagnitude);
                Vector3 spot = Vector3.zero; bool found = false;
                NearestHide(n.T.position, range);
                foreach (var hide in _hides)
                {
                    if (hide == null) continue;
                    Vector3 p = hide.transform.position; float distance = (p - n.T.position).sqrMagnitude;
                    if (distance < 2.25f || distance >= best || (p - n.FrightFrom).sqrMagnitude < clearance) continue;
                    best = distance; spot = p; found = true;
                }
                if (!found) found = Nav.CoverSpot(n.T.position, n.FrightFrom, range, out spot)
                    && (spot - n.T.position).sqrMagnitude > 2.25f && (spot - n.FrightFrom).sqrMagnitude >= clearance;
                if (found)
                {
                    StartCover(n, spot, now, 6f, false, "weapon retreat to cover");
                    n.HasHeading = false; Unfreeze(n); Crouch(n, false); Move(n, true);
                }
            }
            if (n.Mode != Mode.Cover) return false;
            n.CoverUntil = n.FrightUntil;
            Vector3 delta = n.CoverPos - n.T.position; delta.y = 0f;
            if (delta.sqrMagnitude <= 2.25f)
            {
                if (!n.CoverArrived) Move(n, false);
                n.CoverArrived = true; n.HasHeading = false;
            }
            else
            {
                if (n.CoverArrived || !string.Equals(n.Movement.ActiveStateName, "Run", System.StringComparison.OrdinalIgnoreCase)) Move(n, true);
                n.CoverArrived = false; MoveToward(n, n.CoverPos, delta, delta.magnitude, now);
            }
            return true;
        }
    }
}
