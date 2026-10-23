using System;
using System.Collections.Generic;
using ES3Internal;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Survival
{
    /// <summary>Native food and attachment templates with independent, stable ES3 identities.</summary>
    internal static class HumanFood
    {
        internal const long RawId = 7392481500621300211L, CookedId = 7392481500621300212L, SkullId = 7392481500621300213L;
        private static GameObject _holder, _raw, _cooked, _skull;
        internal static ES3Prefab Resolve(long id)
        {
            if (id != RawId && id != CookedId && id != SkullId) return null;
            Prepare(); return (id == RawId ? _raw : id == CookedId ? _cooked : _skull).GetComponent<ES3Prefab>();
        }
        internal static void Prepare()
        {
            if (_raw != null && _cooked != null && _skull != null)
            { RegisterExisting(_raw); RegisterExisting(_cooked); RegisterExisting(_skull); return; }
            if (_holder == null)
            {
                _holder = new GameObject("Survival.HumanFood.Templates"); _holder.SetActive(false); Object.DontDestroyOnLoad(_holder);
            }
            if (_cooked == null) _cooked = Meat("meat_mutant_cooked", "meat_human_cooked", CookedId, false);
            if (_raw == null) _raw = Meat("meat_mutant_raw", "meat_human_raw", RawId, true);
            if (_skull == null) _skull = Skull();
        }
        private static GameObject Copy(string source, string name)
        {
            var prefab = PrefabCatalog.Find(source);
            if (prefab == null) throw new InvalidOperationException("Missing native prefab: " + source);
            var go = Object.Instantiate(prefab, _holder.transform, false); go.SetActive(false); go.name = name; return go;
        }
        private static GameObject Meat(string source, string name, long id, bool raw)
        {
            var go = Copy(source, name);
            try
            {
                FsmUtil.Find(go, "ItemName").FsmVariables.GetFsmString("ItemName").Value = raw ? "Raw Human meat" : "Cooked Human meat";
                foreach (var fsm in go.GetComponents<PlayMakerFSM>()) foreach (var state in fsm.FsmStates)
                {
                    state.Fsm = fsm.Fsm;
                    foreach (var action in state.Actions)
                    {
                        var food = action as FsmFloatAdd;
                        if (fsm.FsmName == "Food" && food != null)
                        {
                            if (food.variableName.Value == "SanityAdd") food.add = raw ? 6f : 5f;
                            else if (food.variableName.Value == "Health") food.add = 5f;
                        }
                        var text = action as UiTextSetText;
                        if (fsm.FsmName == "ItemName" && text != null && text.text.Value.StartsWith("Hunger", StringComparison.Ordinal))
                            text.text = raw ? "Hunger -5\nInsanity +30" : "Hunger -15\nHealth +5\nInsanity +25";
                        var create = action as CreateObject;
                        if (fsm.FsmName == "Cook" && create != null) create.gameObject = _cooked;
                    }
                    state.SaveActions();
                }
                Register(go, id); return go;
            }
            catch { Object.Destroy(go); throw; }
        }
        private static GameObject Skull()
        {
            var go = Copy("trophy_professor", "trophy_human_skull");
            try
            {
                var donor = PrefabCatalog.Find("supercharger_8");
                var top = donor != null ? donor.transform.Find("skull_top") : null;
                var bottom = donor != null ? donor.transform.Find("skull_bottom") : null;
                if (top == null || bottom == null) throw new InvalidOperationException("Native skull meshes are missing");
                go.GetComponent<MeshRenderer>().enabled = false;
                var upper = Object.Instantiate(top.gameObject, go.transform, false).transform;
                upper.name = "SkullTop"; upper.localPosition = Vector3.zero; upper.localRotation = Quaternion.identity;
                var lower = Object.Instantiate(bottom.gameObject, go.transform, false).transform;
                lower.name = "SkullBottom"; lower.localPosition = bottom.localPosition - top.localPosition; lower.localRotation = Quaternion.identity;
                upper.gameObject.layer = lower.gameObject.layer = go.layer;
                Bounds bounds = upper.GetComponent<MeshFilter>().sharedMesh.bounds;
                bounds.center = Vector3.Scale(bounds.center, upper.localScale); bounds.size = Vector3.Scale(bounds.size, upper.localScale);
                var jaw = lower.GetComponent<MeshFilter>().sharedMesh.bounds;
                jaw.center = lower.localPosition + Vector3.Scale(jaw.center, lower.localScale); jaw.size = Vector3.Scale(jaw.size, lower.localScale); bounds.Encapsulate(jaw);
                var collider = go.GetComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
                go.GetComponent<Rigidbody>().mass = 1f;
                FsmUtil.Find(go, "ItemName").FsmVariables.GetFsmString("ItemName").Value = "Human Skull";
                Register(go, SkullId); return go;
            }
            catch { Object.Destroy(go); throw; }
        }
        private static void Register(GameObject go, long id)
        {
            var prefab = go.GetComponent<ES3Prefab>(); prefab.prefabId = id; prefab.localRefs = new ES3RefIdDictionary();
            RegisterNode(prefab, go.transform, go.name);
            go.SetActive(true);
            RegisterExisting(go);
        }
        private static void RegisterExisting(GameObject go)
        {
            var manager = ES3ReferenceMgrBase.Current; var prefab = go.GetComponent<ES3Prefab>();
            if (manager != null && !manager.prefabs.Contains(prefab))
            { manager.prefabs.Add(prefab); Log.Always(null, "[Survival] Registered " + go.name + " with current save manager."); }
        }
        private static long Hash(string value)
        { unchecked { ulong hash = 14695981039346656037UL; for (int i = 0; i < value.Length; i++) { hash ^= value[i]; hash *= 1099511628211UL; } return (long)(hash & 0x7fffffffffffffffUL); } }
        private static void RegisterNode(ES3Prefab prefab, Transform node, string path)
        {
            prefab.localRefs[node.gameObject] = Hash(path + ":go");
            var components = node.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++) prefab.localRefs[components[i]] = Hash(path + ":" + components[i].GetType().Name + ":" + i);
            var mesh = node.GetComponent<MeshFilter>();
            if (mesh != null && mesh.sharedMesh != null) prefab.localRefs[mesh.sharedMesh] = Hash(prefab.name + ":mesh:" + mesh.sharedMesh.name);
            var renderer = node.GetComponent<MeshRenderer>();
            if (renderer != null) foreach (var material in renderer.sharedMaterials) if (material != null)
            {
                prefab.localRefs[material] = Hash(prefab.name + ":material:" + material.name);
                if (material.mainTexture != null) prefab.localRefs[material.mainTexture] = Hash(prefab.name + ":texture:" + material.mainTexture.name);
            }
            foreach (Transform child in node) RegisterNode(prefab, child, path + "/" + child.name);
        }
        internal static bool Butcher(GameObject corpse)
        {
            Prepare();
            if (!ItemSpawner.Available()) return false;
            var made = new GameObject[3 + (SkillPerks.Has("CleanCut") && UnityEngine.Random.value < .25f ? 1 : 0)];
            try
            {
                for (int i = 0; i < made.Length; i++)
                {
                    var template = i == made.Length - 1 ? _skull : _raw;
                    made[i] = Object.Instantiate(template, corpse.transform.position + Vector3.up * .25f + Vector3.right * ((i - 1) * .22f), Quaternion.identity);
                    if (!ItemSpawner.RegisterSaved(made[i], template.name)) throw new InvalidOperationException("Human butcher loot could not be registered");
                }
            }
            catch
            {
                foreach (var item in made) if (item != null) { ItemSpawner.Unregister(item); Object.Destroy(item); }
                throw;
            }
            ItemSpawner.Unregister(corpse); corpse.SetActive(false); Object.Destroy(corpse); return true;
        }
    }
}
