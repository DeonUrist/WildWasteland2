using ES3Internal;
using HutongGames.PlayMaker.Actions;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPCAI
{
    internal static class Arachnophobia
    {
        private const long RatPrefabId = 362379904028309406L;

        internal static bool BeforeCreate(CreateObject action)
        {
            bool spiders = NpcaiSettings.ReplaceSpiders.Value, scorpions = NpcaiSettings.ReplaceScorpions.Value;
            if (!spiders && !scorpions) return true;
            // Native enemy spawners use the same Item variable for the prefab and the spawned actor.
            if (action.Fsm.Name != "ItemSpawner" || action.gameObject == null || !ReferenceEquals(action.gameObject, action.storeObject)) return true;
            var source = action.gameObject.Value;
            if (source == null) return true;
            string name = source.name;
            bool spider = spiders && (name == "Spider_Big" || name == "Spider_Small");
            if (!spider && !(scorpions && (name == "Scorpion_Big" || name == "Scorpion_Small"))) return true;

            var manager = ES3ReferenceMgrBase.Current;
            var prefab = manager == null ? null : manager.GetPrefab(RatPrefabId, true);
            var rat = prefab != null ? prefab.gameObject : PrefabCatalog.ById(RatPrefabId);
            if (rat == null) return true;
            action.gameObject.Value = rat;
            NpcaiRuntime.Deep(spider ? "Arachnophobia: replacing spider with Rat" : "Arachnophobia: replacing scorpion with Rat");
            return true;
        }
    }
}
