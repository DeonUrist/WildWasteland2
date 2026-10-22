using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>
    /// NPCAI's projection of the Core FSM index for the follower / walker code (replaces the Quests QuestWorld lists it used): living
    /// people (Health and ID FSMs), per-object FSM arrays, a non-allocating ray buffer and the quest name normaliser.
    /// </summary>
    internal static class NpcWorld
    {
        static readonly List<PlayMakerFSM> _people = new List<PlayMakerFSM>();
        static int _peopleRevision = -1;
        static readonly Dictionary<int, PlayMakerFSM[]> _sets = new Dictionary<int, PlayMakerFSM[]>();
        static readonly Dictionary<int, GameObject> _setOwners = new Dictionary<int, GameObject>();
        static RaycastHit[] _rays = new RaycastHit[64];
        static readonly Dictionary<int, RaycastHit[]> _exact = new Dictionary<int, RaycastHit[]>();

        /// <summary>Tracks the Health and ID FSMs in the Core FSM index for NPCAI (module start).</summary>
        internal static void Install(IModule owner) { FsmIndex.Track(owner, "Health"); FsmIndex.Track(owner, "ID"); }

        /// <summary>Scene load / module stop: drops the per-object caches and the people list.</summary>
        internal static void Clear() { _people.Clear(); _peopleRevision = -1; _sets.Clear(); _setOwners.Clear(); }

        /// <summary>Health FSMs then ID FSMs of the last Core FSM index pass (rebuilt once per index revision; entries may be destroyed).</summary>
        internal static List<PlayMakerFSM> People
        {
            get
            {
                if (_peopleRevision == FsmIndex.Revision) return _people;
                _peopleRevision = FsmIndex.Revision; _people.Clear();
                foreach (var fsm in FsmIndex.ByName("Health")) if (fsm != null) _people.Add(fsm);
                foreach (var fsm in FsmIndex.ByName("ID")) if (fsm != null) _people.Add(fsm);
                return _people;
            }
        }

        /// <summary>Every PlayMakerFSM of an object, enabled or not, cached per object until the scene resets.</summary>
        internal static PlayMakerFSM[] Fsms(GameObject gameObject)
        {
            if (gameObject == null) return new PlayMakerFSM[0];
            int id = gameObject.GetInstanceID(); PlayMakerFSM[] result; GameObject owner;
            if (_setOwners.TryGetValue(id, out owner) && owner == gameObject && _sets.TryGetValue(id, out result)) return result;
            result = gameObject.GetComponents<PlayMakerFSM>(); _sets[id] = result; _setOwners[id] = gameObject; return result;
        }

        /// <summary>Raycast over every layer into a reused buffer; returns an exact-length array reused per hit count.</summary>
        internal static RaycastHit[] RaycastAll(Vector3 start, Vector3 direction, float length) { return RaycastAll(start, direction, length, ~0, QueryTriggerInteraction.UseGlobal); }

        /// <summary>Raycast into a reused (growing) buffer; returns an exact-length array reused per hit count.</summary>
        internal static RaycastHit[] RaycastAll(Vector3 start, Vector3 direction, float length, int mask, QueryTriggerInteraction triggers)
        {
            int count;
            while ((count = Physics.RaycastNonAlloc(start, direction, _rays, length, mask, triggers)) >= _rays.Length && _rays.Length < 4096) Array.Resize(ref _rays, _rays.Length * 2);
            RaycastHit[] exact;
            if (!_exact.TryGetValue(count, out exact)) _exact[count] = exact = new RaycastHit[count];
            Array.Copy(_rays, exact, count); return exact;
        }

        /// <summary>Quest name normaliser: lower case without spaces, underscores, dashes and dots.</summary>
        internal static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ' || c == '_' || c == '-' || c == '.') continue;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
