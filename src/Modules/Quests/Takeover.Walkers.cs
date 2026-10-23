using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    internal static partial class Takeover
    {
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        static readonly List<SavedWalk> pending = new List<SavedWalk>();
        static float pendingUntil;
        static readonly System.Random prng = new System.Random();
        const float SettleSeconds = 10f;
        static float settleUntil;

        public static List<SavedWalk> ActiveWalks() { return new List<SavedWalk>(pending); }
        public static void RestoreWalks(List<SavedWalk> saved)
        { pending.Clear(); pending.AddRange(saved); pendingUntil = Time.unscaledTime + 90f; }
        public static void ClearWalks()
        { pending.Clear(); giverMissingSince.Clear(); giverDisarmed.Clear(); giverSeen.Clear(); prefabMissUntil.Clear(); settleUntil = Time.unscaledTime + SettleSeconds; }

        // Old W records refer to already saved native NPCs. Hand their final walk to NPC AI; never clone replacements.
        static void ResolvePending()
        {
            var api = ModApi.Get<INpcApi>(); if (api == null) return;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var npc = Game.FindLive(pending[i].Name);
                if (npc != null) { api.Travel(npc.gameObject, Core.Origin.Scene(pending[i].TargetRel), 4f); pending.RemoveAt(i); }
                else if (Time.unscaledTime > pendingUntil) pending.RemoveAt(i);
            }
        }

        /// <summary>A real NPC has a Movement FSM. Some assets share an NPC's name but are only the model (no FSMs, no animator state), which stands in a T-pose and never moves, so those are never used.</summary>
        static bool Usable(GameObject go)
        {
            PlayMakerFSM[] fsms = QuestWorld.Fsms(go);
            for (int i = 0; i < fsms.Length; i++) if (fsms[i] != null && fsms[i].FsmName == "Movement") return true;
            return false;
        }

        // a name that could not be found is not looked for again (a full scan of every object) for a while
        static readonly Dictionary<string, float> prefabMissUntil = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The prefab asset for a name, or (fallback) a living object with that name to copy.</summary>
        static GameObject FindPrefab(string name)
        {
            GameObject cached;
            if (prefabs.TryGetValue(name, out cached) && cached != null) return cached;
            float missUntil;
            if (prefabMissUntil.TryGetValue(name, out missUntil) && Time.unscaledTime < missUntil) return null;
            GameObject found = ScanForPrefab(name);
            if (found == null) prefabMissUntil[name] = Time.unscaledTime + 30f;
            else prefabMissUntil.Remove(name);
            return found;
        }

        /// <summary>Prefab asset for a name, else a living object with that name to copy.</summary>
        static GameObject ScanForPrefab(string name)
        {
            GameObject living = null;
            var all = QuestWorld.Objects;
            for (int i = 0; i < all.Count; i++)
            {
                GameObject go = all[i];
                if (go == null || go.transform.parent != null) continue;
                string n = Core.Names.Base(go.name);
                if (!string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) continue;
                if (!Usable(go)) continue;          // model only, no AI: would T-pose
                if (!go.scene.IsValid())            // not in a scene = an asset / prefab
                {
                    if (go.name.IndexOf("(Clone)", StringComparison.Ordinal) >= 0) continue;
                    prefabs[name] = go;
                    return go;
                }
                if (living == null && go.activeInHierarchy) living = go;
            }
            if (living != null) prefabs[name] = living;
            return living;
        }

        // These helpers are still used by the hired follower; takeover groups use NPC AI Travel.
        static int solidMask;

        static bool solidMaskSet;

        /// <summary>Layers that block a walker (everything except actors, items, player, water, UI).</summary>
        static int SolidMask()
        {
            if (!solidMaskSet)
            {
                solidMaskSet = true;
                int m = ~0;
                string[] skip = { "Actor", "Item", "Ignore Raycast", "Player", "Water", "UI" };
                for (int i = 0; i < skip.Length; i++)
                {
                    int l = LayerMask.NameToLayer(skip[i]);
                    if (l >= 0) m &= ~(1 << l);
                }
                solidMask = m;
            }
            return solidMask;
        }

        /// <summary>is something solid (not a slope) within dist metres in direction dir, at knee or chest height?</summary>
        static bool Blocked(Transform t, Vector3 dir, float dist)
        {
            for (int h = 0; h < 2; h++)
            {
                Vector3 from = t.position + Vector3.up * (h == 0 ? 0.5f : 1.3f);
                RaycastHit hit;
                if (!Physics.Raycast(from, dir, out hit, dist, SolidMask(), QueryTriggerInteraction.Ignore)) continue;
                if (hit.transform == t || hit.transform.IsChildOf(t)) continue;
                if (hit.normal.y > 0.7f) continue;
                return true;
            }
            return false;
        }

        /// <summary>the free heading closest to `want` (turning up to 140 degrees either way), or zero when every way is blocked</summary>
        internal static Vector3 Steer(Transform t, Vector3 want, float look)
        {
            float[] turns = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 125f, -125f };
            for (int i = 0; i < turns.Length; i++)
            {
                Vector3 d = Quaternion.Euler(0f, turns[i], 0f) * want;
                if (!Blocked(t, d, look)) return d;
            }
            return Vector3.zero;
        }

        /// <summary>is there room to stand at this ground point (nothing solid where a person would be)?</summary>
        internal static bool RoomAt(Vector3 ground)
        {
            return !Physics.CheckCapsule(ground + Vector3.up * 0.5f, ground + Vector3.up * 1.6f, 0.4f, SolidMask(), QueryTriggerInteraction.Ignore);
        }

    }
}
