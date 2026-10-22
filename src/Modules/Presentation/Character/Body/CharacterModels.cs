using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.NPC;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    internal static class CharacterModels
    {
        static readonly string[] Names = { "Flexa", "Flexa", "Merchant", "Bodyguard", "Sprokka", "Pistoleer", "Mechanic", "Organic_Mechanic", "Flexa", "Sprokka" };
        internal static int ChoiceCount { get { return Names.Length; } }
        internal static bool CustomSkin(int choice) { return choice < 2 || choice == 8 || choice == 9; }
        static readonly Dictionary<string, Animator> Sources = new Dictionary<string, Animator>();
        static float _nextFind;
        internal static bool NativeSelection { get { return (int)CharacterSettings.Character.Value >= 2; } }
        internal static Animator SelectedSource() { return NativeSelection ? Source((int)CharacterSettings.Character.Value) : null; }
        internal static Animator Source(int choice)
        {
            if (choice < 0 || choice >= Names.Length) return null;
            Animator source; string name = Names[choice];
            if (Sources.TryGetValue(name, out source) && source != null) return source;
            if (Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 5f;
                foreach (var animator in Resources.FindObjectsOfTypeAll<Animator>())
                    if (animator != null && !animator.gameObject.scene.IsValid() && animator.transform.Find("mixamorig:Hips") != null)
                    {
                        string root = animator.transform.root.name;
                        if (Array.IndexOf(Names, root) >= 0) Sources[root] = animator;
                    }
                Sources.TryGetValue(name, out source);
            }
            return source;
        }
    }
}
