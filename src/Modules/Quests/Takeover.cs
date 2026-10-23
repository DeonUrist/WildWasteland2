using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Quest takeover objectives and giver recovery; POI owns camps and Patrols owns reinforcement journeys.</summary>
    internal static partial class Takeover
    {
        /// <summary>Clears quest-local compatibility and giver recovery caches.</summary>
        internal static void Shutdown()
        {
            ClearWalks(); prefabs.Clear(); giverUnusable.Clear(); giverLogged.Clear();
        }
        public static Action<string> Log = delegate { };

        // ---- choosing and describing the target -----------------------------------------------

        // A camp owned by a faction that is not friendly with the giver's, within minD..maxD of the giver.
        /// <summary>Chooses the closest eligible hostile camp for a takeover offer.</summary>
        public static Poi PickCamp(Faction giver, Vector3 giverWorld, string wantFaction, float minD, float maxD, List<Quest> active, System.Random rng, HashSet<string> blocked)
        {
            if (giver == null) return null;
            string want = Util.Norm(wantFaction);
            if (want == "*" || want == "any") want = "";
            var cands = new List<Poi>();
            foreach (Poi p in Game.FindPois(giverWorld, minD, maxD, "camp"))
            {
                if (!p.Name.StartsWith("Camp", StringComparison.OrdinalIgnoreCase)) continue;
                Faction owner = Factions.OwnerOfPoi(p);
                if (owner == null || Factions.Friendly(giver, owner)) continue;
                if (giver.War.Count > 0 && !Factions.AtWar(giver, owner)) continue;   // only take camps of a faction we are at war with
                if (want.Length > 0 && Util.Norm(owner.Name) != want) continue;
                Vector3 rel = Core.Origin.World(p.Pos);
                bool taken = false;
                for (int i = 0; i < active.Count; i++)
                    if (active[i].Type == QType.Takeover && active[i].HasPos && Core.WorldGeometry.HorizontalDistance(active[i].Pos, rel) < 5f) { taken = true; break; }
                if (blocked != null && blocked.Contains(QuestGen.PosKey(rel))) taken = true;     // a neighbour is already offering this camp
                if (!taken) cands.Add(p);
            }
            // always the closest one
            Poi best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < cands.Count; i++)
            {
                float d = Core.WorldGeometry.HorizontalDistance(cands[i].Pos, giverWorld);
                if (d < bd) { bd = d; best = cands[i]; }
            }
            return best;
        }

        // living people of `tag` within `radius` of the camp (only counts what is loaded right now)
        /// <summary>Counts currently loaded live defenders belonging to the target faction.</summary>
        public static int CountDefenders(Vector3 campWorld, string tag, float radius)
        {
            int n = 0;
            foreach (EnemyInfo e in Game.FindEnemies(campWorld, radius))
                if (Util.Norm(e.Tag) == Util.Norm(tag)) n++;
            return n;
        }

        /// <summary>Fills a takeover quest from a selected camp and owner.</summary>
        public static void Fill(Quest q, Poi camp, Faction owner, float radius)
        {
            q.Type = QType.Takeover;
            q.Target = owner.Name;
            q.Label = owner.Name + " camp";
            q.HasPos = true;
            q.Pos = Core.Origin.World(camp.Pos);
            q.Radius = radius;
            // The camp's people only exist while you are near it. When some are loaded, that is the head count; when none are
            // (the camp is too far away to tell), the count stays unknown (0) until you get close, and the quest log is updated then.
            int here = CountDefenders(camp.Pos, owner.Name, radius);
            if (here > 0) { if (q.Count <= 0 || q.Count > here) q.Count = here; }
            else if (!q.CountExplicit) q.Count = 0;
            q.ReturnToGiver = true;
            if (string.IsNullOrEmpty(q.Title) || q.Title == "Odd job" || q.Title == "Take the camp") q.Title = "Take the " + owner.Name + " camp";
        }

        // ---- the takeover itself ---------------------------------------------------------------

        // Called when the quest's kills are done. Returns how many people were sent.
        /// <summary>Publishes capture of the exact objective camp for POI and Patrols.</summary>
        public static int Begin(Quest q, System.Random rng)
        {
            var locations = ModApi.Get<IWorldLocations>(); WorldLocation target;
            Poi selected = null; float closest = q.Radius;
            if (q.HasPos) foreach (var camp in Game.FindPois(Core.Origin.Scene(q.Pos), 0f, q.Radius, "camp"))
            {
                float distance = Core.WorldGeometry.HorizontalDistance(Core.Origin.World(camp.Pos), q.Pos);
                if (camp.Obj != null && camp.Kind == "Camp" && distance < closest) { closest = distance; selected = camp; }
            }
            if (selected == null || locations == null || !locations.TryGet(CampId(selected), out target))
            { Log("Takeover: target camp is not registered; ownership unchanged."); return -1; }
            Factions.Overrides[Factions.KeyOf(selected)] = "Coyotes";
            var change = new Core.Events.CampOwnershipChanged { CampId = target.Key, OldFaction = target.Faction, NewFaction = "Coyotes", WorldPos = target.WorldPos };
            ModEvents.Publish(ref change);
            Log("Camp " + target.Name + " now belongs to Coyotes; traveler settlement requested.");
            return 0;
        }

        static string CampId(Poi camp)
        {
            Vector3 world = Core.Origin.World(camp.Pos);
            return Core.Names.Prefab(camp.Obj.name) + ":" + Mathf.RoundToInt(world.x) + ":" + Mathf.RoundToInt(world.z);
        }

        // Reconcile old quest ownership records into the shared registry without dispatching a second group.
        public static void ReconcileCamps(Vector3 playerWorld)
        {
            ResolvePending();
            var locations = ModApi.Get<IWorldLocations>(); if (locations == null || Factions.Overrides.Count == 0) return;
            foreach (var camp in Game.FindPois(playerWorld, 0f, 3000f, "camp"))
            {
                string faction; WorldLocation target;
                if (camp.Obj == null || !Factions.Overrides.TryGetValue(Factions.KeyOf(camp), out faction) || !locations.TryGet(CampId(camp), out target) || target.Faction == faction) continue;
                var change = new Core.Events.CampOwnershipChanged { CampId = target.Key, OldFaction = faction, NewFaction = faction, WorldPos = target.WorldPos };
                ModEvents.Publish(ref change);
            }
        }

        // ---- quest givers come back ------------------------------------------------------------------

        public static float GiverRespawnRange = 500f;
        static float nextGiver;
        static readonly Dictionary<string, float> giverMissingSince = new Dictionary<string, float>();
        static readonly HashSet<string> giverDisarmed = new HashSet<string>();     // died/vanished while you were watching: wait until you have been away
        static readonly HashSet<string> giverSeen = new HashSet<string>();         // seen alive this visit
        static readonly HashSet<string> giverUnusable = new HashSet<string>();     // copies of these have no AI: never tried again
        static readonly HashSet<string> giverLogged = new HashSet<string>();       // "nothing to copy" already said once

        // Everyone who gave you a job you still have to hand in is remembered where you last saw them. Come back to that
        // spot (after having been away) and if the NPC is not there any more (killed, or unloaded and lost), a new copy is
        // created in the same place and takes over the job.
        /// <summary>Restores eligible missing quest givers after takeover settlement.</summary>
        public static void GiverTick(QuestBook book, Vector3 playerWorld)
        {
            if (book == null || book.Active.Count == 0 || Time.unscaledTime < nextGiver) return;
            nextGiver = Time.unscaledTime + 1f;
            if (Time.unscaledTime < settleUntil) return;                        // a save was just loaded: the game first restores its own NPCs
            float rt = Time.unscaledTime;
            Dictionary<string, Transform> live = Game.LiveNames();
            for (int qi = 0; qi < book.Active.Count; qi++)
            {
                Quest q = book.Active[qi];
                string id = q.GiverId;
                if (string.IsNullOrEmpty(id) || !q.HasGiverPos) continue;
                if (!q.ReturnToGiver || q.Type == QType.Talk || Plugin.Returning(q)) continue;        // not handed in to the giver: no need to bring them back
                Transform tr;
                if (live.TryGetValue(id, out tr) && tr != null)
                {
                    giverMissingSince.Remove(id);
                    if (Core.WorldGeometry.HorizontalDistance(playerWorld, tr.position) <= GiverRespawnRange) { q.GiverPos = Core.Origin.World(tr.position); giverSeen.Add(id); }
                    continue;
                }
                Vector3 world = Core.Origin.Scene(q.GiverPos);
                float d = Core.WorldGeometry.HorizontalDistance(playerWorld, world);
                if (d > GiverRespawnRange * 1.5f) { giverDisarmed.Remove(id); giverSeen.Remove(id); giverMissingSince.Remove(id); continue; }
                if (d > GiverRespawnRange) { giverMissingSince.Remove(id); continue; }
                if (giverSeen.Contains(id)) { giverDisarmed.Add(id); continue; }   // it was here a moment ago: killed in front of you, so no instant replacement
                if (giverDisarmed.Contains(id)) continue;
                Vector3 g;
                if (!Core.WorldGeometry.GroundAt(world.x, world.z, out g)) continue;          // ground not loaded yet
                float since;
                if (!giverMissingSince.TryGetValue(id, out since)) { giverMissingSince[id] = rt; continue; }
                if (rt - since < 3f) continue;                                  // give the game time to bring it back itself

                string baseName = Core.Names.Base(id);
                if (giverUnusable.Contains(baseName)) continue;
                if (InactiveExists(id)) { giverDisarmed.Add(id); continue; }     // it is there, just switched off: leave it to the game
                GameObject src = FindPrefab(baseName);
                if (src == null)
                {
                    if (giverLogged.Add(baseName)) Log("Giver: no prefab or living copy of " + baseName + " to bring " + id + " back; will try again.");
                    giverMissingSince[id] = rt + 27f;                           // look again in about 30 s, not every second
                    continue;
                }
                GameObject go = UnityEngine.Object.Instantiate(src, g + Vector3.up, Quaternion.Euler(0f, (float)prng.NextDouble() * 360f, 0f));
                go.SetActive(true);
                if (!Usable(go)) { UnityEngine.Object.Destroy(go); giverUnusable.Add(baseName); Log("Giver: the copy of " + baseName + " has no movement AI; not used."); continue; }
                string status = Game.RegisterSpawned(go, baseName);
                Log("Giver: " + id + " was gone from where you last saw it; " + baseName + " " + status + ".");
                string newName = go.name;
                long stamp;
                if (book.GiverStamp.TryGetValue(id, out stamp)) { book.GiverStamp.Remove(id); book.GiverStamp[newName] = stamp; }
                for (int k = 0; k < book.Active.Count; k++)
                    if (book.Active[k].GiverId == id) book.Active[k].GiverId = newName;     // every job from that NPC now belongs to the copy
                giverMissingSince.Remove(id);
            }
        }

        /// <summary>Checks whether a matching native actor is present but inactive.</summary>
        static bool InactiveExists(string name)
        {
            var all = QuestWorld.Objects;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].name == name && all[i].scene.IsValid()) return true;
            return false;
        }
    }

    /// <summary>A walker as stored in the save (AQ1 "W|" lines): object name, origin-relative target, camp key. Merged from SavedWalk.cs.</summary>
    internal class SavedWalk
    {
        public string Name;
        public Vector3 TargetRel;
        public string CampKey;      // "camp:<key>" or empty
    }
}
