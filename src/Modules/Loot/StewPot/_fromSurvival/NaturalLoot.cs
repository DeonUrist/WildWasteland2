using System;
using ES3Internal;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.Survival
{
    internal static class NaturalLoot
    {
        static readonly System.Random random = new System.Random();

        internal static bool BeforeCreate(CreateObject action)
        {
            if (action.Fsm.Name != "ItemSpawner") return true;
            var owner = action.Fsm.GameObject;
            if (owner == null || !owner.name.StartsWith("ItemSpawn", StringComparison.Ordinal)) return true;
            if (!ReferenceEquals(action.gameObject, action.storeObject)) return true;
            var source = action.gameObject.Value;
            if (source == null || source.name != "gasoline_stove") return true;
            var prefab = source.GetComponent<ES3Prefab>();
            if (prefab == null || prefab.prefabId != 2571597281285591657L || random.Next(4) != 0) return true;
            GameObject template;
            try { template = Factory.Template(); }
            catch (Exception error)
            {
                Core.Log.Warn("[Survival] Natural Stew Pot unavailable; retaining gas stove: " + error.Message);
                return true;
            }
            // Native CreateObject overwrites the shared Item variable with the clone, then names and saves it.
            action.gameObject.Value = template;
            if (SurvivalModule.Verbose.Value) Core.Log.Always("[Survival] Natural Stew Pot selected: " + owner.name);
            return true;
        }
    }
}
